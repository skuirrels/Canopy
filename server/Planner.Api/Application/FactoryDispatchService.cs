using Planner.Api.Domain;
using System.Text.Json.Serialization;

namespace Planner.Api.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartFactoryRun(Guid RequestId, long ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelFactoryRun(long ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideFactoryStep(Guid RequestId, long ExpectedRevision, [property: JsonRequired] bool Approved, string? Note);

public sealed class FactoryDispatchService(IWorkspaceStore store, IFactoryEnvironment environment, IAgentEnvironment agents)
{
    public async Task<FactoryRunDetail> Start(Guid id, StartFactoryRun input, CancellationToken ct)
    {
        if (input.RequestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        var before = await store.ReadAsync(ct); var run = Find(before, id);
        if (run.StartRequestId == input.RequestId && input.RequestId != Guid.Empty) return FactoryRunService.Detail(before, run);
        Rules.Revision(input.ExpectedRevision, run.Revision);
        if (run.State != "prepared") throw new RuleException("Only prepared work orders can start. Review interrupted work before preparing a new run.", 409);
        CheckCurrent(before, run);
        var inspected = await environment.InspectCaptured(run.Snapshot.Environment, ct);
        var error = inspected.Checks.FirstOrDefault(x => x.Severity == "error");
        if (error is not null) throw new RuleException(error.Message);
        var saved = await store.WriteAsync(data =>
        {
            var current = Find(data, id);
            if (current.StartRequestId == input.RequestId && input.RequestId != Guid.Empty) return;
            CheckCurrent(data, current); current.Queue(input.ExpectedRevision, input.RequestId);
        }, ct);
        return FactoryRunService.Detail(saved, Find(saved, id));
    }
    public async Task<FactoryRunDetail> Cancel(Guid id, CancelFactoryRun input, CancellationToken ct)
    {
        var saved = await store.WriteAsync(data =>
        {
            var run = Find(data, id); run.Cancel(input.ExpectedRevision);
            foreach (var child in Children(data, id).Where(x => x.Active)) child.Cancel();
            if (!run.Active) End(data, run, "cancelled", null);
        }, ct);
        agents.Cancel(id); foreach (var child in Children(saved, id)) agents.Cancel(child.Id);
        return FactoryRunService.Detail(saved, Find(saved, id));
    }
    public async Task<FactoryRunDetail> Decide(Guid id, Guid stepId, DecideFactoryStep input, CancellationToken ct)
    {
        var saved = await store.WriteAsync(data =>
        {
            var run = Find(data, id);
            var step = data.FactoryStepRuns.SingleOrDefault(x => x.RunId == id && x.Id == stepId) ?? throw new RuleException("Factory step not found.", 404);
            if (step.DecisionRequestId == input.RequestId && input.RequestId != Guid.Empty)
            {
                if (step.Decision != input.Approved || step.DecisionNote != AgentRun.Text(input.Note, "Decision note")) throw new RuleException("That decision ID was used for a different decision.", 409);
                return;
            }
            Rules.Revision(input.ExpectedRevision, run.Revision);
            if (run.State != "waiting") throw new RuleException("The factory run is not waiting for approval.", 409);
            var stage = data.FactoryStageRuns.Single(x => x.Id == step.StageRunId);
            var definition = run.Snapshot.Environment.Definition.Stages.Single(x => x.Id == stage.StageId).Steps.Single(x => x.Id == step.StepId);
            if (definition.Kind != "manual") throw new RuleException("This is not a human approval step.");
            step.Decide(input.RequestId, input.Approved, input.Note);
            if (input.Approved) { Capture(data, run, step, definition); stage.Continue(); run.Continue(); }
            else End(data, run, "failed", "Human approval was declined.");
        }, ct);
        return FactoryRunService.Detail(saved, Find(saved, id));
    }
    private static void CheckCurrent(WorkspaceData data, FactoryRun run)
    {
        if (!data.Items.Any(x => x.Id == run.ItemId)) throw new RuleException("Task not found.", 404);
        var connection = data.AgentConnections.SingleOrDefault(x => x.ProjectId == run.ProjectId);
        if (connection is null || connection.RepositoryPath != run.Snapshot.Environment.RepositoryPath) throw new RuleException("Connect the project to the captured repository before starting.");
        var version = data.PipelineVersions.Single(x => x.Id == run.Snapshot.PipelineVersionId);
        if (data.FactoryPipelines.Single(x => x.Id == version.PipelineId).Archived) throw new RuleException("This pipeline is archived. Prepare work from an active pipeline.");
        if (data.AgentRuns.Any(x => x.ItemId == run.ItemId && x.Active) || data.FactoryRuns.Any(x => x.ItemId == run.ItemId && x.Id != run.Id && x.Active))
            throw new RuleException("This task already has active work.", 409);
        if (data.Dependencies.Where(x => x.ItemId == run.ItemId).Any(d => !data.Statuses.Single(s => s.Id == data.Items.Single(i => i.Id == d.DependsOnId).StatusId).IsCompleted))
            throw new RuleException("Complete prerequisite tasks before starting this run.", 409);
    }
    public static FactoryRun Find(WorkspaceData data, Guid id) => data.FactoryRuns.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Factory run not found.", 404);
    public static IEnumerable<AgentRun> Children(WorkspaceData data, Guid id)
    {
        var stepIds = data.FactoryStepRuns.Where(x => x.RunId == id).Select(x => x.Id).ToHashSet();
        return data.AgentRuns.Where(x => x.FactoryStepRunId.HasValue && stepIds.Contains(x.FactoryStepRunId.Value));
    }
    public static void Capture(WorkspaceData data, FactoryRun run, FactoryStepRun step, FactoryStep definition)
    {
        foreach (var name in definition.Produces ?? []) data.FactoryArtifacts.Add(FactoryArtifact.Capture(run.Id, step, name));
    }
    public static void End(WorkspaceData data, FactoryRun run, string state, string? error)
    {
        run.End(state, error); state = run.State;
        foreach (var step in data.FactoryStepRuns.Where(x => x.RunId == run.Id))
        {
            if (step.AgentRunId is { } childId && data.AgentRuns.SingleOrDefault(x => x.Id == childId) is { } child) step.RecoverAgent(child);
            if (step.State is "running" or "queued" or "waiting") step.Complete(state == "succeeded" ? "failed" : state, step.Evidence, error);
            else step.NotRun(state == "failed" ? "blocked" : state == "succeeded" ? "blocked" : state);
        }
        foreach (var stage in data.FactoryStageRuns.Where(x => x.RunId == run.Id)) stage.End(state == "failed" && stage.State == "pending" ? "blocked" : state);
    }
}
