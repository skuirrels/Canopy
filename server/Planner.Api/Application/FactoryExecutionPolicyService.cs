using System.Text.Json.Serialization;
using Planner.Api.Domain;

namespace Planner.Api.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditFactoryExecutionPolicy(long ExpectedRevision, FactoryExecutionPolicy Policy);
public sealed record FactoryExecutionCapabilities(bool InvocationLimits, bool ElapsedTimeLimit, bool HardTokenCap, bool HardCurrencyCap, string UsageSemantics, int MaximumConcurrentStages);
public sealed record FactoryExecutionPolicyDetail(long ProjectRevision, FactoryExecutionPolicy? Policy, IReadOnlyList<string> Roles, IReadOnlyList<AgentProvider> Providers, FactoryExecutionCapabilities Capabilities);
public sealed record FactoryPolicyInspection(FactoryInspection Inspection, FactoryExecutionSnapshot? Execution);
public sealed record FactoryBudgetView(int ReservedAgentInvocations, int ReservedCommandInvocations, DateTime? Deadline, string? BlockedReason);

public sealed class FactoryExecutionPolicyService(IWorkspaceStore store, IAgentEnvironment agents, IFactoryEnvironment factory)
{
    public async Task<FactoryExecutionPolicyDetail> Read(Guid projectId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct);
        return new(data.Graph(projectId).Project.Revision, data.ExecutionPolicies.SingleOrDefault(x => x.Id == projectId)?.Policy,
            FactoryRules.AgentRoles, agents.Providers(), new(true, factory.CanBoundProcessLifetime, false, false,
                "Provider completion counters are observations; missing usage is unknown and counters do not establish account billing.", factory.MaximumConcurrentStages));
    }
    public async Task<FactoryExecutionPolicyDetail> Edit(Guid projectId, EditFactoryExecutionPolicy input, CancellationToken ct)
    {
        if (input.Policy is null) throw new RuleException("Supply an explicit execution policy.");
        var policy = input.Policy.Normalize();
        foreach (var route in policy.Routes)
            if (route.Effort is not null && !agents.Providers().Single(x => x.Id == route.Provider).EffortLevels.Contains(route.Effort)) throw new RuleException("The route effort is not configured for its provider.");
        await store.WriteAsync(data =>
        {
            var graph = data.Graph(projectId, input.ExpectedRevision); var saved = data.ExecutionPolicies.SingleOrDefault(x => x.Id == projectId);
            if (saved is null) data.ExecutionPolicies.Add(ProjectFactoryExecutionPolicy.Adopt(projectId, policy)); else saved.Replace(policy);
            graph.Project.Touch();
        }, ct);
        await Enforce(ct);
        return await Read(projectId, ct);
    }
    public async Task Enforce(CancellationToken ct)
    {
        var before = await store.ReadAsync(ct);
        if (!before.FactoryRuns.Any(run => (run.Active || run.State == "prepared") && Failure(before, run) is not null)) return;
        var cancel = new HashSet<Guid>();
        await store.WriteAsync(data =>
        {
            foreach (var run in data.FactoryRuns.Where(x => x.Active || x.State == "prepared"))
            {
                if (Failure(data, run) is not { } reason) continue;
                var children = FactoryDispatchService.Children(data, run.Id).Where(x => x.Active).ToList();
                if ((run.State is "prepared" or "queued" or "waiting") && children.Count == 0) FactoryDispatchService.End(data, run, "failed", reason);
                else { cancel.Add(run.Id); foreach (var child in children) { child.Cancel(); cancel.Add(child.Id); } }
            }
        }, ct);
        foreach (var id in cancel) agents.Cancel(id);
    }
    public static FactoryExecutionPolicy? Current(WorkspaceData data, Guid projectId) => data.ExecutionPolicies.SingleOrDefault(x => x.Id == projectId)?.Policy is { Enabled: true } policy ? policy : null;
    public static string? CurrentHash(WorkspaceData data, Guid projectId) => Current(data, projectId) is { } policy ? Hash(policy) : null;
    public static bool Matches(WorkspaceData data, Guid projectId, FactoryExecutionSnapshot? snapshot) => CurrentHash(data, projectId) == snapshot?.PolicyHash;
    public static void RequireCurrent(WorkspaceData data, Guid projectId, FactoryExecutionSnapshot? snapshot)
    { if (!Matches(data, projectId, snapshot)) throw new RuleException("Execution policy changed. Prepare new work with the current model routes and limits.", 409); }
    public static FactoryBudgetView Budget(WorkspaceData data, FactoryRun run, string? nextKind = null)
    {
        var steps = data.FactoryStepRuns.Where(x => x.RunId == run.Id).ToList();
        var stages = data.FactoryStageRuns.Where(x => x.RunId == run.Id).ToDictionary(x => x.Id, x => x.StageId);
        var commands = run.Snapshot.Environment.Definition.Stages.SelectMany(stage => stage.Steps.Where(x => x.Kind == "command").Select(step => (stage.Id, step.Id))).ToHashSet();
        var agents = steps.Count(x => x.AgentRunId.HasValue); var calls = steps.Count(x => x.StartedAt.HasValue && commands.Contains((stages[x.StageRunId], x.StepId)));
        var snapshot = run.Snapshot.Execution;
        return new(agents, calls, snapshot is not null && run.StartedAt.HasValue ? run.StartedAt.Value.AddSeconds(snapshot.Policy.MaxElapsedSeconds) : null,
            !Matches(data, run.ProjectId, snapshot) ? "Execution policy changed. Prepare current work before further dispatch." : snapshot?.Policy.Exceeded(run.StartedAt, run.FinishedAt ?? DateTime.UtcNow, agents, calls, nextKind));
    }
    public static void RequireLifetime(FactoryExecutionSnapshot? execution, bool canBound)
    { if (execution is not null && !canBound) throw new RuleException("Enabled elapsed budgets require an owned-process lifetime adapter for this operating system.", 503); }
    public static void RequireCapacity(FactoryExecutionSnapshot? execution, int capacity)
    { if (execution?.Policy.MaxConcurrentStages is { } requested && requested > capacity) throw new RuleException("Captured parallelism exceeds this backend's configured stage capacity.", 503); }
    public static string? Failure(WorkspaceData data, FactoryRun run) => Budget(data, run).BlockedReason;
    public static void Require(WorkspaceData data, FactoryRun run, string? nextKind = null)
    { if (Budget(data, run, nextKind).BlockedReason is { } error) throw new RuleException(error, 409); }

    public static async Task<FactoryPolicyInspection> Inspect(WorkspaceData data, Guid projectId, FactoryDefinition definition,
        ProjectAgentConnection? connection, IFactoryEnvironment environment, CancellationToken ct, FactoryOverrides? overrides = null)
    {
        var policy = Current(data, projectId);
        if (policy is null) return new(await environment.Inspect(definition, connection, ct), null);
        var routed = definition with { Stages = definition.Stages.Select(stage => stage with { Steps = stage.Steps.Select(step =>
        {
            var route = Route(policy, step);
            return route is null ? step : step with { Provider = route.Provider, Model = route.Model, Effort = step.Effort ?? route.Effort };
        }).ToArray() }).ToArray() };
        var inspection = await environment.Inspect(routed, connection, ct); var checks = inspection.Checks.ToList();
        var flat = routed.Stages.SelectMany(x => x.Steps).ToList();
        if (!environment.CanBoundProcessLifetime) checks.Add(new("execution.processLifetime", "error", "Enabled elapsed budgets require an owned-process lifetime adapter for this operating system. No execution is authorized against an unsupported guarantee."));
        if (policy.Unsupported(flat.Any(x => x.Kind == "agent")) is { } unsupported) checks.Add(new("execution.unsupported", "error", unsupported));
        if (flat.Count(x => x.Kind == "agent") > policy.MaxAgentInvocations) checks.Add(new("execution.agentBudget", "error", "The pipeline needs more agent invocations than the adopted work-order budget."));
        if (flat.Count(x => x.Kind == "command") > policy.MaxCommandInvocations) checks.Add(new("execution.commandBudget", "error", "The pipeline needs more command invocations than the adopted work-order budget."));
        if (policy.MaxConcurrentStages > environment.MaximumConcurrentStages) checks.Add(new("execution.parallelCapacity", "error", "Requested parallel stages exceed the backend capacity."));
        var routes = new List<FactoryResolvedRoute>();
        if (inspection.Environment is { } resolved)
            foreach (var stage in definition.Stages)
                foreach (var step in stage.Steps.Where(x => x.Kind == "agent"))
                {
                    var actual = resolved.Definition.Stages.Single(x => x.Id == stage.Id).Steps.Single(x => x.Id == step.Id); var selected = Route(policy, step);
                    if (actual.Model is null) checks.Add(new("execution.modelRequired", "error", "Enabled execution policy requires an explicit model ID; configure the role, step or provider settings.", stage.Id, step.Id));
                    var taskOverride = overrides?.Steps.SingleOrDefault(x => x.StageId == stage.Id && x.StepId == step.Id);
                    routes.Add(new(stage.Id, step.Id, step.Role!, actual.Provider!, actual.Model, actual.Effort,
                        selected is not null ? "role" : taskOverride?.Provider is not null || taskOverride?.Model is not null ? "task" : step.Provider is not null || step.Model is not null ? "step" : "project",
                        taskOverride?.Effort is not null ? "task" : step.Effort is not null ? "step" : selected?.Effort is not null ? "role" : actual.Effort is not null ? "project" : "providerDefault"));
                }
        return new(inspection with { Checks = checks }, new(policy, Hash(policy), routes));
    }
    private static FactoryModelRoute? Route(FactoryExecutionPolicy policy, FactoryStep step) => step.Kind == "agent" && step.Provider is null && step.Model is null ? policy.Routes.SingleOrDefault(x => x.Role == step.Role) : null;
    private static string Hash(FactoryExecutionPolicy policy) => FactoryArtifact.Hash(FactoryRules.Serialize(policy));
}
