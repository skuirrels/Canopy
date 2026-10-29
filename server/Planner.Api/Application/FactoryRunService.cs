using Planner.Api.Domain;
using System.Text.Json.Serialization;

namespace Planner.Api.Application;

public sealed record FactoryInspection(FactoryEnvironmentSnapshot? Environment, IReadOnlyList<FactoryReadinessCheck> Checks);
public interface IFactoryEnvironment
{
    Task<FactoryInspection> InspectCaptured(FactoryEnvironmentSnapshot snapshot, CancellationToken ct);
    Task<FactoryInspection> Inspect(FactoryDefinition definition, ProjectAgentConnection? connection, CancellationToken ct);
}
public sealed record FactoryReadiness(long ProjectRevision, bool CanPrepare, string ExecutionReadiness,
    DateTime CheckedAt, IReadOnlyList<FactoryReadinessCheck> Checks, FactoryEnvironmentSnapshot? Environment);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareFactoryRun(Guid RequestId, long ExpectedRevision);
public sealed record FactoryRunDetail(FactoryRun Run, IReadOnlyList<FactoryStageRun> Stages, IReadOnlyList<FactoryStepRun> Steps, IReadOnlyList<FactoryArtifact> Artifacts);
public sealed record FactoryRunSummary(Guid Id, Guid ItemId, string State, DateTime CreatedAt, string PipelineName, int VersionNumber, string Title, long Revision);

