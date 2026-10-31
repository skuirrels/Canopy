using Planner.Api.Domain;

namespace Planner.Api.Application;

public sealed class FactoryParallelOptions
{
    public int MaximumConcurrentStages { get; set; }
    public int MaximumIndexBytes { get; set; }
    public int MaximumPatchBytes { get; set; }
    public void Validate()
    { if (MaximumConcurrentStages < 1 || MaximumIndexBytes < 1 || MaximumPatchBytes < 1) throw new InvalidOperationException("Configure parallel stage capacity and index/patch byte bounds."); }
}
public interface IFactoryParallelWorkspace
{
    string StagePath(FactoryRun run, Guid stageId);
    Task Initialize(FactoryRun run, CancellationToken ct);
    Task Materialize(FactoryRun run, FactoryStageRun stage, CancellationToken ct);
    Task Normalize(FactoryRun run, FactoryStageRun stage, CancellationToken ct);
    Task CaptureStep(FactoryRun run, FactoryStageRun stage, Guid stepId, CancellationToken ct);
    Task CompleteStage(FactoryRun run, FactoryStageRun stage, CancellationToken ct);
    Task CheckStage(FactoryRun run, FactoryStageRun stage, CancellationToken ct);
    Task Integrate(FactoryRun run, CancellationToken ct);
}
