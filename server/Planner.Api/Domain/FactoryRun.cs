using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

public sealed record FactoryReadinessCheck(string Code, string Severity, string Message, string? StageId = null, string? StepId = null);
public sealed record FactorySkillSnapshot(string Id, string Source, string Path, string Hash);
public sealed record FactoryEnvironmentSnapshot(string RepositoryPath, string BaseRef, string BaseCommit,
    string ProjectInstructions, string DefaultProvider, FactoryDefinition Definition, IReadOnlyList<FactorySkillSnapshot> Skills);
public sealed record FactoryRunSnapshot(Guid PipelineVersionId, int VersionNumber, string PipelineName, string Source,
    string Title, string SpecJson, long ProjectRevision, FactoryOverrides Overrides,
    FactoryEnvironmentSnapshot Environment, IReadOnlyList<FactoryReadinessCheck> Checks, DateTime CheckedAt);

public sealed class FactoryRun
{
    private FactoryRun() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public string State { get; private set; } = "prepared";
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
    public static FactoryStepRun Prepare(Guid runId, Guid stageRunId, FactoryStep step) => new()
        { Id = Guid.NewGuid(), RunId = runId, StageRunId = stageRunId, StepId = step.Id };
}
