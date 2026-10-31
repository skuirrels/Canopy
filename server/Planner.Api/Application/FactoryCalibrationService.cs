using System.Text;
using System.Text.Json;
using Planner.Api.Domain;
namespace Planner.Api.Application;

public sealed class FactoryCalibrationService(IWorkspaceStore store, ICalibrationWorkspace workspace, IAgentEnvironment agents,
    FactoryDeliveryService deliveries, IDeliveryProvider provider, IVerificationEnvironment verification, FactoryCalibrationOptions limits)
{
    public async Task<CalibrationPolicyDetail> Policy(Guid project, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); return new(data.Graph(project).Project.Revision, data.CalibrationPolicies.SingleOrDefault(x => x.Id == project)?.Policy,
            agents.Providers(), workspace.ScopeConfigured, workspace.ProcessLifetimeSupported);
    }
    public async Task<CalibrationPolicyDetail> EditPolicy(Guid project, EditCalibrationPolicy input, CancellationToken ct)
    {
        var policy = (input.Policy ?? throw new RuleException("Supply an explicit calibration policy.")).Normalize();
        if (policy.Targets.Count > limits.MaximumTargets) throw new RuleException("Calibration target count exceeds configured capacity.");
        if (policy.Execution is { } execution) foreach (var route in execution.Routes)
            if (route.Effort is not null && !agents.Providers().Single(x => x.Id == route.Provider).EffortLevels.Contains(route.Effort)) throw new RuleException("The selected effort is unavailable for this provider.");
        await store.WriteAsync(data =>
        {
            var graph = data.Graph(project, input.ExpectedRevision); var existing = data.CalibrationPolicies.SingleOrDefault(x => x.Id == project);
            if (existing is null) data.CalibrationPolicies.Add(ProjectCalibrationPolicy.Adopt(project, policy)); else existing.Replace(policy);
            graph.Project.Touch();
        }, ct);
        await Enforce(ct); return await Policy(project, ct);
    }
    public async Task<CalibrationLibrary> Library(Guid delivery, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var source = Source(data, delivery); var errors = await Errors(data, source, ct);
        return new(new(delivery, source.Revision, errors.Count == 0, errors), data.Calibrations.Where(x => x.DeliveryId == delivery).OrderByDescending(x => x.CreatedAt).ToList(), data.CalibrationTriggers.Where(x => x.DeliveryId == delivery).OrderByDescending(x => x.CreatedAt).ToList(), agents.PollIntervalMs);
    }
    private async Task<List<string>> Errors(WorkspaceData data, FactoryDelivery delivery, CancellationToken ct, Guid? exceptJob = null)
    {
        var caller = ct; using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(limits.ObservationTimeoutSeconds)); ct = deadline.Token;
        var errors = LocalErrors(data, delivery, exceptJob).ToList();
        var policy = data.CalibrationPolicies.SingleOrDefault(x => x.Id == delivery.ProjectId)?.Policy;
        if (policy is not { Enabled: true }) errors.Add("Calibration is disabled; configure an installed calibrator, existing targets and explicit execution limits.");
        if (!workspace.ScopeConfigured) errors.Add("No administrator-configured canonical skill edit roots are available.");
        if (!workspace.ProcessLifetimeSupported) errors.Add("Owned calibration process lifetime is unsupported on this backend.");
        if (errors.Count > 0) return errors;
        try
        {
            var repository = data.FactoryRuns.Single(x => x.Id == delivery.RunId).Snapshot.Environment.RepositoryPath;
            await workspace.Resolve(repository, [policy!.Calibrator!], false, ct); await workspace.Resolve(repository, policy.Targets, true, ct);
            var route = policy.Execution!.Routes.Single(); if (!agents.Providers().Single(x => x.Id == route.Provider).Available) throw new RuleException("The calibration provider executable is unavailable.");
            if (provider.Binding != delivery.Capture.ProviderBinding) throw new RuleException("Delivery provider binding changed; current merge evidence is unavailable.");
            var run = data.FactoryRuns.Single(x => x.Id == delivery.RunId);
            if ((await verification.Observe(run, ct)).Fingerprint != delivery.Capture.Context.Fingerprint) throw new RuleException("Delivered source verification inputs changed; the captured context is stale.");
            await deliveries.VerifyMerge(delivery, delivery.Remote!.MergeSha!, ct);
            var observation = await provider.Observe(delivery.Policy, delivery.Capture, delivery.Remote, null, ct);
            if (observation.PullRequest is not { } pr || pr.State != "merged" || pr.Number != delivery.Remote.Number || pr.HeadSha != delivery.Capture.HeadSha || pr.MergeSha != delivery.Remote.MergeSha) throw new RuleException("Fresh provider PR evidence does not identify the captured merged work.");
            foreach (var required in delivery.Policy.RequiredChecks)
                if (!observation.Checks.Any(x => x.Name == required.Name && x.AppId == required.AppId && x.HeadSha == delivery.Capture.HeadSha && x.Status == "completed" && x.Conclusion == "success")) throw new RuleException("Fresh required check evidence is unavailable for the reviewed head.");
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested) { errors.Add("Calibration readiness observation exceeded its configured time bound."); }
        catch (Exception error) when (error is RuleException or IOException or UnauthorizedAccessException) { errors.Add(error.Message); }
        return errors;
    }
    public async Task<FactoryCalibration> Prepare(Guid deliveryId, PrepareCalibration input, CancellationToken ct, string? automaticPolicyHash = null, DateTime? automaticActivation = null)
    {
        var hash = FactoryArtifact.Hash(FactoryRules.Serialize(new { deliveryId, input })); var data = await store.ReadAsync(ct);
        var replay = data.Calibrations.SingleOrDefault(x => x.RequestId == input.RequestId);
        if (replay is not null) { if (replay.RequestHash != hash) throw new RuleException("That request ID belongs to a different calibration capture.", 409); return replay; }
        if (automaticPolicyHash is not null) RequireAutomatic(data, Source(data, deliveryId).ProjectId, automaticPolicyHash, automaticActivation!.Value);
        var source = Source(data, deliveryId); Rules.Revision(input.ExpectedRevision, source.Revision);
        var errors = await Errors(data, source, ct); if (errors.Count > 0) throw new RuleException(string.Join(" ", errors), 409);
        var policy = data.CalibrationPolicies.Single(x => x.Id == source.ProjectId).Policy;
        var existing = data.Calibrations.SingleOrDefault(x => x.DeliveryId == deliveryId && x.PolicyHash == policy.Hash); if (existing is not null) return existing;
        var run = data.FactoryRuns.Single(x => x.Id == source.RunId); var repository = run.Snapshot.Environment.RepositoryPath;
        var calibrator = (await workspace.Resolve(repository, [policy.Calibrator!], false, ct)).Single(); var targets = await workspace.Resolve(repository, policy.Targets, true, ct);
        var steps = data.FactoryStepRuns.Where(x => x.RunId == run.Id).ToList(); var reviews = data.ReviewRounds.Where(x => x.RunId == run.Id).ToList();
        var receipts = data.VerificationReceipts.Where(x => x.RunId == run.Id).ToList(); var verdicts = data.CriterionVerdicts.Where(x => x.RunId == run.Id).ToList();
        var findings = data.ReviewFindings.Where(x => reviews.Any(r => r.Id == x.RoundId)).ToList(); var operations = data.DeliveryOperations.Where(x => x.DeliveryId == source.Id).OrderBy(x => x.CreatedAt).ToList();
        var attempts = data.RepairAttempts.Where(x => x.RunId == run.Id).ToList();
        if (steps.Count + reviews.Count + receipts.Count + verdicts.Count + findings.Count + operations.Count + attempts.Count > limits.MaximumEvidenceItems) throw new RuleException("Current-delivery evidence exceeds configured item bounds; no incomplete packet was captured.");
        var packet = FactoryRules.Serialize(new { version = 1, source.Id, source.RunId, source.Capture, source.Remote, task = new { run.Snapshot.Title, run.Snapshot.SpecJson, run.Snapshot.Criteria },
            usedSkills = run.Snapshot.Environment.Skills.Select(x => new { x.Id, x.Source, x.Hash }), steps, receipts, verdicts, reviews, findings, attempts, operations });
        if (Encoding.UTF8.GetByteCount(packet) > limits.MaximumPacketBytes) throw new RuleException("Current-delivery evidence exceeds configured byte bounds; no truncated packet was captured.");
        var capture = new CalibrationCapture(source.Binding, source.Revision, repository, source.Policy.RemoteName, source.Policy.TargetBranch, source.Remote!.MergeSha!, policy, calibrator, targets, packet, FactoryArtifact.Hash(packet), workspace.ScopeHash, DateTime.UtcNow);
        var job = FactoryCalibration.Prepare(input.RequestId, hash, source, capture); var id = job.Id;
        await store.WriteAsync(saved =>
        {
            var duplicate = saved.Calibrations.SingleOrDefault(x => x.RequestId == input.RequestId); if (duplicate is not null) { if (duplicate.RequestHash != hash) throw new RuleException("That request ID records a different capture.", 409); id = duplicate.Id; return; }
            var prior = saved.Calibrations.SingleOrDefault(x => x.DeliveryId == deliveryId && x.PolicyHash == policy.Hash); if (prior is not null) { id = prior.Id; return; }
            if (automaticPolicyHash is not null) RequireAutomatic(saved, source.ProjectId, automaticPolicyHash, automaticActivation!.Value);
            Require(saved, job); saved.Calibrations.Add(job);
        }, ct); return Find(await store.ReadAsync(ct), id);
    }
    public static void RequireAutomatic(WorkspaceData data, Guid project, string hash, DateTime activation)
    {
        var current = data.CalibrationPolicies.SingleOrDefault(x => x.Id == project);
        if (current is null || current.Policy is not { Enabled: true, AutoRun: true } || current.Policy.Hash != hash || current.ActivatedAt != activation) throw new RuleException("Automatic calibration policy or activation boundary changed.", 409);
    }
    public async Task QueueAutomatic(Guid id, string hash, DateTime activation, CancellationToken ct)
    {
        await store.WriteAsync(data =>
        {
            var job = Find(data, id); RequireAutomatic(data, job.ProjectId, hash, activation); Require(data, job);
            if (job.State != "prepared") return;
            var request = Guid.NewGuid(); job.Act(request, job.Revision, "start", FactoryArtifact.Hash(FactoryRules.Serialize(new { automatic = true, id, request, hash, activation })), null);
        }, ct);
    }
    public async Task<FactoryCalibration> Act(Guid id, ActOnCalibration input, CancellationToken ct)
    {
        var hash = FactoryArtifact.Hash(FactoryRules.Serialize(new { id, input }));
        var saved = await store.WriteAsync(data =>
        {
            if (data.Calibrations.Any(x => x.Id != id && x.Actions.Any(a => a.RequestId == input.RequestId))) throw new RuleException("That action ID belongs to another calibration.", 409);
            var job = Find(data, id); if (job.Replay(input.RequestId, input.Action, hash)) return;
            if (input.Action is "start" or "apply") Require(data, job);
            job.Act(input.RequestId, input.ExpectedRevision, input.Action, hash, input.ProposalHash, input.Note);
            if (input.Action == "cancel" && job.AgentRunId is { } child) AgentService.Find(data, child).Cancel();
        }, ct);
        var current = Find(saved, id); if (input.Action == "cancel") { agents.Cancel(id); if (current.AgentRunId is { } child) agents.Cancel(child); }
        return current;
    }
    public async Task Validate(FactoryCalibration job, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(limits.ObservationTimeoutSeconds));
        var data = await store.ReadAsync(deadline.Token); Require(data, job);
        var errors = await Errors(data, Source(data, job.DeliveryId), deadline.Token, job.Id);
        if (errors.Count > 0) throw new RuleException(string.Join(" ", errors), 409);
        var current = (await workspace.Resolve(job.Capture.RepositoryPath, [job.Capture.Calibrator.Reference], false, deadline.Token)).Single();
        if (current.Path != job.Capture.Calibrator.Path || current.Hash != job.Capture.Calibrator.Hash) throw new RuleException("The captured calibrator changed.", 409);
        foreach (var target in job.Capture.Targets)
            if (await workspace.Current(target, job.Capture.RepositoryPath, deadline.Token) != target.Hash) throw new RuleException("A canonical skill changed; fresh calibration is required.", 409);
    }
    public async Task<FactoryCalibration> Reconcile(Guid id, ActOnCalibration input, CancellationToken ct)
    {
        if (input.Action != "reconcile") throw new RuleException("Choose the reconciliation action.");
        var job = await Act(id, input, ct); if (job.State != "needsAttention") return job;
        var observed = new List<CalibrationWrite>();
        foreach (var edit in job.Edits)
        {
            try
            {
                var actual = await workspace.Current(job.Capture.Targets[edit.Target], job.Capture.RepositoryPath, ct, false);
                observed.Add(new(edit.Target, actual == edit.AfterHash ? "applied" : actual == edit.BeforeHash ? "pending" : "conflict", actual, actual == edit.BeforeHash || actual == edit.AfterHash ? null : "Canonical file differs from both captured versions; preserve external changes.", DateTime.UtcNow));
            }
            catch (RuleException error) { observed.Add(new(edit.Target, "conflict", null, error.Message, DateTime.UtcNow)); }
        }
        var saved = await store.WriteAsync(data => { var current = Find(data, id); if (current.State != "needsAttention") return; foreach (var result in observed) current.Write(result.Target, result.State, result.ActualHash, result.Error); current.Reconciled(); }, ct);
        return Find(saved, id);
    }
    public async Task<FactoryCalibration> Read(Guid id, CancellationToken ct) => Find(await store.ReadAsync(ct), id);
    public static FactoryCalibration Find(WorkspaceData data, Guid id) => data.Calibrations.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Calibration not found.", 404);
    public static FactoryDelivery Source(WorkspaceData data, Guid id) => data.Deliveries.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Delivery not found.", 404);
    public static IEnumerable<string> LocalErrors(WorkspaceData data, FactoryDelivery source, Guid? exceptJob)
    {
        if (source.State != "delivered" || source.Remote is not { State: "merged", MergeSha: not null } || source.CurrentOperationId is not null) yield return "A fully reconciled delivered merge is required.";
        if (data.FactoryRuns.Any(x => x.ItemId == source.ItemId && x.Active) || data.AgentRuns.Any(x => x.ItemId == source.ItemId && x.Active && (exceptJob is null || x.CalibrationJobId != exceptJob)) || data.RepairLoops.Any(x => x.ItemId == source.ItemId && x.Open) || data.ReviewRounds.Any(x => x.ItemId == source.ItemId && x.Open)) yield return "Finish active task work, reviews and repair loops first.";
        if (data.Calibrations.Any(x => x.ItemId == source.ItemId && x.Id != exceptJob && (x.Active || x.State == "needsAttention"))) yield return "Finish or reconcile the task's current calibration first.";
        if (data.DeliveryOperations.Any(x => x.DeliveryId == source.Id && (x.Pending || x.State == "uncertain")) || data.Deployments.Any(x => x.DeliveryId == source.Id && x.Open)) yield return "Finish selected delivery and deployment operations before calibration.";
        var run = data.FactoryRuns.Single(x => x.Id == source.RunId);
        if (FactoryQualityService.Source(data, run) != source.Capture.SourceHash) yield return "Task inputs changed since delivery; historical work does not authorize current calibration.";
        var qa = FactoryQualityService.Detail(data, run, source.Capture.Context, null);
        if (FactoryArtifact.Hash(FactoryRules.Serialize(qa.Criteria.Select(x => new { x.Criterion.Id, x.Status, verdictId = x.History.FirstOrDefault()?.Id }))) != source.Capture.QaHash || !data.VerificationReceipts.Any(x => x.RunId == run.Id && data.FactoryStepRuns.SingleOrDefault(s => s.Id == x.StepRunId) is { State: "succeeded", ExitCode: 0 } step && x.Matches(source.Capture.Context, step))) yield return "Captured QA or command evidence changed or is unavailable.";
        if (!data.ReviewRounds.Any(x => x.Id == source.Capture.ReviewId && x.Submission?.Verdict == "approve" && FactoryArtifact.Hash(FactoryRules.Serialize(new { x.Id, x.Revision, x.Submission })) == source.Capture.ReviewHash)) yield return "Captured independent review proof is unavailable or changed.";
    }
    public void Require(WorkspaceData data, FactoryCalibration job)
    {
        var policy = data.CalibrationPolicies.SingleOrDefault(x => x.Id == job.ProjectId)?.Policy;
        if (policy is not { Enabled: true } || policy.Hash != job.PolicyHash || job.Capture.ScopeHash != workspace.ScopeHash || !workspace.ProcessLifetimeSupported) throw new RuleException("Calibration policy, edit scope or process capability changed.", 409);
        var source = Source(data, job.DeliveryId);
        if (source.Revision != job.Capture.DeliveryRevision || source.Binding != job.Capture.DeliveryBinding || data.AgentConnections.SingleOrDefault(x => x.ProjectId == job.ProjectId)?.RepositoryPath != job.Capture.RepositoryPath) throw new RuleException("Captured delivery or repository connection changed.", 409);
        if (LocalErrors(data, source, job.Id).FirstOrDefault() is { } reason) throw new RuleException(reason, 409);
        if (job.Capture.Policy.Execution!.Exceeded(job.StartedAt, DateTime.UtcNow, job.AgentRunId.HasValue ? 1 : 0, 0) is { } limit && job.Active && job.State != "applying") throw new RuleException(limit, 409);
    }
    public async Task Enforce(CancellationToken ct)
    {
        var data = await store.ReadAsync(ct);
        foreach (var record in data.Calibrations.Where(x => x.Active || x.State is "prepared" or "proposed"))
        {
            try { Require(data, record); }
            catch (RuleException error)
            {
                await store.WriteAsync(saved => { var job = Find(saved, record.Id); if (job.AgentRunId is { } id && saved.AgentRuns.SingleOrDefault(x => x.Id == id) is { Active: true } child) child.Cancel(); if (job.State is "prepared" or "queued" or "proposed") job.Stop("failed", error.Message); else job.RetainFailure(error.Message); }, ct);
                agents.Cancel(record.Id); if (record.AgentRunId is { } id) agents.Cancel(id);
            }
        }
    }
}
