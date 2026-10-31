using System.Text.Json;
using System.Text.Json.Serialization;
namespace Planner.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalibrationPolicy(bool Enabled, bool AutoRun, bool AutoApply, FactorySkill? Calibrator,
    IReadOnlyList<FactorySkill> Targets, FactoryExecutionPolicy? Execution)
{
    public CalibrationPolicy Normalize()
    {
        if (Targets is null || Targets.Any(x => x is null) || Targets.Distinct().Count() != Targets.Count) throw new RuleException("Supply distinct existing skill targets, using [] for none.");
        foreach (var target in Targets.Append(Calibrator).Where(x => x is not null)) { Rules.Name(target!.Id, "Skill ID"); Rules.Name(target.Source, "Skill source"); }
        var execution = Execution?.Normalize();
        if (Enabled)
        {
            if (Calibrator is null || Targets.Count == 0 || execution is not { Enabled: true }) throw new RuleException("Enable calibration only with an installed calibrator, explicit targets and execution policy.");
            if (execution.MaxAgentInvocations != 1 || execution.MaxCommandInvocations != 0 || execution.MaxConcurrentStages is > 1 || execution.Routes.Count != 1 || execution.Routes[0].Role != "documentation") throw new RuleException("Calibration permits one documentation-role invocation, no commands and no parallel stages. Configure its explicit model route.");
            if (execution.Unsupported(true) is { } reason) throw new RuleException(reason);
        }
        return this with { Execution = execution };
    }
    [JsonIgnore] public string Hash => FactoryArtifact.Hash(FactoryRules.Serialize(this));
}
public sealed class ProjectCalibrationPolicy
{
    private ProjectCalibrationPolicy() { }
    public Guid Id { get; private set; }
    [JsonIgnore] public string PolicyJson { get; private set; } = "";
    public CalibrationPolicy Policy => JsonSerializer.Deserialize<CalibrationPolicy>(PolicyJson, FactoryRules.Json)!;
    public DateTime ActivatedAt { get; private set; }
    public static ProjectCalibrationPolicy Adopt(Guid id, CalibrationPolicy policy) { var record = new ProjectCalibrationPolicy { Id = id }; record.Replace(policy); return record; }
    public void Replace(CalibrationPolicy policy)
    { PolicyJson = FactoryRules.Serialize(policy.Normalize()); ActivatedAt = DateTime.UtcNow; }
}
public sealed record CalibrationSkill(FactorySkill Reference, string Path, string Hash, string Markdown);
public sealed record CalibrationCapture(string DeliveryBinding, long DeliveryRevision, string RepositoryPath, string RemoteName,
    string TargetBranch, string MergeSha, CalibrationPolicy Policy, CalibrationSkill Calibrator, IReadOnlyList<CalibrationSkill> Targets,
    string EvidenceJson, string EvidenceHash, string ScopeHash, DateTime VerifiedAt);
