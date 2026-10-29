using System.Text.Json;
using System.Text.Json.Serialization;
using Planner.Api.Domain;

namespace Planner.Api.Application;

public interface IReviewChanges { Task<string> RawChange(FactoryRun run, string baseCommit, CancellationToken ct); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BeginFactoryReview(Guid RequestId, long ExpectedRevision, string ExpectedContext, string Reviewer);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssessFactoryReview(Guid RequestId, long ExpectedRevision, string? Assessment, Guid? AssessmentStepRunId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecheckReviewFinding(Guid FindingId, string Status, string Reason, IReadOnlyList<ReviewCodeReference> CodeReferences, IReadOnlyList<CriterionEvidence> Evidence);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NewReviewFinding(Guid Id, string Title, string Description, string Category, string Severity, string Owner,
    IReadOnlyList<Guid> CriterionIds, IReadOnlyList<ReviewCodeReference> CodeReferences, IReadOnlyList<CriterionEvidence> Evidence);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitFactoryReview(Guid RequestId, long ExpectedRevision, string ExpectedContext, string Verdict, string Summary,
    IReadOnlyList<RecheckReviewFinding> Rechecks, IReadOnlyList<NewReviewFinding> NewFindings);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AbandonFactoryReview(Guid RequestId, long ExpectedRevision, string Reason);
public sealed record ReviewFindingHistory(Guid RoundId, Guid RunId, int RoundNumber, string Reviewer, string Status, string Reason,
    VerificationContext Context, IReadOnlyList<CapturedReviewCode> CodeReferences, IReadOnlyList<CapturedCriterionEvidence> Evidence);
public sealed record ReviewFindingView(FactoryReviewFinding Finding, string Status, IReadOnlyList<ReviewFindingHistory> History);
public sealed record IndependentAssessmentSource(Guid StepRunId, string Name);
public sealed record FactoryReviewRoundSummary(Guid Id, Guid RunId, int Number, long Revision, string Reviewer, string State, bool Open,
    string? Verdict, DateTime CreatedAt, string? StaleReason);
public sealed record FactoryReviewLibrary(Guid RunId, long RunRevision, VerificationContext? Context, string? ContextError, bool SourceChanged,
    IReadOnlyList<FactoryReviewRoundSummary> Rounds, IReadOnlyList<ReviewFindingView> Findings, IReadOnlyList<IndependentAssessmentSource> Assessments);
public sealed record FactoryReviewDetail(FactoryReviewRound Round, IndependentReviewPacket Packet, IReadOnlyList<ReviewBuilderReport>? BuilderReports,
    string? StaleReason, IReadOnlyList<ReviewFindingView> Findings);

public sealed class FactoryReviewService(IWorkspaceStore store, IVerificationEnvironment environment, IReviewChanges changes, FactoryQualityService quality)
{
    public async Task<IndependentReviewPacket> CapturePacket(WorkspaceData data, FactoryRun run, CancellationToken ct)
    {
        var first = data.ReviewRounds.Where(x => x.ItemId == run.ItemId && data.FactoryRuns.Single(r => r.Id == x.RunId).Snapshot.Environment.RepositoryPath == run.Snapshot.Environment.RepositoryPath).OrderBy(x => x.Number).FirstOrDefault();
        var baseCommit = first?.Packet.BaseCommit ?? run.Snapshot.Environment.BaseCommit;
        var before = await environment.Observe(run, ct); var raw = await changes.RawChange(run, baseCommit, ct);
        var after = await environment.Observe(run, ct);
        if (before.Fingerprint != after.Fingerprint) throw new RuleException("Code changed while capturing the independent review packet.", 409);
        var prior = Prior(data, run, before);
        var verification = data.FactoryStepRuns.Where(x => x.RunId == run.Id && x.Evidence.Length > 0 && Definition(data, run, x)?.Kind == "command")
            .Select(x => new ReviewVerificationEvidence(x.Id, Definition(data, run, x)!.Name, x.Evidence,
                data.VerificationReceipts.SingleOrDefault(receipt => receipt.StepRunId == x.Id)?.Matches(before, x) == true, FactoryArtifact.Hash(x.Evidence))).ToList();
        return new(run.Snapshot.Title, run.Snapshot.SpecJson, baseCommit, before, prior, run.Snapshot.Criteria ?? [], raw, verification);
    }
    public async Task<FactoryReviewLibrary> List(Guid runId, CancellationToken ct)
    {
        var q = await quality.Read(runId, ct); var data = await store.ReadAsync(ct); var run = FactoryDispatchService.Find(data, runId);
        q = FactoryQualityService.Detail(data, run, q.Context, q.ContextError);
        return new(runId, run.Revision, q.Context, q.ContextError, FactoryQualityService.Source(data, run) != FactoryQualityService.CapturedSource(run),
            data.ReviewRounds.Where(x => x.ItemId == run.ItemId).OrderByDescending(x => x.Number).Select(x =>
                new FactoryReviewRoundSummary(x.Id, x.RunId, x.Number, x.Revision, x.Reviewer, x.State, x.Open, x.Submission?.Verdict, x.CreatedAt,
                    x.RunId == runId ? Stale(x, data, run, q) : "Historical review of another work order; open it to check current inputs.")).ToList(), Findings(data, run, q.Context),
            data.FactoryStepRuns.Where(x => x.RunId == runId && IndependentAssessment(data, run, x, q.Context) is not null)
                .Select(x => new IndependentAssessmentSource(x.Id, Definition(data, run, x)!.Name)).ToList());
    }
    public async Task<FactoryReviewDetail> Read(Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var round = Find(data, id); var run = FactoryDispatchService.Find(data, round.RunId);
        var q = await quality.Read(run.Id, ct);
        var stale = Stale(round, data, run, q);
        return new(round, round.Packet, round.AssessedAt.HasValue ? round.BuilderReports : null, stale, Findings(data, run, q.Context));
    }
    public async Task<FactoryReviewDetail> Begin(Guid runId, BeginFactoryReview input, CancellationToken ct)
    {
        var hash = Hash(input); var data = await store.ReadAsync(ct);
        var replay = Replay(data, input.RequestId, "begin", hash);
        if (replay is not null) { if (replay.RunId != runId) throw new RuleException("That request ID belongs to another run.", 409); return await Read(replay.Id, ct); }
        var run = FactoryDispatchService.Find(data, runId); Idle(data, run);
        var repair = data.RepairLoops.SingleOrDefault(x => x.ItemId == run.ItemId && x.Open);
        if (repair is not null && data.RepairAttempts.Where(x => x.LoopId == repair.Id).OrderByDescending(x => x.Number).FirstOrDefault()?.RunId != runId)
            throw new RuleException("Review the current repair attempt, or stop its loop before reviewing other work orders.", 409);
        Rules.Revision(input.ExpectedRevision, run.Revision);
        var q = await quality.Read(runId, ct); RequireContext(q, input.ExpectedContext);
        var packet = await CapturePacket(data, run, ct);
        if (packet.Context.Fingerprint != q.Context!.Fingerprint) throw new RuleException("Review inputs changed. Refresh before starting.", 409);
        Guid result = Guid.Empty;
        await store.WriteAsync(saved =>
        {
            var duplicate = Replay(saved, input.RequestId, "begin", hash);
            if (duplicate is not null) { if (duplicate.RunId != runId) throw new RuleException("That request ID belongs to another run.", 409); result = duplicate.Id; return; }
            var current = FactoryDispatchService.Find(saved, runId); Idle(saved, current);
            var currentRepair = saved.RepairLoops.SingleOrDefault(x => x.ItemId == current.ItemId && x.Open);
            if (currentRepair is not null && saved.RepairAttempts.Where(x => x.LoopId == currentRepair.Id).OrderByDescending(x => x.Number).FirstOrDefault()?.RunId != runId) throw new RuleException("Review the current repair attempt.", 409);
            Rules.Revision(input.ExpectedRevision, current.Revision);
            if (saved.ReviewRounds.Any(x => x.ItemId == run.ItemId && x.Open)) throw new RuleException("Finish or abandon the task's open review before starting another round.", 409);
            SameSource(data, saved, run);
            if (Hash(Findings(data, run, q.Context)) != Hash(Findings(saved, current, q.Context))) throw new RuleException("Prior findings changed. Refresh the review packet.", 409);
            var round = FactoryReviewRound.Begin(current, saved.ReviewRounds.Count(x => x.ItemId == run.ItemId) + 1, input.RequestId, input.Reviewer, hash,
                FactoryQualityService.Source(saved, current), packet, BuilderReports(saved, current));
            saved.ReviewRounds.Add(round); current.Touch(); result = round.Id;
        }, ct);
        return await Read(result, ct);
    }
    public async Task<FactoryReviewDetail> Assess(Guid id, AssessFactoryReview input, CancellationToken ct)
    {
        var hash = Hash(input); var data = await store.ReadAsync(ct); var round = Find(data, id);
        if (ReplayFor(data, id, input.RequestId, "assessment", hash)) return await Read(id, ct);
        var run = FactoryDispatchService.Find(data, round.RunId); Idle(data, run);
        var q = await quality.Read(run.Id, ct); RequireRound(round, data, run, q);
        string assessment; string? evidenceHash = null;
        if (input.AssessmentStepRunId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(input.Assessment)) throw new RuleException("Import an independent reviewer report or write an operator assessment, not both.");
            var step = data.FactoryStepRuns.SingleOrDefault(x => x.Id == input.AssessmentStepRunId.Value && x.RunId == run.Id) ?? throw new RuleException("Independent reviewer step not found.", 404);
            assessment = IndependentAssessment(data, run, step, q.Context) ?? throw new RuleException("This step has no intact current independent read-only assessment.", 409);
            evidenceHash = FactoryArtifact.Hash(step.Evidence);
        }
        else assessment = input.Assessment ?? "";
        await CheckAgain(run, q.Context!, ct);
        await store.WriteAsync(saved =>
        {
            if (ReplayFor(saved, id, input.RequestId, "assessment", hash)) return;
            var current = Find(saved, id); Idle(saved, run); SameSource(data, saved, run);
            if (input.AssessmentStepRunId.HasValue && FactoryArtifact.Hash(saved.FactoryStepRuns.Single(x => x.Id == input.AssessmentStepRunId).Evidence) != evidenceHash)
                throw new RuleException("Reviewer evidence changed.", 409);
            current.Assess(input.ExpectedRevision, input.RequestId, hash, assessment, input.AssessmentStepRunId, evidenceHash);
            FactoryDispatchService.Find(saved, run.Id).Touch();
        }, ct);
        return await Read(id, ct);
    }
    public async Task<FactoryReviewDetail> Submit(Guid id, SubmitFactoryReview input, CancellationToken ct)
    {
        var hash = Hash(input); var data = await store.ReadAsync(ct); var round = Find(data, id);
        if (ReplayFor(data, id, input.RequestId, "submit", hash)) return await Read(id, ct);
        var run = FactoryDispatchService.Find(data, round.RunId); Idle(data, run);
        var q = await quality.Read(run.Id, ct); RequireRound(round, data, run, q); RequireContext(q, input.ExpectedContext);
        if (input.Rechecks is null || input.NewFindings is null || input.NewFindings.Count > 100 || input.Rechecks.Any(x => x is null) || input.NewFindings.Any(x => x is null))
            throw new RuleException("Supply prior finding rechecks and at most 100 new findings.");
        var rechecks = new List<CapturedFindingRecheck>();
        foreach (var recheck in input.Rechecks)
            rechecks.Add(new(recheck.FindingId, recheck.Status, FactoryReviewRound.Required(recheck.Reason, "Recheck reason"),
                await Code(run, recheck.CodeReferences, ct), await quality.CaptureEvidence(data, run, recheck.Evidence, q.Context, recheck.Status == "fixed", ct)));
        var newFindings = new List<(Guid Id, ReviewFindingData Data)>();
        foreach (var finding in input.NewFindings)
        {
            if (finding.CriterionIds is null || finding.CriterionIds.Count > 100 || finding.CriterionIds.Distinct().Count() != finding.CriterionIds.Count ||
                finding.CriterionIds.Any(criterionId => !round.Packet.Criteria.Any(c => c.Id == criterionId))) throw new RuleException("Use distinct criterion IDs captured in this work order.");
            newFindings.Add((finding.Id, new(finding.Title, finding.Description, finding.Category, finding.Severity, finding.Owner, finding.CriterionIds,
                await Code(run, finding.CodeReferences, ct), await quality.CaptureEvidence(data, run, finding.Evidence, q.Context, false, ct))));
        }
        if (newFindings.Select(x => x.Id).Distinct().Count() != newFindings.Count) throw new RuleException("New finding IDs must be distinct.");
        await CheckAgain(run, q.Context!, ct);
        var qaHash = QaHash(q);
        await store.WriteAsync(saved =>
        {
            if (ReplayFor(saved, id, input.RequestId, "submit", hash)) return;
            var current = Find(saved, id); var currentRun = FactoryDispatchService.Find(saved, run.Id); Idle(saved, currentRun); SameSource(data, saved, run);
            var currentQa = FactoryQualityService.Detail(saved, currentRun, q.Context, null);
            if (QaHash(currentQa) != qaHash) throw new RuleException("QA changed during review. Refresh before submitting.", 409);
            if (newFindings.Any(x => saved.ReviewFindings.Any(f => f.Id == x.Id))) throw new RuleException("That finding ID already belongs to recorded history. Recheck the existing finding.", 409);
            var next = saved.ReviewFindings.Count(x => x.ItemId == run.ItemId) + 1;
            var roots = newFindings.Select(x => FactoryReviewFinding.Create(current, x.Id, next++, x.Data)).ToList();
            current.Submit(input.ExpectedRevision, input.RequestId, hash, new(input.Verdict, input.Summary, qaHash, QaReceipts(q), rechecks, roots.Select(x => x.Id).ToList()), roots, QaComplete(currentQa));
            saved.ReviewFindings.AddRange(roots); currentRun.Touch();
        }, ct);
        return await Read(id, ct);
    }
    public async Task<FactoryReviewDetail> Abandon(Guid id, AbandonFactoryReview input, CancellationToken ct)
    {
        var hash = Hash(input);
        await store.WriteAsync(data =>
        {
            if (ReplayFor(data, id, input.RequestId, "abandon", hash)) return;
            var round = Find(data, id); round.Abandon(input.ExpectedRevision, input.RequestId, hash, input.Reason); FactoryDispatchService.Find(data, round.RunId).Touch();
        }, ct);
        return await Read(id, ct);
    }
    private async Task<IReadOnlyList<CapturedReviewCode>> Code(FactoryRun run, IReadOnlyList<ReviewCodeReference>? references, CancellationToken ct)
    {
        if (references is null || references.Count > 100 || references.Any(x => x is null)) throw new RuleException("Supply up to 100 code references.");
        var result = new List<CapturedReviewCode>();
        foreach (var reference in references)
        {
            try { result.Add(new(reference.Path, reference.Line, await environment.TestFile(run, reference.Path, reference.Line, ct))); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { throw new RuleException("The code reference cannot be read inside this run's worktree."); }
        }
        return result;
    }
    private async Task CheckAgain(FactoryRun run, VerificationContext context, CancellationToken ct)
    { if ((await environment.Observe(run, ct)).Fingerprint != context.Fingerprint) throw new RuleException("Code changed during review. Refresh before continuing.", 409); }
    private static void RequireContext(FactoryQualityDetail q, string expected)
    { if (q.Context is null || q.SourceChanged || q.Context.Fingerprint != expected) throw new RuleException(q.ContextError ?? "Review inputs changed. Refresh and prepare a current work order.", 409); }
    private static void RequireRound(FactoryReviewRound round, WorkspaceData data, FactoryRun run, FactoryQualityDetail q)
    { var stale = Stale(round, data, run, q); if (stale is not null) throw new RuleException(stale, 409); }
    public static string? Stale(FactoryReviewRound round, WorkspaceData data, FactoryRun run, FactoryQualityDetail q)
    {
        if (q.Context is null) return q.ContextError ?? "Verification context is unavailable.";
        if (q.SourceChanged || round.SourceHash != FactoryQualityService.Source(data, run)) return "Task inputs changed. Abandon unfinished review and prepare a new work order.";
        if (q.Context.Fingerprint != round.Packet.Context.Fingerprint) return "The reviewed code or configuration changed. A new review round is required.";
        if (round.AssessmentStepRunId.HasValue)
        {
            var step = data.FactoryStepRuns.SingleOrDefault(x => x.Id == round.AssessmentStepRunId);
            if (step is null || FactoryArtifact.Hash(step.Evidence) != round.AssessmentEvidenceHash) return "The independent assessment evidence is no longer intact.";
        }
        if (round.Submission?.Verdict == "approve" && data.ReviewRounds.Any(x => x.ItemId == round.ItemId && x.Number > round.Number && x.State == "submitted"))
            return "A later review supersedes this approval. Inspect the latest submitted round.";
        if (round.Submission?.Verdict == "approve" && (round.Submission.QaHash != QaHash(q) || !QaComplete(q))) return "Acceptance QA changed or is no longer complete. Approval requires a new review round.";
        return null;
    }
    private static IReadOnlyList<PriorReviewFinding> Prior(WorkspaceData data, FactoryRun run, VerificationContext? context) => Findings(data, run, context)
        .Select(x => new PriorReviewFinding(x.Finding.Id, x.Finding.Number, x.Finding.Data, x.History[0].Status, x.History[0].Reason, x.History[0].Context)).ToList();
    private static bool QaComplete(FactoryQualityDetail q) => q.Criteria.Count > 0 && q.Criteria.All(x => x.Status is "pass" or "notApplicable");
    private static IReadOnlyList<ReviewQaReceipt> QaReceipts(FactoryQualityDetail q) => q.Criteria.Select(x => new ReviewQaReceipt(x.Criterion.Id, x.Status, x.History.FirstOrDefault()?.Id)).ToList();
    private static string QaHash(FactoryQualityDetail q) => Hash(QaReceipts(q));
    private static void SameSource(WorkspaceData before, WorkspaceData after, FactoryRun run)
    { if (FactoryQualityService.Source(before, run) != FactoryQualityService.Source(after, run)) throw new RuleException("Task inputs changed during review.", 409); }
    private static void Idle(WorkspaceData data, FactoryRun run)
    {
        if (run.State == "prepared" || data.FactoryRuns.Any(x => x.ItemId == run.ItemId && x.Active) || data.AgentRuns.Any(x => x.ItemId == run.ItemId && x.Active))
            throw new RuleException("Finish or stop task execution before independent review.", 409);
    }
    private static string Hash<T>(T value) => FactoryArtifact.Hash(JsonSerializer.Serialize(value, FactoryRules.Json));
    private static FactoryReviewRound Find(WorkspaceData data, Guid id) => data.ReviewRounds.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Review round not found.", 404);
    private static FactoryReviewRound? Replay(WorkspaceData data, Guid requestId, string kind, string hash)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        var round = data.ReviewRounds.SingleOrDefault(x => x.Operations.Any(op => op.RequestId == requestId));
        if (round is not null && !round.Operations.Any(op => op.RequestId == requestId && op.Kind == kind && op.Hash == hash)) throw new RuleException("That request ID records a different review action.", 409);
        return round;
    }
    private static bool ReplayFor(WorkspaceData data, Guid id, Guid requestId, string kind, string hash)
    { var round = Replay(data, requestId, kind, hash); if (round is not null && round.Id != id) throw new RuleException("That request ID belongs to another review.", 409); return round is not null; }
    private static FactoryStep? Definition(WorkspaceData data, FactoryRun run, FactoryStepRun step)
    { var stage = data.FactoryStageRuns.Single(x => x.Id == step.StageRunId); return run.Snapshot.Environment.Definition.Stages.Single(x => x.Id == stage.StageId).Steps.Single(x => x.Id == step.StepId); }
    private static string? IndependentAssessment(WorkspaceData data, FactoryRun run, FactoryStepRun step, VerificationContext? context)
    {
        if (step.State != "succeeded" || Definition(data, run, step) is not { Role: "review", Permission: "read-only" } || context is null) return null;
        try
        {
            using var evidence = JsonDocument.Parse(step.Evidence); var root = evidence.RootElement;
            if (!root.TryGetProperty("reviewInput", out var packet) || packet.ValueKind != JsonValueKind.Object || !root.TryGetProperty("reviewContextStable", out var stable) || stable.ValueKind != JsonValueKind.True) return null;
            var captured = packet.Deserialize<IndependentReviewPacket>(FactoryRules.Json);
            return captured?.Context.Fingerprint == context.Fingerprint && Hash(captured.PriorFindings) == Hash(Prior(data, run, context)) && root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String ? result.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
    private static IReadOnlyList<ReviewBuilderReport> BuilderReports(WorkspaceData data, FactoryRun run) => data.FactoryStepRuns.Where(x => x.RunId == run.Id && Definition(data, run, x)?.Role == "implementation")
        .Select(x => { using var value = JsonDocument.Parse(x.Evidence.Length == 0 ? "{}" : x.Evidence); var report = value.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String ? result.GetString()! : ""; return new ReviewBuilderReport(x.Id, Definition(data, run, x)!.Name, report, FactoryArtifact.Hash(x.Evidence)); }).Where(x => x.Report.Length > 0).ToList();
    public static IReadOnlyList<ReviewFindingView> Findings(WorkspaceData data, FactoryRun run, VerificationContext? context) => data.ReviewFindings.Where(x => x.ItemId == run.ItemId).OrderBy(x => x.Number).Select(finding =>
    {
        var history = data.ReviewRounds.Where(x => x.ItemId == run.ItemId && x.State == "submitted").OrderByDescending(x => x.Number).SelectMany(round =>
        {
            var check = round.Submission!.Rechecks.SingleOrDefault(x => x.FindingId == finding.Id);
            return check is not null ? new[] { new ReviewFindingHistory(round.Id, round.RunId, round.Number, round.Reviewer, check.Status, check.Reason, round.Packet.Context, check.CodeReferences, check.Evidence) }
                : round.Id == finding.RoundId ? [new ReviewFindingHistory(round.Id, round.RunId, round.Number, round.Reviewer, "unfixed", finding.Data.Description, round.Packet.Context, finding.Data.CodeReferences, finding.Data.Evidence)] : [];
        }).ToList();
        var latest = history[0]; var source = data.ReviewRounds.Single(x => x.Id == latest.RoundId).SourceHash;
        var status = context?.Fingerprint == latest.Context.Fingerprint && source == FactoryQualityService.Source(data, run) ? latest.Status : "needsRecheck";
        return new ReviewFindingView(finding, status, history);
    }).ToList();
}
