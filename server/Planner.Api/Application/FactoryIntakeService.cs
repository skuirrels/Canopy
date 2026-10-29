using System.Text;
using System.Text.Json;
using Planner.Api.Domain;
namespace Planner.Api.Application;

public sealed class IntakeLimits
{
    public int MaximumCollectionBytes { get; set; }
    public int MaximumReviewFindings { get; set; }
    public int CollectionTimeoutSeconds { get; set; }
    public void Validate() { if (MaximumCollectionBytes < 1 || MaximumReviewFindings < 1 || CollectionTimeoutSeconds < 1) throw new InvalidOperationException("Configure Intake collection and review evidence limits."); }
}
public sealed class FactoryIntakeService(IWorkspaceStore store, IIntakeIssues issues, IntakeLimits limits)
{
    public async Task<IntakeLibrary> List(Guid projectId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var project = data.Graph(projectId).Project;
        return new(project.Revision, data.IntakeSources.Where(x => x.ProjectId == projectId).ToList(), data.IntakeCollections.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).ToList(),
            data.IntakeCandidates.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.CreatedAt).Select(x => Preview(data, x)).ToList());
    }
    public async Task<FactoryIntakeSource> AddSource(Guid projectId, AddIntakeSource input, CancellationToken ct)
    {
        FactoryIntakeSource? result = null;
        await store.WriteAsync(data =>
        {
            var graph = data.Graph(projectId, input.ExpectedRevision); var config = input.Configuration?.Validate() ?? throw new RuleException("Source configuration is required.");
            result = FactoryIntakeSource.Create(projectId, config, config.Kind == "githubIssues" ? issues.Binding : "local-review", config.Kind == "githubIssues" ? issues.Identity : "local-review");
            if (data.IntakeSources.Any(x => x.ProjectId == projectId && x.Scope == result.Scope)) throw new RuleException("This source scope already exists. Edit its configuration instead.", 409);
            data.IntakeSources.Add(result); graph.Project.Touch();
        }, ct); return result!;
    }
    public async Task<FactoryIntakeSource> EditSource(Guid id, EditIntakeSource input, CancellationToken ct)
    {
        FactoryIntakeSource? result = null;
        await store.WriteAsync(data => { result = Source(data, id); var graph = data.Graph(result.ProjectId, input.ExpectedRevision); var config = input.Configuration?.Validate() ?? throw new RuleException("Source configuration is required.");
            var binding = config.Kind == "githubIssues" ? issues.Binding : "local-review"; var identity = config.Kind == "githubIssues" ? issues.Identity : "local-review";
            if (data.IntakeSources.Any(x => x.Id != id && x.ProjectId == result.ProjectId && x.Scope == FactoryIntakeSource.ScopeFor(config, identity))) throw new RuleException("This provider/repository source scope already exists.", 409);
            result.Edit(input.ExpectedSourceRevision, config, binding, identity); graph.Project.Touch(); }, ct); return result!;
    }
    public async Task<FactoryIntakeCollection> Collect(Guid id, CollectIntake input, CancellationToken ct)
    {
        IntakeRules.Request(input.RequestId); var hash = IntakeRules.Fingerprint(id, input); FactoryIntakeCollection? batch = null; FactoryIntakeSource? source = null; var replay = false;
        await store.WriteAsync(data =>
        {
            source = Source(data, id); batch = data.IntakeCollections.SingleOrDefault(x => x.RequestId == input.RequestId);
            if (batch is not null) { if (batch.RequestHash != hash) throw new RuleException("Request ID already belongs to different collection input.", 409); replay = true; return; }
            Rules.Revision(input.ExpectedSourceRevision, source.Revision); RequireEnabled(source);
            if (data.IntakeCollections.Any(x => x.SourceId == id && x.State == "running")) throw new RuleException("A collection is already running for this source.", 409);
            batch = FactoryIntakeCollection.Begin(source, input.RequestId, hash); data.IntakeCollections.Add(batch);
        }, ct);
        if (replay) return batch!;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(limits.CollectionTimeoutSeconds)); ct = deadline.Token;
        try
        {
            var before = await store.ReadAsync(ct);
            var signals = source!.Configuration.Kind == "githubIssues" ? await issues.Collect(source.Configuration, ct) : ReviewSignals(before, source);
            if (signals.Sum(x => (long)Encoding.UTF8.GetByteCount(x.EvidenceJson)) > limits.MaximumCollectionBytes) throw new RuleException("Configured collection byte limit reached; coverage is incomplete.", 503);
            await store.WriteAsync(data =>
            {
                var current = Source(data, id); RequireEnabled(current); Rules.Revision(batch!.SourceRevision, current.Revision);
                if (current.Configuration.Kind == "reviewFindings" && SignalsHash(signals) != SignalsHash(ReviewSignals(data, current))) throw new RuleException("Review evidence changed during collection. Collect again.", 409);
                var persisted = data.IntakeCollections.Single(x => x.Id == batch.Id);
                var fingerprints = signals.Select(x => x.Fingerprint).ToHashSet();
                var missing = data.IntakeCandidates.Where(x => x.ProjectId == current.ProjectId && data.IntakeObservations.Single(o => o.Id == x.ObservationId) is { } o && data.IntakeCollections.Single(c => c.Id == o.CollectionId).SourceId == id && !fingerprints.Contains(x.Fingerprint))
                    .Select(x => data.IntakeObservations.Single(o => o.Id == x.ObservationId).Signal with { Available = false }).ToList();
                foreach (var signal in signals.Concat(missing))
                {
                    var candidate = data.IntakeCandidates.SingleOrDefault(x => x.ProjectId == current.ProjectId && x.Fingerprint == signal.Fingerprint);
                    if (candidate is null) { if (!signal.Available) continue; candidate = FactoryIntakeCandidate.Create(current.ProjectId, signal.Fingerprint); data.IntakeCandidates.Add(candidate); }
                    var observation = IntakeObservation.Create(candidate.Id, batch.Id, signal); data.IntakeObservations.Add(observation); candidate.Observe(observation);
                }
                persisted.Finish("complete", null, signals.Count); batch = persisted;
            }, ct);
        }
        catch (Exception error)
        {
            var message = error is RuleException ? error.Message : error is OperationCanceledException ? "Collection interrupted or cancelled; coverage is unavailable. No candidates were changed." : "Collection failed; no complete source coverage was established. No candidates were changed.";
            await store.WriteAsync(data => { batch = data.IntakeCollections.Single(x => x.Id == batch!.Id); if (batch.State == "running") batch.Finish("failed", message); }, CancellationToken.None);
        }
        return batch!;
    }
    public async Task<IntakeCandidateDetail> Read(Guid id, CancellationToken ct) => Detail(await store.ReadAsync(ct), id);
    public async Task<IntakeCandidateDetail> Act(Guid id, ActOnIntake input, CancellationToken ct)
    {
        IntakeRules.Request(input.RequestId);
        if (input.Action is not ("verify" or "dismiss" or "reopen" or "file" or "link")) throw new RuleException("Choose verify, dismiss, reopen, file or link.");
        if ((input.Action != "link" && input.TaskId is not null) || (input.Action != "file" && (input.StatusId is not null || input.TaskTitle is not null))) throw new RuleException("Only filing accepts a title/column; only linking accepts an existing task ID.");
        Rules.Name(input.Reason, "Triage rationale");
        var hash = IntakeRules.Fingerprint(id, input); var before = await store.ReadAsync(ct);
        if (Replay(before, input.RequestId, hash)) return Detail(before, id);
        var candidate = Candidate(before, id); Rules.Revision(input.ExpectedRevision, candidate.Revision); _ = before.Graph(candidate.ProjectId, input.ExpectedProjectRevision);
        var observation = Observation(before, candidate); var source = Source(before, before.IntakeCollections.Single(x => x.Id == observation.CollectionId).SourceId);
        var requiresCurrent = input.Action is "verify" or "file" or "link";
        if (requiresCurrent)
        {
            RequireCurrent(before, candidate);
            var fresh = source.Configuration.Kind == "githubIssues" ? await issues.Refresh(observation.Signal, source.Configuration, ct) : ReviewSignals(before, source).SingleOrDefault(x => x.Fingerprint == candidate.Fingerprint);
            if (fresh?.Hash != observation.Signal.Hash) throw new RuleException("Source evidence changed or is no longer available. Collect and verify again.", 409);
        }
        await store.WriteAsync(data =>
        {
            if (Replay(data, input.RequestId, hash)) return;
            var current = Candidate(data, id); Rules.Revision(input.ExpectedRevision, current.Revision); var graph = data.Graph(current.ProjectId, input.ExpectedProjectRevision);
            var observed = Observation(data, current);
            if (requiresCurrent)
            {
                RequireCurrent(data, current);
                if (Source(data, source.Id).Revision != source.Revision) throw new RuleException("Source configuration changed. Refresh before triage.", 409);
                if (source.Configuration.Kind == "reviewFindings" && ReviewSignals(data, source).SingleOrDefault(x => x.Fingerprint == current.Fingerprint)?.Hash != observed.Signal.Hash) throw new RuleException("Review evidence changed. Collect again.", 409);
            }
            Rules.Name(input.Reason, "Triage rationale"); Guid? taskId = null;
            if (input.Action is "file" or "link")
            {
                IntakeRules.CanFile(current, observed);
                if (input.Action == "link") { if (input.TaskId is null) throw new RuleException("Choose an existing task in this project."); taskId = graph.Item(input.TaskId.Value).Id; }
                else
                {
                    if (input.StatusId is null) throw new RuleException("Explicitly choose the backlog column."); graph.RequireStatus(input.StatusId.Value);
                    if (graph.Statuses.Single(x => x.Id == input.StatusId).IsCompleted) throw new RuleException("Intake requires an incomplete backlog column.");
                    var title = Rules.Name(input.TaskTitle, "Task title");
                    if (graph.Items.Any(x => IntakeRules.Normalize(x.Title) == IntakeRules.Normalize(title))) throw new RuleException("A task with this title already exists. Link the candidate to existing work instead.", 409);
                    var task = graph.Add(title, null, input.StatusId); task.Specify(Spec(observed.Signal, input.Reason)); data.Items.Add(task); taskId = task.Id;
                }
            }
            current.Decide(input.Action, input.Reason, observed, taskId); data.IntakeDecisions.Add(IntakeDecision.Create(input.RequestId, hash, current, input.Action, input.Reason)); graph.Project.Touch();
        }, ct);
        return Detail(await store.ReadAsync(ct), id);
    }
    private IReadOnlyList<IntakeSignal> ReviewSignals(WorkspaceData data, FactoryIntakeSource source)
    {
        if (data.ReviewFindings.Count(x => x.ProjectId == source.ProjectId) > limits.MaximumReviewFindings) throw new RuleException("Configured review finding limit reached; coverage is incomplete.", 503);
        return ReviewIntakeSignals.Collect(data, source.ProjectId, source.Configuration);
    }
    private void RequireEnabled(FactoryIntakeSource source)
    {
        if (!source.Configuration.Enabled) throw new RuleException("Enable this explicitly configured source before collection or triage.", 409);
        if (source.Configuration.Kind == "githubIssues" && source.ProviderBinding != issues.Binding) throw new RuleException("Provider configuration changed. Explicitly save this source to adopt the current provider binding before collection.", 409);
    }
    private void RequireCurrent(WorkspaceData data, FactoryIntakeCandidate candidate)
    {
        var observation = Observation(data, candidate); var batch = data.IntakeCollections.Single(x => x.Id == observation.CollectionId); var source = Source(data, batch.SourceId); RequireEnabled(source);
        var latest = data.IntakeCollections.Where(x => x.SourceId == source.Id).OrderByDescending(x => x.CreatedAt).First();
        if (latest.Id != batch.Id || latest.State != "complete" || batch.SourceRevision != source.Revision || !observation.Signal.Available) throw new RuleException("Current complete source coverage is required. Collect again before triage.", 409);
    }
    private IntakeCandidatePreview Preview(WorkspaceData data, FactoryIntakeCandidate candidate)
    {
        var current = true; try { RequireCurrent(data, candidate); } catch (RuleException) { current = false; }
        var observation = Observation(data, candidate);
        return new(candidate, observation, current, candidate.TaskId.HasValue && data.Items.Any(x => x.Id == candidate.TaskId),
            data.Items.Where(x => x.ProjectId == candidate.ProjectId && IntakeRules.Normalize(x.Title) == IntakeRules.Normalize(observation.Signal.Title)).Select(x => x.Id).ToList());
    }
    private IntakeCandidateDetail Detail(WorkspaceData data, Guid id) => new(Preview(data, Candidate(data, id)), data.IntakeObservations.Where(x => x.CandidateId == id).ToList(), data.IntakeDecisions.Where(x => x.CandidateId == id).OrderBy(x => x.CreatedAt).ToList());
    private static bool Replay(WorkspaceData data, Guid request, string hash)
    {
        var previous = data.IntakeDecisions.SingleOrDefault(x => x.RequestId == request); if (previous is null) return false;
        if (previous.RequestHash != hash) throw new RuleException("Request ID already belongs to different triage input.", 409); return true;
    }
    private static string SignalsHash(IEnumerable<IntakeSignal> signals) => IntakeRules.Fingerprint(signals.OrderBy(x => x.Fingerprint).Select(x => x.Hash).ToArray());
    private static FactoryIntakeSource Source(WorkspaceData data, Guid id) => data.IntakeSources.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Intake source not found.", 404);
    private static FactoryIntakeCandidate Candidate(WorkspaceData data, Guid id) => data.IntakeCandidates.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Intake candidate not found.", 404);
    private static IntakeObservation Observation(WorkspaceData data, FactoryIntakeCandidate candidate) => data.IntakeObservations.Single(x => x.Id == candidate.ObservationId);
    private static string Spec(IntakeSignal signal, string reason)
    {
        var lines = new[] { "Intake provenance", signal.Title, signal.Description, "Source: " + signal.Origin, signal.Url ?? "Local recurring review findings", "Fingerprint: " + signal.Fingerprint, "Triage rationale: " + reason, "Captured source evidence: " + signal.EvidenceJson };
        return JsonSerializer.Serialize(new { type = "doc", content = lines.Where(text => text.Length > 0).Select(text => new { type = "paragraph", content = new[] { new { type = "text", text } } }) }, FactoryRules.Json);
    }
}
