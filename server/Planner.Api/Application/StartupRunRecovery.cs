namespace Planner.Api.Application;

// Runs before HTTP/worker startup, so new requests cannot be mistaken for work
// left active by a previous backend process. Never automatically replay work.
public sealed class StartupRunRecovery(IWorkspaceStore store)
{
    public Task Recover(CancellationToken ct) => store.WriteAsync(data =>
    {
        foreach (var child in data.AgentRuns.Where(x => x.Active)) child.Finish("interrupted", child.Result, child.Diff, "Canopy restarted before this run finished. Review its workspace before continuing.");
        foreach (var run in data.FactoryRuns.Where(x => x.Active)) FactoryDispatchService.End(data, run, "interrupted", "Canopy restarted. Review the retained worktree and evidence before preparing new work.");
    }, ct);
}
