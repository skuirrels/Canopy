using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
namespace Planner.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntakeConfiguration(string Name, string Kind, bool Enabled, string Repository, IReadOnlyList<string> Labels, int MinimumOccurrences, IReadOnlyList<string> Categories,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ErrorFeedId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Levels = null)
{
    public IntakeConfiguration Validate()
    {
        if (Kind is not ("githubIssues" or "reviewFindings" or "applicationErrors")) throw new RuleException("Choose GitHub issues, recurring review findings or configured application errors.");
        if (Labels is null || Categories is null) throw new RuleException("Explicit label and category lists are required.");
        var labels = Labels.Select(x => Rules.Name(x, "Label")).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var categories = Categories.Distinct().Order().ToArray();
        if (Kind == "githubIssues" && (!Regex.IsMatch(Repository ?? "", "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") || Repository!.Split('/').Any(x => x is "." or "..") || MinimumOccurrences != 0 || categories.Length != 0))
            throw new RuleException("GitHub intake requires an owner/repository, explicit labels, zero recurrence threshold and no review categories.");
        if (Kind == "reviewFindings" && (Repository != "" || labels.Length != 0 || MinimumOccurrences < 2 || categories.Length == 0 || categories.Any(x => x is not ("defect" or "missingBehavior" or "verificationGap" or "scopeDrift" or "improvement"))))
            throw new RuleException("Review intake requires at least two distinct tasks, explicit finding categories and no GitHub repository/labels.");
        if (Kind != "applicationErrors" && (ErrorFeedId is not null || Levels is not null)) throw new RuleException("Only application-error sources accept a configured feed ID and levels.");
        if (Kind == "applicationErrors" && (Repository != "" || labels.Length != 0 || categories.Length != 0 || MinimumOccurrences < 1 || string.IsNullOrWhiteSpace(ErrorFeedId) || Levels is null || Levels.Count == 0)) throw new RuleException("Error intake requires a configured feed, explicit levels, a positive distinct-event threshold and no repository/labels/review categories.");
        return this with { Name = Rules.Name(Name), Repository = Repository.ToLowerInvariant(), Labels = labels, Categories = categories, ErrorFeedId = ErrorFeedId is null ? null : Rules.Name(ErrorFeedId, "Error feed ID"), Levels = Levels?.Select(x => Rules.Name(x, "Error level")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
    }
}
public sealed record IntakeSignal(string Fingerprint, string Title, string Description, string Origin, string? Url, bool Available, string EvidenceJson)
{
    [JsonIgnore] public string Hash => FactoryArtifact.Hash(JsonSerializer.Serialize(this, FactoryRules.Json));
}
public static class IntakeRules
{
    public static string Normalize(string value) => Regex.Replace(value.Trim().Normalize(), "\\s+", " ").ToUpperInvariant();
    public static string Fingerprint(params object[] values) => FactoryArtifact.Hash(JsonSerializer.Serialize(values, FactoryRules.Json));
    public static void Request(Guid id) { if (id == Guid.Empty) throw new RuleException("A unique request ID is required."); }
    public static void CanFile(FactoryIntakeCandidate candidate, IntakeObservation observation)
    {
        if (candidate.State != "verified" || candidate.VerifiedHash != observation.Signal.Hash || !observation.Signal.Available)
            throw new RuleException("Verify the current available evidence before filing or linking this candidate.", 409);
    }
}
public sealed class FactoryIntakeSource
{
    private FactoryIntakeSource() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public long Revision { get; private set; } = 1;
    public string Scope { get; private set; } = "";
    public string ProviderBinding { get; private set; } = "";
    [JsonIgnore] public string ConfigurationJson { get; private set; } = "";
    public IntakeConfiguration Configuration => JsonSerializer.Deserialize<IntakeConfiguration>(ConfigurationJson, FactoryRules.Json)!;
    public static FactoryIntakeSource Create(Guid project, IntakeConfiguration config, string binding, string identity)
    {
        config = config.Validate(); return new() { Id = Guid.NewGuid(), ProjectId = project, ProviderBinding = binding,
            Scope = ScopeFor(config, identity), ConfigurationJson = JsonSerializer.Serialize(config, FactoryRules.Json) };
    }
    public static string ScopeFor(IntakeConfiguration config, string identity) => IntakeRules.Fingerprint(config.Kind, identity, config.Repository);
    public void Edit(long expected, IntakeConfiguration input, string? binding, string? identity)
    {
        Rules.Revision(expected, Revision); var config = input.Validate();
        if (config.Kind != Configuration.Kind || config.Repository != Configuration.Repository || config.ErrorFeedId != Configuration.ErrorFeedId) throw new RuleException("Source kind, repository and error feed are immutable. Configure a new source instead.");
        if ((binding is null || identity is null) && config.Enabled) throw new RuleException("An enabled source requires its configured provider binding.", 409);
        ConfigurationJson = JsonSerializer.Serialize(config, FactoryRules.Json);
        if (binding is not null && identity is not null) { ProviderBinding = binding; Scope = ScopeFor(config, identity); }
        Revision++;
    }
}
public sealed class FactoryIntakeCollection
{
    private FactoryIntakeCollection() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid SourceId { get; private set; }
    public Guid RequestId { get; private set; }
    public string RequestHash { get; private set; } = "";
    public long SourceRevision { get; private set; }
    public string ConfigurationJson { get; private set; } = "";
    public string State { get; private set; } = "running";
    public string? Error { get; private set; }
    public int Observed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public static FactoryIntakeCollection Begin(FactoryIntakeSource source, Guid request, string hash)
    { IntakeRules.Request(request); return new() { Id = Guid.NewGuid(), SourceId = source.Id, ProjectId = source.ProjectId, SourceRevision = source.Revision, ConfigurationJson = source.ConfigurationJson, RequestId = request, RequestHash = hash, CreatedAt = DateTime.UtcNow }; }
    public void Finish(string state, string? error, int count = 0)
    { if (State != "running") throw new RuleException("Collection already settled.", 409); State = state; Error = error; Observed = count; FinishedAt = DateTime.UtcNow; }
}
public sealed class FactoryIntakeCandidate
{
    private FactoryIntakeCandidate() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public Guid ObservationId { get; private set; }
    public long Revision { get; private set; } = 1;
    public string State { get; private set; } = "proposed";
    public string? VerifiedHash { get; private set; }
    public Guid? TaskId { get; private set; }
    public string Reason { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
    public static FactoryIntakeCandidate Create(Guid project, string fingerprint) => new() { Id = Guid.NewGuid(), ProjectId = project, Fingerprint = fingerprint, CreatedAt = DateTime.UtcNow };
    public void Observe(IntakeObservation observation)
    {
        ObservationId = observation.Id; Revision++;
        if (State == "verified" && VerifiedHash != observation.Signal.Hash) { State = "proposed"; VerifiedHash = null; Reason = "Source evidence changed; verify again."; }
    }
    public void Decide(string action, string reason, IntakeObservation observation, Guid? task)
    {
        reason = Rules.Name(reason, "Triage rationale");
        if (State == "filed") throw new RuleException("This candidate already links to a task. Its durable link prevents duplicate filing even if that task was deleted.", 409);
        switch (action)
        {
            case "verify": if (!observation.Signal.Available) throw new RuleException("This signal is no longer available in a complete collection.", 409); State = "verified"; VerifiedHash = observation.Signal.Hash; break;
            case "dismiss": State = "dismissed"; VerifiedHash = null; break;
            case "reopen": State = "proposed"; VerifiedHash = null; break;
            case "file": case "link": IntakeRules.CanFile(this, observation); if (task is null) throw new RuleException("A task link is required."); TaskId = task; State = "filed"; break;
            default: throw new RuleException("Choose verify, dismiss, reopen, file or link.");
        }
        Reason = reason; Revision++;
    }
}
public sealed class IntakeObservation
{
    private IntakeObservation() { }
    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid CollectionId { get; private set; }
    [JsonIgnore] public string SignalJson { get; private set; } = "";
    public IntakeSignal Signal => JsonSerializer.Deserialize<IntakeSignal>(SignalJson, FactoryRules.Json)!;
    public static IntakeObservation Create(Guid candidate, Guid collection, IntakeSignal signal) => new() { Id = Guid.NewGuid(), CandidateId = candidate, CollectionId = collection, SignalJson = JsonSerializer.Serialize(signal, FactoryRules.Json) };
}
public sealed class IntakeDecision
{
    private IntakeDecision() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid ObservationId { get; private set; }
    public string RequestHash { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public Guid? TaskId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public static IntakeDecision Create(Guid request, string hash, FactoryIntakeCandidate candidate, string action, string reason) => new() { Id = Guid.NewGuid(), RequestId = request, CandidateId = candidate.Id, ObservationId = candidate.ObservationId, RequestHash = hash, Action = action, Reason = reason, TaskId = candidate.TaskId, CreatedAt = DateTime.UtcNow };
}
