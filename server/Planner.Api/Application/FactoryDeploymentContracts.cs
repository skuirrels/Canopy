using System.Text.Json.Serialization;
using Planner.Api.Domain;
namespace Planner.Api.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareDeployment(Guid RequestId, long ExpectedRevision, string TargetId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActOnDeployment(Guid RequestId, long ExpectedRevision, string Action, string ApprovalNote = "");
public sealed record DeploymentTargetInfo(DeploymentTarget Target, bool Allowed, bool Ready, string? Error);
public sealed record DeploymentLibrary(Guid DeliveryId, long SourceRevision, IReadOnlyList<DeploymentTargetInfo> Targets, IReadOnlyList<FactoryDeployment> Deployments, int PollIntervalMs);
public sealed record DeploymentDetail(FactoryDeployment Deployment, IReadOnlyList<FactoryDeploymentOperation> Operations, IReadOnlyList<string> AuthorityErrors, string DeployBinding, string RecoveryBinding, int PollIntervalMs);
