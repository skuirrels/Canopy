using System.Text.Json;
using System.Text.Json.Serialization;
using Planner.Api.Domain;

namespace Planner.Api.Application;

public interface IRepairWorkspace { Task Fork(FactoryRun parent, FactoryRun target, VerificationContext expected, CancellationToken ct); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditRepairPolicy(long ExpectedRevision, RepairPolicy Policy);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareRepairLoop(Guid RequestId, long ExpectedRevision, string ExpectedContext, string Owner, IReadOnlyList<Guid> FindingIds, IReadOnlyList<Guid> CriterionIds);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdvanceRepairLoop(Guid RequestId, long ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopRepairLoop(Guid RequestId, long ExpectedRevision, string Reason);
public sealed record RepairPolicyDetail(long ProjectRevision, RepairPolicy? Policy);
public sealed record RepairAttemptView(FactoryRepairAttempt Attempt, FactoryRunSummary Run, int ActualAgentInvocations);
public sealed record RepairLoopDetail(FactoryRepairLoop Loop, IReadOnlyList<RepairAttemptView> Attempts, bool CanAdvance, string? BlockedReason, string CostStatus, int PollIntervalMs);
public sealed record RepairLibrary(Guid RunId, long RunRevision, VerificationContext? Context, string? ContextError, RepairPolicyDetail Policy,
    IReadOnlyList<RepairIssue> Issues, IReadOnlyList<RepairLoopDetail> Loops, int PollIntervalMs);

// Filesystem observations happen outside the native single-writer transaction. Revisions,
// source hashes and operation receipts are checked again inside it before dispatch.
public sealed class FactoryRepairService(IWorkspaceStore store, IFactoryEnvironment environment, IVerificationEnvironment verification,
    FactoryQualityService quality, IAgentEnvironment agents)
{
    public async Task<RepairPolicyDetail> Policy(Guid id, CancellationToken ct) => Policy(await store.ReadAsync(ct), id);
    private static RepairPolicyDetail Policy(WorkspaceData data, Guid id) => new(data.Graph(id).Project.Revision, data.RepairPolicies.SingleOrDefault(x => x.Id == id)?.Policy);
    public async Task<RepairPolicyDetail> EditPolicy(Guid id, EditRepairPolicy input, CancellationToken ct)
    {
        if (input.Policy is null) throw new RuleException("Supply an explicit repair policy.");
        await store.WriteAsync(data =>
        {
            var graph = data.Graph(id, input.ExpectedRevision); input.Policy.Validate();
            foreach (var (owner, versionId) in PlanIds(input.Policy)) ValidatePlan(data, id, owner, versionId);
            var policy = data.RepairPolicies.SingleOrDefault(x => x.Id == id);
            if (policy is null) data.RepairPolicies.Add(ProjectRepairPolicy.Adopt(id, input.Policy)); else policy.Replace(input.Policy);
            graph.Project.Touch();
        }, ct);
        return await Policy(id, ct);
    }
    public async Task<RepairLibrary> List(Guid runId, CancellationToken ct)
    {
        var q = await quality.Read(runId, ct); var data = await store.ReadAsync(ct); var run = FactoryDispatchService.Find(data, runId);
        q = FactoryQualityService.Detail(data, run, q.Context, q.ContextError);
        return new(runId, run.Revision, q.Context, q.ContextError, Policy(data, run.ProjectId), Issues(data, run, q, ""),
            data.RepairLoops.Where(x => x.ItemId == run.ItemId).OrderByDescending(x => x.CreatedAt).Select(x => Detail(data, x)).ToList(), agents.PollIntervalMs);
    }
    public async Task<RepairLoopDetail> Read(Guid id, CancellationToken ct) { var data = await store.ReadAsync(ct); return Detail(data, Find(data, id)); }
    public async Task<RepairLoopDetail> Prepare(Guid runId, PrepareRepairLoop input, CancellationToken ct)
    {
        var hash = Hash(new { runId, input }); var data = await store.ReadAsync(ct);
        var replay = Replay(data, input.RequestId, "prepare", hash); if (replay is not null) return Detail(data, replay);
        var run = FactoryDispatchService.Find(data, runId); Idle(data, run.ItemId); Rules.Revision(input.ExpectedRevision, run.Revision);
        if (run.Active || run.State == "prepared" || run.WorkspacePath is null) throw new RuleException("Use finished work with a retained worktree.", 409);
        if (input.Owner is not ("implementation" or "verification")) throw new RuleException("Choose implementation or verification ownership.");
        var q = await quality.Read(runId, ct);
        if (q.Context is null || q.SourceChanged || q.Context.Fingerprint != input.ExpectedContext) throw new RuleException(q.ContextError ?? "Source inputs changed. Refresh before capturing repair.", 409);
        var policy = Policy(data, run.ProjectId).Policy ?? throw new RuleException("Adopt a project repair policy first."); policy.Validate();
        var selected = Select(Issues(data, run, q, input.Owner), input);
        RequireSelectedBlockers(data, run, q, input.FindingIds);
        var connection = data.AgentConnections.SingleOrDefault(x => x.ProjectId == run.ProjectId) ?? throw new RuleException("Connect the project repository first.");
        if (connection.RepositoryPath != run.Snapshot.Environment.RepositoryPath) throw new RuleException("Reconnect the captured repository before repair.", 409);
        var pinned = ProjectAgentConnection.Create(run.ProjectId);
        pinned.Configure(connection.RepositoryPath, q.Context.HeadCommit, connection.Provider, connection.Model, connection.Instructions, connection.Settings);
        var plans = new List<RepairPlan>();
        foreach (var (owner, versionId) in PlanIds(policy))
        {
            var version = ValidatePlan(data, run.ProjectId, owner, versionId);
            var inspected = await environment.Inspect(version.Definition, pinned, ct); RequireInspection(inspected);
            plans.Add(new(owner, version.Id, version.Number, version.Name, version.Source, inspected.Environment!));
        }
        if (!plans.Any(x => x.Owner == input.Owner)) throw new RuleException("Configure a published repair plan for the selected responsible role.");
        if ((await verification.Observe(run, ct)).Fingerprint != q.Context.Fingerprint) throw new RuleException("Source worktree changed while capturing repair.", 409);
        var scope = new RepairScope(input.Owner, selected, FactoryQualityService.Source(data, run),
            run.Snapshot.Repair?.SourcePipelineVersionId ?? run.Snapshot.PipelineVersionId, run.Snapshot.Repair?.SourceOverrides ?? run.Snapshot.Overrides, plans);
        Guid id = Guid.Empty;
        var saved = await store.WriteAsync(current =>
        {
            var existing = Replay(current, input.RequestId, "prepare", hash); if (existing is not null) { id = existing.Id; return; }
            var source = FactoryDispatchService.Find(current, runId); RequireSelectedBlockers(current, source, FactoryQualityService.Detail(current, source, q.Context, null), input.FindingIds); Rules.Revision(input.ExpectedRevision, source.Revision); Idle(current, source.ItemId);
            if (current.RepairLoops.Any(x => x.ItemId == run.ItemId && x.Open)) throw new RuleException("Stop the task's open repair loop first.", 409);
            if (FactoryQualityService.Source(current, source) != scope.SourceHash || Hash(Policy(current, run.ProjectId).Policy) != Hash(policy)) throw new RuleException("Task inputs or policy changed during preparation.", 409);
            foreach (var plan in plans) ValidatePlan(current, run.ProjectId, plan.Owner, plan.PipelineVersionId);
            if (Hash(Select(Issues(current, source, FactoryQualityService.Detail(current, source, q.Context, null), input.Owner), input)) != Hash(selected)) throw new RuleException("Repair causes changed. Refresh before capturing.", 409);
            var loop = FactoryRepairLoop.Prepare(input.RequestId, source, policy, scope, q.Context, hash); current.RepairLoops.Add(loop); id = loop.Id;
        }, ct);
        return Detail(saved, Find(saved, id));
    }
    public async Task<RepairLoopDetail> Advance(Guid id, AdvanceRepairLoop input, CancellationToken ct)
    {
        var hash = Hash(new { id, input }); var data = await store.ReadAsync(ct);
        if (ReplayFor(data, id, input.RequestId, "advance", hash)) return Detail(data, Find(data, id));
        Rules.Revision(input.ExpectedRevision, Find(data, id).Revision);
        await Queue(id, input, hash, ct); return await Read(id, ct);
    }
    public async Task<RepairLoopDetail> Stop(Guid id, StopRepairLoop input, CancellationToken ct)
    {
        var hash = Hash(new { id, input });
        var saved = await store.WriteAsync(data =>
        {
            if (ReplayFor(data, id, input.RequestId, "stop", hash)) return;
            var loop = Find(data, id); Rules.Revision(input.ExpectedRevision, loop.Revision);
            loop.Stopping(input.Reason); loop.Remember(input.RequestId, "stop", hash); Cancel(data, loop);
        }, ct);
        Signal(saved, Find(saved, id)); return await Read(id, ct);
    }
    public async Task Halt(Guid id, string reason, CancellationToken ct)
    {
        var saved = await store.WriteAsync(data => { var loop = data.RepairLoops.SingleOrDefault(x => x.Id == id); if (loop?.Open == true) { loop.Stopping(reason); Cancel(data, loop); } }, ct);
        if (saved.RepairLoops.SingleOrDefault(x => x.Id == id) is { } loop) Signal(saved, loop);
    }
    public async Task Reconcile(Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var loop = Find(data, id); if (!loop.Open) return;
        var run = Candidate(data, loop); FactoryQualityDetail? q = null;
        if (!run.Active && run.WorkspacePath is not null && loop.State != "stopping" && loop.Expired(DateTime.UtcNow) is null && FactoryQualityService.Source(data, run) == loop.Scope.SourceHash) q = await quality.Read(run.Id, ct);
        var saved = await store.WriteAsync(current =>
        {
            var captured = Find(current, id); var candidate = Candidate(current, captured);
            if (!captured.Open || candidate.Id != run.Id || candidate.Revision != run.Revision) return;
            Evaluate(current, captured, candidate, q);
            if (captured.State == "stopping") Cancel(current, captured);
        }, ct);
        loop = Find(saved, id); if (loop.State == "stopping") Signal(saved, loop);
        if (loop.State == "needsDecision" && loop.Policy.AutoContinue)
        {
            // Every automatic dispatch uses the same checks and transaction as an explicit advance.
            try { var input = new AdvanceRepairLoop(Guid.NewGuid(), loop.Revision); await Queue(id, input, Hash(new { id, input }), ct); }
            catch (RuleException error) when (error.Status == 409) { /* Concurrent operator action wins; re-observe next tick. */ }
        }
    }
    private async Task Queue(Guid id, AdvanceRepairLoop input, string hash, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var loop = Find(data, id);
        if (ReplayFor(data, id, input.RequestId, "advance", hash)) return;
        Rules.Revision(input.ExpectedRevision, loop.Revision);
        if (!Detail(data, loop).CanAdvance) throw new RuleException(Detail(data, loop).BlockedReason ?? "Refresh the loop before continuing.", 409);
        var parent = Candidate(data, loop); Idle(data, parent.ItemId);
        var context = await verification.Observe(parent, ct);
        if (loop.State == "prepared" && context.Fingerprint != loop.Context.Fingerprint) throw new RuleException("The captured source worktree changed. Stop and prepare a current loop.", 409);
        if (loop.State == "needsDecision" && Latest(data, loop)?.Feedback?.CodeHash != context.Fingerprint) throw new RuleException("The failed candidate changed. Refresh QA and review before continuing.", 409);
        var plan = loop.Scope.Plans.Single(x => x.Owner == loop.Scope.Owner);
        ValidatePlan(data, loop.ProjectId, plan.Owner, plan.PipelineVersionId);
        var env = plan.Environment with { BaseCommit = context.HeadCommit, BaseRef = context.HeadCommit };
        RequireInspection(await environment.InspectCaptured(env, ct));
        if ((await verification.Observe(parent, ct)).Fingerprint != context.Fingerprint) throw new RuleException("Repair source changed during dispatch.", 409);
        await store.WriteAsync(current =>
        {
            if (ReplayFor(current, id, input.RequestId, "advance", hash)) return;
            var captured = Find(current, id); Rules.Revision(input.ExpectedRevision, captured.Revision);
            var source = Candidate(current, captured); Rules.Revision(parent.Revision, source.Revision); Idle(current, source.ItemId);
            ValidateCurrent(current, captured, source); ValidatePlan(current, loop.ProjectId, plan.Owner, plan.PipelineVersionId);
            var count = current.RepairAttempts.Count(x => x.LoopId == id); var calls = FactoryRepairRules.AgentCalls(env.Definition);
            if (captured.Budget(count, calls, DateTime.UtcNow) is { } reason) throw new RuleException(reason, 409);
            if (captured.State is not ("prepared" or "needsDecision")) throw new RuleException("The loop is not ready for dispatch.", 409);
            // Re-evaluate explicit QA/review evidence inside the writer before a retry.
            if (captured.State == "needsDecision")
            {
                var q = FactoryQualityService.Detail(current, source, context, null);
                var decision = Decision(current, captured, source, q);
                if (decision.State != "needsDecision" || decision.Feedback is null) throw new RuleException(decision.Reason, 409);
            }
            var attemptId = Guid.NewGuid(); var feedback = Latest(current, captured)?.Feedback ?? new RepairFeedback("initial", "Explicitly selected repair scope.", captured.Scope.Issues, null, context.Fingerprint);
            var lineage = new FactoryRepairLineage(id, attemptId, source.Id, captured.RootRunId, count + 1, captured.Scope.SourcePipelineVersionId, captured.Scope.SourceOverrides, context, feedback);
            var snapshot = new FactoryRunSnapshot(plan.PipelineVersionId, plan.VersionNumber, plan.Name, plan.Source, source.Snapshot.Title, source.Snapshot.SpecJson,
                current.Graph(loop.ProjectId).Project.Revision, new FactoryOverrides([]), env, [], DateTime.UtcNow, source.Snapshot.Criteria ?? [], lineage);
            if (current.FactoryRuns.Any(x => x.RequestId == input.RequestId)) throw new RuleException("That request ID belongs to an existing work order.", 409);
            var item = current.Items.Single(x => x.Id == source.ItemId); var child = FactoryRun.Prepare(input.RequestId, item, snapshot);
            child.Queue(child.Revision, input.RequestId); current.FactoryRuns.Add(child);
            foreach (var stage in env.Definition.Stages)
            {
                var stageRun = FactoryStageRun.Prepare(child.Id, stage); current.FactoryStageRuns.Add(stageRun);
                current.FactoryStepRuns.AddRange(stage.Steps.Select(step => FactoryStepRun.Prepare(child.Id, stageRun.Id, step)));
            }
            current.RepairAttempts.Add(FactoryRepairAttempt.Create(attemptId, captured, child, source.Id, count + 1));
            captured.Queue(calls); captured.Remember(input.RequestId, "advance", hash);
        }, ct);
    }
    private static void Evaluate(WorkspaceData data, FactoryRepairLoop loop, FactoryRun run, FactoryQualityDetail? observed)
    {
        if (!loop.Open) return;
        if (loop.State == "stopping") { if (!run.Active && !FactoryDispatchService.Children(data, run.Id).Any(x => x.Active)) loop.End(false, loop.Reason); return; }
        if (FactoryQualityService.Source(data, run) != loop.Scope.SourceHash) { loop.Stopping("Task specification, criteria or attachment changed. Capture a new repair scope."); return; }
        if (loop.Expired(DateTime.UtcNow) is { } expired) { loop.Stopping(expired); return; }
        if (Latest(data, loop) is null) return;
        if (run.Active) return;
        if (run.State is "cancelled" or "interrupted") { loop.End(false, "Execution was cancelled or interrupted; no automatic retry."); return; }
        var q = FactoryQualityService.Detail(data, run, observed?.Context, observed?.ContextError);
        var decision = Decision(data, loop, run, q);
        if (decision.Feedback is { } feedback)
        {
            var attempt = Latest(data, loop)!; var first = attempt.Feedback is null;
            if (attempt.Record(feedback)) loop.Feedback(feedback.Remaining.Count, first);
        }
        if (decision.State == "completed") loop.End(true, decision.Reason);
        else if (decision.State == "stopped") loop.End(false, decision.Reason);
        else
        {
            var budget = decision.State == "needsDecision" ? loop.Budget(data.RepairAttempts.Count(x => x.LoopId == loop.Id), FactoryRepairRules.AgentCalls(loop.Scope.Plans.Single(x => x.Owner == loop.Scope.Owner).Environment.Definition), DateTime.UtcNow) : null;
            if (budget is not null) loop.End(false, budget); else loop.Progress(decision.State, decision.Reason);
        }
    }
    private sealed record RepairDecision(string State, string Reason, RepairFeedback? Feedback = null);
    private static RepairDecision Decision(WorkspaceData data, FactoryRepairLoop loop, FactoryRun run, FactoryQualityDetail q)
    {
        if (q.Context is null || q.SourceChanged) return new("stopped", q.ContextError ?? "Verification source changed; recapture the repair scope.");
        var scope = loop.Scope; var findings = FactoryReviewService.Findings(data, run, q.Context);
        var selectedFindings = scope.Issues.Where(x => x.Kind == "finding").Select(x => x.Id).ToHashSet();
        var selectedCriteria = scope.Issues.Where(x => x.Kind == "criterion").Select(x => x.Id).Concat(findings.Where(x => selectedFindings.Contains(x.Finding.Id)).SelectMany(x => x.Finding.Data.CriterionIds)).ToHashSet();
        var outside = findings.Any(x => x.Finding.Data.Category != "improvement" && (x.History[0].Status is "unfixed" or "unverified") && !selectedFindings.Contains(x.Finding.Id));
        if (outside) return new("stopped", "New or unselected blocking findings require a new human repair decision.");
        RepairFeedback Feedback(string kind, string reason, IReadOnlyList<RepairIssue> remaining, Guid? reviewId = null) => new(kind, reason, remaining, reviewId, q.Context.Fingerprint);
        if (run.State == "failed")
        {
            var failed = data.FactoryStepRuns.Where(x => x.RunId == run.Id && x.State == "failed" && x.ExitCode is not null and not 0)
                .Any(step => data.VerificationReceipts.SingleOrDefault(x => x.StepRunId == step.Id)?.Matches(q.Context, step) == true);
            return failed ? new("needsDecision", "A real verification command failed. The next bounded attempt will address the captured scope.", Feedback("commandFailed", run.Error ?? "Verification command failed.", scope.Issues))
                : new("stopped", "Execution failed without intact failed-command evidence. Inspect the worktree and decide manually.");
        }
        if (q.Criteria.Any(x => x.Status is "unverified" or "stale") || q.Criteria.Count == 0) return new("awaitingQa", "Record fresh criterion QA and asserting test evidence for this attempt.");
        var failedCriteria = q.Criteria.Where(x => x.Status == "fail").ToList();
        if (failedCriteria.Any(x => !selectedCriteria.Contains(x.Criterion.Id))) return new("stopped", "A criterion outside the selected repair scope failed; a new human decision is required.");
        if (failedCriteria.Count > 0)
        {
            var remaining = scope.Issues.Where(x => x.Kind == "finding" || failedCriteria.Any(c => c.Criterion.Id == x.Id)).ToList();
            return new("needsDecision", "Current QA failed within the selected scope.", Feedback("qaFailed", "Fresh explicit criterion QA failed.", remaining));
        }
        var review = data.ReviewRounds.Where(x => x.RunId == run.Id && x.State == "submitted").OrderByDescending(x => x.Number).FirstOrDefault();
        if (review is null || FactoryReviewService.Stale(review, data, run, q) is not null || data.ReviewRounds.Any(x => x.ItemId == run.ItemId && x.Open)) return new("awaitingReview", "Submit a fresh independent review of this attempt after QA.");
        var remainingFindings = findings.Where(x => selectedFindings.Contains(x.Finding.Id) && x.Status is not ("fixed" or "noLongerApplies")).Select(x => Issue(x)).ToList();
        if (review.Submission!.Verdict == "approve" && remainingFindings.Count == 0) return new("completed", "Fresh QA and independent approval resolved the selected issues. Task acceptance remains explicit.", Feedback("approved", review.Submission.Summary, [], review.Id));
        if (review.Submission.Verdict == "requestChanges" && remainingFindings.Count > 0 && remainingFindings.All(x => x.Owner == scope.Owner)) return new("needsDecision", "Independent review still requires repair within the selected scope.", Feedback("reviewFailed", review.Submission.Summary, remainingFindings, review.Id));
        return new("stopped", "Review was inconclusive or requires a changed repair scope. A human decision is required.");
    }
    private RepairLoopDetail Detail(WorkspaceData data, FactoryRepairLoop loop)
    {
        var attempts = data.RepairAttempts.Where(x => x.LoopId == loop.Id).OrderBy(x => x.Number).Select(x =>
        {
            var r = FactoryDispatchService.Find(data, x.RunId); return new RepairAttemptView(x, new(r.Id, r.ItemId, r.State, r.CreatedAt, r.Snapshot.PipelineName, r.Snapshot.VersionNumber, r.Snapshot.Title, r.Revision), FactoryDispatchService.Children(data, r.Id).Count(a => a.StartedAt.HasValue));
        }).ToList();
        var calls = FactoryRepairRules.AgentCalls(loop.Scope.Plans.Single(x => x.Owner == loop.Scope.Owner).Environment.Definition);
        var blocked = loop.State is not ("prepared" or "needsDecision") ? loop.Reason : loop.Budget(attempts.Count, calls, DateTime.UtcNow);
        return new(loop, attempts, loop.Open && (loop.State is "prepared" or "needsDecision") && blocked is null, blocked, "unavailable", agents.PollIntervalMs);
    }
    private static IEnumerable<(string Owner, Guid Id)> PlanIds(RepairPolicy p)
    { if (p.ImplementationPlanVersionId is { } implementation) yield return ("implementation", implementation); if (p.VerificationPlanVersionId is { } verification) yield return ("verification", verification); }
    private static PipelineVersion ValidatePlan(WorkspaceData data, Guid projectId, string owner, Guid id)
    {
        var version = data.PipelineVersions.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Published repair plan not found.", 404);
        var pipeline = data.FactoryPipelines.Single(x => x.Id == version.PipelineId);
        if (pipeline.ProjectId != projectId || pipeline.Archived) throw new RuleException("Choose an active published repair plan from this project.");
        FactoryRepairRules.Plan(version.Definition, owner); return version;
    }
    private static void RequireInspection(FactoryInspection inspected)
    { if (inspected.Environment is null || inspected.Checks.Any(x => x.Severity == "error")) throw new RuleException(inspected.Checks.FirstOrDefault(x => x.Severity == "error")?.Message ?? "Repair environment is unavailable."); }
    private static IReadOnlyList<RepairIssue> Issues(WorkspaceData data, FactoryRun run, FactoryQualityDetail q, string owner) => q.Context is null || q.SourceChanged ? [] :
        FactoryReviewService.Findings(data, run, q.Context).Where(x => (x.Status is "unfixed" or "unverified") && x.Finding.Data.Owner is "implementation" or "verification" && x.Finding.Data.Category != "improvement").Select(Issue)
        .Concat(q.Criteria.Where(x => x.Status == "fail").Select(x => new RepairIssue("criterion", x.Criterion.Id, x.Criterion.Description, owner, x.History[0].Reason, x.History[0].Id))).ToList();
    private static RepairIssue Issue(ReviewFindingView view) => new("finding", view.Finding.Id, view.Finding.Data.Title, view.Finding.Data.Owner, view.History[0].Reason, view.History[0].RoundId);
    private static void RequireSelectedBlockers(WorkspaceData data, FactoryRun run, FactoryQualityDetail q, IReadOnlyList<Guid> ids)
    {
        if (FactoryReviewService.Findings(data, run, q.Context).Any(x => x.Finding.Data.Category != "improvement" && (x.History[0].Status is "unfixed" or "unverified") && !ids.Contains(x.Finding.Id)))
            throw new RuleException("Every unresolved blocking finding must be current, selected and owned by the same repair role. Re-review stale findings or split their scope first.", 409);
    }
    private static IReadOnlyList<RepairIssue> Select(IReadOnlyList<RepairIssue> eligible, PrepareRepairLoop input)
    {
        if (input.FindingIds is null || input.CriterionIds is null || input.FindingIds.Count + input.CriterionIds.Count is < 1 or > 100 || input.FindingIds.Distinct().Count() != input.FindingIds.Count || input.CriterionIds.Distinct().Count() != input.CriterionIds.Count) throw new RuleException("Select 1–100 distinct current findings or failed criteria.");
        var selected = eligible.Where(x => x.Kind == "finding" ? input.FindingIds.Contains(x.Id) : input.CriterionIds.Contains(x.Id)).ToList();
        if (selected.Count != input.FindingIds.Count + input.CriterionIds.Count || selected.Any(x => x.Owner != input.Owner)) throw new RuleException("Select current actionable issues assigned to the chosen repair role.", 409);
        // Unselected blockers cannot silently disappear into a narrow repair loop.
        if (eligible.Any(x => x.Kind == "finding" && !input.FindingIds.Contains(x.Id))) throw new RuleException("Address all current blocking findings together for one role, or resolve/split their scope first.", 409);
        return selected;
    }
    private static void ValidateCurrent(WorkspaceData data, FactoryRepairLoop loop, FactoryRun source)
    {
        if (FactoryQualityService.Source(data, source) != loop.Scope.SourceHash) throw new RuleException("Repair scope changed. Stop and recapture.", 409);
        if (data.AgentConnections.SingleOrDefault(x => x.ProjectId == loop.ProjectId)?.RepositoryPath != source.Snapshot.Environment.RepositoryPath) throw new RuleException("Captured repository connection changed.", 409);
        if (data.Dependencies.Where(x => x.ItemId == source.ItemId).Any(d => !data.Statuses.Single(s => s.Id == data.Items.Single(i => i.Id == d.DependsOnId).StatusId).IsCompleted)) throw new RuleException("Complete prerequisite tasks before repair.", 409);
    }
    private static void Idle(WorkspaceData data, Guid itemId)
    { if (data.Deliveries.Any(x => x.ItemId == itemId && x.Open) || data.FactoryRuns.Any(x => x.ItemId == itemId && x.Active) || data.AgentRuns.Any(x => x.ItemId == itemId && x.Active) || data.ReviewRounds.Any(x => x.ItemId == itemId && x.Open)) throw new RuleException("Finish task execution/review and close the open delivery first.", 409); }
    private static FactoryRepairAttempt? Latest(WorkspaceData data, FactoryRepairLoop loop) => data.RepairAttempts.Where(x => x.LoopId == loop.Id).OrderByDescending(x => x.Number).FirstOrDefault();
    private static FactoryRun Candidate(WorkspaceData data, FactoryRepairLoop loop) => FactoryDispatchService.Find(data, Latest(data, loop)?.RunId ?? loop.RootRunId);
    private static FactoryRepairLoop Find(WorkspaceData data, Guid id) => data.RepairLoops.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Repair loop not found.", 404);
    private static string Hash<T>(T value) => FactoryArtifact.Hash(JsonSerializer.Serialize(value, FactoryRules.Json));
    private static FactoryRepairLoop? Replay(WorkspaceData data, Guid requestId, string kind, string hash)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        var loop = data.RepairLoops.SingleOrDefault(x => x.Operations.Any(op => op.RequestId == requestId));
        if (loop is not null && !loop.Operations.Any(op => op.RequestId == requestId && op.Kind == kind && op.Hash == hash)) throw new RuleException("That request ID records a different repair action.", 409); return loop;
    }
    private static bool ReplayFor(WorkspaceData data, Guid id, Guid requestId, string kind, string hash)
    { var loop = Replay(data, requestId, kind, hash); if (loop is not null && loop.Id != id) throw new RuleException("That request ID belongs to another repair loop.", 409); return loop is not null; }
    private static void Cancel(WorkspaceData data, FactoryRepairLoop loop)
    {
        var run = Candidate(data, loop);
        if (Latest(data, loop) is null) { loop.End(false, loop.Reason); return; }
        if (run.Active) run.Cancel(run.Revision);
        foreach (var child in FactoryDispatchService.Children(data, run.Id).Where(x => x.Active)) child.Cancel();
        if (!run.Active && !FactoryDispatchService.Children(data, run.Id).Any(x => x.Active)) { FactoryDispatchService.End(data, run, "cancelled", null); loop.End(false, loop.Reason); }
    }
    private void Signal(WorkspaceData data, FactoryRepairLoop loop)
    { var run = Candidate(data, loop); agents.Cancel(run.Id); foreach (var child in FactoryDispatchService.Children(data, run.Id)) agents.Cancel(child.Id); }
}
