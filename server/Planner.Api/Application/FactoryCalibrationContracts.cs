using System.Text.Json.Serialization;
using Planner.Api.Domain;
namespace Planner.Api.Application;

public sealed class FactoryCalibrationOptions
{
    public string[] EditableRoots { get; set; } = [];
    public int MaximumTargets { get; set; }
    public int MaximumPacketBytes { get; set; }
    public int MaximumEvidenceItems { get; set; }
    public int MaximumProposalBytes { get; set; }
    public int MaximumWorkspaceFiles { get; set; }
    public long MaximumWorkspaceBytes { get; set; }
    public int MaximumManifestBytes { get; set; }
    public int ObservationTimeoutSeconds { get; set; }
    public void Validate() { if (EditableRoots is null || MaximumTargets < 1 || MaximumPacketBytes < 1 || MaximumEvidenceItems < 1 || MaximumProposalBytes < 1 || MaximumWorkspaceFiles < 1 || MaximumWorkspaceBytes < 1 || MaximumManifestBytes < 1 || ObservationTimeoutSeconds < 1) throw new InvalidOperationException("Configure calibration edit roots and positive evidence/proposal/time bounds."); }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record EditCalibrationPolicy(long ExpectedRevision, CalibrationPolicy Policy);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record PrepareCalibration(Guid RequestId, long ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record ActOnCalibration(Guid RequestId, long ExpectedRevision, string Action, string? ProposalHash = null, string? Note = null);
public sealed record CalibrationPolicyDetail(long ProjectRevision, CalibrationPolicy? Policy, IReadOnlyList<AgentProvider> Providers, bool EditableScopeConfigured, bool ProcessLifetimeSupported);
public sealed record CalibrationReadiness(Guid DeliveryId, long DeliveryRevision, bool Ready, IReadOnlyList<string> Errors);
public sealed record CalibrationLibrary(CalibrationReadiness Readiness, IReadOnlyList<FactoryCalibration> Jobs, IReadOnlyList<CalibrationTriggerReceipt> Triggers, int PollIntervalMs);
public interface ICalibrationWorkspace
{
    string ScopeHash { get; }
    bool ScopeConfigured { get; }
    bool ProcessLifetimeSupported { get; }
    string PathFor(Guid job);
    Task<IReadOnlyList<CalibrationSkill>> Resolve(string repository, IReadOnlyList<FactorySkill> targets, bool writable, CancellationToken ct);
    Task<CalibrationWorkspaceInput> Prepare(FactoryCalibration job, CancellationToken ct);
    Task<IReadOnlyList<CalibrationEdit>> Inspect(FactoryCalibration job, CancellationToken ct);
    Task<string> Current(CalibrationSkill target, string repository, CancellationToken ct, bool writable = true);
    Task<string> Apply(CalibrationSkill target, string repository, CalibrationEdit edit, CancellationToken ct);
}
