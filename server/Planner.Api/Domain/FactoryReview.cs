using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewCodeReference(string Path, int Line);
public sealed record CapturedReviewCode(string Path, int Line, string FileHash);
public sealed record ReviewFindingData(string Title, string Description, string Category, string Severity, string Owner,
    IReadOnlyList<Guid> CriterionIds, IReadOnlyList<CapturedReviewCode> CodeReferences, IReadOnlyList<CapturedCriterionEvidence> Evidence);
public sealed record PriorReviewFinding(Guid Id, int Number, ReviewFindingData Data, string LastStatus, string LastReason, VerificationContext LastContext);
public sealed record ReviewVerificationEvidence(Guid StepRunId, string Name, string Evidence, bool Current, string EvidenceHash);
public sealed record IndependentReviewPacket(string Title, string SpecJson, string BaseCommit, VerificationContext Context, IReadOnlyList<PriorReviewFinding> PriorFindings,
    IReadOnlyList<AcceptanceCriterion> Criteria, string RawChange, IReadOnlyList<ReviewVerificationEvidence> VerificationEvidence);
public sealed record ReviewBuilderReport(Guid StepRunId, string StepName, string Report, string EvidenceHash);
public sealed record ReviewOperation(Guid RequestId, string Kind, string Hash);
public sealed record CapturedFindingRecheck(Guid FindingId, string Status, string Reason, IReadOnlyList<CapturedReviewCode> CodeReferences,
    IReadOnlyList<CapturedCriterionEvidence> Evidence);
public sealed record ReviewQaReceipt(Guid CriterionId, string Status, Guid? VerdictId);
public sealed record ReviewSubmission(string Verdict, string Summary, string QaHash, IReadOnlyList<ReviewQaReceipt> QaReceipts, IReadOnlyList<CapturedFindingRecheck> Rechecks, IReadOnlyList<Guid> NewFindingIds);

