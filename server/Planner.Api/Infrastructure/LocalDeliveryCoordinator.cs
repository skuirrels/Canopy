using Planner.Api.Application;

namespace Planner.Api.Infrastructure;

public sealed class LocalDeliveryCoordinator(IWorkspaceStore store, FactoryDeliveryService deliveries, FactoryDeploymentService deployments, IAgentEnvironment agents, ILogger<LocalDeliveryCoordinator> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var data = await store.ReadAsync(ct); var operation = data.DeliveryOperations.Where(x => x.State == "queued").OrderBy(x => x.CreatedAt).FirstOrDefault();
            var deployment = data.DeploymentOperations.Where(x => x.State == "queued").OrderBy(x => x.CreatedAt).FirstOrDefault();
            if (operation is null && deployment is null) { await Task.Delay(agents.PollIntervalMs, ct); continue; }
            try { if (operation is not null && (deployment is null || operation.CreatedAt <= deployment.CreatedAt)) await deliveries.Execute(operation.Id, ct); else await deployments.Execute(deployment!.Id, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Delivery/deployment operation {OperationId} stopped; retained intent requires reconciliation", operation?.Id ?? deployment!.Id); await Task.Delay(agents.PollIntervalMs, ct); }
        }
    }
}
