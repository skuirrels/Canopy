using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

public sealed record FactoryReadinessCheck(string Code, string Severity, string Message, string? StageId = null, string? StepId = null);
public sealed record FactorySkillSnapshot(string Id, string Source, string Path, string Hash);
public sealed record FactoryEnvironmentSnapshot(string RepositoryPath, string BaseRef, string BaseCommit,
    string ProjectInstructions, string DefaultProvider, FactoryDefinition Definition, IReadOnlyList<FactorySkillSnapshot> Skills);
public sealed record FactoryRunSnapshot(Guid PipelineVersionId, int VersionNumber, string PipelineName, string Source,
    string Title, string SpecJson, long ProjectRevision, FactoryOverrides Overrides,
    FactoryEnvironmentSnapshot Environment, IReadOnlyList<FactoryReadinessCheck> Checks, DateTime CheckedAt, IReadOnlyList<AcceptanceCriterion>? Criteria = null, FactoryRepairLineage? Repair = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FactoryExecutionSnapshot? Execution = null);

public sealed class FactoryRun
{
    private FactoryRun() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public string State { get; private set; } = "prepared";
    public long Revision { get; private set; } = 1;
    public Guid? StartRequestId { get; private set; }
    public string? WorkspacePath { get; private set; }
    public string? Error { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public bool Active => State is "queued" or "running" or "waiting" or "cancelling";
    public void Touch() => Revision++;
    public void Queue(long revision, Guid requestId)
    {
        Rules.Revision(revision, Revision);
        if (State != "prepared" || requestId == Guid.Empty) throw new RuleException("Start a prepared run with a unique request ID.", 409);
        StartRequestId = requestId; State = "queued"; Touch();
    }
    public void Begin(string workspace)
    {
        if (State != "queued") throw new RuleException("This factory run is no longer queued.", 409);
        WorkspacePath = workspace; State = "running"; StartedAt = DateTime.UtcNow; Touch();
    }
    public void Wait() { if (State == "running") { State = "waiting"; Touch(); } }
    public void Continue() { if (State != "waiting") throw new RuleException("This run is not waiting for a decision.", 409); State = "running"; Touch(); }
    public void Cancel(long revision)
    {
        Rules.Revision(revision, Revision);
        if (!Active && State != "prepared") return;
        if (State is "prepared" or "queued" or "waiting") End("cancelled", null);
        else { State = "cancelling"; Touch(); }
    }
    public void End(string state, string? error)
    {
        if (!Active && State != "prepared") return;
        if (state is not ("succeeded" or "failed" or "cancelled" or "interrupted")) throw new RuleException("Invalid factory run outcome.");
        State = State == "cancelling" && state != "interrupted" ? "cancelled" : state; Error = error; FinishedAt = DateTime.UtcNow; Touch();
    }
    [JsonIgnore] public string SnapshotJson { get; private set; } = "";
    public FactoryRunSnapshot Snapshot => JsonSerializer.Deserialize<FactoryRunSnapshot>(SnapshotJson, FactoryRules.Json)!;
    public DateTime CreatedAt { get; private set; }
    public static FactoryRun Prepare(Guid requestId, WorkItem item, FactoryRunSnapshot snapshot)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        if (snapshot.Checks.Any(check => check.Severity == "error")) throw new RuleException("Resolve readiness errors before preparing a run.");
        FactoryRules.RequirePublishable(snapshot.Environment.Definition);
        return new() { Id = Guid.NewGuid(), RequestId = requestId, ProjectId = item.ProjectId, ItemId = item.Id,
            SnapshotJson = JsonSerializer.Serialize(snapshot, FactoryRules.Json), CreatedAt = DateTime.UtcNow };
    }
}

public sealed class FactoryStageRun
{
    private FactoryStageRun() { }
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public string StageId { get; private set; } = "";
    public string State { get; private set; } = "pending";
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public void Begin() { if (State == "pending") { State = "running"; StartedAt = DateTime.UtcNow; } }
    public void Wait() { if (State == "running") State = "waiting"; }
    public void Continue() { if (State == "waiting") State = "running"; }
    public void End(string state) { if (state is not ("succeeded" or "failed" or "cancelled" or "interrupted" or "blocked")) throw new RuleException("Invalid stage outcome."); if (State is "pending" or "running" or "waiting") { State = state; FinishedAt = DateTime.UtcNow; } }
    public static FactoryStageRun Prepare(Guid runId, FactoryStage stage) => new() { Id = Guid.NewGuid(), RunId = runId, StageId = stage.Id };
}