public sealed class FactoryReviewRound
{
    private FactoryReviewRound() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid RunId { get; private set; }
    public int Number { get; private set; }
    public long Revision { get; private set; } = 1;
    public string Reviewer { get; private set; } = "";
    public string State { get; private set; } = "assessing";
    public string SourceHash { get; private set; } = "";
    public string Assessment { get; private set; } = "";
    public Guid? AssessmentStepRunId { get; private set; }
    public string? AssessmentEvidenceHash { get; private set; }
    public string? AbandonReason { get; private set; }
    [JsonIgnore] public string PacketJson { get; private set; } = "";
    [JsonIgnore] public string BuilderReportsJson { get; private set; } = "[]";
    [JsonIgnore] public string OperationsJson { get; private set; } = "[]";
    [JsonIgnore] public string SubmissionJson { get; private set; } = "";
    [JsonIgnore] public IndependentReviewPacket Packet => JsonSerializer.Deserialize<IndependentReviewPacket>(PacketJson, FactoryRules.Json)!;
    [JsonIgnore] public IReadOnlyList<ReviewBuilderReport> BuilderReports => JsonSerializer.Deserialize<ReviewBuilderReport[]>(BuilderReportsJson, FactoryRules.Json)!;
    [JsonIgnore] public IReadOnlyList<ReviewOperation> Operations => JsonSerializer.Deserialize<ReviewOperation[]>(OperationsJson, FactoryRules.Json)!;
    public ReviewSubmission? Submission => SubmissionJson.Length == 0 ? null : JsonSerializer.Deserialize<ReviewSubmission>(SubmissionJson, FactoryRules.Json);
    public DateTime CreatedAt { get; private set; }
    public DateTime? AssessedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public bool Open => State is "assessing" or "assessed";
    public static FactoryReviewRound Begin(FactoryRun run, int number, Guid requestId, string reviewer, string hash, string source,
        IndependentReviewPacket packet, IReadOnlyList<ReviewBuilderReport> reports)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        var round = new FactoryReviewRound { Id = Guid.NewGuid(), RequestId = requestId, ProjectId = run.ProjectId, ItemId = run.ItemId,
            RunId = run.Id, Number = number, Reviewer = Rules.Name(reviewer, "Reviewer"), SourceHash = source,
            PacketJson = JsonSerializer.Serialize(packet, FactoryRules.Json), BuilderReportsJson = JsonSerializer.Serialize(reports, FactoryRules.Json), CreatedAt = DateTime.UtcNow };
        round.Remember(requestId, "begin", hash); return round;
    }
    public void Assess(long revision, Guid requestId, string hash, string assessment, Guid? stepId, string? evidenceHash)
    {
        Require(revision, "assessing");
        Assessment = Required(assessment, "Independent assessment"); AssessmentStepRunId = stepId; AssessmentEvidenceHash = evidenceHash;
        State = "assessed"; AssessedAt = DateTime.UtcNow; Revision++; Remember(requestId, "assessment", hash);
    }
    public void Submit(long revision, Guid requestId, string hash, ReviewSubmission submission, IReadOnlyList<FactoryReviewFinding> newFindings, bool qaComplete)
    {
        Require(revision, "assessed");
        if (submission.Verdict is not ("approve" or "requestChanges" or "inconclusive")) throw new RuleException("Choose approve, requestChanges or inconclusive.");
        var prior = Packet.PriorFindings;
        if (submission.Rechecks.Count != prior.Count || !submission.Rechecks.Select(x => x.FindingId).ToHashSet().SetEquals(prior.Select(x => x.Id)))
            throw new RuleException("Recheck every prior finding exactly once. Earlier findings cannot be silently dropped.");
        foreach (var recheck in submission.Rechecks)
        {
            if (recheck.Status is not ("unfixed" or "fixed" or "noLongerApplies" or "unverified")) throw new RuleException("Choose a valid finding recheck status.");
            Required(recheck.Reason, "Recheck reason");
            var previous = prior.Single(x => x.Id == recheck.FindingId);
            if (recheck.Status == "fixed" && previous.LastStatus != "fixed" && previous.LastContext.CodeHash == Packet.Context.CodeHash)
                throw new RuleException("The finding's reviewed source has not changed. Record it as unfixed, or explain why it no longer applies.", 409);
            if (recheck.Status == "fixed" && (recheck.CodeReferences.Count == 0 || recheck.Evidence.Count == 0))
                throw new RuleException("A fixed finding requires code and successful verification evidence.");
        }
        var blocking = newFindings.Any(x => x.Data.Category != "improvement") || submission.Rechecks.Any(x =>
            prior.Single(p => p.Id == x.FindingId).Data.Category != "improvement" && x.Status is "unfixed" or "unverified");
        if (submission.Verdict == "approve" && (!qaComplete || blocking)) throw new RuleException("Approval requires current complete acceptance QA and no unresolved blocking findings.", 409);
        if (submission.Verdict == "requestChanges" && !blocking) throw new RuleException("Record an actionable blocking finding or use an inconclusive verdict.");
        SubmissionJson = JsonSerializer.Serialize(submission with { Summary = Required(submission.Summary, "Review summary") }, FactoryRules.Json);
        State = "submitted"; FinishedAt = DateTime.UtcNow; Revision++; Remember(requestId, "submit", hash);
    }
    public void Abandon(long revision, Guid requestId, string hash, string reason)
    {
        Rules.Revision(revision, Revision); if (!Open) throw new RuleException("Only an unfinished review can be abandoned.", 409);
        AbandonReason = Required(reason, "Abandon reason"); State = "abandoned"; FinishedAt = DateTime.UtcNow; Revision++; Remember(requestId, "abandon", hash);
    }
    private void Require(long revision, string state)
    { Rules.Revision(revision, Revision); if (State != state) throw new RuleException("The review phase changed. Refresh before continuing.", 409); }
    private void Remember(Guid requestId, string kind, string hash)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        OperationsJson = JsonSerializer.Serialize(Operations.Append(new ReviewOperation(requestId, kind, hash)), FactoryRules.Json);
    }
    public static string Required(string? text, string label)
    { var value = AgentRun.Text(text, label); return value.Length > 0 ? value : throw new RuleException($"{label} is required."); }
}

public sealed class FactoryReviewFinding
{
    private FactoryReviewFinding() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid RoundId { get; private set; }
    public int Number { get; private set; }
    [JsonIgnore] public string DataJson { get; private set; } = "";
    public ReviewFindingData Data => JsonSerializer.Deserialize<ReviewFindingData>(DataJson, FactoryRules.Json)!;
    public static FactoryReviewFinding Create(FactoryReviewRound round, Guid id, int number, ReviewFindingData input)
    {
        if (id == Guid.Empty) throw new RuleException("Each new finding needs a stable nonempty ID.");
        if (input.Category is not ("defect" or "missingBehavior" or "verificationGap" or "scopeDrift" or "improvement")) throw new RuleException("Choose a valid finding category.");
        if (input.Severity is not ("critical" or "high" or "medium" or "low")) throw new RuleException("Choose a valid finding severity.");
        if (input.Owner is not ("implementation" or "verification" or "planning")) throw new RuleException("Choose a responsible role.");
        if (input.CriterionIds.Count + input.CodeReferences.Count + input.Evidence.Count == 0) throw new RuleException("A finding requires a code, criterion or verification reference.");
        var normalized = input with { Title = Rules.Name(input.Title, "Finding title"), Description = FactoryReviewRound.Required(input.Description, "Finding description") };
        return new() { Id = id, ProjectId = round.ProjectId, ItemId = round.ItemId, RoundId = round.Id, Number = number, DataJson = JsonSerializer.Serialize(normalized, FactoryRules.Json) };
    }
}
