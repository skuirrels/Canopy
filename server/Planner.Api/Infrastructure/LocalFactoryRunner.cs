using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalFactoryRunner(IWorkspaceStore store, LocalAgentEnvironment agents, LocalFactoryStepExecutor executor,
    AgentOptions options, IRepairWorkspace repairWorkspace, ILogger<LocalFactoryRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var data = await store.ReadAsync(stoppingToken);
            var run = data.FactoryRuns.FirstOrDefault(x => !FactoryParallel.Enabled(x) && x.State is "queued" or "running" or "cancelling");
            if (run is not null) await Advance(run.Id, stoppingToken);
            else await Task.Delay(options.PollIntervalMs, stoppingToken);
        }
    }
    private async Task Advance(Guid id, CancellationToken stoppingToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var activeDeadline = DateTime.UtcNow.AddMinutes(options.MaxRunMinutes);
        lifetime.CancelAfter(TimeSpan.FromMinutes(options.MaxRunMinutes)); agents.Register(id, lifetime);
        try
        {
            var ct = lifetime.Token; var data = await store.ReadAsync(ct); var run = FactoryDispatchService.Find(data, id);
            if (run.State == "cancelling") { await StopChildren(data, id); await End(id, "cancelled", null); return; }
            FactoryExecutionPolicyService.Require(data, run);
            FactoryExecutionPolicyService.RequireLifetime(run.Snapshot.Execution, LocalProcessHost.Supported);
            if (run.State == "queued")
            {
                var workspace = Path.Combine(options.WorkspaceRoot, run.Id.ToString());
                var begun = await store.WriteAsync(current =>
                {
                    var saved = FactoryDispatchService.Find(current, id);
                    if (saved.State == "queued") { FactoryExecutionPolicyService.Require(current, saved); saved.Begin(workspace); }
                }, ct);
                if (FactoryDispatchService.Find(begun, id).State != "running") return;
                ApplyDeadline(begun, FactoryDispatchService.Find(begun, id), lifetime, activeDeadline);
                Directory.CreateDirectory(options.WorkspaceRoot);
                // A guided revision continues from the previous build's committed result.
                await agents.Git(run.Snapshot.Environment.RepositoryPath, ["worktree", "add", "--detach", workspace, run.Snapshot.Guided?.StartCommit ?? run.Snapshot.Environment.BaseCommit], ct);
                if (run.Snapshot.Repair is { } lineage)
                    await repairWorkspace.Fork(FactoryDispatchService.Find(begun, lineage.ParentRunId), FactoryDispatchService.Find(begun, id), lineage.ParentContext, ct);
            }
            var budgetData = await store.ReadAsync(ct);
            ApplyDeadline(budgetData, FactoryDispatchService.Find(budgetData, id), lifetime, activeDeadline);
            data = await store.WriteAsync(current =>
            {
                var saved = FactoryDispatchService.Find(current, id);
                if (saved.State != "running") return;
                foreach (var stage in current.FactoryStageRuns.Where(x => x.RunId == id && x.State is "running" or "waiting"))
                    if (current.FactoryStepRuns.Where(x => x.StageRunId == stage.Id).All(x => x.State == "succeeded")) { stage.End("succeeded"); saved.Touch(); }
            }, ct);
            run = FactoryDispatchService.Find(data, id);
            if (run.State != "running") return;
            var stages = data.FactoryStageRuns.Where(x => x.RunId == id).ToList(); var steps = data.FactoryStepRuns.Where(x => x.RunId == id).ToList();
            if (stages.All(x => x.State == "succeeded")) { await End(id, "succeeded", null); return; }
            var next = FactoryExecution.Next(run, stages, steps) ?? throw new RuleException("No executable stage is ready; inspect retained state.");
            if (next.StepRun.State != "pending") throw new RuleException("The next step did not complete. Review its evidence before preparing new work.");
            if (next.Step.Kind == "manual")
            {
                executor.CheckInputs(data, run.Id, next.Step);
                // In a guided build, pressing Build approves any confirmation that comes before work exists.
                // A manual step that consumes results is the person's keep-or-revise decision, so it waits.
                var startedWithBuild = run.Snapshot.Guided is not null && !(next.Step.Consumes?.Any() ?? false);
                await store.WriteAsync(current =>
                {
                    var saved = FactoryDispatchService.Find(current, id); if (saved.State != "running") return;
                    var stage = current.FactoryStageRuns.Single(x => x.Id == next.StageRun.Id); var step = current.FactoryStepRuns.Single(x => x.Id == next.StepRun.Id);
                    FactoryExecutionPolicyService.Require(current, saved);
                    stage.Begin(); step.Begin(); step.Wait(); stage.Wait(); saved.Wait();
                    if (!startedWithBuild) return;
                    step.Decide(saved.StartRequestId!.Value, true, "Approved by pressing Build.");
                    FactoryDispatchService.Capture(current, saved, step, next.Step); stage.Continue(); saved.Continue();
                }, ct);
                return;
            }
            await executor.Execute(run, next, ct);
        }
        catch (OperationCanceledException)
        {
            var data = await store.ReadAsync(CancellationToken.None); var run = FactoryDispatchService.Find(data, id);
            await StopChildren(data, id);
            await End(id, stoppingToken.IsCancellationRequested ? "interrupted" : run.State == "cancelling" ? "cancelled" : "failed",
                stoppingToken.IsCancellationRequested ? "Canopy stopped during the run. The worktree and evidence are retained." : run.State == "cancelling" ? null : FactoryExecutionPolicyService.Failure(data, run) ?? "The configured active-step time limit was reached.");
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Factory run {RunId} failed", id);
            await StopChildren(await store.ReadAsync(CancellationToken.None), id);
            await End(id, "failed", error.Message);
        }
        finally { agents.Unregister(id); }
    }
    private static void ApplyDeadline(WorkspaceData data, FactoryRun run, CancellationTokenSource lifetime, DateTime activeDeadline)
    {
        var deadline = FactoryExecutionPolicyService.Budget(data, run).Deadline;
        var limit = deadline.HasValue && deadline.Value < activeDeadline ? deadline.Value : activeDeadline;
        lifetime.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(0, (limit - DateTime.UtcNow).TotalMilliseconds)));
    }
    private Task End(Guid id, string state, string? error) => store.WriteAsync(data => FactoryDispatchService.End(data, FactoryDispatchService.Find(data, id), state, error), CancellationToken.None);
    private async Task StopChildren(WorkspaceData data, Guid id)
    {
        var children = FactoryDispatchService.Children(data, id).Where(x => x.Active).Select(x => x.Id).ToHashSet();
        foreach (var child in children)
        {
            await store.WriteAsync(saved => AgentService.Find(saved, child).Cancel(), CancellationToken.None); agents.Cancel(child);
        }
        // The agent worker owns process shutdown and final diff capture. Keep the
        // parent stopping until those records settle, then preserve that evidence.
        while ((await store.ReadAsync(CancellationToken.None)).AgentRuns.Any(x => children.Contains(x.Id) && x.Active))
            await Task.Delay(options.PollIntervalMs, CancellationToken.None);
    }
}
