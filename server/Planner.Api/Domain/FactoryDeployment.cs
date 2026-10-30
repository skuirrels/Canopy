using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

public sealed record DeploymentTarget(string Id, string Name, string Environment, string DeployHookId, string? RecoveryHookId = null);
public sealed record DeploymentCapture(DeploymentTarget Target, string ResourceKey, string ConfigurationHash, string ProviderBinding, string MergeSha, string PolicyHash);

public sealed class FactoryDeployment
{
    private FactoryDeployment() { }
    public Guid Id { get; private set; }
    public Guid DeliveryId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid RequestId { get; private set; }
    public string RequestHash { get; private set; } = "";
    public long Revision { get; private set; } = 1;
    public string TargetId { get; private set; } = "";
    public string State { get; private set; } = "captured";
    public string Reason { get; private set; } = "Captured only. Deployment requires separate approval and execution.";
    public Guid? CurrentOperationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool Open => State is not ("deployed" or "recovered" or "closed");
    [JsonIgnore] public string CaptureJson { get; private set; } = "";
    [JsonIgnore] public string ApprovalsJson { get; private set; } = "[]";
    [JsonIgnore] public string DeploymentJson { get; private set; } = "";
    [JsonIgnore] public string RecoveryJson { get; private set; } = "";
    public DeploymentCapture Capture => JsonSerializer.Deserialize<DeploymentCapture>(CaptureJson, FactoryRules.Json)!;
    public IReadOnlyList<DeliveryApproval> Approvals => JsonSerializer.Deserialize<DeliveryApproval[]>(ApprovalsJson, FactoryRules.Json)!;
    public DeliveryHookReceipt? Deployment => Receipt(DeploymentJson);
    public DeliveryHookReceipt? Recovery => Receipt(RecoveryJson);
    public string Binding(string action) => FactoryArtifact.Hash(CaptureJson + action + (action == "recover" ? DeploymentJson : ""));
    public bool Approved(string action) => Approvals.Any(x => x.Action == action && x.Binding == Binding(action));
    public static FactoryDeployment Prepare(FactoryDelivery source, Guid requestId, string requestHash, DeploymentCapture capture)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        if (source.Remote?.MergeSha != capture.MergeSha) throw new RuleException("Capture the source delivery's actual merged revision.", 409);
        return new() { Id = Guid.NewGuid(), DeliveryId = source.Id, ProjectId = source.ProjectId, ItemId = source.ItemId, RequestId = requestId,
            RequestHash = requestHash, TargetId = capture.Target.Id, CaptureJson = FactoryRules.Serialize(capture), CreatedAt = DateTime.UtcNow };
    }
    public void RequireAction(string action)
    {
        if (!Open || CurrentOperationId is not null) throw new RuleException("Finish the current deployment operation or retain this completed record.", 409);
        if (action is not ("approveDeploy" or "deploy" or "approveRecovery" or "recover" or "reconcile" or "close")) throw new RuleException("Choose a supported deployment action.");
        if (action is "approveDeploy" or "deploy" && Deployment is not null) throw new RuleException("Deployment was already attempted. Reconcile or explicitly recover; it cannot be replayed.", 409);
        if (action is "approveRecovery" or "recover" && (Deployment is null || Recovery is not null || Capture.Target.RecoveryHookId is null)) throw new RuleException("An attempted deployment and unused configured recovery command are required.", 409);
        if (action == "deploy" && !Approved("deploy") || action == "recover" && !Approved("recover")) throw new RuleException("Approve this revision, environment and action first.", 409);
    }
    public void Queue(Guid operationId) { CurrentOperationId = operationId; Revision++; }
    public void Release(Guid operationId) { if (CurrentOperationId == operationId) { CurrentOperationId = null; Revision++; } }
    public void Change(string state, string reason) { State = state; Reason = reason; Revision++; }
    public void Approve(string action, string note)
    {
        ApprovalsJson = FactoryRules.Serialize(Approvals.Append(new DeliveryApproval(action, Binding(action), FactoryReviewRound.Required(note, "Deployment approval rationale and migration/recovery considerations"), DateTime.UtcNow)));
        Revision++;
    }
    public void Record(bool recovery, DeliveryHookReceipt receipt)
    { if (recovery) RecoveryJson = FactoryRules.Serialize(receipt); else DeploymentJson = FactoryRules.Serialize(receipt); Revision++; }
    private static DeliveryHookReceipt? Receipt(string value) => value.Length == 0 ? null : JsonSerializer.Deserialize<DeliveryHookReceipt>(value, FactoryRules.Json);
}

public sealed class FactoryDeploymentOperation
{
    private FactoryDeploymentOperation() { }
    public Guid Id { get; private set; }
    public Guid DeploymentId { get; private set; }
    public Guid RequestId { get; private set; }
    public string RequestHash { get; private set; } = "";
    public string Kind { get; private set; } = "";
    public string ApprovalNote { get; private set; } = "";
    public string State { get; private set; } = "queued";
    public string? Error { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public bool Pending => State is "queued" or "inFlight";
    [JsonIgnore] public string ResultJson { get; private set; } = "";
    public JsonElement? Result => ResultJson.Length == 0 ? null : JsonSerializer.Deserialize<JsonElement>(ResultJson);
    public static FactoryDeploymentOperation Create(Guid id, Guid requestId, string hash, string action, string note)
    {
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        return new() { Id = Guid.NewGuid(), DeploymentId = id, RequestId = requestId, RequestHash = hash, Kind = action, ApprovalNote = AgentRun.Text(note, "Approval note"), CreatedAt = DateTime.UtcNow };
    }
    public void Begin() { if (State != "queued") throw new RuleException("Operation is no longer queued.", 409); State = "inFlight"; }
    public void End<T>(string state, T result, string? error = null)
    { if (!Pending) return; ResultJson = FactoryRules.Serialize(result); State = state; Error = error; FinishedAt = DateTime.UtcNow; }
}

public static class DeploymentVerification
{
    public static string? Error(DeliveryProbe before, DeliveryProbe after, string expectedRevision, int? exitCode) =>
        exitCode is not (null or 0) ? "The configured deployment command failed; retain the original failure receipt." :
        after.Revision != expectedRevision ? "The target does not report the approved revision." :
        after.Instance == before.Instance ? "No changed application instance was observed." :
        after.SnapshotHash != before.SnapshotHash ? "Persistence changed across deployment; inspect the retained receipts." : null;
}
