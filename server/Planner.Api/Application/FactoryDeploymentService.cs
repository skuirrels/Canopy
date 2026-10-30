using Planner.Api.Domain;

namespace Planner.Api.Application;

public sealed class FactoryDeploymentService(IWorkspaceStore store, IDeliveryProvider provider, IDeliveryHooks hooks, FactoryDeliveryService deliveries, IAgentEnvironment agents)
{
    public async Task<DeploymentLibrary> List(Guid deliveryId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var source = Source(data, deliveryId);
        var policy = data.DeliveryPolicies.SingleOrDefault(x => x.Id == source.ProjectId)?.Policy;
        var configured = hooks.List();
        var targets = hooks.Targets().Select(target =>
        {
            var allowed = policy?.Enabled == true && policy.DeploymentTargetIds?.Contains(target.Id) == true;
            var resource = hooks.ResourceKey(target.DeployHookId);
            var reserved = data.Deployments.Any(x => x.Open && x.Capture.ResourceKey == resource) || data.Deliveries.Any(x => x.Open && x.Policy.PostMergeHookId is { } hook && configured.Any(c => c.Id == hook) && hooks.ResourceKey(hook) == resource);
            var ready = !reserved && new[] { target.DeployHookId, target.RecoveryHookId }.Where(x => x is not null).All(id => configured.Any(x => x.Id == id && x.Ready));
            return new DeploymentTargetInfo(target, allowed, ready, !allowed ? "Adopt this target in project delivery policy first." : reserved ? "This application is reserved by an open deployment or post-merge delivery." : !ready ? "Configured deployment or recovery command is unavailable." : null);
        }).ToList();
        return new(deliveryId, source.Revision, targets, data.Deployments.Where(x => x.DeliveryId == deliveryId).OrderByDescending(x => x.CreatedAt).ToList(), agents.PollIntervalMs);
    }
    public async Task<DeploymentDetail> Read(Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var deployment = Find(data, id); var errors = new List<string>();
        try { Authority(data, deployment); } catch (RuleException error) { errors.Add(error.Message); }
        return new(deployment, Operations(data, id).ToList(), errors, deployment.Binding("deploy"), deployment.Binding("recover"), agents.PollIntervalMs);
    }
    public async Task<DeploymentDetail> Prepare(Guid deliveryId, PrepareDeployment input, CancellationToken ct)
    {
        if (input.RequestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        var hash = Hash(new { deliveryId, input }); var data = await store.ReadAsync(ct);
        var duplicate = data.Deployments.SingleOrDefault(x => x.RequestId == input.RequestId);
        if (duplicate is not null) { ReplayCapture(duplicate, deliveryId, hash); return await Read(duplicate.Id, ct); }
        var source = Source(data, deliveryId); Rules.Revision(input.ExpectedRevision, source.Revision);
        if (source.CurrentOperationId is not null) throw new RuleException("Finish the source delivery operation first.", 409);
        var target = hooks.Targets().SingleOrDefault(x => x.Id == input.TargetId) ?? throw new RuleException("Choose a configured deployment target.");
        var policy = Policy(data, source, target.Id); await VerifySource(source, ct);
        var capture = new DeploymentCapture(target, hooks.ResourceKey(target.DeployHookId), ConfigurationHash(target), provider.Binding, source.Remote!.MergeSha!, Hash(policy));
        var candidate = FactoryDeployment.Prepare(source, input.RequestId, hash, capture); var id = candidate.Id;
        await store.WriteAsync(saved =>
        {
            var replay = saved.Deployments.SingleOrDefault(x => x.RequestId == input.RequestId);
            if (replay is not null) { ReplayCapture(replay, deliveryId, hash); id = replay.Id; return; }
            Rules.Revision(input.ExpectedRevision, Source(saved, deliveryId).Revision); Authority(saved, candidate);
            if (saved.Deployments.Any(x => x.Open && x.Capture.ResourceKey == capture.ResourceKey) || saved.Deliveries.Any(x => x.Open && x.Policy.PostMergeHookId is { } hook && hooks.ResourceKey(hook) == capture.ResourceKey)) throw new RuleException("This target is reserved by an open deployment or post-merge delivery. Finish or explicitly close it first.", 409);
            saved.Deployments.Add(candidate);
        }, ct);
        return await Read(id, ct);
    }
    public async Task<DeploymentDetail> Act(Guid id, ActOnDeployment input, CancellationToken ct)
    {
        var hash = Hash(new { id, input });
        await store.WriteAsync(data =>
        {
            var replay = data.DeploymentOperations.SingleOrDefault(x => x.RequestId == input.RequestId);
            if (replay is not null) { if (replay.DeploymentId != id || replay.RequestHash != hash) throw new RuleException("That request ID records a different deployment action.", 409); return; }
            var d = Find(data, id); Rules.Revision(input.ExpectedRevision, d.Revision); d.RequireAction(input.Action);
            if (input.Action != "close") Authority(data, d);
            if (input.Action.StartsWith("approve", StringComparison.Ordinal)) FactoryReviewRound.Required(input.ApprovalNote, "Deployment approval rationale and migration/recovery considerations");
            var op = FactoryDeploymentOperation.Create(id, input.RequestId, hash, input.Action, input.ApprovalNote); d.Queue(op.Id); data.DeploymentOperations.Add(op);
        }, ct);
        return await Read(id, ct);
    }
    public async Task Execute(Guid operationId, CancellationToken ct)
    {
        var data = await store.WriteAsync(saved => { var operation = Operation(saved, operationId); if (operation.State == "queued") operation.Begin(); }, ct);
        var op = Operation(data, operationId); if (op.State != "inFlight") return; var d = Find(data, op.DeploymentId); var sent = false;
        try
        {
            if (op.Kind == "close") { await Finish(op, "succeeded", new { closed = true }, saved => Find(saved, d.Id).Change("closed", "Tracking closed explicitly. Target state and all original receipts are retained; no external action was reversed."), ct); return; }
            Authority(data, d); var source = Source(data, d.DeliveryId); await VerifySource(source, ct);
            var recovery = op.Kind is "approveRecovery" or "recover" || op.Kind == "reconcile" && d.Recovery is not null;
            var hookId = recovery ? d.Capture.Target.RecoveryHookId! : d.Capture.Target.DeployHookId;
            var revision = recovery ? d.Deployment!.Before.Revision : d.Capture.MergeSha;
            if (recovery && !await provider.TargetContains(source.Policy, revision, ct)) throw new RuleException("The recorded pre-deployment revision is not an observed commit on the configured target branch. Recovery is blocked.", 409);
            if (op.Kind is "approveDeploy" or "approveRecovery")
            { await Finish(op, "succeeded", new { revision, target = d.Capture.Target, binding = d.Binding(recovery ? "recover" : "deploy") }, saved => Find(saved, d.Id).Approve(recovery ? "recover" : "deploy", op.ApprovalNote), ct); return; }
            if (op.Kind == "reconcile")
            {
                var receipt = (recovery ? d.Recovery : d.Deployment) ?? throw new RuleException("No deployment command intent is recorded. Nothing will be replayed.", 409);
                var after = await hooks.Probe(hookId, ct); var baseline = recovery ? receipt.Before with { SnapshotHash = d.Deployment!.Before.SnapshotHash } : receipt.Before;
                var error = DeploymentVerification.Error(baseline, after, revision, receipt.ExitCode);
                if (error is not null) throw new RuleException(error + " Reconciliation does not execute commands or erase the original failure.", 409);
                await Finish(op, "succeeded", new { observed = after, originalReceipt = receipt }, saved => Find(saved, d.Id).Change(recovery ? "recovered" : "deployed", "Actual target revision, changed instance, health and original persistence observed. Original command receipts remain unchanged."), ct); return;
            }
            if (op.Kind is not ("deploy" or "recover") || !d.Approved(recovery ? "recover" : "deploy")) throw new RuleException("Explicit revision-bound approval is required.", 409);
            var before = recovery ? d.Deployment!.Before : await hooks.Probe(hookId, ct);
            // Persist the command intent before dispatch. Recovery always compares with
            // the original pre-deployment snapshot, even if the failed command altered it.
            var baselineProbe = recovery ? before with { SnapshotHash = d.Deployment!.Before.SnapshotHash } : before;
            await store.WriteAsync(saved =>
            {
                var current = Find(saved, d.Id); Authority(saved, current);
                if (!current.Approved(recovery ? "recover" : "deploy")) throw new RuleException("Deployment approval changed during preflight.", 409);
                current.Record(recovery, new(baselineProbe, null, null, "", false, "Command intent persisted; actual outcome is not established."));
            }, ct);
            sent = true;
            var receiptResult = await hooks.RunTarget(hookId, new(source.Id, d.Id, source.Policy.Repository, d.Capture.MergeSha, revision, d.TargetId, d.Capture.Target.Environment, recovery), baselineProbe, ct);
            await Finish(op, receiptResult.Error is null ? "succeeded" : "uncertain", receiptResult, saved => { var current = Find(saved, d.Id); current.Record(recovery, receiptResult); current.Change(receiptResult.Error is null ? recovery ? "recovered" : "deployed" : "needsAttention", receiptResult.Error ?? "Approved revision, changed target instance, health and original persistence verified."); }, ct, receiptResult.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            var reason = error is RuleException ? error.Message : "Deployment outcome could not be established. Inspect retained receipts and reconcile the target.";
            await Finish(op, sent ? "uncertain" : "failed", new { outcomeEstablished = false }, saved => Find(saved, d.Id).Change("needsAttention", reason), CancellationToken.None, reason);
        }
    }
    private async Task VerifySource(FactoryDelivery source, CancellationToken ct)
    {
        if (source.Remote?.MergeSha is not { } sha || source.Remote.HeadSha != source.Capture.HeadSha || provider.Binding != source.Capture.ProviderBinding) throw new RuleException("A reconciled source merge and its captured provider identity are required.", 409);
        var observed = await provider.Observe(source.Policy, source.Capture, source.Remote, source.Marker, ct);
        if (observed.PullRequest is not { State: "merged" } actual || actual.HeadSha != source.Capture.HeadSha || actual.MergeSha != sha) throw new RuleException("Current provider identity does not match the recorded source merge.", 409);
        await deliveries.VerifyMerge(source, sha, ct);
    }
    private void Authority(WorkspaceData data, FactoryDeployment deployment)
    {
        var source = Source(data, deployment.DeliveryId); var policy = Policy(data, source, deployment.TargetId); var capture = deployment.Capture;
        var target = hooks.Targets().SingleOrDefault(x => x.Id == deployment.TargetId);
        if (target is null || target != capture.Target || Hash(policy) != capture.PolicyHash || provider.Binding != capture.ProviderBinding || ConfigurationHash(target) != capture.ConfigurationHash || source.Remote?.MergeSha != capture.MergeSha) throw new RuleException("Deployment policy, target configuration or source identity changed. Restore captured authority or close and capture anew.", 409);
    }
    private string ConfigurationHash(DeploymentTarget target) => Hash(new { target, deploy = hooks.ConfigurationHash(target.DeployHookId), recovery = hooks.ConfigurationHash(target.RecoveryHookId) });
    private static DeliveryPolicy Policy(WorkspaceData data, FactoryDelivery source, string target)
    {
        var policy = data.DeliveryPolicies.SingleOrDefault(x => x.Id == source.ProjectId)?.Policy;
        if (policy?.Enabled != true || policy.DeploymentTargetIds?.Contains(target) != true || policy.Repository != source.Policy.Repository || policy.TargetBranch != source.Policy.TargetBranch) throw new RuleException("Current project policy must explicitly authorize this target for the source repository and branch.", 409);
        return policy;
    }
    private async Task Finish<T>(FactoryDeploymentOperation operation, string state, T result, Action<WorkspaceData> change, CancellationToken ct, string? error = null) =>
        await store.WriteAsync(data => { var current = Operation(data, operation.Id); if (!current.Pending) return; change(data); current.End(state, result, error); Find(data, current.DeploymentId).Release(current.Id); }, ct);
    private static IEnumerable<FactoryDeploymentOperation> Operations(WorkspaceData data, Guid id) => data.DeploymentOperations.Where(x => x.DeploymentId == id).OrderBy(x => x.CreatedAt);
    private static FactoryDeployment Find(WorkspaceData data, Guid id) => data.Deployments.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Deployment not found.", 404);
    private static FactoryDelivery Source(WorkspaceData data, Guid id) => data.Deliveries.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Source delivery not found.", 404);
    private static FactoryDeploymentOperation Operation(WorkspaceData data, Guid id) => data.DeploymentOperations.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Deployment operation not found.", 404);
    private static string Hash<T>(T value) => FactoryArtifact.Hash(FactoryRules.Serialize(value));
    private static void ReplayCapture(FactoryDeployment deployment, Guid id, string hash) { if (deployment.DeliveryId != id || deployment.RequestHash != hash) throw new RuleException("That request ID records a different deployment capture.", 409); }
}
