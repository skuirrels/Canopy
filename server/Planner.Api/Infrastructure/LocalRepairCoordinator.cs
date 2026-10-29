using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalRepairCoordinator(IWorkspaceStore store, FactoryRepairService repairs, IAgentEnvironment agents, ILogger<LocalRepairCoordinator> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var data = await store.ReadAsync(ct);
            foreach (var loop in data.RepairLoops.Where(x => x.Open))
            {
                try { await repairs.Reconcile(loop.Id, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (RuleException error) when (error.Status == 404) { /* A terminal loop was deleted after the snapshot. */ }
                catch (Exception error)
                {
                    logger.LogError(error, "Repair coordination failed for {LoopId}; stopping without replay", loop.Id);
                    // Cancel active processes as well as marking the loop for stopping.
                    await repairs.Halt(loop.Id, "Repair coordination failed. Inspect retained evidence before preparing new work.", ct);
                }
            }
            await Task.Delay(agents.PollIntervalMs, ct);
        }
    }
}
