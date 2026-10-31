using System.Text.Json;
using Planner.Api.Domain;

namespace Planner.Api.Application;

public sealed class FactoryDeliveryService(IWorkspaceStore store, FactoryQualityService quality, IVerificationEnvironment verification,
    IDeliveryWorkspace workspace, IDeliveryProvider provider, IDeliveryHooks hooks, IAgentEnvironment agents)
{
    public async Task<DeliveryPolicyDetail> Policy(Guid projectId, CancellationToken ct) => Policy(await store.ReadAsync(ct), projectId);
    private DeliveryPolicyDetail Policy(WorkspaceData data, Guid id) => new(data.Graph(id).Project.Revision, data.DeliveryPolicies.SingleOrDefault(x => x.Id == id)?.Policy, hooks.List(), hooks.Targets());
    public async Task<DeliveryPolicyDetail> EditPolicy(Guid id, EditDeliveryPolicy input, CancellationToken ct)
    {
        if (input.Policy is null) throw new RuleException("Supply an explicit delivery policy.");
        await store.WriteAsync(data =>
        {
            var graph = data.Graph(id, input.ExpectedRevision); input.Policy.Validate();
            if (input.Policy.PostMergeHookId is { } hook && !hooks.List().Any(x => x.Id == hook)) throw new RuleException("Choose an administrator-configured post-merge hook.");
            if (input.Policy.DeploymentTargetIds?.Any(id => !hooks.Targets().Any(x => x.Id == id)) == true) throw new RuleException("Choose administrator-configured deployment targets.");
            var policy = data.DeliveryPolicies.SingleOrDefault(x => x.Id == id);
            if (policy is null) data.DeliveryPolicies.Add(ProjectDeliveryPolicy.Adopt(id, input.Policy)); else policy.Replace(input.Policy);
            graph.Project.Touch();
        }, ct);
        return await Policy(id, ct);
    }
    public async Task<DeliveryLibrary> List(Guid runId, CancellationToken ct)
    {
        var q = await quality.Read(runId, ct); var data = await store.ReadAsync(ct); var run = FactoryDispatchService.Find(data, runId);
        return new(runId, Policy(data, run.ProjectId), Local(data, run, q), data.Deliveries.Where(x => x.ItemId == run.ItemId).OrderByDescending(x => x.CreatedAt).ToList(), agents.PollIntervalMs);
    }
    public async Task<DeliveryDetail> Read(Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var delivery = Find(data, id); var run = FactoryDispatchService.Find(data, delivery.RunId); var q = await quality.Read(run.Id, ct);
        data = await store.ReadAsync(ct); delivery = Find(data, id); run = FactoryDispatchService.Find(data, delivery.RunId);
        var local = Local(data, run, q);
        try { RequirePolicy(data, delivery); RequireBound(data, run, delivery, q); }
        catch (RuleException error) { local = local with { Ready = false, Errors = local.Errors.Append(error.Message).Distinct().ToList() }; }
        return new(delivery, Operations(data, delivery).ToList(), local, FactoryDeliveryRules.MergeErrors(delivery, delivery.Observation, false), agents.PollIntervalMs);
    }
    public async Task<DeliveryDetail> Prepare(Guid runId, PrepareDelivery input, CancellationToken ct)
    {
        if (input.RequestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        var hash = Hash(new { runId, input }); var data = await store.ReadAsync(ct);
        var duplicate = data.Deliveries.SingleOrDefault(x => x.RequestId == input.RequestId);
        if (duplicate is not null) { ReplayCapture(duplicate, runId, hash); return await Read(duplicate.Id, ct); }
        var run = FactoryDispatchService.Find(data, runId); Rules.Revision(input.ExpectedRevision, run.Revision);
        var q = await quality.Read(runId, ct); var local = Local(data, run, q); Require(local.Errors);
        var policy = Policy(data, run.ProjectId).Policy ?? throw new RuleException("Adopt a project delivery policy first.");
        policy.Validate(); var remote = await provider.Observe(policy, null, null, null, ct); var git = await workspace.Inspect(run, policy, remote, ct);
        if (git.HeadSha != local.Context!.HeadCommit || (await verification.Observe(run, ct)).Fingerprint != local.Context.Fingerprint) throw new RuleException("Candidate changed during delivery capture.", 409);
        var capture = new DeliveryCapture(local.Context, FactoryQualityService.Source(data, run), local.ReviewId!.Value, local.ReviewHash!, local.QaHash!, git.HeadSha, git.TreeSha, remote.BaseSha, input.BranchName, provider.Binding, hooks.ConfigurationHash(policy.PostMergeHookId), FactoryDeliveryRules.MatchedFiles(git.ChangedFiles, policy.SensitivePaths), FactoryDeliveryRules.MatchedFiles(git.ChangedFiles, policy.MigrationPaths));
        var candidate = FactoryDelivery.Prepare(input.RequestId, hash, run, policy, capture); Guid id = candidate.Id;
        await store.WriteAsync(saved =>
        {
            var replay = saved.Deliveries.SingleOrDefault(x => x.RequestId == input.RequestId);
            if (replay is not null) { ReplayCapture(replay, runId, hash); id = replay.Id; return; }
            var current = FactoryDispatchService.Find(saved, runId); Rules.Revision(input.ExpectedRevision, current.Revision);
            if (saved.Deliveries.Any(x => x.ItemId == run.ItemId && x.Open)) throw new RuleException("Close the task's open delivery before capturing another.", 409);
            if (Hash(Policy(saved, run.ProjectId).Policy) != Hash(policy)) throw new RuleException("Delivery policy changed during capture.", 409);
            RequireBound(saved, current, candidate, FactoryQualityService.Detail(saved, current, local.Context, null));
            saved.Deliveries.Add(candidate);
        }, ct);
        return await Read(id, ct);
    }
    public async Task<DeliveryDetail> Act(Guid id, ActOnDelivery input, CancellationToken ct)
    {
        var hash = Hash(new { id, input });
        await store.WriteAsync(data =>
        {
            if (Replay(data, id, input.RequestId, hash)) return;
            var delivery = Find(data, id); Rules.Revision(input.ExpectedRevision, delivery.Revision);
            if (input.Action is "approveMerge" or "approvePostMerge") FactoryReviewRound.Required(input.ApprovalNote, "Approval rationale and sensitive-change/migration notes");
            var operation = FactoryDeliveryOperation.Create(id, input.RequestId, input.Action, hash, input.ApprovalNote);
            if (input.Action is not ("reconcile" or "close"))
            {
                RequirePolicy(data, delivery);
                if (Operations(data, delivery).Any(x => x.State == "uncertain" && x.Kind is "publish" or "merge" or "postMerge")) throw new RuleException("Reconcile the unknown external outcome before another mutation.", 409);
                if (input.Action == "publish" && delivery.Remote is not null) throw new RuleException("This delivery already has a recorded PR.", 409);
                if (input.Action == "merge" && (delivery.State == "mergePending" || delivery.Remote?.MergeRequestId is not null)) throw new RuleException("Observe the pending merge instead of submitting it again.", 409);
                if (input.Action == "postMerge" && delivery.Hook is not null) throw new RuleException("A post-merge hook was already attempted. Reconcile its outcome; it will not be repeated.", 409);
                if (input.Action is "approvePostMerge" or "postMerge" && delivery.Policy.PostMergeHookId is { } hookId && data.Deployments.Any(x => x.Open && x.Capture.ResourceKey == hooks.ResourceKey(hookId))) throw new RuleException("An open deployment reserves this target. Finish or explicitly close it first.", 409);
            }
            delivery.Queue(operation.Id); data.DeliveryOperations.Add(operation);
        }, ct);
        return await Read(id, ct);
    }
    public async Task Execute(Guid operationId, CancellationToken ct)
    {
        var begun = await store.WriteAsync(data => { var operation = Operation(data, operationId); if (operation.State == "queued") operation.Begin(); }, ct);
        var op = Operation(begun, operationId); if (op.State != "inFlight") return;
        var delivery = Find(begun, op.DeliveryId); var run = FactoryDispatchService.Find(begun, delivery.RunId); var sent = false;
        try
        {
            if (op.Kind == "close")
            { await Finish(op.Id, data => Find(data, delivery.Id).Change("closed", "Operator closed delivery tracking; remote PRs and merges are retained."), new { closed = true }, ct); return; }
            if (op.Kind == "reconcile") { await Reconcile(op.Id, delivery, ct); return; }
            RequirePolicy(begun, delivery);
            var q = await quality.Read(run.Id, ct); var data = await store.ReadAsync(ct); delivery = Find(data, delivery.Id); run = FactoryDispatchService.Find(data, delivery.RunId);
            RequireBound(data, run, delivery, q);
            var observed = await provider.Observe(delivery.Policy, delivery.Capture, delivery.Remote, delivery.Marker, ct);
            await Observe(op.Id, observed, ct);
            if (op.Kind is "publish" or "approveMerge" or "merge")
            {
                var git = await workspace.Inspect(run, delivery.Policy, observed, ct);
                if (git.HeadSha != delivery.Capture.HeadSha || git.TreeSha != delivery.Capture.TreeSha || observed.BaseSha != delivery.Capture.BaseSha) throw new RuleException("Head or target base changed. Capture a new delivery after renewed verification/review.", 409);
            }
            if (op.Kind == "publish")
            {
                if (!delivery.Policy.AllowDraftPr) throw new RuleException("Project policy does not authorize publication.", 409);
                await CheckCurrent(delivery, ct);
                sent = true;
                await workspace.Publish(run, delivery, observed, ct);
                observed = await provider.Observe(delivery.Policy, delivery.Capture, null, delivery.Marker, ct);
                if (observed.BranchSha != delivery.Capture.HeadSha || observed.BaseSha != delivery.Capture.BaseSha) throw new RuleException("Remote publication head/base changed before PR creation.", 409);
                var remote = observed.PullRequest;
                if (remote is null)
                {
                    remote = await provider.CreateDraft(delivery, run.Snapshot.Title,
                        $"Factory work order `{run.Id}`\n\nReviewed commit `{delivery.Capture.HeadSha}`, target `{delivery.Capture.BaseSha}`.\nIndependent review `{delivery.Capture.ReviewId}`; QA digest `{delivery.Capture.QaHash}`.\n\nTask acceptance remains explicit.", ct);
                    if (!remote.Draft) throw new RuleException("The provider did not establish the requested draft state. Reconcile the actual PR.", 409);
                }
                if (remote.HeadSha != delivery.Capture.HeadSha || remote.BaseSha != delivery.Capture.BaseSha) throw new RuleException("Created PR no longer matches the captured delivery.", 409);
                await Finish(op.Id, saved => { var d = Find(saved, delivery.Id); d.RecordRemote(remote); d.Change("prCreated", "Draft PR recorded. Mark it ready on GitHub, then obtain explicit merge approval."); }, new { remote }, ct); return;
            }
            if (op.Kind is "approveMerge" or "merge")
            {
                if (observed.PullRequest is not null) delivery.RecordRemote(observed.PullRequest with { MergeRequestId = delivery.Remote?.MergeRequestId });
                Require(FactoryDeliveryRules.MergeErrors(delivery, observed, op.Kind == "merge"));
                if (op.Kind == "approveMerge") { await Finish(op.Id, saved => { var d = Find(saved, delivery.Id); d.RecordRemote(observed.PullRequest!); d.Approve("merge", op.ApprovalNote); }, new { action = "merge", binding = delivery.Binding }, ct); return; }
                // Check the local evidence and policy again after provider I/O, before dispatch.
                await CheckCurrent(delivery, ct);
                sent = true; var result = await provider.Merge(delivery, ct);
                await SaveMergeResult(op.Id, delivery, result, ct); return;
            }
            if (delivery.Remote?.MergeSha is null || delivery.Policy.PostMergeHookId is not { } hookId) throw new RuleException("A reconciled merge and configured post-merge hook are required.", 409);
            await VerifyMerge(delivery, delivery.Remote.MergeSha, ct);
            if (op.Kind == "approvePostMerge")
            {
                if (!hooks.List().Any(x => x.Id == hookId && x.Ready)) throw new RuleException("The captured post-merge hook is unavailable.", 409);
                await Finish(op.Id, saved => Find(saved, delivery.Id).Approve("postMerge", op.ApprovalNote), new { action = "postMerge", binding = delivery.Binding }, ct); return;
            }
            if (op.Kind != "postMerge" || !delivery.Approved("postMerge")) throw new RuleException("Explicit approval for this merged revision's post-merge action is required.", 409);
            if (delivery.Hook is not null) throw new RuleException("The post-merge hook cannot be replayed.", 409);
            var before = await hooks.Probe(hookId, ct); await CheckCurrent(delivery, ct);
            await store.WriteAsync(saved => Find(saved, delivery.Id).RecordHook(new(before, null, null, "", false, "Hook intent recorded; outcome is not yet established.")), ct);
            sent = true; var receipt = await hooks.Run(hookId, delivery, before, ct);
            await Finish(op.Id, saved => { var d = Find(saved, delivery.Id); d.RecordHook(receipt); d.Change(receipt.Error is null ? "delivered" : "needsAttention", receipt.Error ?? "Merged revision, new application instance, health and persistence were verified."); }, receipt, ct, receipt.Error is null ? "succeeded" : "uncertain");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            var reason = error is RuleException ? error.Message : "Delivery could not establish an outcome. Inspect and reconcile retained receipts.";
            await Finish(op.Id, saved => Find(saved, op.DeliveryId).Change("needsAttention", reason), new { outcomeEstablished = false }, CancellationToken.None, sent ? "uncertain" : "failed", reason);
        }
    }
    private async Task Reconcile(Guid operationId, FactoryDelivery delivery, CancellationToken ct)
    {
        if (provider.Binding != delivery.Capture.ProviderBinding || hooks.ConfigurationHash(delivery.Policy.PostMergeHookId) != delivery.Capture.HookHash) throw new RuleException("Provider or hook configuration changed; restore the captured configuration before reconciliation.", 409);
        var observed = await provider.Observe(delivery.Policy, delivery.Capture, delivery.Remote, delivery.Marker, ct);
        await Observe(operationId, observed, ct);
        if (observed.PullRequest is not { } remote)
        { await Finish(operationId, saved => Find(saved, delivery.Id).Change("needsAttention", "No matching PR was observed. Unknown publication intents are retained and will not be replayed."), observed, ct); return; }
        remote = remote with { MergeRequestId = delivery.Remote?.MergeRequestId }; delivery.RecordRemote(remote);
        if (remote.HeadSha != delivery.Capture.HeadSha) throw new RuleException("The actual PR head changed. Existing approval cannot authorize it.", 409);
        if (remote.MergeSha is null && remote.MergeRequestId is not null)
        { await SaveMergeResult(operationId, delivery, await provider.MergeResult(delivery, ct), ct); return; }
        if (remote.MergeSha is not null)
        {
            await VerifyMerge(delivery, remote.MergeSha, ct);
            if (delivery.Hook is { } hook && delivery.Policy.PostMergeHookId is { } hookId)
            {
                var after = await hooks.Probe(hookId, ct);
                if (after.Revision != remote.MergeSha || after.Instance == hook.Before.Instance || after.SnapshotHash != hook.Before.SnapshotHash || hook.ExitCode is not (null or 0)) throw new RuleException("Restart/health/persistence is not verified. Retained hook will not be repeated.", 409);
                var receipt = hook with { After = after, Error = null };
                await Finish(operationId, saved => { var d = Find(saved, delivery.Id); d.RecordRemote(remote); d.RecordHook(receipt); ResolveUnknown(saved, d, "postMerge", receipt); d.Change("delivered", "Observed merged revision, changed application instance, health and intact persistence after reconciliation."); }, receipt, ct); return;
            }
            await Finish(operationId, saved => { var d = Find(saved, delivery.Id); d.RecordRemote(remote); ResolveUnknown(saved, d, "merge", remote); ResolveUnknown(saved, d, "publish", remote); d.Change(d.Policy.PostMergeHookId is null ? "delivered" : "merged", "Actual merge commit, verified tree, parents and target ancestry reconciled." + (d.Policy.PostMergeHookId is null ? " No post-merge hook is configured." : " Post-merge action requires separate approval.")); }, remote, ct); return;
        }
        await Finish(operationId, saved => { var d = Find(saved, delivery.Id); d.RecordRemote(remote); ResolveUnknown(saved, d, "publish", remote); d.Change("prCreated", "Matching provider PR observed. Unknown merge outcomes remain blocked until established."); }, remote, ct);
    }
    private async Task SaveMergeResult(Guid operationId, FactoryDelivery delivery, DeliveryMergeResult result, CancellationToken ct)
    {
        // Persist provider facts before further I/O can fail. A returned SHA/request ID
        // is evidence even when subsequent ancestry/health inspection is unavailable.
        await store.WriteAsync(data =>
        {
            var d = Find(data, delivery.Id); var op = Operation(data, operationId); op.Receipt(result);
            if (d.Remote is not null) d.RecordRemote(d.Remote with { MergeSha = result.Sha ?? d.Remote.MergeSha,
                State = result.Status == "merged" ? "merged" : d.Remote.State,
                MergeRequestId = result.Status == "failed" ? null : result.RequestId ?? d.Remote.MergeRequestId });
        }, ct);
        if (result.Status == "merged" && result.Sha is { } sha)
        {
            await VerifyMerge(delivery, sha, ct);
            await Finish(operationId, saved => { var d = Find(saved, delivery.Id); d.RecordRemote(delivery.Remote! with { MergeSha = sha, State = "merged", MergeRequestId = result.RequestId ?? delivery.Remote!.MergeRequestId }); ResolveUnknown(saved, d, "merge", result); d.Change(d.Policy.PostMergeHookId is null ? "delivered" : "merged", "Actual merge commit, tree, parents and target ancestry verified." + (d.Policy.PostMergeHookId is null ? " No post-merge hook is configured." : " Approve the post-merge action separately.")); }, result, ct); return;
        }
        var state = result.Status == "pending" ? "mergePending" : "needsAttention";
        await Finish(operationId, saved => { var d = Find(saved, delivery.Id); d.RecordRemote(delivery.Remote! with { MergeRequestId = result.Status == "failed" ? null : result.RequestId ?? delivery.Remote!.MergeRequestId }); if (result.Status == "failed") foreach (var op in Operations(saved, d).Where(x => x.Kind == "merge")) op.Resolve("failed", result); d.Change(state, result.Status == "pending" ? "GitHub accepted a pending merge. Reconcile its actual outcome; it is not delivery success." : "Provider merge is " + result.Status + ". Inspect the retained result; no merge will be replayed."); }, result, ct);
    }
    public async Task VerifyMerge(FactoryDelivery delivery, string sha, CancellationToken ct)
    {
        var commit = await provider.Commit(delivery.Policy, sha, ct);
        if (commit.Sha != sha || commit.TreeSha != delivery.Capture.TreeSha || commit.Parents.Count != 2 || commit.Parents[0] != delivery.Capture.BaseSha || commit.Parents[1] != delivery.Capture.HeadSha || !await provider.TargetContains(delivery.Policy, sha, ct))
            throw new RuleException("Actual merge tree, parent commits or target ancestry do not match captured authority. Delivery needs attention.", 409);
    }
    private async Task CheckCurrent(FactoryDelivery delivery, CancellationToken ct)
    {
        var q = await quality.Read(delivery.RunId, ct); var data = await store.ReadAsync(ct); var current = Find(data, delivery.Id); RequirePolicy(data, current);
        RequireBound(data, FactoryDispatchService.Find(data, current.RunId), current, q);
        if (current.Binding != delivery.Binding) throw new RuleException("Delivery authority changed during preflight.", 409);
    }
    private async Task Observe(Guid operationId, DeliveryObservation observation, CancellationToken ct) => await store.WriteAsync(data => Find(data, Operation(data, operationId).DeliveryId).Observe(observation), ct);
    private async Task Finish<T>(Guid operationId, Action<WorkspaceData> apply, T receipt, CancellationToken ct, string state = "succeeded", string? error = null)
    {
        await store.WriteAsync(data => { var op = Operation(data, operationId); if (!op.Pending) return; apply(data); op.Receipt(new { previous = op.Result, receipt }); op.End(state, error); Find(data, op.DeliveryId).Release(op.Id); }, ct);
    }
    private static void ResolveUnknown<T>(WorkspaceData data, FactoryDelivery delivery, string kind, T proof) { foreach (var op in Operations(data, delivery).Where(x => x.Kind == kind && x.State == "uncertain")) op.Resolve("succeeded", proof); }
    private static DeliveryLocalReadiness Local(WorkspaceData data, FactoryRun run, FactoryQualityDetail q)
    {
        q = FactoryQualityService.Detail(data, run, q.Context, q.ContextError); var errors = new List<string>();
        if (data.CalibrationBusy(run.ItemId)) errors.Add("Finish or reconcile active task calibration first.");
        if (run.State != "succeeded") errors.Add("Finish a successful factory run before delivery.");
        if (q.Context is null) errors.Add(q.ContextError ?? "Current verification context is unavailable.");
        if (q.SourceChanged) errors.Add("Task specification, criteria or attachment changed; prepare current work.");
        if (!data.VerificationReceipts.Where(x => x.RunId == run.Id).Any(receipt => data.FactoryStepRuns.SingleOrDefault(x => x.Id == receipt.StepRunId) is { State: "succeeded", ExitCode: 0 } step && receipt.Matches(q.Context, step))) errors.Add("A current successful verification command receipt is required for delivery.");
        if (q.Criteria.Count == 0 || q.Criteria.Any(x => x.Status is not ("pass" or "notApplicable"))) errors.Add("Current complete criterion QA is required.");
        if (data.FactoryRuns.Any(x => x.ItemId == run.ItemId && x.Active) || data.AgentRuns.Any(x => x.ItemId == run.ItemId && x.Active) || data.RepairLoops.Any(x => x.ItemId == run.ItemId && x.Open)) errors.Add("Finish active task execution and repair loops first.");
        if (data.ReviewRounds.Any(x => x.ItemId == run.ItemId && x.Open)) errors.Add("Finish or abandon the open independent review.");
        var review = data.ReviewRounds.Where(x => x.ItemId == run.ItemId && x.State == "submitted").OrderByDescending(x => x.Number).FirstOrDefault();
        if (review is null || review.RunId != run.Id || review.Submission?.Verdict != "approve" || FactoryReviewService.Stale(review, data, run, q) is not null) errors.Add("A current independent approval of this exact run is required.");
        return new(errors.Count == 0, run.Revision, q.Context, review?.Id, review is null ? null : Hash(new { review.Id, review.Revision, review.Submission }), Hash(q.Criteria.Select(x => new { x.Criterion.Id, x.Status, verdictId = x.History.FirstOrDefault()?.Id })), errors);
    }
    private void RequireBound(WorkspaceData data, FactoryRun run, FactoryDelivery delivery, FactoryQualityDetail q)
    {
        var local = Local(data, run, q); Require(local.Errors); var capture = delivery.Capture;
        if (capture.Context.Fingerprint != local.Context?.Fingerprint || capture.SourceHash != FactoryQualityService.Source(data, run) || capture.ReviewId != local.ReviewId || capture.ReviewHash != local.ReviewHash || capture.QaHash != local.QaHash) throw new RuleException("Captured QA/review or code changed. Capture a new delivery after verification.", 409);
    }
    private void RequirePolicy(WorkspaceData data, FactoryDelivery delivery)
    { if (provider.Binding != delivery.Capture.ProviderBinding || hooks.ConfigurationHash(delivery.Policy.PostMergeHookId) != delivery.Capture.HookHash || Hash(Policy(data, delivery.ProjectId).Policy) != Hash(delivery.Policy) || !delivery.Policy.Enabled) throw new RuleException("Delivery policy/provider/hook configuration changed or was revoked. Close this record and capture new authority.", 409); }
    private static void Require(IReadOnlyList<string> errors) { if (errors.Count > 0) throw new RuleException(string.Join(" ", errors), 409); }
    private static FactoryDelivery Find(WorkspaceData data, Guid id) => data.Deliveries.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Delivery not found.", 404);
    private static FactoryDeliveryOperation Operation(WorkspaceData data, Guid id) => data.DeliveryOperations.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Delivery operation not found.", 404);
    private static IEnumerable<FactoryDeliveryOperation> Operations(WorkspaceData data, FactoryDelivery delivery) => data.DeliveryOperations.Where(x => x.DeliveryId == delivery.Id).OrderBy(x => x.CreatedAt);
    private static string Hash<T>(T value) => FactoryArtifact.Hash(JsonSerializer.Serialize(value, FactoryRules.Json));
    private static void ReplayCapture(FactoryDelivery delivery, Guid runId, string hash) { if (delivery.RunId != runId || delivery.RequestHash != hash) throw new RuleException("That request ID records a different delivery capture.", 409); }
    private static bool Replay(WorkspaceData data, Guid id, Guid requestId, string hash)
    { var op = data.DeliveryOperations.SingleOrDefault(x => x.RequestId == requestId); if (op is not null && (op.DeliveryId != id || op.RequestHash != hash)) throw new RuleException("That request ID records a different delivery action.", 409); return op is not null; }
}
