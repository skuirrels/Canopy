using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalBuildRepository(LocalAgentEnvironment git) : IBuildRepository
{
    private const int MaximumFiles = 300;

    // Files changed in the build's working copy since it started, committed or not.
    public async Task<BuildChanges?> Changes(FactoryRun run, CancellationToken ct)
    {
        if (run.WorkspacePath is not { } workspace || !Directory.Exists(workspace)) return null;
        var files = new List<BuildFile>();
        foreach (var line in (await git.Git(workspace, ["diff", "--numstat", "--no-renames", run.Snapshot.Environment.BaseCommit, "--"], ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 3);
            if (parts.Length == 3) files.Add(new(parts[2], int.TryParse(parts[0], out var added) ? added : null, int.TryParse(parts[1], out var removed) ? removed : null, false));
        }
        foreach (var path in (await git.Git(workspace, ["ls-files", "--others", "--exclude-standard", "-z"], ct)).Split('\0', StringSplitOptions.RemoveEmptyEntries))
            files.Add(new(path.Trim(), Lines(Path.Combine(workspace, path.Trim())), null, true));
        return new(files.Take(MaximumFiles).ToList(), files.Count > MaximumFiles);
    }

    public async Task<string> Candidate(FactoryRun run, CancellationToken ct)
    {
        if (run.WorkspacePath is not { } workspace || !Directory.Exists(workspace)) throw new RuleException("This build's working copy no longer exists. Start a new build.", 409);
        return (await git.Git(workspace, ["rev-parse", "HEAD"], ct)).Trim();
    }

    // Merges the build's commit into its base branch without touching the build's working copy, then moves the branch.
    // A checked-out branch is only fast-forwarded when its working tree has no uncommitted changes.
    public async Task<string> Keep(FactoryRun run, string message, CancellationToken ct)
    {
        var repository = run.Snapshot.Environment.RepositoryPath; var branch = run.Snapshot.Environment.BaseRef;
        var candidate = await Candidate(run, ct);
        string tip;
        try { tip = (await git.Git(repository, ["rev-parse", "--verify", "--end-of-options", $"refs/heads/{branch}^{{commit}}"], ct)).Trim(); }
        catch (RuleException) { throw new RuleException($"Keeping a build merges it into a local branch, but this project builds from '{branch}'. Choose a local branch in the project's connection.", 409); }
        if (candidate == run.Snapshot.Environment.BaseCommit) throw new RuleException("This build has no committed changes to keep.", 409);
        if ((await git.Git(repository, ["merge-base", candidate, tip], ct)).Trim() == candidate) return tip;
        var merged = (await git.Git(repository, ["merge-tree", "--write-tree", "--name-only", "--no-messages", tip, candidate], ct, acceptDiff: true))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (merged.Length == 0) throw new RuleException("Git could not merge the build.");
        if (merged.Length > 1)
            throw new RuleException($"{branch} has changed since this build started, and both change {string.Join(", ", merged.Skip(1).Distinct().Take(5))}. Merge the build by hand, or build the task again from the latest {branch}.", 409);
        if ((await git.Git(repository, ["config", "user.email"], ct, acceptDiff: true)).Trim().Length == 0)
            throw new RuleException("Git needs your name and email to record the merge. Run git config user.name and git config user.email in the repository, then press Keep again.", 409);
        var commit = (await git.Git(repository, ["commit-tree", merged[0], "-p", tip, "-p", candidate, "-m", message], ct)).Trim();
        var checkedOut = CheckedOut(await git.Git(repository, ["worktree", "list", "--porcelain"], ct), branch);
        if (checkedOut is null)
        {
            await git.Git(repository, ["update-ref", $"refs/heads/{branch}", commit, tip], ct);
            return commit;
        }
        if ((await git.Git(checkedOut, ["status", "--porcelain", "--untracked-files=no"], ct)).Trim().Length > 0)
            throw new RuleException($"{branch} is checked out at {checkedOut} with uncommitted changes. Commit or stash them, then press Keep again.", 409);
        try { await git.Git(checkedOut, ["merge", "--ff-only", commit], ct); }
        catch (RuleException error) { throw new RuleException($"Git could not update {branch} at {checkedOut}. {error.Message}", 409); }
        return commit;
    }

    private static string? CheckedOut(string worktrees, string branch)
    {
        string? path = null;
        foreach (var line in worktrees.Split('\n'))
        {
            if (line.StartsWith("worktree ")) path = line[9..].Trim();
            else if (line.Trim() == $"branch refs/heads/{branch}") return path;
        }
        return null;
    }
    private static int? Lines(string path)
    {
        try { var info = new FileInfo(path); return info.Exists && info.Length <= 1_000_000 ? File.ReadLines(path).Count() : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
