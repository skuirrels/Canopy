using System.Security.Cryptography;
using System.Text;
using Planner.Api.Application;
using Planner.Api.Domain;
namespace Planner.Api.Infrastructure;

public sealed class LocalCalibrationWorkspace(IFactorySkillCatalogue catalogue, IMarkdownFiles markdown, LocalAgentEnvironment git,
    LocalVerificationEnvironment verification, AgentOptions agents, FactoryCalibrationOptions options) : ICalibrationWorkspace
{
    public string ScopeHash => FactoryArtifact.Hash(FactoryRules.Serialize(options));
    public bool ScopeConfigured => options.EditableRoots.Length > 0;
    public bool ProcessLifetimeSupported => LocalProcessHost.Supported;
    public string PathFor(Guid id) => Path.GetFullPath(Path.Combine(agents.WorkspaceRoot, id + ".calibration"));
    private const string Folder = ".canopy-calibration";
    private static string Mirror(int index) => $"{Folder}/targets/{index}/SKILL.md";
    private static string Reference(FactoryCalibration job) => $"refs/canopy/calibration/{job.Id:N}/input";
    public async Task<IReadOnlyList<CalibrationSkill>> Resolve(string repository, IReadOnlyList<FactorySkill> targets, bool writable, CancellationToken ct)
    {
        if (targets.Count > options.MaximumTargets) throw new RuleException("Calibration skill count exceeds configured capacity.");
        var found = await catalogue.Read(repository, ct); var results = new List<CalibrationSkill>();
        foreach (var reference in targets)
        {
            ct.ThrowIfCancellationRequested(); var entry = found.Resolve(reference); var path = entry.Paths.Single();
            if (Path.GetFileName(path) != "SKILL.md") throw new RuleException("Calibration uses existing canonical SKILL.md files only.");
            if (writable && !options.EditableRoots.Any(root => Inside(path, root, repository))) throw new RuleException("The selected skill is outside administrator-configured edit roots.");
            var content = markdown.Read(path); if (content.Hash != entry.Hash) throw new RuleException("Skill content changed during catalogue resolution.", 409);
            if (Encoding.UTF8.GetByteCount(content.Markdown) > options.MaximumProposalBytes) throw new RuleException("Selected skill exceeds the configured proposal byte bound.");
            results.Add(new(reference, content.Path, content.Hash, content.Markdown));
        }
        if (results.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count() != results.Count) throw new RuleException("Multiple selected identities resolve to the same canonical skill file.");
        return results;
    }
    private static bool Inside(string file, string configured, string repository)
    {
        if (string.IsNullOrWhiteSpace(configured)) throw new RuleException("Configure nonempty canonical skill edit roots.");
        var root = Path.GetFullPath(configured, repository); LocalVerificationEnvironment.RejectLinks(root);
        if (!Directory.Exists(root)) throw new RuleException("A configured canonical skill edit root is unavailable.");
        var relative = Path.GetRelativePath(root, file); return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
    public async Task<string> Current(CalibrationSkill target, string repository, CancellationToken ct, bool writable = true)
    {
        var current = (await Resolve(repository, [target.Reference], writable, ct)).Single(); if (current.Path != target.Path) throw new RuleException("Canonical skill resolution changed.", 409); return current.Hash;
    }
    public async Task<string> Apply(CalibrationSkill target, string repository, CalibrationEdit edit, CancellationToken ct)
    {
        if (await Current(target, repository, ct) != target.Hash) throw new RuleException("Canonical skill changed outside this proposal. Refresh and review it.", 409);
        if (FactoryArtifact.Hash(edit.Markdown) != edit.AfterHash) throw new RuleException("Stored candidate bytes do not match the proposal hash.");
        return markdown.Write(target.Path, target.Hash, edit.Markdown).Hash;
    }
    public async Task<CalibrationWorkspaceInput> Prepare(FactoryCalibration job, CancellationToken ct)
    {
        var path = PathFor(job.Id); LocalVerificationEnvironment.RejectLinks(path); Directory.CreateDirectory(agents.WorkspaceRoot);
        // Fetch only the already configured repository remote; never publish refs.
        await git.Git(job.Capture.RepositoryPath, ["fetch", "--no-tags", "--", job.Capture.RemoteName, job.Capture.TargetBranch], ct, environment: new Dictionary<string, string> { ["GIT_TERMINAL_PROMPT"] = "0" });
        if ((await git.Git(job.Capture.RepositoryPath, ["rev-parse", "--verify", job.Capture.MergeSha + "^{commit}"], ct)).Trim() != job.Capture.MergeSha) throw new RuleException("The verified merge commit is unavailable locally.");
        if (Directory.Exists(path) || File.Exists(path)) throw new RuleException("The calibration checkout already exists; unfinished work will not be replayed.");
        Directory.CreateDirectory(path);
        // A separate repository keeps private skill blobs out of the product's
        // shared object store and refs. Fetch exactly the verified product commit.
        await git.Git(path, ["init", "--quiet"], ct);
        await git.Git(path, ["fetch", "--no-tags", "--", job.Capture.RepositoryPath, job.Capture.MergeSha], ct, environment: new Dictionary<string, string> { ["GIT_TERMINAL_PROMPT"] = "0" });
        await git.Git(path, ["checkout", "--detach", job.Capture.MergeSha], ct);
        if (Directory.Exists(Path.Combine(path, Folder)) || File.Exists(Path.Combine(path, Folder))) throw new RuleException("The reserved calibration workspace path already exists in this repository.");
        var folder = Path.Combine(path, Folder); Directory.CreateDirectory(Path.Combine(folder, "calibrator"));
        await File.WriteAllTextAsync(Path.Combine(folder, "calibrator/SKILL.md"), job.Capture.Calibrator.Markdown, new UTF8Encoding(false, true), ct);
        await File.WriteAllTextAsync(Path.Combine(folder, "evidence.json"), job.Capture.EvidenceJson, new UTF8Encoding(false, true), ct);
        for (var i = 0; i < job.Capture.Targets.Count; i++)
        { var target = Path.Combine(path, Mirror(i)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); await File.WriteAllTextAsync(target, job.Capture.Targets[i].Markdown, new UTF8Encoding(false, true), ct); }
        await git.Git(path, ["add", "--force", "--", Folder], ct); var tree = (await git.Git(path, ["write-tree"], ct)).Trim();
        await git.Git(path, ["update-ref", Reference(job), tree, ""], ct);
        return new(tree, await Files(path, ct));
    }
    private async Task<IReadOnlyDictionary<string, string>> Files(string root, CancellationToken ct)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal); long bytes = 0; var pending = new Stack<string>(); pending.Push(root); var directories = 0;
        while (pending.TryPop(out var directory))
        {
            ct.ThrowIfCancellationRequested(); LocalVerificationEnvironment.RejectLinks(directory);
            if (directory != root) files.Add(Path.GetRelativePath(root, directory).Replace(Path.DirectorySeparatorChar, '/') + "/", "directory:" + (OperatingSystem.IsWindows() ? "" : ((int)File.GetUnixFileMode(directory)).ToString()));
            if (++directories > options.MaximumWorkspaceFiles) throw new RuleException("Calibration workspace directory coverage exceeds configured bounds.");
            foreach (var item in Directory.EnumerateFileSystemEntries(directory))
            {
                ct.ThrowIfCancellationRequested(); LocalVerificationEnvironment.RejectLinks(item);
                var relative = Path.GetRelativePath(root, item).Replace(Path.DirectorySeparatorChar, '/');
                // Git may update its objects/index/logs/refs while the CLI runs.
                // Audit static metadata (including config and hooks) as files;
                // HEAD and the immutable input ref are also checked explicitly.
                if (relative is ".git/objects" or ".git/logs" or ".git/refs" or ".git/index" || relative.StartsWith(".git/objects/", StringComparison.Ordinal) || relative.StartsWith(".git/logs/", StringComparison.Ordinal) || relative.StartsWith(".git/refs/", StringComparison.Ordinal)) continue;
                if (Directory.Exists(item)) { pending.Push(item); continue; }
                if (files.Count >= options.MaximumWorkspaceFiles) throw new RuleException("Calibration workspace file coverage exceeds configured bounds.");
                var info = new FileInfo(item); bytes = checked(bytes + info.Length); if (bytes > options.MaximumWorkspaceBytes) throw new RuleException("Calibration workspace coverage exceeds configured byte bounds.");
                await using var stream = File.OpenRead(item); var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
                files.Add(Path.GetRelativePath(root, item).Replace(Path.DirectorySeparatorChar, '/'), hash + ":" + (OperatingSystem.IsWindows() ? "" : ((int)File.GetUnixFileMode(item)).ToString()));
            }
        }
        if (Encoding.UTF8.GetByteCount(FactoryRules.Serialize(files)) > options.MaximumManifestBytes) throw new RuleException("Calibration workspace manifest exceeds configured byte bounds.");
        return files;
    }
    public async Task<IReadOnlyList<CalibrationEdit>> Inspect(FactoryCalibration job, CancellationToken ct)
    {
        var root = PathFor(job.Id); if (job.WorkspacePath != root || job.Input is not { } input) throw new RuleException("The captured calibration workspace is unavailable.");
        var after = await Files(root, ct); var permitted = Enumerable.Range(0, job.Capture.Targets.Count).ToDictionary(Mirror, x => x, StringComparer.Ordinal);
        foreach (var path in input.Files.Keys.Union(after.Keys))
        {
            input.Files.TryGetValue(path, out var before); after.TryGetValue(path, out var observed);
            if (before == observed) continue;
            if (!permitted.ContainsKey(path) || before is null || observed is null || before.Split(':')[1] != observed.Split(':')[1]) throw new RuleException($"Unexpected calibration path, deletion or permission edit: '{path}'. Inspect the retained worktree; no canonical edit is authorized.");
        }
        if ((await git.Git(root, ["rev-parse", "HEAD"], ct)).Trim() != job.Capture.MergeSha || (await git.Git(root, ["rev-parse", "--verify", Reference(job) + "^{tree}"], ct)).Trim() != input.Tree) throw new RuleException("Calibration Git identity or captured reference changed.");
        var calibrator = (await Resolve(job.Capture.RepositoryPath, [job.Capture.Calibrator.Reference], false, ct)).Single();
        if (calibrator.Path != job.Capture.Calibrator.Path || calibrator.Hash != job.Capture.Calibrator.Hash) throw new RuleException("The installed calibrator changed during execution.");
        var edits = new List<CalibrationEdit>(); long bytes = 0;
        for (var i = 0; i < job.Capture.Targets.Count; i++)
        {
            var original = job.Capture.Targets[i]; if (await Current(original, job.Capture.RepositoryPath, ct) != original.Hash) throw new RuleException("A canonical skill changed during isolated execution. No adoption will occur.", 409);
            var path = Mirror(i); var file = markdown.Read(Path.Combine(root, path));
            // Capture Markdown writer bytes, independent of a source UTF-8 BOM.
            if (file.Markdown == original.Markdown) continue;
            if (string.IsNullOrWhiteSpace(file.Markdown)) throw new RuleException("Calibration cannot replace an existing skill with empty instructions.");
            var beforePath = PathFor(job.Id) + "." + Guid.NewGuid() + ".before.md"; LocalVerificationEnvironment.RejectLinks(beforePath);
            string diff;
            try
            {
                await using (var output = new FileStream(beforePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await output.WriteAsync(new UTF8Encoding(false, true).GetBytes(original.Markdown), ct);
                diff = await verification.ReadGit(root, ["diff", "--no-index", "--no-ext-diff", "--no-textconv", "--binary", "--", beforePath, Path.Combine(root, path)], ct, true);
            }
            finally { File.Delete(beforePath); }
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(file.Markdown) + Encoding.UTF8.GetByteCount(diff));
            if (bytes > options.MaximumProposalBytes) throw new RuleException("Actual calibration proposal exceeds configured byte bounds; no partial proposal was accepted.");
            edits.Add(new(i, original.Hash, FactoryArtifact.Hash(file.Markdown), file.Markdown, diff));
        }
        return edits;
    }
}
