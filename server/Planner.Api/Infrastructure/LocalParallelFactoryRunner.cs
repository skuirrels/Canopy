using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalParallelFactoryRunner(IWorkspaceStore store, LocalAgentEnvironment agents, AgentOptions options,
    FactoryParallelOptions capacity, IFactoryParallelWorkspace workspace, LocalFactoryStepExecutor executor,
    LocalVerificationEnvironment verification, IRepairWorkspace repairs, ILogger<LocalParallelFactoryRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var data = await store.ReadAsync(stoppingToken);
            var next = data.FactoryRuns.FirstOrDefault(x => FactoryParallel.Enabled(x) && x.State is "queued" or "running" or "cancelling");
            if (next is null) await Task.Delay(options.PollIntervalMs, stoppingToken); else await Run(next.Id, stoppingToken);
        }
    }
    private async Task Run(Guid id, CancellationToken host)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(host); agents.Register(id, lifetime);
        var tasks = new Dictionary<Guid, Task>();
        try
        {
            var ct = lifetime.Token; var data = await store.ReadAsync(ct); var run = FactoryDispatchService.Find(data, id);
            FactoryExecutionPolicyService.Require(data, run); FactoryExecutionPolicyService.RequireLifetime(run.Snapshot.Execution, LocalProcessHost.Supported);
            FactoryExecutionPolicyService.RequireCapacity(run.Snapshot.Execution, capacity.MaximumConcurrentStages);
            if (run.State == "queued")
            {
                var root = Path.Combine(options.WorkspaceRoot, id.ToString());
                await store.WriteAsync(saved => { var current = FactoryDispatchService.Find(saved, id); FactoryExecutionPolicyService.Require(saved, current); current.Begin(root); saved.ParallelRuns.Add(FactoryParallelRun.Begin(id)); }, ct);
                run = FactoryDispatchService.Find(await store.ReadAsync(ct), id);
                Directory.CreateDirectory(options.WorkspaceRoot);
                await agents.Git(run.Snapshot.Environment.RepositoryPath, ["worktree", "add", "--detach", root, run.Snapshot.Environment.BaseCommit], ct);
                if (run.Snapshot.Repair is { } repair) await repairs.Fork(FactoryDispatchService.Find(await store.ReadAsync(ct), repair.ParentRunId), run, repair.ParentContext, ct);
                await workspace.Initialize(run, ct);
            }
            lifetime.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(0, (run.StartedAt!.Value.AddSeconds(run.Snapshot.Execution!.Policy.MaxElapsedSeconds) - DateTime.UtcNow).TotalMilliseconds)));
            while (true)
            {
                ct.ThrowIfCancellationRequested(); data = await store.ReadAsync(ct); run = FactoryDispatchService.Find(data, id);
                if (!run.Active) { lifetime.Cancel(); await Settle(tasks, id); return; }
                if (run.State == "cancelling") throw new OperationCanceledException(ct);
                FactoryExecutionPolicyService.Require(data, run);
                var failure = data.FactoryStepRuns.FirstOrDefault(x => x.RunId == id && x.State is "failed" or "cancelled" or "interrupted");
                if (failure is not null) throw new RuleException(failure.Error ?? "A parallel step did not succeed.");
                foreach (var finished in tasks.Where(x => x.Value.IsCompleted).ToList()) { await finished.Value; tasks.Remove(finished.Key); }
                data = await store.ReadAsync(ct); run = FactoryDispatchService.Find(data, id);
                var stages = data.FactoryStageRuns.Where(x => x.RunId == id).ToList();
                if (stages.All(x => x.State == "succeeded")) { await workspace.Integrate(run, ct); return; }
                var order = FactoryParallel.Ordered(run.Snapshot.Environment.Definition);
                foreach (var definition in order)
                {
                    if (tasks.Count >= run.Snapshot.Execution!.Policy.MaxConcurrentStages!.Value) break;
                    var stage = stages.Single(x => x.StageId == definition.Id);
                    if (tasks.ContainsKey(stage.Id) || stage.State is not ("pending" or "running") || definition.DependsOn.Any(parent => stages.Single(x => x.StageId == parent).State != "succeeded")) continue;
                    tasks.Add(stage.Id, Stage(run, stage.Id, ct));
                }
                if (tasks.Count == 0)
                {
                    if (!stages.Any(x => x.State == "waiting")) throw new RuleException("No parallel stage is ready; inspect retained dependency state.");
                    var paused = await store.WriteAsync(saved =>
                    {
                        var current = FactoryDispatchService.Find(saved, id);
                        var states = saved.FactoryStageRuns.Where(x => x.RunId == id).ToList();
                        var ready = run.Snapshot.Environment.Definition.Stages.Any(definition => states.Single(x => x.StageId == definition.Id).State is "pending" or "running" && definition.DependsOn.All(parent => states.Single(x => x.StageId == parent).State == "succeeded"));
                        if (!ready && current.State == "running") current.Wait();
                    }, ct);
                    if (FactoryDispatchService.Find(paused, id).State == "waiting") return;
                    continue;
                }
                await Task.WhenAny(tasks.Values.Append(Task.Delay(options.PollIntervalMs, ct)));
            }
        }
        catch (Exception error)
        {
            lifetime.Cancel(); await Settle(tasks, id);
            var data = await store.ReadAsync(CancellationToken.None); var run = FactoryDispatchService.Find(data, id);
            var failed = data.FactoryStepRuns.FirstOrDefault(x => x.RunId == id && x.State == "failed");
            var state = host.IsCancellationRequested ? "interrupted" : run.State == "cancelling" ? "cancelled" : "failed";
            var reason = state == "cancelled" ? null : host.IsCancellationRequested ? "Canopy stopped. Parallel work and integration intents are retained without replay." : failed?.Error ?? FactoryExecutionPolicyService.Failure(data, run) ?? (error is OperationCanceledException ? "Parallel execution was interrupted before completion." : error.Message);
            logger.LogWarning(error, "Parallel factory run {RunId} stopped", id);
            await store.WriteAsync(saved => FactoryDispatchService.End(saved, FactoryDispatchService.Find(saved, id), state, reason), CancellationToken.None);
        }
        finally { agents.Unregister(id); }
    }
    private async Task Stage(FactoryRun run, Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var stage = data.FactoryStageRuns.Single(x => x.Id == id); var record = data.ParallelStages.SingleOrDefault(x => x.Id == id);
        if (record is null)
        {
            await store.WriteAsync(saved => { var current = FactoryDispatchService.Find(saved, run.Id); FactoryExecutionPolicyService.Require(saved, current); saved.FactoryStageRuns.Single(x => x.Id == id).Begin(); saved.ParallelStages.Add(FactoryParallelStage.Begin(run.Id, id, workspace.StagePath(run, id))); current.Touch(); }, ct);
            await workspace.Materialize(run, stage, ct);
        }
        else await workspace.CheckStage(run, stage, ct);
        var definition = run.Snapshot.Environment.Definition.Stages.Single(x => x.Id == stage.StageId);
        var view = run.InspectStage(id, workspace.StagePath(run, id));
        foreach (var step in definition.Steps)
        {
            ct.ThrowIfCancellationRequested(); data = await store.ReadAsync(ct); var attempt = data.FactoryStepRuns.Single(x => x.StageRunId == id && x.StepId == step.Id);
            if (attempt.State == "succeeded") continue;
            if (attempt.State != "pending") throw new RuleException("A retained parallel step did not complete. No step will be replayed.");
            await workspace.Normalize(run, stage, ct);
            if (step.Kind == "manual")
            {
                executor.CheckInputs(data, run.Id, step);
                var context = await verification.Observe(view, ct);
                await store.WriteAsync(saved => { var current = FactoryDispatchService.Find(saved, run.Id); FactoryExecutionPolicyService.Require(saved, current); var gate = saved.FactoryStepRuns.Single(x => x.Id == attempt.Id); gate.Begin(); gate.Wait(); saved.FactoryStageRuns.Single(x => x.Id == id).Wait(); saved.ParallelStages.Single(x => x.Id == id).Wait(context); current.Touch(); }, ct);
                return;
            }
            using var stepDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct); stepDeadline.CancelAfter(TimeSpan.FromMinutes(options.MaxRunMinutes));
            await executor.Execute(view, new(definition, step, stage, attempt), stepDeadline.Token);
            var completed = (await store.ReadAsync(ct)).FactoryStepRuns.Single(x => x.Id == attempt.Id);
            await workspace.CaptureStep(run, stage, attempt.Id, ct);
            if (completed.State != "succeeded") throw new RuleException(completed.Error ?? "Parallel step did not succeed.");
        }
        await workspace.CompleteStage(run, stage, ct);
    }
    private async Task Settle(Dictionary<Guid, Task> tasks, Guid id)
    {
        var data = await store.ReadAsync(CancellationToken.None); var children = FactoryDispatchService.Children(data, id).Where(x => x.Active).Select(x => x.Id).ToHashSet();
        foreach (var child in children) { await store.WriteAsync(saved => AgentService.Find(saved, child).Cancel(), CancellationToken.None); agents.Cancel(child); }
        foreach (var task in tasks.Values) try { await task; } catch (Exception) { /* Preserve the owning failure after every stage settles. */ }
        while ((await store.ReadAsync(CancellationToken.None)).AgentRuns.Any(x => children.Contains(x.Id) && x.Active)) await Task.Delay(options.PollIntervalMs, CancellationToken.None);
    }
}
