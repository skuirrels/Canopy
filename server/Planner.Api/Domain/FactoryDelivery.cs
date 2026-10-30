using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Planner.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeliveryCheck(string Name, long AppId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeliveryPolicy(bool Enabled, string Repository, string RemoteName, string TargetBranch, string BranchPrefix,
    bool AllowDraftPr, bool AllowMerge, IReadOnlyList<DeliveryCheck> RequiredChecks, string? PostMergeHookId,
    string Environment, IReadOnlyList<string> SensitivePaths, IReadOnlyList<string> MigrationPaths,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? DeploymentTargetIds = null)
{
    public void Validate()
    {
        if (Repository is null || !Regex.IsMatch(Repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") || Repository.Length > 201 || Repository.Split('/').Any(x => x is "." or "..")) throw new RuleException("Use an explicit owner/repository.");
        if (RemoteName is null || !Regex.IsMatch(RemoteName, @"^[A-Za-z0-9_-]+$") || RemoteName.Length > 100) throw new RuleException("Use an explicit local Git remote name.");
        Branch(TargetBranch); Branch((BranchPrefix ?? "") + "candidate");
        if (string.IsNullOrWhiteSpace(BranchPrefix) || BranchPrefix == TargetBranch) throw new RuleException("Set an explicit publication branch prefix.");
        if (RequiredChecks is null || RequiredChecks.Count > 100 || RequiredChecks.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 200 || x.AppId < 1) || RequiredChecks.Select(x => (x.Name, x.AppId)).Distinct().Count() != RequiredChecks.Count)
            throw new RuleException("Supply distinct required check names with positive GitHub App IDs.");
        if (AllowMerge && RequiredChecks.Count == 0) throw new RuleException("Merge authority requires explicitly configured GitHub checks.");
        Rules.Name(Environment, "Delivery environment");
        foreach (var patterns in new[] { SensitivePaths, MigrationPaths })
            if (patterns is null || patterns.Count > 100 || patterns.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 1000 || Path.IsPathRooted(x) || x.Split('/','\\').Contains("..") || x.Any(char.IsControl))) throw new RuleException("Supply up to 100 explicit relative sensitive/migration path patterns, using [] for none.");
        if (PostMergeHookId is not null) Rules.Name(PostMergeHookId, "Post-merge hook");
        if (DeploymentTargetIds is not null && (DeploymentTargetIds.Count > 100 || DeploymentTargetIds.Any(string.IsNullOrWhiteSpace) || DeploymentTargetIds.Distinct().Count() != DeploymentTargetIds.Count)) throw new RuleException("Choose distinct configured deployment targets.");
    }
    public static void Branch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.StartsWith('-') || value.StartsWith('/') || value.EndsWith('/') || value.EndsWith('.') || value.Contains("..") || value.Contains("@{") || value.Contains("//") || value == "@" || value.Split('/').Any(x => x.StartsWith('.') || x.EndsWith(".lock")) || value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || "~^:?*[\\".Contains(c))) throw new RuleException("Use a valid explicit Git branch name.");
    }
}
public sealed class ProjectDeliveryPolicy
{
    private ProjectDeliveryPolicy() { }
    public Guid Id { get; private set; }
    [JsonIgnore] public string PolicyJson { get; private set; } = "";
    public DeliveryPolicy Policy => JsonSerializer.Deserialize<DeliveryPolicy>(PolicyJson, FactoryRules.Json)!;
    public static ProjectDeliveryPolicy Adopt(Guid id, DeliveryPolicy policy) { var result = new ProjectDeliveryPolicy { Id = id }; result.Replace(policy); return result; }
    public void Replace(DeliveryPolicy policy) { policy.Validate(); PolicyJson = FactoryRules.Serialize(policy); }
}
public sealed record DeliveryCapture(VerificationContext Context, string SourceHash, Guid ReviewId, string ReviewHash, string QaHash,
    string HeadSha, string TreeSha, string BaseSha, string BranchName, string ProviderBinding, string? HookHash, IReadOnlyList<string> SensitiveFiles, IReadOnlyList<string> MigrationFiles);
public sealed record DeliveryApproval(string Action, string Binding, string Note, DateTime CreatedAt);
public sealed record DeliveryRemote(int Number, string Url, string HeadSha, string BaseSha, string State, bool Draft, string? MergeSha,
    string? MergeRequestId = null, bool? Mergeable = null);
public sealed record DeliveryCheckResult(long Id, string Name, long AppId, string HeadSha, string Status, string? Conclusion);
public sealed record DeliveryObservation(string Repository, string CloneUrl, string SshUrl, string BaseSha, string? BranchSha, DeliveryRemote? PullRequest,
    IReadOnlyList<DeliveryCheckResult> Checks, bool ProtectionStrict, bool AdminEnforced, bool ReviewBypass,
    IReadOnlyList<DeliveryCheck> ProtectedChecks, bool MergeAllowed, bool LinearHistory, bool MergeQueue, bool BaseIsAncestor, DateTime ObservedAt);