public sealed record CalibrationWorkspaceInput(string Tree, IReadOnlyDictionary<string, string> Files);
public sealed record CalibrationEdit(int Target, string BeforeHash, string AfterHash, string Markdown, string Diff);
public sealed record CalibrationWrite(int Target, string State, string? ActualHash, string? Error, DateTime RecordedAt);
public sealed record CalibrationAction(Guid RequestId, string Kind, string Hash, string? Note, DateTime RecordedAt);
public sealed class FactoryCalibration
{
    private FactoryCalibration() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid DeliveryId { get; private set; }
    public Guid RunId { get; private set; }
    public Guid RequestId { get; private set; }
    public string RequestHash { get; private set; } = "";
    public string PolicyHash { get; private set; } = "";
    public long Revision { get; private set; } = 1;
    public string State { get; private set; } = "prepared";
    public string? Error { get; private set; }
    public string? WorkspacePath { get; private set; }
    [JsonIgnore] public string InputJson { get; private set; } = "";
    public CalibrationWorkspaceInput? Input => InputJson.Length == 0 ? null : JsonSerializer.Deserialize<CalibrationWorkspaceInput>(InputJson, FactoryRules.Json);
    public Guid? AgentRunId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public bool Active => State is "queued" or "preparing" or "running" or "cancelling" or "applying";
    [JsonIgnore] public string CaptureJson { get; private set; } = "";
    [JsonIgnore] public string EditsJson { get; private set; } = "[]";
    [JsonIgnore] public string WritesJson { get; private set; } = "[]";
    [JsonIgnore] public string ActionsJson { get; private set; } = "[]";
    public CalibrationCapture Capture => JsonSerializer.Deserialize<CalibrationCapture>(CaptureJson, FactoryRules.Json)!;
    public IReadOnlyList<CalibrationEdit> Edits => JsonSerializer.Deserialize<CalibrationEdit[]>(EditsJson, FactoryRules.Json)!;
    public IReadOnlyList<CalibrationWrite> Writes => JsonSerializer.Deserialize<CalibrationWrite[]>(WritesJson, FactoryRules.Json)!;
    public IReadOnlyList<CalibrationAction> Actions => JsonSerializer.Deserialize<CalibrationAction[]>(ActionsJson, FactoryRules.Json)!;
    public string ProposalHash => FactoryArtifact.Hash(CaptureJson + EditsJson);
    public static FactoryCalibration Prepare(Guid requestId, string requestHash, FactoryDelivery delivery, CalibrationCapture capture)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        return new() { Id = Guid.NewGuid(), RequestId = requestId, RequestHash = requestHash, DeliveryId = delivery.Id, RunId = delivery.RunId,
            ProjectId = delivery.ProjectId, ItemId = delivery.ItemId, CaptureJson = FactoryRules.Serialize(capture), PolicyHash = capture.Policy.Hash, CreatedAt = DateTime.UtcNow };
    }
    public bool Replay(Guid requestId, string kind, string hash)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        var found = Actions.SingleOrDefault(x => x.RequestId == requestId);
        if (found is null) return false;
        if (found.Kind != kind || found.Hash != hash) throw new RuleException("That request ID records a different calibration action.", 409);
        return true;
    }
    public void Act(Guid requestId, long revision, string action, string hash, string? proposalHash, string? note = null)
    {
        if (Replay(requestId, action, hash)) return; Rules.Revision(revision, Revision);
        if (action == "start" && State == "prepared") State = "queued";
        else if (action == "apply" && State == "proposed" && proposalHash == ProposalHash)
        { State = "applying"; WritesJson = FactoryRules.Serialize(Edits.Select(x => new CalibrationWrite(x.Target, "pending", null, null, DateTime.UtcNow)).ToArray()); }
        else if (action == "cancel" && State is "prepared" or "queued" or "proposed") { State = "cancelled"; FinishedAt = DateTime.UtcNow; }
        else if (action == "cancel" && State is "preparing" or "running") State = "cancelling";
        else if (action == "close" && State == "needsAttention" && !string.IsNullOrWhiteSpace(note)) { State = Writes.Any(x => x.State == "applied") ? "partiallyApplied" : "closed"; Error = FactoryReviewRound.Required(note, "Partial outcome acknowledgment"); FinishedAt = DateTime.UtcNow; }
        else if (action == "reconcile" && State == "needsAttention") { /* Observe actual hashes before offering further adoption. */ }
        else throw new RuleException("That calibration action is not available for this state or proposal.", 409);
        ActionsJson = FactoryRules.Serialize(Actions.Append(new(requestId, action, hash, note, DateTime.UtcNow)).ToArray()); Revision++;
    }
    public void Begin(string path) { Require("queued"); State = "preparing"; WorkspacePath = path; StartedAt = DateTime.UtcNow; Revision++; }
    public void Dispatch(CalibrationWorkspaceInput input, Guid agent) { Require("preparing"); InputJson = FactoryRules.Serialize(input); AgentRunId = agent; State = "running"; Revision++; }
    public void Propose(IReadOnlyList<CalibrationEdit> edits)
    { Require("running"); if (edits.Select(x => x.Target).Distinct().Count() != edits.Count || edits.Any(x => x.Target < 0 || x.Target >= Capture.Targets.Count || x.BeforeHash != Capture.Targets[x.Target].Hash || x.AfterHash == x.BeforeHash)) throw new RuleException("Invalid calibration proposal."); EditsJson = FactoryRules.Serialize(edits); State = edits.Count == 0 ? "noChange" : "proposed"; FinishedAt = DateTime.UtcNow; Revision++; }
    public void Write(int target, string state, string? actualHash, string? error)
    {
        if (State is not ("applying" or "needsAttention") || !Writes.Any(x => x.Target == target) || state is not ("pending" or "intent" or "applied" or "conflict")) throw new RuleException("Invalid calibration write journal transition.");
        WritesJson = FactoryRules.Serialize(Writes.Select(x => x.Target == target ? new(target, state, actualHash, error, DateTime.UtcNow) : x).ToArray()); Revision++;
    }
    public void Applied() { Require("applying"); if (Writes.Any(x => x.State != "applied")) throw new RuleException("Every canonical write requires observed completion."); State = "applied"; Error = null; FinishedAt = DateTime.UtcNow; Revision++; }
    public void Reconciled()
    {
        Require("needsAttention");
        if (Writes.All(x => x.State == "applied")) { State = "applied"; Error = null; FinishedAt = DateTime.UtcNow; }
        else if (Writes.All(x => x.State == "pending")) { State = "proposed"; WritesJson = "[]"; Error = null; }
        // Partial adoption remains explicit; never replay it or undo another writer.
        Revision++;
    }
    public void RetainFailure(string reason) { if (Active && Error != reason) { Error = reason; Revision++; } }
    public void Stop(string state, string? error)
    {
        if (State is "applied" or "partiallyApplied" or "closed" || State is "noChange" or "failed" or "cancelled" or "interrupted") return;
        if (state is not ("failed" or "cancelled" or "interrupted" or "needsAttention")) throw new RuleException("Invalid calibration outcome.");
        State = State == "applying" ? "needsAttention" : State == "cancelling" ? "cancelled" : state; Error = error; FinishedAt = DateTime.UtcNow; Revision++;
    }
    private void Require(string state) { if (State != state) throw new RuleException("Calibration state changed. Refresh before continuing.", 409); }
}
public sealed class CalibrationTriggerReceipt
{
    private CalibrationTriggerReceipt() { }
    public Guid Id { get; private set; }
    public Guid DeliveryId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public string PolicyHash { get; private set; } = "";
    public string ScopeHash { get; private set; } = "";
    public string State { get; private set; } = "observing";
    public Guid? JobId { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public static CalibrationTriggerReceipt Begin(FactoryDelivery source, string policy, string scope) => new() { Id = Guid.NewGuid(), DeliveryId = source.Id, ProjectId = source.ProjectId, ItemId = source.ItemId, PolicyHash = policy, ScopeHash = scope, CreatedAt = DateTime.UtcNow };
    public void End(string state, Guid? job, string? error)
    { if (State != "observing") return; if (state is not ("prepared" or "unavailable" or "interrupted")) throw new RuleException("Invalid calibration observation outcome."); State = state; JobId = job; Error = error; FinishedAt = DateTime.UtcNow; }
}
