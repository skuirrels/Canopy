using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Planner.Api.Domain;

namespace Planner.Api.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartBuild(Guid RequestId, long ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KeepBuild(Guid RequestId, long ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestBuildChanges(Guid RequestId, long ExpectedRevision, string Note);

public sealed record BuildFile(string Path, int? Added, int? Removed, bool New);
public sealed record BuildChanges(IReadOnlyList<BuildFile> Files, bool Truncated);
public sealed record BuildTest(string Command, string State, int? ExitCode, string Output);
public sealed record BuildSummary(Guid RunId, Guid ItemId, string Title, long Revision, string State, string Phase, string? FailedWhile,
    string? Problem, string Agent, string? Model, BuildChanges? Changes, string? BuilderReport, string? ReviewReport, IReadOnlyList<BuildTest> Tests,
    string? ChangeRequest, Guid? PreviousRunId, Guid? NextRunId, string? KeptInto, string? KeptCommit, bool TaskDone, DateTime CreatedAt, DateTime? FinishedAt);
public sealed record BuildListItem(Guid RunId, Guid ItemId, string Title, string Phase, DateTime CreatedAt);

// The git side of a guided build: what changed in its working copy, and merging the committed result into the base branch.
public interface IBuildRepository
{
    Task<BuildChanges?> Changes(FactoryRun run, CancellationToken ct);
    Task<string> Candidate(FactoryRun run, CancellationToken ct);
    Task<string> Keep(FactoryRun run, string message, CancellationToken ct);
}

// One path from a task to a kept result: Build prepares and starts a run, Keep merges its commit and marks the task done,
// and asking for changes starts a revision from the previous result.
public sealed class FactoryBuildService(IWorkspaceStore store, FactoryRunService runs, FactoryDispatchService dispatch, IBuildRepository repository)
{
    private static readonly HashSet<string> Active = ["prepared", "queued", "running", "waiting", "cancelling"];

    public async Task<BuildSummary> Build(Guid taskId, StartBuild input, CancellationToken ct)
    {
        var detail = await runs.Prepare(taskId, new PrepareFactoryRun(input.RequestId, input.ExpectedRevision), new FactoryGuidedBuild(), null, ct);
        return await Start(detail, input.RequestId, ct);
    }

    public async Task<BuildSummary> Keep(Guid runId, KeepBuild input, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var run = Guided(data, runId);
        var (step, _) = Acceptance(data, run);
        if (step.DecisionRequestId == input.RequestId) return await Summary(runId, ct);
        Rules.Revision(input.ExpectedRevision, run.Revision);
        if (run.State != "waiting" || step.State != "waiting") throw new RuleException("This build isn't waiting for you to keep it.", 409);
        var branch = run.Snapshot.Environment.BaseRef;
        var commit = await repository.Keep(run, $"Merge Canopy build: {run.Snapshot.Title}", ct);
        await store.WriteAsync(saved =>
        {
            var current = Guided(saved, runId); var (decision, stage) = Acceptance(saved, current);
            if (decision.DecisionRequestId == input.RequestId) return;
            if (current.State != "waiting" || decision.State != "waiting") throw new RuleException("This build isn't waiting for you to keep it.", 409);
            decision.Decide(input.RequestId, true, $"Kept: merged into {branch}.", new { keptInto = branch, commit });
            stage.Continue(); current.Continue();
            var item = saved.Items.Single(x => x.Id == current.ItemId); var graph = saved.Graph(item.ProjectId);
            var done = saved.Statuses.Where(x => x.ProjectId == item.ProjectId && x.IsCompleted).OrderBy(x => x.Position).FirstOrDefault();
            if (done is not null && item.StatusId != done.Id) { graph.Move(item.Id, item.ParentId, done.Id, null); graph.Project.Touch(); }
        }, ct);
        return await Summary(runId, ct);
    }

    public async Task<BuildSummary> RequestChanges(Guid runId, RequestBuildChanges input, CancellationToken ct)
    {
        var note = AgentRun.Text(input.Note, "Change request");
        if (string.IsNullOrWhiteSpace(note)) throw new RuleException("Say what should change.");
        var data = await store.ReadAsync(ct); var run = Guided(data, runId);
        var (step, _) = Acceptance(data, run);
        if (step.DecisionRequestId != input.RequestId)
        {
            Rules.Revision(input.ExpectedRevision, run.Revision);
            if (run.State == "waiting" && step.State == "waiting")
                await store.WriteAsync(saved =>
                {
                    var current = Guided(saved, runId); var (decision, _) = Acceptance(saved, current);
                    if (decision.DecisionRequestId == input.RequestId) return;
                    if (current.State != "waiting" || decision.State != "waiting") throw new RuleException("This build changed. Look at it again before asking for changes.", 409);
                    decision.Decide(input.RequestId, false, note, new { changesRequested = true }, "You asked for changes.");
                    FactoryDispatchService.End(saved, current, "failed", "You asked for changes.");
                }, ct);
            else if (Active.Contains(run.State)) throw new RuleException("Wait for the build to finish before asking for changes.", 409);
        }
        data = await store.ReadAsync(ct); run = Guided(data, runId);
        var next = data.FactoryRuns.FirstOrDefault(x => x.Snapshot.Guided?.PreviousRunId == runId);
        if (next is not null) return await Summary(next.Id, ct);
        var candidate = await repository.Candidate(run, ct);
        var project = data.Graph(run.ProjectId).Project.Revision;
        var detail = await runs.Prepare(run.ItemId, new PrepareFactoryRun(Derived(input.RequestId, "prepare"), project),
            new FactoryGuidedBuild(runId, candidate, note), run.Snapshot.Environment.BaseCommit, ct);
        return await Start(detail, Derived(input.RequestId, "start"), ct);
    }

    // Sets aside a build that is waiting for you and builds the task again from the base branch as it is now,
    // for when the branch has moved on and the build no longer merges.
    public async Task<BuildSummary> BuildAgain(Guid runId, KeepBuild input, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var run = Guided(data, runId);
        var (step, _) = Acceptance(data, run);
        if (step.DecisionRequestId != input.RequestId)
        {
            Rules.Revision(input.ExpectedRevision, run.Revision);
            if (run.State != "waiting" || step.State != "waiting") throw new RuleException("Only a build that is waiting for you can be built again this way. Use Build on the task instead.", 409);
            var reason = $"You built it again from the latest {run.Snapshot.Environment.BaseRef}.";
            await store.WriteAsync(saved =>
            {
                var current = Guided(saved, runId); var (decision, _) = Acceptance(saved, current);
                if (decision.DecisionRequestId == input.RequestId) return;
                if (current.State != "waiting" || decision.State != "waiting") throw new RuleException("This build changed. Look at it again first.", 409);
                decision.Decide(input.RequestId, false, reason, new { builtAgain = true }, reason);
                FactoryDispatchService.End(saved, current, "failed", reason);
            }, ct);
        }
        data = await store.ReadAsync(ct); run = Guided(data, runId);
        var detail = await runs.Prepare(run.ItemId, new PrepareFactoryRun(Derived(input.RequestId, "prepare"), data.Graph(run.ProjectId).Project.Revision), new FactoryGuidedBuild(runId), null, ct);
        return await Start(detail, Derived(input.RequestId, "start"), ct);
    }

    public async Task<BuildSummary?> Latest(Guid taskId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct);
        if (!data.Items.Any(x => x.Id == taskId)) throw new RuleException("Task not found.", 404);
        var latest = data.FactoryRuns.Where(x => x.ItemId == taskId && x.Snapshot.Guided is not null).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        return latest is null ? null : await Summary(latest.Id, ct);
    }

    public async Task<IReadOnlyList<BuildListItem>> List(Guid projectId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); data.Graph(projectId);
        return data.FactoryRuns.Where(x => x.ProjectId == projectId && x.Snapshot.Guided is not null)
            .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.CreatedAt).First())
            .Select(run => new BuildListItem(run.Id, run.ItemId, run.Snapshot.Title, Describe(data, run).Phase, run.CreatedAt))
            .OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<BuildSummary> Summary(Guid runId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var run = Guided(data, runId);
        var steps = Steps(data, run);
        var (phase, failedWhile, problem) = Describe(data, run);
        var writer = steps.FirstOrDefault(x => x.Step.Kind == "agent" && x.Step.Permission == "workspace-write");
        var reviewer = steps.FirstOrDefault(x => x.Step.Kind == "agent" && x.Step.Role == "review");
        var agent = writer.Run?.AgentRunId is { } agentId ? data.AgentRuns.SingleOrDefault(x => x.Id == agentId) : null;
        var acceptance = steps.FirstOrDefault(x => IsAcceptance(x.Step)).Run;
        var outcome = acceptance is null ? default : Field(acceptance.Evidence, "outcome");
        var item = data.Items.Single(x => x.Id == run.ItemId);
        BuildChanges? changes = null;
        try { changes = await repository.Changes(run, ct); } catch (RuleException) { }
        return new(run.Id, run.ItemId, run.Snapshot.Title, run.Revision, run.State, phase, failedWhile, problem,
            Provider(agent?.Provider ?? writer.Step?.Provider ?? run.Snapshot.Environment.DefaultProvider), agent?.Model ?? writer.Step?.Model,
            changes, Text(writer.Run?.Evidence, "result"), Text(reviewer.Run?.Evidence, "result"),
            steps.Where(x => x.Step.Kind == "command" && x.Step.Command is not null).Select(x => new BuildTest(
                string.Join(' ', new[] { x.Step.Command!.Executable }.Concat(x.Step.Command.Arguments)), x.Run.State, x.Run.ExitCode, AgentRun.Tail(x.Run.Output, 6000))).ToList(),
            run.Snapshot.Guided!.ChangeRequest, run.Snapshot.Guided.PreviousRunId,
            data.FactoryRuns.FirstOrDefault(x => x.Snapshot.Guided?.PreviousRunId == run.Id)?.Id,
            outcome is { ValueKind: JsonValueKind.Object } kept && kept.TryGetProperty("keptInto", out var into) ? into.GetString() : null,
            outcome is { ValueKind: JsonValueKind.Object } merged && merged.TryGetProperty("commit", out var sha) ? sha.GetString() : null,
            data.Statuses.Any(x => x.Id == item.StatusId && x.IsCompleted), run.CreatedAt, run.FinishedAt);
    }

    private async Task<BuildSummary> Start(FactoryRunDetail detail, Guid requestId, CancellationToken ct)
    {
        if (detail.Run.State == "prepared") await dispatch.Start(detail.Run.Id, new StartFactoryRun(Derived(requestId, "start"), detail.Run.Revision), ct);
        return await Summary(detail.Run.Id, ct);
    }

    // What the build is doing now, in the person's terms: starting, writing, testing, checking, yourTurn, kept,
    // changesRequested, finished, failed or stopped. A failure says which part failed and why.
    private static (string Phase, string? FailedWhile, string? Problem) Describe(WorkspaceData data, FactoryRun run)
    {
        var steps = Steps(data, run);
        var acceptance = steps.FirstOrDefault(x => IsAcceptance(x.Step)).Run;
        if (acceptance?.Decision == true && Field(acceptance.Evidence, "outcome").ValueKind == JsonValueKind.Object) return ("kept", null, null);
        if (acceptance?.Decision == false) return ("changesRequested", null, null);
        if (run.State is "cancelled" or "interrupted") return ("stopped", null, run.Error);
        if (run.State == "succeeded") return ("finished", null, null);
        if (run.State == "failed")
        {
            var failed = steps.FirstOrDefault(x => x.Run.State is "failed" or "interrupted" or "cancelled");
            return ("failed", failed.Step is null ? null : Activity(failed.Step), failed.Run?.Error ?? run.Error);
        }
        if (acceptance?.State == "waiting") return ("yourTurn", null, null);
        // Between steps, keep reporting the last thing done rather than flickering back to starting.
        var current = steps.FirstOrDefault(x => x.Run.State is "queued" or "running" or "waiting");
        if (current.Step is null) current = steps.LastOrDefault(x => x.Run.State == "succeeded" && x.Step.Kind != "manual");
        return (current.Step is null ? "starting" : Activity(current.Step), null, null);
    }
    private static string Activity(FactoryStep step) => step.Kind switch
    {
        "command" => "testing",
        "agent" when step.Permission == "workspace-write" => "writing",
        "agent" => "checking",
        _ => "starting",
    };
    private static bool IsAcceptance(FactoryStep step) => step.Kind == "manual" && (step.Consumes?.Any() ?? false);
    private static List<(FactoryStep Step, FactoryStepRun Run)> Steps(WorkspaceData data, FactoryRun run)
    {
        var stages = data.FactoryStageRuns.Where(x => x.RunId == run.Id).ToDictionary(x => x.StageId);
        var steps = data.FactoryStepRuns.Where(x => x.RunId == run.Id).ToList();
        return run.Snapshot.Environment.Definition.Stages.SelectMany(stage => stage.Steps.Select(step =>
            (step, steps.Single(x => x.StageRunId == stages[stage.Id].Id && x.StepId == step.Id)))).ToList();
    }
    private static (FactoryStepRun Step, FactoryStageRun Stage) Acceptance(WorkspaceData data, FactoryRun run)
    {
        var step = Steps(data, run).FirstOrDefault(x => IsAcceptance(x.Step)).Run ?? throw new RuleException("This workflow has no step where you keep the result.", 409);
        return (step, data.FactoryStageRuns.Single(x => x.Id == step.StageRunId));
    }
    private static FactoryRun Guided(WorkspaceData data, Guid id)
    {
        var run = FactoryDispatchService.Find(data, id);
        return run.Snapshot.Guided is null ? throw new RuleException("This run wasn't started with Build. Open it from the advanced run history.", 409) : run;
    }
    private static JsonElement Field(string? json, string name)
    {
        if (string.IsNullOrEmpty(json)) return default;
        try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty(name, out var value) ? value.Clone() : default; }
        catch (JsonException) { return default; }
    }
    private static string? Text(string? json, string name) => Field(json, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    private static string Provider(string id) => id switch { "claude" => "Claude", "codex" => "Codex", "gemini" => "Gemini", _ => id };
    private static Guid Derived(Guid requestId, string purpose) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{requestId}:{purpose}"))[..16]);
}
