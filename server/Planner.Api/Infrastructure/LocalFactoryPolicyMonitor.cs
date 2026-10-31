using Planner.Api.Application;

namespace Planner.Api.Infrastructure;

public sealed class LocalFactoryPolicyMonitor(FactoryExecutionPolicyService policies, IAgentEnvironment agents, ILogger<LocalFactoryPolicyMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await policies.Enforce(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Execution policy monitor could not establish current limits"); }
            await Task.Delay(agents.PollIntervalMs, ct);
        }
    }
}
