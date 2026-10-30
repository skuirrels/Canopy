using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RepairPolicy(bool Enabled, Guid? ImplementationPlanVersionId, Guid? VerificationPlanVersionId,
    int MaxAttempts, int MaxElapsedMinutes, int MaxNoProgressRounds, int MaxAgentInvocations, bool AutoContinue,
    bool CommandsSafeToRepeat, bool AllowUnmeteredCost, decimal? MaxCost, string? Currency)
{
    public void Validate()
    {
        if (MaxAttempts < 1 || MaxElapsedMinutes < 1 || MaxNoProgressRounds < 1 || MaxAgentInvocations < 0)
            throw new RuleException("Set positive attempt, elapsed-time and no-progress limits, and a nonnegative agent-invocation limit.");
        if (Enabled && (!CommandsSafeToRepeat || (ImplementationPlanVersionId is null && VerificationPlanVersionId is null)))
            throw new RuleException("Choose a published repair plan and explicitly confirm its commands are safe to repeat.");
        if (ImplementationPlanVersionId == Guid.Empty || VerificationPlanVersionId == Guid.Empty) throw new RuleException("Choose valid published repair versions.");
        if (Currency is not null) Rules.Name(Currency, "Currency");
        if (MaxCost.HasValue && (MaxCost < 0 || string.IsNullOrWhiteSpace(Currency))) throw new RuleException("A monetary limit requires a nonnegative amount and an explicit currency.");
    }
}
public sealed class ProjectRepairPolicy
{
    private ProjectRepairPolicy() { }
    public Guid Id { get; private set; }
    [JsonIgnore] public string PolicyJson { get; private set; } = "";
    public RepairPolicy Policy => JsonSerializer.Deserialize<RepairPolicy>(PolicyJson, FactoryRules.Json)!;
    public static ProjectRepairPolicy Adopt(Guid id, RepairPolicy policy) { policy.Validate(); return new() { Id = id, PolicyJson = JsonSerializer.Serialize(policy, FactoryRules.Json) }; }
    public void Replace(RepairPolicy policy) { policy.Validate(); PolicyJson = JsonSerializer.Serialize(policy, FactoryRules.Json); }
}
public sealed record RepairIssue(string Kind, Guid Id, string Title, string Owner, string Reason, Guid EvidenceId);
public sealed record RepairPlan(string Owner, Guid PipelineVersionId, int VersionNumber, string Name, string Source, FactoryEnvironmentSnapshot Environment,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FactoryExecutionSnapshot? Execution = null);
public sealed record RepairScope(string Owner, IReadOnlyList<RepairIssue> Issues, string SourceHash, Guid SourcePipelineVersionId, FactoryOverrides SourceOverrides,
    IReadOnlyList<RepairPlan> Plans);
public sealed record RepairOperation(Guid RequestId, string Kind, string Hash);
public sealed record RepairFeedback(string Kind, string Reason, IReadOnlyList<RepairIssue> Remaining, Guid? ReviewId, string CodeHash);
public sealed record FactoryRepairLineage(Guid LoopId, Guid AttemptId, Guid ParentRunId, Guid RootRunId, int AttemptNumber,
    Guid SourcePipelineVersionId, FactoryOverrides SourceOverrides, VerificationContext ParentContext, RepairFeedback Feedback);