public sealed record DeliveryProbe(string Instance, string Revision, string SnapshotHash, string HealthHash);
public sealed record DeliveryHookReceipt(DeliveryProbe Before, DeliveryProbe? After, int? ExitCode, string Output, bool OutputTruncated, string? Error);
public sealed class FactoryDelivery
{
    private FactoryDelivery() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid RunId { get; private set; }
    public Guid RequestId { get; private set; }
    public string RequestHash { get; private set; } = "";
    public long Revision { get; private set; } = 1;
    public string State { get; private set; } = "captured";
    public string Reason { get; private set; } = "Captured only; publication requires an explicit action.";
    public Guid? CurrentOperationId { get; private set; }
    public bool Open => State is not ("closed" or "delivered");
    public DateTime CreatedAt { get; private set; }
    [JsonIgnore] public string PolicyJson { get; private set; } = "";
    [JsonIgnore] public string CaptureJson { get; private set; } = "";
    [JsonIgnore] public string RemoteJson { get; private set; } = "";
    [JsonIgnore] public string ApprovalsJson { get; private set; } = "[]";
    [JsonIgnore] public string ObservationJson { get; private set; } = "";
    [JsonIgnore] public string HookJson { get; private set; } = "";
    public DeliveryPolicy Policy => JsonSerializer.Deserialize<DeliveryPolicy>(PolicyJson, FactoryRules.Json)!;
    public DeliveryCapture Capture => JsonSerializer.Deserialize<DeliveryCapture>(CaptureJson, FactoryRules.Json)!;
    public DeliveryRemote? Remote => RemoteJson.Length == 0 ? null : JsonSerializer.Deserialize<DeliveryRemote>(RemoteJson, FactoryRules.Json);
    public IReadOnlyList<DeliveryApproval> Approvals => JsonSerializer.Deserialize<DeliveryApproval[]>(ApprovalsJson, FactoryRules.Json)!;
    public DeliveryObservation? Observation => ObservationJson.Length == 0 ? null : JsonSerializer.Deserialize<DeliveryObservation>(ObservationJson, FactoryRules.Json);
    public DeliveryHookReceipt? Hook => HookJson.Length == 0 ? null : JsonSerializer.Deserialize<DeliveryHookReceipt>(HookJson, FactoryRules.Json);
    public string Binding => FactoryArtifact.Hash(CaptureJson + PolicyJson + (Remote?.MergeSha ?? ""));
    public string Marker => $"<!-- canopy-delivery:{Id} -->";
    public static FactoryDelivery Prepare(Guid requestId, string hash, FactoryRun run, DeliveryPolicy policy, DeliveryCapture capture)
    {
        policy.Validate(); DeliveryPolicy.Branch(capture.BranchName);
        if (!policy.Enabled || !policy.AllowDraftPr || !capture.BranchName.StartsWith(policy.BranchPrefix, StringComparison.Ordinal) || capture.BranchName == policy.TargetBranch) throw new RuleException("Enable draft delivery and choose a publication branch within project policy.");
        return new() { Id = Guid.NewGuid(), RequestId = requestId, RequestHash = hash, RunId = run.Id, ProjectId = run.ProjectId, ItemId = run.ItemId,
            PolicyJson = FactoryRules.Serialize(policy), CaptureJson = FactoryRules.Serialize(capture), CreatedAt = DateTime.UtcNow };
    }
    public void Queue(Guid operationId)
    { if (!Open || CurrentOperationId.HasValue) throw new RuleException("Finish or reconcile the current delivery operation first.", 409); CurrentOperationId = operationId; Revision++; }
    public void Release(Guid operationId) { if (CurrentOperationId == operationId) { CurrentOperationId = null; Revision++; } }
    public void Change(string state, string reason) { if (!Open) throw new RuleException("This delivery is closed.", 409); State = state; Reason = reason; Revision++; }
    public void Observe(DeliveryObservation observation) { ObservationJson = FactoryRules.Serialize(observation); Revision++; }
    public void RecordRemote(DeliveryRemote remote) { RemoteJson = FactoryRules.Serialize(remote); Revision++; }
    public void RecordHook(DeliveryHookReceipt receipt) { HookJson = FactoryRules.Serialize(receipt); Revision++; }
    public void Approve(string action, string note)
    {
        if (action == "merge" && (!Policy.AllowMerge || Remote is null || Remote.MergeSha is not null)) throw new RuleException("This delivery is not eligible for merge approval.", 409);
        if (action == "postMerge" && (Policy.PostMergeHookId is null || Remote?.MergeSha is null)) throw new RuleException("A recorded merge and configured hook are required.", 409);
        if (action is not ("merge" or "postMerge")) throw new RuleException("Choose a specific delivery action.");
        ApprovalsJson = FactoryRules.Serialize(Approvals.Append(new DeliveryApproval(action, Binding, FactoryReviewRound.Required(note, "Approval rationale and sensitive-change/migration notes"), DateTime.UtcNow)).ToList()); Revision++;
    }
    public bool Approved(string action) => Approvals.Any(x => x.Action == action && x.Binding == Binding);
}
public sealed class FactoryDeliveryOperation
{
    private FactoryDeliveryOperation() { }
    public Guid Id { get; private set; }
    public Guid DeliveryId { get; private set; }
    public Guid RequestId { get; private set; }
    public string Kind { get; private set; } = "";
    public string ApprovalNote { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string State { get; private set; } = "queued";
    public string? Error { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public bool Pending => State is "queued" or "inFlight";
    [JsonIgnore] public string ResultJson { get; private set; } = "";
    public JsonElement? Result => ResultJson.Length == 0 ? null : JsonSerializer.Deserialize<JsonElement>(ResultJson);
    public static FactoryDeliveryOperation Create(Guid deliveryId, Guid requestId, string kind, string hash, string note)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        if (kind is not ("publish" or "approveMerge" or "merge" or "approvePostMerge" or "postMerge" or "reconcile" or "close")) throw new RuleException("Choose a supported delivery action.");
        return new() { Id = Guid.NewGuid(), DeliveryId = deliveryId, RequestId = requestId, Kind = kind, RequestHash = hash, ApprovalNote = AgentRun.Text(note, "Approval note"), CreatedAt = DateTime.UtcNow };
    }
    public void Begin() { if (State != "queued") throw new RuleException("Operation is no longer queued.", 409); State = "inFlight"; }
    public void Receipt<T>(T value) => ResultJson = FactoryRules.Serialize(value);
    public void Resolve<T>(string state, T proof)
    {
        if (State != "uncertain" || state is not ("succeeded" or "failed")) return;
        Receipt(new { previous = Result, reconciliation = proof }); State = state; Error = null; FinishedAt = DateTime.UtcNow;
    }
    public void End(string state, string? error = null)
    { if (!Pending) return; if (state is not ("succeeded" or "failed" or "uncertain")) throw new RuleException("Invalid delivery outcome."); State = state; Error = error; FinishedAt = DateTime.UtcNow; }
}

public static class FactoryDeliveryRules
{
    public static IReadOnlyList<string> MatchedFiles(IReadOnlyList<string> files, IReadOnlyList<string> patterns) => files.Where(file => patterns.Any(pattern => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, file, ignoreCase: false))).Distinct().Order(StringComparer.Ordinal).ToList();
    public static IReadOnlyList<string> MergeErrors(FactoryDelivery delivery, DeliveryObservation? observed, bool approval)
    {
        var errors = new List<string>(); var policy = delivery.Policy; var capture = delivery.Capture;
        if (!policy.AllowMerge) errors.Add("Project policy does not grant merge authority.");
        if (approval && !delivery.Approved("merge")) errors.Add("Explicit human merge approval bound to this capture is required.");
        if (observed is null) { errors.Add("Fresh provider observation is required; reconcile first."); return errors; }
        var pr = observed.PullRequest;
        if (pr is null || pr.State != "open" || pr.Draft || pr.Mergeable != true) errors.Add("A ready, open, mergeable PR is required. Mark the draft ready on GitHub.");
        if (pr?.HeadSha != capture.HeadSha || observed.BranchSha != capture.HeadSha || observed.BaseSha != capture.BaseSha || pr.BaseSha != capture.BaseSha) errors.Add("Remote head/base changed or is unavailable; captured approval is stale.");
        if (!observed.BaseIsAncestor) errors.Add("The candidate must contain the current target base.");
        if (!observed.ProtectionStrict || !observed.AdminEnforced || observed.ReviewBypass || observed.LinearHistory || observed.MergeQueue || !observed.MergeAllowed) errors.Add("Verified strict non-bypassable protection supporting merge commits is required; unsupported rules/queues block this adapter.");
        foreach (var check in policy.RequiredChecks)
        {
            if (!observed.ProtectedChecks.Contains(check)) errors.Add($"Require {check.Name} from App {check.AppId} in server-side branch protection.");
            var results = observed.Checks.Where(x => x.Name == check.Name && x.AppId == check.AppId).ToList();
            if (results.Count != 1 || results[0].HeadSha != capture.HeadSha || results[0].Status != "completed" || results[0].Conclusion != "success") errors.Add($"Required check {check.Name} from App {check.AppId} is missing, ambiguous or unsuccessful.");
        }
        return errors;
    }
}
