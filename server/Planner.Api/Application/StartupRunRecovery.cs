using Planner.Api.Domain;
namespace Planner.Api.Application;

// Runs before HTTP/worker startup, so new requests cannot be mistaken for work
// left active by a previous backend process. Never automatically replay work.
public sealed class StartupRunRecovery(IWorkspaceStore store)
{
    public Task Recover(CancellationToken ct) => store.WriteAsync(data =>
    {
        foreach (var receipt in data.CalibrationTriggers.Where(x => x.State == "observing")) receipt.End("interrupted", null, "Backend restarted. Eligibility observation was not replayed.");
        foreach (var calibration in data.Calibrations.Where(x => x.Active)) calibration.Stop("interrupted", "Backend restarted. Calibration and canonical writes were not replayed; reconcile any file adoption intents.");
        foreach (var operation in data.DeploymentOperations.Where(x => x.Pending))
        {
            operation.End("uncertain", new { interrupted = true }, "Backend restarted. Deployment commands were not replayed.");
            var deployment = data.Deployments.Single(x => x.Id == operation.DeploymentId);
            deployment.Change("needsAttention", "Interrupted deployment. Observe the target or explicitly approve configured recovery; no command was replayed."); deployment.Release(operation.Id);
        }
        foreach (var collection in data.IntakeCollections.Where(x => x.State == "running")) collection.Finish("interrupted", "Backend restarted. Source coverage is unavailable; collection was not replayed.");
        foreach (var operation in data.DeliveryOperations.Where(x => x.Pending))
        {
            operation.End("uncertain", "Backend restarted. External outcomes must be reconciled; no action was replayed.");
            var delivery = data.Deliveries.Single(x => x.Id == operation.DeliveryId);
            delivery.Change("needsAttention", "Interrupted delivery. Reconcile actual provider/hook results before another action."); delivery.Release(operation.Id);
        }
        foreach (var loop in data.RepairLoops.Where(x => x.Open)) loop.End(false, "Canopy restarted. Unfinished repair loops are stopped without replay; inspect retained attempts before explicitly preparing new work.");
        foreach (var child in data.AgentRuns.Where(x => x.Active)) child.Finish("interrupted", child.Result, child.Diff, "Canopy restarted before this run finished. Review its workspace before continuing.");
        // A guided build waiting for the person to keep it has nothing executing, so it keeps waiting across a restart.
        foreach (var run in data.FactoryRuns.Where(x => x.Active && !(x.State == "waiting" && x.Snapshot.Guided is not null && !FactoryParallel.Enabled(x)))) FactoryDispatchService.End(data, run, "interrupted", "Canopy restarted. Review the retained worktree and evidence before preparing new work.");
    }, ct);
}
