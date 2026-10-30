using System.Text.Json;
using System.Text.Json.Serialization;
using Planner.Api.Domain;

namespace Planner.Api.Application;

public interface IVerificationEnvironment
{
    Task<VerificationContext> Observe(FactoryRun run, CancellationToken ct);
    Task<string> TestFile(FactoryRun run, string path, int line, CancellationToken ct);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditAcceptanceCriteria(long ExpectedRevision, IReadOnlyList<AcceptanceCriterion> Criteria);
public sealed record AcceptanceCriteriaDetail(long ProjectRevision, IReadOnlyList<AcceptanceCriterion> Criteria, IReadOnlyList<FactoryReadinessCheck> Checks);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecordCriterionVerdict(Guid RequestId, long ExpectedRevision, string? ExpectedContext, Guid CriterionId, string Verdict, string Reason, IReadOnlyList<CriterionEvidence> Evidence);
public sealed record CriterionQuality(AcceptanceCriterion Criterion, string Status, string? StaleReason, IReadOnlyList<FactoryCriterionVerdict> History);
public sealed record FactoryQualityDetail(Guid RunId, long RunRevision, VerificationContext? Context, string? ContextError, bool SourceChanged,
    IReadOnlyList<CriterionQuality> Criteria, IReadOnlyList<FactoryVerificationReceipt> Receipts);

public sealed class FactoryQualityService(IWorkspaceStore store, IVerificationEnvironment environment)
{
    public async Task<AcceptanceCriteriaDetail> Criteria(Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var item = Item(data, id);
        var criteria = data.AcceptanceCriteria.SingleOrDefault(x => x.Id == id)?.Criteria ?? [];
        return new(data.Graph(item.ProjectId).Project.Revision, criteria, TaskAcceptanceCriteria.Check(criteria));
    }
    public async Task<AcceptanceCriteriaDetail> Edit(Guid id, EditAcceptanceCriteria input, CancellationToken ct)
    {
        await store.WriteAsync(data =>
        {
            var item = Item(data, id); var graph = data.ForItem(id, input.ExpectedRevision);
            if (data.FactoryRuns.Any(x => x.ItemId == id && x.Active)) throw new RuleException("Stop factory execution before changing acceptance criteria.", 409);
            var criteria = data.AcceptanceCriteria.SingleOrDefault(x => x.Id == id);
            if (criteria is null) { criteria = TaskAcceptanceCriteria.Create(item); data.AcceptanceCriteria.Add(criteria); }
            criteria.Replace(input.Criteria); graph.Project.Touch();
        }, ct);
        return await Criteria(id, ct);
    }
    public async Task<FactoryQualityDetail> Read(Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var run = FactoryDispatchService.Find(data, id);
        VerificationContext? context = null; string? error = null;
        if (run.Active) error = "Execution is active. Refresh QA after it finishes or stops.";
        else if (run.WorkspacePath is not null)
        {
            try { context = await environment.Observe(run, ct); }
            catch (Exception exception) when (exception is RuleException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            { error = exception.Message; }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { error = "Verification inspection timed out. Refresh to retry."; }
        }
        else error = "Start the captured run to obtain verification evidence.";
        return Detail(data, run, context, error);
    }
    public static FactoryQualityDetail Detail(WorkspaceData data, FactoryRun run, VerificationContext? context, string? error)
    {
        var id = run.Id;
        var changed = Source(data, run) != CapturedSource(run);
        var criteria = (run.Snapshot.Criteria ?? []).Select(criterion =>
        {
            var history = data.CriterionVerdicts.Where(x => x.RunId == id && x.CriterionId == criterion.Id).OrderByDescending(x => x.RecordedRevision).ToList();
            var latest = history.FirstOrDefault();
            var reason = latest is null || latest.Verdict == "unverified" ? null : changed ? "Task specification, criteria or pipeline attachment changed. Prepare a new work order."
                : context is null ? error : latest.Context?.Fingerprint != context.Fingerprint ? "Code or verification configuration changed. Fresh verification is required."
                : latest.Evidence.Any(reference => !EvidenceIntact(data, reference, context)) ? "Recorded command evidence is unavailable or no longer intact." : null;
            return new CriterionQuality(criterion, reason is not null ? "stale" : latest?.Verdict ?? "unverified", reason, history);
        }).ToList();
        return new(run.Id, run.Revision, context, error, changed, criteria, data.VerificationReceipts.Where(x => x.RunId == id).ToList());
    }
    public async Task<FactoryQualityDetail> Record(Guid id, RecordCriterionVerdict input, CancellationToken ct)
    {
        var hash = FactoryArtifact.Hash(JsonSerializer.Serialize(input, FactoryRules.Json));
        var data = await store.ReadAsync(ct); var run = FactoryDispatchService.Find(data, id);
        var duplicate = data.CriterionVerdicts.SingleOrDefault(x => x.RequestId == input.RequestId);
        if (duplicate is not null) { Replay(duplicate, id, hash); return await Read(id, ct); }
        Rules.Revision(input.ExpectedRevision, run.Revision);
        if (run.Active || run.State == "prepared") throw new RuleException("Finish or stop execution before recording QA verdicts.", 409);
        var criterion = (run.Snapshot.Criteria ?? []).SingleOrDefault(x => x.Id == input.CriterionId) ?? throw new RuleException("Captured acceptance criterion not found.", 404);
        if (input.Evidence is null || input.Evidence.Count > 100 || input.Evidence.Any(x => x is null)) throw new RuleException("Supply up to 100 evidence references.");
        var quality = await Read(id, ct);
        if (input.ExpectedContext != quality.Context?.Fingerprint) throw new RuleException("Verification inputs changed. Refresh QA before recording a verdict.", 409);
        if (quality.SourceChanged && input.Verdict != "unverified") throw new RuleException("The task inputs changed. Prepare a new captured work order before QA.", 409);
        var evidence = await CaptureEvidence(data, run, input.Evidence, quality.Context, input.Verdict == "pass", ct);
        if (quality.Context is not null && (await environment.Observe(run, ct)).Fingerprint != quality.Context.Fingerprint)
            throw new RuleException("Verification inputs changed during QA. Refresh before recording.", 409);
        await store.WriteAsync(saved =>
        {
            var replay = saved.CriterionVerdicts.SingleOrDefault(x => x.RequestId == input.RequestId);
            if (replay is not null) { Replay(replay, id, hash); return; }
            var current = FactoryDispatchService.Find(saved, id); Rules.Revision(input.ExpectedRevision, current.Revision);
            if (Source(saved, current) != Source(data, run)) throw new RuleException("Task inputs changed during QA. Refresh before recording.", 409);
            saved.CriterionVerdicts.Add(FactoryCriterionVerdict.Record(current, criterion, input.RequestId, input.Verdict, input.Reason, hash,
                Source(saved, current), quality.Context, evidence)); current.Touch();
        }, ct);
        return await Read(id, ct);
    }
    public async Task<IReadOnlyList<CapturedCriterionEvidence>> CaptureEvidence(WorkspaceData data, FactoryRun run, IReadOnlyList<CriterionEvidence>? references,
        VerificationContext? context, bool requireSuccess, CancellationToken ct)
    {
        if (references is null || references.Count > 100 || references.Any(x => x is null)) throw new RuleException("Supply up to 100 evidence references.");
        var evidence = new List<CapturedCriterionEvidence>();
        foreach (var reference in references)
        {
            var step = data.FactoryStepRuns.SingleOrDefault(x => x.Id == reference.StepRunId && x.RunId == run.Id) ?? throw new RuleException("Evidence must belong to this run.");
            var receipt = data.VerificationReceipts.SingleOrDefault(x => x.StepRunId == step.Id);
            if (receipt?.Matches(context, step) != true || step.State is not ("succeeded" or "failed"))
                throw new RuleException("The command evidence is missing, unstable or stale. Run fresh verification before recording this verdict.", 409);
            if (requireSuccess && (step.State != "succeeded" || step.ExitCode != 0)) throw new RuleException("Passing criteria require successful command evidence.");
            var assertion = AgentRun.Text(reference.Assertion, "Assertion explanation"); var failure = AgentRun.Text(reference.FailureCase, "Failure case");
            if (assertion.Length == 0 || failure.Length == 0) throw new RuleException("Explain the assertion and the defect or negative case it detects.");
            string fileHash;
            try { fileHash = await environment.TestFile(run, reference.TestPath, reference.Line, ct); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { throw new RuleException("The asserting test reference cannot be read. Choose an available text file inside this run's worktree."); }
            evidence.Add(new(step.Id, receipt.EvidenceHash, reference.TestPath, reference.Line, fileHash, assertion, failure));
        }
        return evidence;
    }
    private static bool EvidenceIntact(WorkspaceData data, CapturedCriterionEvidence evidence, VerificationContext context)
    {
        var step = data.FactoryStepRuns.SingleOrDefault(x => x.Id == evidence.StepRunId);
        var receipt = data.VerificationReceipts.SingleOrDefault(x => x.StepRunId == evidence.StepRunId);
        return step is not null && receipt?.Matches(context, step) == true && receipt.EvidenceHash == evidence.EvidenceHash;
    }
    private static void Replay(FactoryCriterionVerdict verdict, Guid id, string hash)
    { if (verdict.RunId != id || verdict.InputHash != hash) throw new RuleException("That request ID already records a different verdict.", 409); }
    private static WorkItem Item(WorkspaceData data, Guid id) => data.Items.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Task not found.", 404);
    private static object? Attachment(WorkspaceData data, FactoryRun run)
    {
        var attachment = data.TaskFactoryConfigurations.SingleOrDefault(x => x.Id == run.ItemId);
        return attachment is null ? null : new { attachment.PipelineVersionId, attachment.Overrides };
    }
    private static string WithExecution(string source, string? policyHash) => policyHash is null ? source : FactoryArtifact.Hash(source + policyHash);
    public static string Source(WorkspaceData data, FactoryRun run) => WithExecution(FactoryCriterionVerdict.Source(Item(data, run.ItemId).SpecJson,
        data.AcceptanceCriteria.SingleOrDefault(x => x.Id == run.ItemId)?.Criteria ?? [], Attachment(data, run)), FactoryExecutionPolicyService.CurrentHash(data, run.ProjectId));
    public static string CapturedSource(FactoryRun run) => WithExecution(FactoryCriterionVerdict.Source(run.Snapshot.SpecJson, run.Snapshot.Criteria ?? [],
        new { PipelineVersionId = run.Snapshot.Repair?.SourcePipelineVersionId ?? run.Snapshot.PipelineVersionId, Overrides = run.Snapshot.Repair?.SourceOverrides ?? run.Snapshot.Overrides }), run.Snapshot.Execution?.PolicyHash);
}
