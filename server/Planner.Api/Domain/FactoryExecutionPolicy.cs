using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactoryModelRoute(string Role, string Provider, string Model, string? Effort = null)
{
    public FactoryModelRoute Normalize()
    {
        if (!FactoryRules.AgentRoles.Contains(Role)) throw new RuleException("Choose a configured factory agent role.");
        AgentRun.RequireProvider(Provider);
        return this with { Model = Rules.Name(Model, "Route model ID"), Effort = AgentExecutionSettings.Normalize(Effort, "Route effort") };
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactoryExecutionPolicy(bool Enabled, IReadOnlyList<FactoryModelRoute> Routes,
    int MaxAgentInvocations, int MaxCommandInvocations, int MaxElapsedSeconds, bool AllowUnmeteredCost,
    long? MaxTokens = null, decimal? MaxCost = null, string? Currency = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxConcurrentStages = null)
{
    public FactoryExecutionPolicy Normalize()
    {
        if (MaxConcurrentStages is < 1) throw new RuleException("Concurrent stages must be positive, or null for serial execution.");
        if (Routes is null || Routes.Count > FactoryRules.AgentRoles.Count || Routes.Any(x => x is null) || Routes.Select(x => x.Role).Distinct().Count() != Routes.Count) throw new RuleException("Supply distinct role routes, using [] for none.");
        if (MaxAgentInvocations < 0 || MaxCommandInvocations < 0 || MaxElapsedSeconds < 1) throw new RuleException("Set nonnegative invocation limits and a positive elapsed-time limit.");
        if (MaxTokens < 0 || MaxCost < 0 || MaxCost.HasValue && string.IsNullOrWhiteSpace(Currency)) throw new RuleException("Token/currency caps require nonnegative values and an explicit currency for a cost cap.");
        return this with { Routes = Routes.Select(x => x.Normalize()).OrderBy(x => x.Role, StringComparer.Ordinal).ToArray(), Currency = Currency is null ? null : Rules.Name(Currency, "Currency") };
    }
    public string? Unsupported(bool hasAgents) => MaxTokens.HasValue ? "These CLI runners cannot enforce a hard token cap. No execution is authorized against assumed zero usage." :
        MaxCost.HasValue ? "These CLI runners cannot enforce the requested currency cap. Reported token counts or estimated cost are not a billing guarantee." :
        hasAgents && !AllowUnmeteredCost ? "Explicitly permit unmetered model cost before executing agent steps." : null;
    public string? Exceeded(DateTime? startedAt, DateTime now, int agents, int commands, string? nextKind = null)
    {
        if (startedAt.HasValue && now >= startedAt.Value.AddSeconds(MaxElapsedSeconds)) return "The captured work-order elapsed-time budget was exhausted.";
        if ((long)agents + (nextKind == "agent" ? 1 : 0) > MaxAgentInvocations) return "The captured agent-invocation budget cannot reserve this step.";
        if ((long)commands + (nextKind == "command" ? 1 : 0) > MaxCommandInvocations) return "The captured command-invocation budget cannot reserve this step.";
        return null;
    }
}
public sealed class ProjectFactoryExecutionPolicy
{
    private ProjectFactoryExecutionPolicy() { }
    public Guid Id { get; private set; }
    [JsonIgnore] public string PolicyJson { get; private set; } = "";
    public FactoryExecutionPolicy Policy => JsonSerializer.Deserialize<FactoryExecutionPolicy>(PolicyJson, FactoryRules.Json)!;
    public static ProjectFactoryExecutionPolicy Adopt(Guid id, FactoryExecutionPolicy policy) => new() { Id = id, PolicyJson = FactoryRules.Serialize(policy.Normalize()) };
    public void Replace(FactoryExecutionPolicy policy) => PolicyJson = FactoryRules.Serialize(policy.Normalize());
}
public sealed record FactoryResolvedRoute(string StageId, string StepId, string Role, string Provider, string? Model, string? Effort, string SelectionSource, string EffortSource);
public sealed record FactoryExecutionSnapshot(FactoryExecutionPolicy Policy, string PolicyHash, IReadOnlyList<FactoryResolvedRoute> Routes);
