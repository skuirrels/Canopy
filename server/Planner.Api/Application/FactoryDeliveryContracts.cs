using System.Text.Json.Serialization;
using Planner.Api.Domain;

namespace Planner.Api.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditDeliveryPolicy(long ExpectedRevision, DeliveryPolicy Policy);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareDelivery(Guid RequestId, long ExpectedRevision, string BranchName);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActOnDelivery(Guid RequestId, long ExpectedRevision, string Action, string ApprovalNote = "");
public sealed record DeliveryHookInfo(string Id, string Name, bool Ready, string? Error);
public sealed record DeliveryPolicyDetail(long ProjectRevision, DeliveryPolicy? Policy, IReadOnlyList<DeliveryHookInfo> Hooks, IReadOnlyList<DeploymentTarget> Targets);
public sealed record DeliveryLocalReadiness(bool Ready, long RunRevision, VerificationContext? Context, Guid? ReviewId, string? ReviewHash, string? QaHash, IReadOnlyList<string> Errors);
public sealed record DeliveryDetail(FactoryDelivery Delivery, IReadOnlyList<FactoryDeliveryOperation> Operations, DeliveryLocalReadiness Local, IReadOnlyList<string> MergeErrors, int PollIntervalMs);
public sealed record DeliveryLibrary(Guid RunId, DeliveryPolicyDetail Policy, DeliveryLocalReadiness Local, IReadOnlyList<FactoryDelivery> Deliveries, int PollIntervalMs);
public sealed record DeliveryGitState(string HeadSha, string TreeSha, IReadOnlyList<string> ChangedFiles);
public sealed record DeliveryMergeResult(string Status, string? RequestId, string? Sha);
public sealed record DeliveryCommit(string Sha, string TreeSha, IReadOnlyList<string> Parents);
public interface IDeliveryProvider
{
    string Binding { get; }
    Task<DeliveryObservation> Observe(DeliveryPolicy policy, DeliveryCapture? capture, DeliveryRemote? remote, string? marker, CancellationToken ct);
    Task<DeliveryRemote> CreateDraft(FactoryDelivery delivery, string title, string body, CancellationToken ct);
    Task<DeliveryMergeResult> Merge(FactoryDelivery delivery, CancellationToken ct);
    Task<DeliveryMergeResult> MergeResult(FactoryDelivery delivery, CancellationToken ct);
    Task<DeliveryCommit> Commit(DeliveryPolicy policy, string sha, CancellationToken ct);
    Task<bool> TargetContains(DeliveryPolicy policy, string mergeSha, CancellationToken ct);
}
public interface IDeliveryWorkspace
{
    Task<DeliveryGitState> Inspect(FactoryRun run, DeliveryPolicy policy, DeliveryObservation provider, CancellationToken ct);
    Task Publish(FactoryRun run, FactoryDelivery delivery, DeliveryObservation provider, CancellationToken ct);
}
public interface IDeliveryHooks
{
    IReadOnlyList<DeliveryHookInfo> List();
    IReadOnlyList<DeploymentTarget> Targets();
    string ResourceKey(string id);
    string? ConfigurationHash(string? id);
    Task<DeliveryProbe> Probe(string id, CancellationToken ct);
    Task<DeliveryHookReceipt> Run(string id, FactoryDelivery delivery, DeliveryProbe before, CancellationToken ct);
    Task<DeliveryHookReceipt> RunTarget(string id, DeploymentExecution execution, DeliveryProbe before, CancellationToken ct);
}
public sealed record DeploymentExecution(Guid DeliveryId, Guid DeploymentId, string Repository, string MergeSha, string Revision, string TargetId, string Environment, bool Recovery);
