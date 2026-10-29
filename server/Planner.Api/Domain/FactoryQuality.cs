using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcceptanceCriterion(Guid Id, string Description, string ExpectedResult, string VerificationMethod, bool NeedsClarification = false)
{
    [JsonIgnore] public bool Complete => !NeedsClarification && !string.IsNullOrWhiteSpace(ExpectedResult) && !string.IsNullOrWhiteSpace(VerificationMethod);
    public AcceptanceCriterion Normalize()
    {
        if (Id == Guid.Empty) throw new RuleException("Each criterion needs a stable, nonempty ID.");
        return this with { Description = Rules.Name(Description, "Criterion"), ExpectedResult = AgentRun.Text(ExpectedResult, "Expected result"), VerificationMethod = AgentRun.Text(VerificationMethod, "Verification method") };
    }
}
public sealed class TaskAcceptanceCriteria
{
    private TaskAcceptanceCriteria() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    [JsonIgnore] public string CriteriaJson { get; private set; } = "[]";
    public IReadOnlyList<AcceptanceCriterion> Criteria => JsonSerializer.Deserialize<AcceptanceCriterion[]>(CriteriaJson, FactoryRules.Json)!;
    public static TaskAcceptanceCriteria Create(WorkItem item) => new() { Id = item.Id, ProjectId = item.ProjectId };
    public void Replace(IReadOnlyList<AcceptanceCriterion>? criteria)
    {
        if (criteria is null || criteria.Count > 100 || criteria.Any(x => x is null) || criteria.Select(x => x.Id).Distinct().Count() != criteria.Count)
            throw new RuleException("Supply up to 100 criteria with distinct stable IDs.");
        CriteriaJson = JsonSerializer.Serialize(criteria.Select(x => x.Normalize()), FactoryRules.Json);
    }
    public static IReadOnlyList<FactoryReadinessCheck> Check(IReadOnlyList<AcceptanceCriterion> criteria) => criteria.Count == 0
        ? [new("criteria.missing", "warning", "No acceptance criteria recorded. Adopt observable criteria before unattended work; successful execution will not establish acceptance.")]
        : criteria.Where(x => !x.Complete).Select(x => new FactoryReadinessCheck("criteria.incomplete", "warning", $"Criterion '{x.Description}' needs an observable expected result and verification method before QA can pass it.")).ToList();
}

public sealed record VerificationContext(string HeadCommit, string CodeHash, string ConfigurationHash, string Fingerprint);
public sealed class FactoryVerificationReceipt
{
    private FactoryVerificationReceipt() { }
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid StepRunId { get; private set; }
    [JsonIgnore] public string BeforeJson { get; private set; } = "";
    [JsonIgnore] public string AfterJson { get; private set; } = "";
    public VerificationContext? Before => Decode(BeforeJson);
    public VerificationContext? After => Decode(AfterJson);
    public string? Error { get; private set; }
    public string EvidenceHash { get; private set; } = "";
    public bool Stable => Before is not null && After is not null && Before.Fingerprint == After.Fingerprint && Error is null;
    private static VerificationContext? Decode(string json) => json.Length == 0 ? null : JsonSerializer.Deserialize<VerificationContext>(json, FactoryRules.Json);
    public static FactoryVerificationReceipt Capture(FactoryStepRun step, VerificationContext? before, VerificationContext? after, string? error) => new()
    {
        Id = Guid.NewGuid(), RunId = step.RunId, StepRunId = step.Id, EvidenceHash = FactoryArtifact.Hash(step.Evidence),
        BeforeJson = before is null ? "" : JsonSerializer.Serialize(before, FactoryRules.Json),
        AfterJson = after is null ? "" : JsonSerializer.Serialize(after, FactoryRules.Json), Error = error
    };
    public bool Matches(VerificationContext? current, FactoryStepRun step) => Stable && current is not null &&
        After!.Fingerprint == current.Fingerprint && EvidenceHash == FactoryArtifact.Hash(step.Evidence);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CriterionEvidence(Guid StepRunId, string TestPath, int Line, string Assertion, string FailureCase);
public sealed record CapturedCriterionEvidence(Guid StepRunId, string EvidenceHash, string TestPath, int Line, string FileHash, string Assertion, string FailureCase);
public sealed class FactoryCriterionVerdict
{
    private FactoryCriterionVerdict() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid RunId { get; private set; }
    public Guid CriterionId { get; private set; }
    public long RecordedRevision { get; private set; }
    public string Verdict { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public string InputHash { get; private set; } = "";
    public string SourceHash { get; private set; } = "";
    [JsonIgnore] public string ContextJson { get; private set; } = "";
    [JsonIgnore] public string EvidenceJson { get; private set; } = "[]";
    public VerificationContext? Context => ContextJson.Length == 0 ? null : JsonSerializer.Deserialize<VerificationContext>(ContextJson, FactoryRules.Json);
    public IReadOnlyList<CapturedCriterionEvidence> Evidence => JsonSerializer.Deserialize<CapturedCriterionEvidence[]>(EvidenceJson, FactoryRules.Json)!;
    public DateTime CreatedAt { get; private set; }
    public static FactoryCriterionVerdict Record(FactoryRun run, AcceptanceCriterion criterion, Guid requestId, string verdict, string reason,
        string inputHash, string sourceHash, VerificationContext? context, IReadOnlyList<CapturedCriterionEvidence> evidence)
    {
        if (run.Active || run.State == "prepared") throw new RuleException("Finish or stop execution before recording QA verdicts.", 409);
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        if (verdict is not ("pass" or "fail" or "unverified" or "notApplicable")) throw new RuleException("Choose pass, fail, unverified or notApplicable.");
        reason = AgentRun.Text(reason, "Verdict rationale");
        if (reason.Length == 0) throw new RuleException("Explain the verdict, including any not-applicable justification.");
        if (verdict is "pass" or "fail")
        {
            if (!criterion.Complete) throw new RuleException("The captured criterion needs an expected result and verification method. Adopt criteria and prepare a new work order.");
            if (context is null || evidence.Count == 0) throw new RuleException("Pass/fail requires current command evidence and an asserting test reference.");
        }
        if (verdict == "notApplicable" && context is null) throw new RuleException("A not-applicable decision requires a current verification context.");
        return new() { Id = Guid.NewGuid(), RequestId = requestId, RunId = run.Id, CriterionId = criterion.Id, RecordedRevision = run.Revision + 1,
            Verdict = verdict, Reason = reason, InputHash = inputHash, SourceHash = sourceHash,
            ContextJson = context is null ? "" : JsonSerializer.Serialize(context, FactoryRules.Json), EvidenceJson = JsonSerializer.Serialize(evidence, FactoryRules.Json), CreatedAt = DateTime.UtcNow };
    }
    public static string Source(string spec, IReadOnlyList<AcceptanceCriterion> criteria, object? attachment) => FactoryArtifact.Hash(JsonSerializer.Serialize(new { spec, criteria, attachment }, FactoryRules.Json));
}