public sealed class FactoryStepRun
{
    private FactoryStepRun() { }
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid StageRunId { get; private set; }
    public string StepId { get; private set; } = "";
    public string State { get; private set; } = "pending";
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public Guid? AgentRunId { get; private set; }
    public Guid? DecisionRequestId { get; private set; }
    public bool? Decision { get; private set; }
    public string? DecisionNote { get; private set; }
    public string Output { get; private set; } = "";
    public bool OutputLimitReached { get; private set; }
    public string Evidence { get; private set; } = "";
    public string? Error { get; private set; }
    public int? ExitCode { get; private set; }
    public void Begin() { if (State != "pending") throw new RuleException("This step is not pending.", 409); State = "running"; StartedAt = DateTime.UtcNow; }
    public void QueueAgent(Guid id) { if (State != "pending") throw new RuleException("This step is not pending.", 409); AgentRunId = id; State = "queued"; }
    public void AgentProgress(AgentRun agent, int limit)
    {
        if (State is not ("queued" or "running")) return;
        if (agent.StartedAt is not null) { State = "running"; StartedAt = agent.StartedAt; }
        OutputLimitReached |= agent.Output.Length >= limit; Output = AgentRun.Tail(agent.Output, limit);
    }
    public void RecoverAgent(AgentRun agent)
    {
        if (State is not ("queued" or "running")) return;
        Output = agent.Output; StartedAt ??= agent.StartedAt;
        if (Evidence.Length == 0) Evidence = JsonSerializer.Serialize(new { agentRunId = agent.Id, agent.State, agent.Result, agent.Diff, agent.Error, agent.Usage }, FactoryRules.Json);
    }
    public void Wait() { if (State != "running") throw new RuleException("This step cannot wait.", 409); State = "waiting"; }
    public void Progress(string text, int limit) { OutputLimitReached |= Output.Length + text.Length >= limit; Output = AgentRun.Tail(Output + text, limit); }
    public void Complete(string state, string evidence, string? error = null, int? exitCode = null)
    {
        if (State is not ("running" or "queued" or "waiting")) throw new RuleException("This step is no longer active.", 409);
        if (state is not ("succeeded" or "failed" or "cancelled" or "interrupted")) throw new RuleException("Invalid step outcome.");
        State = state; Evidence = evidence; Error = error; ExitCode = exitCode; FinishedAt = DateTime.UtcNow;
    }
    public void NotRun(string state) { if (state is not ("blocked" or "cancelled" or "interrupted")) throw new RuleException("Invalid unexecuted step outcome."); if (State == "pending") { State = state; FinishedAt = DateTime.UtcNow; } }
    public void Decide(Guid requestId, bool approved, string? note)
    {
        if (State != "waiting" || requestId == Guid.Empty) throw new RuleException("This step is not waiting for a decision.", 409);
        DecisionRequestId = requestId; Decision = approved; DecisionNote = AgentRun.Text(note, "Decision note");
        Complete(approved ? "succeeded" : "failed", JsonSerializer.Serialize(new { approved, note = DecisionNote, decidedAt = DateTime.UtcNow }, FactoryRules.Json), approved ? null : "Human approval was declined.");
    }
    public static FactoryStepRun Prepare(Guid runId, Guid stageRunId, FactoryStep step) => new()
        { Id = Guid.NewGuid(), RunId = runId, StageRunId = stageRunId, StepId = step.Id };
}


public sealed class FactoryArtifact
{
    private FactoryArtifact() { }
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid StepRunId { get; private set; }
    public string Name { get; private set; } = "";
    public string Kind { get; private set; } = "step-evidence";
    public string EvidenceHash { get; private set; } = "";
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static FactoryArtifact Capture(Guid runId, FactoryStepRun step, string name)
    {
        if (step.State != "succeeded" || string.IsNullOrEmpty(step.Evidence)) throw new RuleException("Artifacts require actual successful step evidence.");
        return new() { Id = Guid.NewGuid(), RunId = runId, StepRunId = step.Id, Name = name, EvidenceHash = Hash(step.Evidence) };
    }
}
