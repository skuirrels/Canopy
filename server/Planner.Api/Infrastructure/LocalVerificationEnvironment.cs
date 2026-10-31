using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalVerificationEnvironment( IMarkdownFiles markdown, AgentOptions options) : IVerificationEnvironment, IReviewChanges
{
    public async Task<VerificationContext> Observe(FactoryRun run, CancellationToken ct)
    {
        var root = Workspace(run);
        var head = (await Git(root, ["rev-parse", "HEAD"], ct)).Trim();
        var paths = await Git(root, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) { hash.AppendData(Encoding.UTF8.GetBytes(value)); hash.AppendData([0]); }
        foreach (var path in paths.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct().Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var file = SafeFile(root, path);
            Add(path);
            if (Directory.Exists(file)) throw new RuleException("Submodule verification requires an explicit verification provider.");
            if (!File.Exists(file)) { Add("deleted"); continue; }
            await using var stream = File.OpenRead(file);
            Add(Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)));
            Add(OperatingSystem.IsWindows() ? "" : ((int)File.GetUnixFileMode(file)).ToString());
        }
        // Include the index: staged changes and intent-to-add entries are inputs too.
        Add(await Git(root, ["ls-files", "--stage", "-z"], ct));
        var codeHash = Convert.ToHexString(hash.GetHashAndReset());
        var snapshot = run.Snapshot;
        var skills = snapshot.Environment.Skills.Select(skill => new { skill.Id, skill.Source, hash = markdown.Read(skill.Path).Hash }).ToArray();
        var references = snapshot.Environment.Definition.Stages.SelectMany(stage => stage.Steps)
            .SelectMany(step => step.Command?.EnvironmentReferences?.Values.AsEnumerable() ?? []).Distinct().Order(StringComparer.Ordinal)
            .Select(name => new { name, value = Environment.GetEnvironmentVariable(name) ?? throw new RuleException($"Verification environment reference '{name}' is unavailable.") }).ToArray();
        // Only the composite digest is persisted, never resolved environment values.
        var configurationHash = FactoryArtifact.Hash(JsonSerializer.Serialize(new { snapshot.Environment, snapshot.SpecJson, snapshot.Criteria, skills, references }, FactoryRules.Json));
        if (snapshot.Execution is not null) configurationHash = FactoryArtifact.Hash(configurationHash + JsonSerializer.Serialize(snapshot.Execution, FactoryRules.Json));
        if (snapshot.Repair is not null) configurationHash = FactoryArtifact.Hash(configurationHash + JsonSerializer.Serialize(snapshot.Repair, FactoryRules.Json));
        if (head != (await Git(root, ["rev-parse", "HEAD"], ct)).Trim() || paths != await Git(root, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct))
            throw new RuleException("Verification inputs changed during inspection. Refresh and verify again.", 409);
        return new(head, codeHash, configurationHash, FactoryArtifact.Hash(JsonSerializer.Serialize(new { head, codeHash, configurationHash }, FactoryRules.Json)));
    }
    public async Task<string> TestFile(FactoryRun run, string path, int line, CancellationToken ct)
    {
        if (line < 1) throw new RuleException("Choose a positive asserting-test line number.");
        var root = Workspace(run); var file = SafeFile(root, path);
        var canonical = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
        if (!File.Exists(file)) throw new RuleException("The asserting test file is unavailable in this run's worktree.");
        var included = await Git(root, ["ls-files", "--cached", "--others", "--exclude-standard", "-z", "--", canonical], ct);
        if (!included.Split('\0', StringSplitOptions.RemoveEmptyEntries).Contains(canonical)) throw new RuleException("The test must be tracked or non-ignored so its content is included in the verification fingerprint.");
        if (new FileInfo(file).Length > 2_000_000) throw new RuleException("The asserting test reference must be a text file of at most 2 MB.");
        var bytes = await File.ReadAllBytesAsync(file, ct);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { throw new RuleException("The asserting test reference must be UTF-8 text."); }
        var lines = text.Split('\n');
        if (line > lines.Length || string.IsNullOrWhiteSpace(lines[line - 1])) throw new RuleException("The asserting-test line must exist and contain text.");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
    public async Task<string> RawChange(FactoryRun run, string baseCommit, CancellationToken ct)
    {
        var root = Workspace(run); var change = new StringBuilder();
        void Append(string value)
        {
            if ((long)change.Length + value.Length > options.MaxOutputCharacters) throw new RuleException("The raw review change exceeds Agents:MaxOutputCharacters. Increase the configured limit or split the work; partial changes cannot be approved.");
            change.Append(value);
        }
        Append(await Git(root, ["diff", "--no-ext-diff", "--no-textconv", "--binary", baseCommit, "--"], ct, limit: options.MaxOutputCharacters));
        var untracked = await Git(root, ["ls-files", "--others", "--exclude-standard", "-z"], ct);
        foreach (var path in untracked.Split('\0', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal))
            Append(await Git(root, ["diff", "--no-index", "--no-ext-diff", "--no-textconv", "--binary", "--", "/dev/null", SafeFile(root, path)], ct, true, options.MaxOutputCharacters));
        return change.ToString();
    }
    internal string Workspace(FactoryRun run)
    {
        if (run.WorkspacePath is null || !Directory.Exists(run.WorkspacePath)) throw new RuleException("The retained verification worktree is unavailable.");
        var expected = Path.GetFullPath(run.InspectionStageId is { } stageId ? Path.Combine(options.WorkspaceRoot, run.Id + ".stages", stageId.ToString()) : Path.Combine(options.WorkspaceRoot, run.Id.ToString()));
        if (Path.GetFullPath(run.WorkspacePath) != expected) throw new RuleException("The worktree is outside this deployment's configured run directory.");
        RejectLinks(expected);
        return expected;
    }
    internal static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.IndexOf('\0') >= 0 || path.Split('/', '\\').Any(part => part == ".." || part.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            throw new RuleException("Use a relative test/source path inside the retained worktree.");
        var full = Path.GetFullPath(path, root);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new RuleException("The file must remain inside the retained worktree.");
        RejectLinks(full);
        return full;
    }
    internal static void RejectLinks(string path)
    {
        for (FileSystemInfo? current = new FileInfo(path); current is not null; current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
            if (current.LinkTarget is not null) throw new RuleException("Verification cannot traverse symbolic links.");
    }
    private static async Task<string> ReadOutput(StreamReader reader, int? limit, CancellationToken ct)
    {
        var result = new StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer, ct)) > 0)
        {
            if (limit.HasValue && (long)result.Length + count > limit.Value) throw new RuleException("The raw review change exceeds Agents:MaxOutputCharacters. Partial changes cannot be approved.");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
    internal Task<string> ReadGit(string directory, string[] arguments, CancellationToken ct, bool acceptDiff = false) => Git(directory, arguments, ct, acceptDiff);
    private async Task<string> Git(string directory, string[] arguments, CancellationToken ct, bool acceptDiff = false, int? limit = null)
    {
        // Fingerprints must not use the runner's truncated human-readable Git output.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.GitTimeoutSeconds));
        var info = new ProcessStartInfo(LocalAgentEnvironment.Resolve(options.GitExecutable) ?? throw new RuleException("Git is unavailable."))
            { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new RuleException("Git could not inspect verification inputs.");
        try
        {
            var output = ReadOutput(process.StandardOutput, limit, deadline.Token); var error = process.StandardError.ReadToEndAsync(deadline.Token);
            var result = await output; await process.WaitForExitAsync(deadline.Token); await error;
            if (process.ExitCode != 0 && !(acceptDiff && process.ExitCode == 1)) throw new RuleException("Git could not inspect the retained verification worktree.");
            return result;
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
}
