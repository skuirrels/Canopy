using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalRepairWorkspace(LocalVerificationEnvironment environment) : IRepairWorkspace
{
    public async Task Fork(FactoryRun parent, FactoryRun target, VerificationContext expected, CancellationToken ct)
    {
        var before = await environment.Observe(parent, ct);
        if (before.Fingerprint != expected.Fingerprint) throw new RuleException("The parent worktree changed after repair was queued. No stale code will be copied.", 409);
        var source = environment.Workspace(parent); var destination = environment.Workspace(target);
        if (source == destination) throw new RuleException("Repair attempts require separate worktrees.");
        var files = (await environment.ReadGit(source, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct))
            .Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        // Only this newly created run worktree is cleared. Never follow links or touch .git.
        foreach (var entry in Directory.EnumerateFileSystemEntries(destination))
            if (Path.GetFileName(entry) != ".git") Remove(entry);
        foreach (var relative in files)
        {
            ct.ThrowIfCancellationRequested();
            var input = LocalVerificationEnvironment.SafeFile(source, relative);
            if (!File.Exists(input)) continue; // Real tracked deletion, included in the context hash.
            var output = LocalVerificationEnvironment.SafeFile(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.Copy(input, output, false);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(output, File.GetUnixFileMode(input));
        }
        var sourceIndex = (await environment.ReadGit(source, ["rev-parse", "--path-format=absolute", "--git-path", "index"], ct)).Trim();
        var targetIndex = (await environment.ReadGit(destination, ["rev-parse", "--path-format=absolute", "--git-path", "index"], ct)).Trim();
        LocalVerificationEnvironment.RejectLinks(sourceIndex); LocalVerificationEnvironment.RejectLinks(targetIndex);
        File.Delete(targetIndex); if (File.Exists(sourceIndex)) File.Copy(sourceIndex, targetIndex, false);
        var after = await environment.Observe(parent, ct); var fork = await environment.Observe(target, ct);
        if (after.Fingerprint != expected.Fingerprint || fork.HeadCommit != before.HeadCommit || fork.CodeHash != before.CodeHash)
            throw new RuleException("The fork does not match the captured parent code and index. Inspect retained worktrees before preparing new work.", 409);
    }
    private static void Remove(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (info is DirectoryInfo directory && info.LinkTarget is null)
        {
            foreach (var child in directory.EnumerateFileSystemInfos()) Remove(child.FullName);
            directory.Delete();
        }
        else info.Delete();
    }
}