public sealed class FactoryRepairLoop
{
    private FactoryRepairLoop() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid RootRunId { get; private set; }
    public long Revision { get; private set; } = 1;
    public string State { get; private set; } = "prepared";
    public string Reason { get; private set; } = "Prepared only; execution requires an explicit start.";
    public int ReservedAgentInvocations { get; private set; }
    public int NoProgressRounds { get; private set; }
    public int RemainingCount { get; private set; }
    public bool Open => State is not ("completed" or "stopped");
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    [JsonIgnore] public string PolicyJson { get; private set; } = "";
    [JsonIgnore] public string ScopeJson { get; private set; } = "";
    [JsonIgnore] public string ContextJson { get; private set; } = "";
    [JsonIgnore] public string OperationsJson { get; private set; } = "[]";
    public RepairPolicy Policy => JsonSerializer.Deserialize<RepairPolicy>(PolicyJson, FactoryRules.Json)!;
    public RepairScope Scope => JsonSerializer.Deserialize<RepairScope>(ScopeJson, FactoryRules.Json)!;
    public VerificationContext Context => JsonSerializer.Deserialize<VerificationContext>(ContextJson, FactoryRules.Json)!;
    [JsonIgnore] public IReadOnlyList<RepairOperation> Operations => JsonSerializer.Deserialize<RepairOperation[]>(OperationsJson, FactoryRules.Json)!;
    public static FactoryRepairLoop Prepare(Guid requestId, FactoryRun source, RepairPolicy policy, RepairScope scope, VerificationContext context, string hash)
    {
        policy.Validate(); if (!policy.Enabled) throw new RuleException("Enable the project's repair policy first.");
        if (scope.Owner is not ("implementation" or "verification") || scope.Issues.Count == 0 || scope.Issues.Any(x => x.Owner != scope.Owner))
            throw new RuleException("Select actionable issues for one responsible repair role.");
        var loop = new FactoryRepairLoop { Id = Guid.NewGuid(), RequestId = requestId, ProjectId = source.ProjectId, ItemId = source.ItemId, RootRunId = source.Id,
            PolicyJson = JsonSerializer.Serialize(policy, FactoryRules.Json), ScopeJson = JsonSerializer.Serialize(scope, FactoryRules.Json),
            ContextJson = JsonSerializer.Serialize(context, FactoryRules.Json), RemainingCount = scope.Issues.Count, CreatedAt = DateTime.UtcNow };
        loop.Remember(requestId, "prepare", hash); return loop;
    }
    public string? Expired(DateTime now) => StartedAt.HasValue && now >= StartedAt.Value.AddMinutes(Policy.MaxElapsedMinutes) ? "The captured elapsed-time budget was exhausted." : null;
    public string? Budget(int attempts, int agentInvocations, DateTime now)
    {
        var policy = Policy;
        if (policy.MaxCost.HasValue) return "Monetary cost is unavailable: these CLI runners cannot enforce the configured currency cap. No execution was authorized against an assumed zero cost.";
        if (!policy.AllowUnmeteredCost) return "Explicitly permit unmetered cost or configure an enforceable cost provider before execution.";
        if (attempts >= policy.MaxAttempts) return "The captured maximum repair-attempt budget was exhausted.";
        if (NoProgressRounds >= policy.MaxNoProgressRounds) return "Repeated repair rounds made no progress on the selected issues.";
        if (Expired(now) is { } expired) return expired;
        if ((long)ReservedAgentInvocations + agentInvocations > policy.MaxAgentInvocations) return "The captured agent-invocation budget cannot reserve this attempt.";
        return null;
    }
    public void Queue(int calls)
    {
        if (State is not ("prepared" or "needsDecision")) throw new RuleException("This loop is not ready for another attempt.", 409);
        StartedAt ??= DateTime.UtcNow; ReservedAgentInvocations += calls; Set("running", "Repair and verification are executing in a separate retained worktree.");
    }
    public void Progress(string state, string reason)
    {
        if (!Open || State == "stopping") return;
        if (state is not ("awaitingQa" or "awaitingReview" or "needsDecision")) throw new RuleException("Invalid repair-loop transition.");
        if (State != state || Reason != reason) Set(state, reason);
    }
    public void Feedback(int remaining, bool first)
    {
        NoProgressRounds = remaining < RemainingCount ? 0 : NoProgressRounds + (first ? 1 : 0); RemainingCount = remaining; Revision++;
    }
    public void Stopping(string reason) { if (Open) Set("stopping", FactoryReviewRound.Required(reason, "Stop reason")); }
    public void End(bool completed, string reason)
    {
        if (!Open) return;
        Set(completed ? "completed" : "stopped", reason); FinishedAt = DateTime.UtcNow;
    }
    public void Remember(Guid requestId, string kind, string hash)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        OperationsJson = JsonSerializer.Serialize(Operations.Append(new RepairOperation(requestId, kind, hash)), FactoryRules.Json);
    }
    private void Set(string state, string reason) { State = state; Reason = reason; Revision++; }
}
public sealed class FactoryRepairAttempt
{
    private FactoryRepairAttempt() { }
    public Guid Id { get; private set; }
    public Guid LoopId { get; private set; }
    public Guid RunId { get; private set; }
    public Guid ParentRunId { get; private set; }
    public int Number { get; private set; }
    public string? FeedbackHash { get; private set; }
    [JsonIgnore] public string FeedbackJson { get; private set; } = "";
    public RepairFeedback? Feedback => FeedbackJson.Length == 0 ? null : JsonSerializer.Deserialize<RepairFeedback>(FeedbackJson, FactoryRules.Json);
    public static FactoryRepairAttempt Create(Guid id, FactoryRepairLoop loop, FactoryRun run, Guid parentId, int number) => new()
        { Id = id, LoopId = loop.Id, RunId = run.Id, ParentRunId = parentId, Number = number };
    public bool Record(RepairFeedback feedback)
    {
        var json = JsonSerializer.Serialize(feedback, FactoryRules.Json); var hash = FactoryArtifact.Hash(json);
        if (FeedbackHash == hash) return false;
        FeedbackHash = hash; FeedbackJson = json; return true;
    }
}

public static class FactoryRepairRules
{
    public static int AgentCalls(FactoryDefinition definition) => definition.Stages.Sum(s => s.Steps.Count(x => x.Kind == "agent"));
    public static void Plan(FactoryDefinition definition, string owner)
    {
        FactoryRules.RequirePublishable(definition);
        var writers = definition.Stages.Where(s => s.Steps.Any(x => x.Kind == "agent" && x.Permission == "workspace-write")).ToList();
        if (writers.Count == 0 || writers.Any(s => s.Steps.Any(x => x.Permission == "workspace-write" && x.Role != owner)))
            throw new RuleException($"The {owner} repair plan requires write-capable {owner} agents and no other write roles.");
        var commands = definition.Stages.Where(s => s.Steps.Any(x => x.Kind == "command")).ToList();
        var reviewers = definition.Stages.Where(s => s.Steps.Any(x => x.Role == "review" && x.Permission == "read-only")).ToList();
        var byId = definition.Stages.ToDictionary(x => x.Id);
        var ancestry = new Dictionary<(string, string), bool>();
        bool After(FactoryStage stage, string ancestor)
        {
            if (ancestry.TryGetValue((stage.Id, ancestor), out var result)) return result;
            return ancestry[(stage.Id, ancestor)] = stage.DependsOn.Contains(ancestor) || stage.DependsOn.Any(id => After(byId[id], ancestor));
        }
        if (commands.Count == 0 || reviewers.Count == 0 || commands.Any(s => writers.Any(w => !After(s, w.Id))) || reviewers.Any(s => commands.Any(c => !After(s, c.Id))))
            throw new RuleException("Repair plans require separate write stages, verification command stages after every writer, and independent review stages after every verification command.");
    }
}
