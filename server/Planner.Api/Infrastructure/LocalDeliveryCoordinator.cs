using Planner.Api.Application;

namespace Planner.Api.Infrastructure;

public sealed class LocalDeliveryCoordinator(IWorkspaceStore store, FactoryDeliveryService deliveries, IAgentEnvironment agents, ILogger<LocalDeliveryCoordinator> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var data = await store.ReadAsync(ct); var operation = data.DeliveryOperations.Where(x => x.State == "queued").OrderBy(x => x.CreatedAt).FirstOrDefault();
            if (operation is null) { await Task.Delay(agents.PollIntervalMs, ct); continue; }
            try { await deliveries.Execute(operation.Id, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Delivery operation {OperationId} stopped; retained intent requires reconciliation", operation.Id); await Task.Delay(agents.PollIntervalMs, ct); }
        }
    }
}