public sealed class FactoryRunService(IWorkspaceStore store, IFactoryEnvironment environment)
{
    public async Task<FactoryReadiness> Readiness(Guid itemId, CancellationToken ct) => await Inspect(await store.ReadAsync(ct), itemId, ct);
    private async Task<FactoryReadiness> Inspect(WorkspaceData data, Guid itemId, CancellationToken ct)
    {
        var item = Item(data, itemId); var revision = data.Graph(item.ProjectId).Project.Revision;
        var checks = TaskAcceptanceCriteria.Check(data.AcceptanceCriteria.SingleOrDefault(x => x.Id == itemId)?.Criteria ?? []).ToList();
        var attachment = data.TaskFactoryConfigurations.SingleOrDefault(x => x.Id == itemId);
        if (attachment is null) return new(revision, false, "notEvaluated", DateTime.UtcNow,
            [new("attachment.required", "error", "Apply a published pipeline to this task first.")], null);
        var version = data.PipelineVersions.Single(x => x.Id == attachment.PipelineVersionId);
        if (data.FactoryPipelines.Single(x => x.Id == version.PipelineId).Archived)
            checks.Add(new("pipeline.archived", "error", "Choose an active pipeline before preparing new work."));
        if ((data.AgentRuns.Any(x => x.ItemId == itemId && x.Active) || data.FactoryRuns.Any(x => x.ItemId == itemId && x.Active))) checks.Add(new("task.busy", "error", "Wait for the task's active agent run."));
        if (data.Dependencies.Where(x => x.ItemId == itemId).Any(d => !data.Statuses.Single(s => s.Id == data.Items.Single(i => i.Id == d.DependsOnId).StatusId).IsCompleted))
            checks.Add(new("task.blocked", "error", "Complete prerequisite tasks before preparing pipeline work."));
        var definition = FactoryRules.Resolve(version.Definition, attachment.Overrides);
        if (definition.Stages.Any(s => s.Steps.Any(step => step.Kind == "agent" && step.Role == "implementation")) && !AgentRun.HasSpec(item.SpecJson))
            checks.Add(new("task.specification", "error", "Write a task specification before preparing implementation work."));
        foreach (var issue in FactoryRules.Validate(definition).Diagnostics)
            checks.Add(new(issue.Code, issue.Severity, issue.Message, issue.StageId, issue.StepId));
        var inspection = await environment.Inspect(definition, data.AgentConnections.SingleOrDefault(x => x.ProjectId == item.ProjectId), ct);
        checks.AddRange(inspection.Checks);
        return new(revision, inspection.Environment is not null && checks.All(x => x.Severity != "error"), "notEvaluated", DateTime.UtcNow, checks, inspection.Environment);
    }
    public async Task<FactoryRunDetail> Prepare(Guid itemId, PrepareFactoryRun input, CancellationToken ct)
    {
        if (input.RequestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        var before = await store.ReadAsync(ct);
        var duplicate = before.FactoryRuns.SingleOrDefault(x => x.RequestId == input.RequestId);
        if (duplicate is not null) return Replay(before, duplicate, itemId);
        var item = Item(before, itemId); before.Graph(item.ProjectId, input.ExpectedRevision);
        var readiness = await Inspect(before, itemId, ct);
        if (!readiness.CanPrepare) throw new RuleException("Resolve readiness errors before preparing a run.");
        var attachment = before.TaskFactoryConfigurations.Single(x => x.Id == itemId);
        var version = before.PipelineVersions.Single(x => x.Id == attachment.PipelineVersionId);
        var snapshot = new FactoryRunSnapshot(version.Id, version.Number, version.Name, version.Source, item.Title, item.SpecJson,
            input.ExpectedRevision, attachment.Overrides, readiness.Environment!, readiness.Checks, readiness.CheckedAt, before.AcceptanceCriteria.SingleOrDefault(x => x.Id == itemId)?.Criteria ?? []);
        var run = FactoryRun.Prepare(input.RequestId, item, snapshot);
        var saved = await store.WriteAsync(data =>
        {
            var existing = data.FactoryRuns.SingleOrDefault(x => x.RequestId == input.RequestId);
            if (existing is not null) { Replay(data, existing, itemId); return; }
            data.ForItem(itemId, input.ExpectedRevision);
            if ((data.AgentRuns.Any(x => x.ItemId == itemId && x.Active) || data.FactoryRuns.Any(x => x.ItemId == itemId && x.Active))) throw new RuleException("Wait for the task's active agent run.", 409);
            data.FactoryRuns.Add(run);
            foreach (var stage in snapshot.Environment.Definition.Stages)
            {
                var stageRun = FactoryStageRun.Prepare(run.Id, stage); data.FactoryStageRuns.Add(stageRun);
                data.FactoryStepRuns.AddRange(stage.Steps.Select(step => FactoryStepRun.Prepare(run.Id, stageRun.Id, step)));
            }
        }, ct);
        return Detail(saved, saved.FactoryRuns.Single(x => x.RequestId == input.RequestId));
    }
    public async Task<IReadOnlyList<FactoryRunSummary>> List(Guid itemId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); Item(data, itemId);
        return data.FactoryRuns.Where(x => x.ItemId == itemId).OrderByDescending(x => x.CreatedAt)
            .Select(Summary).ToList();
    }
    public async Task<IReadOnlyList<FactoryRunSummary>> ProjectRuns(Guid projectId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); data.Graph(projectId);
        return data.FactoryRuns.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).Select(Summary).ToList();
    }
    private static FactoryRunSummary Summary(FactoryRun run)
    {
        var snapshot = run.Snapshot;
        return new(run.Id, run.ItemId, run.State, run.CreatedAt, snapshot.PipelineName, snapshot.VersionNumber, snapshot.Title, run.Revision);
    }
    public async Task<FactoryRunDetail> Read(Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct);
        return Detail(data, data.FactoryRuns.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Factory run not found.", 404));
    }
    private static WorkItem Item(WorkspaceData data, Guid id) => data.Items.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Task not found.", 404);
    private static FactoryRunDetail Replay(WorkspaceData data, FactoryRun run, Guid itemId) => run.ItemId == itemId ? Detail(data, run) : throw new RuleException("That request ID belongs to another task.", 409);
    public static FactoryRunDetail Detail(WorkspaceData data, FactoryRun run)
    {
        var definition = run.Snapshot.Environment.Definition;
        var stages = data.FactoryStageRuns.Where(x => x.RunId == run.Id).ToDictionary(x => x.StageId);
        var steps = data.FactoryStepRuns.Where(x => x.RunId == run.Id).ToDictionary(x => (x.StageRunId, x.StepId));
        return new(run, definition.Stages.Select(stage => stages[stage.Id]).ToList(),
            definition.Stages.SelectMany(stage => stage.Steps.Select(step => steps[(stages[stage.Id].Id, step.Id)])).ToList(), data.FactoryArtifacts.Where(x => x.RunId == run.Id).OrderBy(x => x.Name).ToList());
    }
}
