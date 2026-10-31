using System.Security.Cryptography;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalFactoryParallelWorkspace(IWorkspaceStore store, LocalAgentEnvironment agents, LocalVerificationEnvironment verification,
    AgentOptions options, FactoryParallelOptions limits) : IFactoryParallelWorkspace
{
    public string StagePath(FactoryRun run, Guid stageId) => Path.GetFullPath(Path.Combine(options.WorkspaceRoot, run.Id + ".stages", stageId.ToString()));
    private string DirectoryFor(FactoryRun run) => Path.GetFullPath(Path.Combine(options.WorkspaceRoot, run.Id + ".stages"));
    private string IndexFor(FactoryRun run) => Path.Combine(DirectoryFor(run), "initial.index");
    // Private refs are execution artifact addresses derived only from native IDs.
    private static string Reference(FactoryRun run, string suffix) => $"refs/canopy/factory/{run.Id:N}/{suffix}";
    private async Task Pin(FactoryRun run, string suffix, string tree, CancellationToken ct)
    { await agents.Git(run.Snapshot.Environment.RepositoryPath, ["update-ref", Reference(run, suffix), tree, ""], ct); }
    private async Task RequirePin(FactoryRun run, string suffix, string tree, CancellationToken ct)
    {
        if ((await agents.Git(run.Snapshot.Environment.RepositoryPath, ["rev-parse", "--verify", "--end-of-options", Reference(run, suffix) + "^{tree}"], ct)).Trim() != tree)
            throw new RuleException("A captured parallel tree reference changed or is unavailable. Inspect retained evidence.", 409);
    }
    private async Task<string> HashFile(string path, CancellationToken ct)
    {
        LocalVerificationEnvironment.RejectLinks(path);
        if (!File.Exists(path) || new FileInfo(path).Length > limits.MaximumIndexBytes) throw new RuleException("The captured Git index is unavailable or exceeds the configured byte limit.");
        await using var file = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
    }
    private async Task<string> CopyIndex(string root, string destination, CancellationToken ct)
    {
        var source = (await verification.ReadGit(root, ["rev-parse", "--path-format=absolute", "--git-path", "index"], ct)).Trim();
        var originalHash = await HashFile(source, ct); LocalVerificationEnvironment.RejectLinks(destination); File.Copy(source, destination, false);
        // Split indexes refer to a worktree-local sharedindex file. Expand only
        // the retained copy, preserving every logical staged entry and flag.
        await agents.Git(root, ["update-index", "--no-split-index"], ct, environment: new Dictionary<string, string> { ["GIT_INDEX_FILE"] = destination });
        if (await HashFile(source, ct) != originalHash) throw new RuleException("Git index changed during capture.", 409);
        return await HashFile(destination, ct);
    }
    private async Task<ParallelTree> Snapshot(FactoryRun run, string root, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows()) throw new RuleException("Parallel file-mode capture requires the supported process/workspace adapter.", 503);
        var index = (await verification.ReadGit(root, ["rev-parse", "--path-format=absolute", "--git-path", "index"], ct)).Trim();
        await HashFile(index, ct);
        var temporary = Path.Combine(DirectoryFor(run), Guid.NewGuid() + ".index"); LocalVerificationEnvironment.RejectLinks(temporary);
        await CopyIndex(root, temporary, ct);
        try
        {
            var env = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = temporary };
            await agents.Git(root, ["add", "--all", "--", "."], ct, environment: env);
            await HashFile(temporary, ct);
            var tree = (await agents.Git(root, ["write-tree"], ct, environment: env)).Trim();
            var paths = (await verification.ReadGit(root, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], ct)).Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct().Order(StringComparer.Ordinal);
            var modes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var relative in paths)
            {
                ct.ThrowIfCancellationRequested(); var file = LocalVerificationEnvironment.SafeFile(root, relative);
                if (Directory.Exists(file)) throw new RuleException("Parallel snapshots do not support submodules.");
                if (File.Exists(file)) modes.Add(relative, (int)File.GetUnixFileMode(file));
            }
            return new(tree, modes);
        }
        finally { File.Delete(temporary); }
    }
    public async Task Initialize(FactoryRun run, CancellationToken ct)
    {
        var before = await verification.Observe(run, ct); var root = verification.Workspace(run);
        Directory.CreateDirectory(DirectoryFor(run)); LocalVerificationEnvironment.RejectLinks(DirectoryFor(run));
        var hash = await CopyIndex(root, IndexFor(run), ct);
        var tree = await Snapshot(run, root, ct); await Pin(run, "initial", tree.Tree, ct);
        if ((await verification.Observe(run, ct)).Fingerprint != before.Fingerprint || await HashFile(IndexFor(run), ct) != hash) throw new RuleException("Initial parallel source/index changed during capture.", 409);
        await store.WriteAsync(data => { var current = FactoryDispatchService.Find(data, run.Id); FactoryExecutionPolicyService.Require(data, current); data.ParallelRuns.Single(x => x.Id == run.Id).Ready(before.HeadCommit, hash, tree, before); current.Touch(); }, ct);
    }
    private async Task<FactoryParallelRun> RootRecord(FactoryRun run, CancellationToken ct)
    {
        var record = (await store.ReadAsync(ct)).ParallelRuns.Single(x => x.Id == run.Id);
        if (record.Initial is null || record.InitialContext is null || await HashFile(IndexFor(run), ct) != record.IndexHash) throw new RuleException("Initial parallel workspace/index evidence is unavailable.");
        await RequirePin(run, "initial", record.Initial.Tree, ct); return record;
    }
    private async Task RequireRoot(FactoryRun run, FactoryParallelRun record, CancellationToken ct)
    { if ((await verification.Observe(run, ct)).Fingerprint != record.InitialContext!.Fingerprint) throw new RuleException("The final run worktree changed while branches were isolated. No branch will overwrite it.", 409); }
    public async Task Normalize(FactoryRun run, FactoryStageRun stage, CancellationToken ct)
    {
        var record = await RootRecord(run, ct); var root = StagePath(run, stage.Id);
        LocalVerificationEnvironment.RejectLinks(root);
        // Normalize only this owned stage's HEAD/index; preserve working code and
        // every original commit in its retained object history/native evidence.
        await agents.Git(root, ["reset", "--mixed", record.Head], ct);
        var index = (await verification.ReadGit(root, ["rev-parse", "--path-format=absolute", "--git-path", "index"], ct)).Trim();
        LocalVerificationEnvironment.RejectLinks(index); File.Copy(IndexFor(run), index, true);
    }
    private async Task<ParallelTree> Compose(FactoryRun run, string destination, IReadOnlyList<FactoryStage> ancestors, FactoryParallelRun record, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows()) throw new RuleException("Parallel file-mode capture requires the supported process/workspace adapter.", 503);
        await agents.Git(destination, ["read-tree", "--reset", "-u", record.Initial!.Tree], ct);
        IReadOnlyDictionary<string, int> modes = record.Initial.Modes;
        var data = await store.ReadAsync(ct);
        foreach (var ancestor in ancestors)
        {
            ct.ThrowIfCancellationRequested();
            var stage = data.FactoryStageRuns.Single(x => x.RunId == run.Id && x.StageId == ancestor.Id);
            var capture = data.ParallelStages.Single(x => x.Id == stage.Id);
            if (stage.State != "succeeded" || capture.State != "complete" || capture.Input is null || capture.Output is null) throw new RuleException("A parallel dependency has no complete captured output.");
            await RequirePin(run, stage.Id.ToString("N") + "/input", capture.Input.Tree, ct);
            await RequirePin(run, stage.Id.ToString("N") + "/output", capture.Output.Tree, ct);
            var patch = Path.Combine(DirectoryFor(run), Guid.NewGuid() + ".patch"); LocalVerificationEnvironment.RejectLinks(patch);
            await agents.Git(destination, ["diff", "--no-ext-diff", "--no-textconv", "--binary", "--full-index", "--output=" + patch, capture.Input.Tree, capture.Output.Tree, "--"], ct);
            if (new FileInfo(patch).Length > limits.MaximumPatchBytes) throw new RuleException("Parallel dependency patch exceeds the configured byte limit. Coverage is incomplete.");
            try { await agents.Git(destination, ["apply", "--3way", "--index", "--allow-empty", "--whitespace=nowarn", patch], ct); }
            catch (RuleException error) { throw new RuleException("Parallel content join failed; inspect retained workspaces and patch before preparing new work. " + error.Message, 409); }
            modes = FactoryParallel.MergeModes(modes, capture.Input, capture.Output);
        }
        foreach (var (path, mode) in modes) { var file = LocalVerificationEnvironment.SafeFile(destination, path); if (!File.Exists(file)) throw new RuleException("A joined source file is unavailable."); File.SetUnixFileMode(file, (UnixFileMode)mode); }
        var tree = (await agents.Git(destination, ["write-tree"], ct)).Trim(); return new(tree, modes);
    }
    public async Task Materialize(FactoryRun run, FactoryStageRun stage, CancellationToken ct)
    {
        var record = await RootRecord(run, ct); await RequireRoot(run, record, ct); var path = StagePath(run, stage.Id);
        await agents.Git(run.Snapshot.Environment.RepositoryPath, ["worktree", "add", "--detach", path, record.Head], ct);
        var tree = await Compose(run, path, FactoryParallel.Ordered(run.Snapshot.Environment.Definition, stage.StageId), record, ct);
        await Pin(run, stage.Id.ToString("N") + "/input", tree.Tree, ct); await Normalize(run, stage, ct);
        var context = await verification.Observe(run.InspectStage(stage.Id, path), ct); await RequireRoot(run, record, ct);
        await store.WriteAsync(data => { FactoryExecutionPolicyService.Require(data, FactoryDispatchService.Find(data, run.Id)); data.ParallelStages.Single(x => x.Id == stage.Id).Ready(tree, context); }, ct);
    }
    public async Task CheckStage(FactoryRun run, FactoryStageRun stage, CancellationToken ct)
    {
        var capture = (await store.ReadAsync(ct)).ParallelStages.Single(x => x.Id == stage.Id);
        if ((await verification.Observe(run.InspectStage(stage.Id, StagePath(run, stage.Id)), ct)).Fingerprint != capture.Checkpoint?.Fingerprint) throw new RuleException("Stage worktree changed while waiting for its explicit decision.", 409);
    }
    public async Task CaptureStep(FactoryRun run, FactoryStageRun stage, Guid stepId, CancellationToken ct)
    {
        var view = run.InspectStage(stage.Id, StagePath(run, stage.Id));
        var before = await verification.Observe(view, ct); var files = await Snapshot(run, view.WorkspacePath!, ct);
        var captured = Path.Combine(DirectoryFor(run), $"{stage.Id:N}-{stepId:N}.index");
        var hash = await CopyIndex(view.WorkspacePath!, captured, ct);
        if (await HashFile(captured, ct) != hash || (await verification.Observe(view, ct)).Fingerprint != before.Fingerprint) throw new RuleException("Step Git state changed during its checkpoint.", 409);
        await Pin(run, $"{stage.Id:N}/step/{stepId:N}/head", before.HeadCommit, ct); await Pin(run, $"{stage.Id:N}/step/{stepId:N}/tree", files.Tree, ct);
        await store.WriteAsync(data => data.ParallelStages.Single(x => x.Id == stage.Id).CaptureStep(new(stepId, before, hash, files)), ct);
    }
    public async Task CompleteStage(FactoryRun run, FactoryStageRun stage, CancellationToken ct)
    {
        await Normalize(run, stage, ct); var view = run.InspectStage(stage.Id, StagePath(run, stage.Id));
        var before = await verification.Observe(view, ct); var tree = await Snapshot(run, view.WorkspacePath!, ct);
        if ((await verification.Observe(view, ct)).Fingerprint != before.Fingerprint) throw new RuleException("Parallel stage changed during output capture.", 409);
        await Pin(run, stage.Id.ToString("N") + "/output", tree.Tree, ct);
        await store.WriteAsync(data => { FactoryExecutionPolicyService.Require(data, FactoryDispatchService.Find(data, run.Id)); data.ParallelStages.Single(x => x.Id == stage.Id).Complete(tree, before); data.FactoryStageRuns.Single(x => x.Id == stage.Id).End("succeeded"); FactoryDispatchService.Find(data, run.Id).Touch(); }, ct);
    }
    public async Task Integrate(FactoryRun run, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows()) throw new RuleException("Parallel file-mode capture requires the supported process/workspace adapter.", 503);
        var record = await RootRecord(run, ct); await RequireRoot(run, record, ct);
        // Build the final join in another retained worktree first. A conflict must
        // not partially modify the actual QA/delivery worktree.
        var destination = Path.Combine(DirectoryFor(run), "integration");
        await agents.Git(run.Snapshot.Environment.RepositoryPath, ["worktree", "add", "--detach", destination, record.Head], ct);
        var tree = await Compose(run, destination, FactoryParallel.Ordered(run.Snapshot.Environment.Definition), record, ct);
        await Pin(run, "final", tree.Tree, ct); await RequireRoot(run, record, ct);
        await store.WriteAsync(data => { FactoryExecutionPolicyService.Require(data, FactoryDispatchService.Find(data, run.Id)); data.ParallelRuns.Single(x => x.Id == run.Id).Integrating(); }, ct);
        await RequireRoot(run, record, ct);
        var root = verification.Workspace(run); await agents.Git(root, ["read-tree", record.Initial!.Tree], ct); await agents.Git(root, ["read-tree", "--reset", "-u", tree.Tree], ct);
        foreach (var (path, mode) in tree.Modes) File.SetUnixFileMode(LocalVerificationEnvironment.SafeFile(root, path), (UnixFileMode)mode);
        var index = (await verification.ReadGit(root, ["rev-parse", "--path-format=absolute", "--git-path", "index"], ct)).Trim(); LocalVerificationEnvironment.RejectLinks(index); File.Copy(IndexFor(run), index, true);
        var context = await verification.Observe(run, ct); var actual = await Snapshot(run, root, ct);
        if (actual.Tree != tree.Tree || actual.Modes.Count != tree.Modes.Count || actual.Modes.Any(x => !tree.Modes.TryGetValue(x.Key, out var mode) || x.Value != mode)) throw new RuleException("The final worktree does not match its captured parallel integration.", 409);
        await store.WriteAsync(data => { FactoryExecutionPolicyService.Require(data, FactoryDispatchService.Find(data, run.Id)); data.ParallelRuns.Single(x => x.Id == run.Id).Complete(tree, context); FactoryDispatchService.End(data, FactoryDispatchService.Find(data, run.Id), "succeeded", null); }, ct);
    }
}
