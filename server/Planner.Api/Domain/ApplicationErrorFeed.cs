using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

public sealed record ErrorFeedIdentity(string SourceId, string Application, string Environment);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ErrorFrame(string Path, string Symbol, int? Line = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ErrorOccurrence(string Id, [property: JsonRequired] DateTimeOffset OccurredAt, string Level, string? Release = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ErrorGroup(string Id, string Title, string ExceptionType, string MessageTemplate,
    IReadOnlyList<ErrorFrame> Frames, IReadOnlyList<ErrorOccurrence> Occurrences, string? Code = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ErrorFeedSnapshot([property: JsonRequired] int SchemaVersion, string SourceId, string Application, string Environment,
    [property: JsonRequired] DateTimeOffset GeneratedAt, [property: JsonRequired] DateTimeOffset WindowStart,
    [property: JsonRequired] DateTimeOffset WindowEnd, [property: JsonRequired] bool Complete,
    [property: JsonRequired] bool Sanitized, IReadOnlyList<ErrorGroup> Groups)
{
    public ErrorFeedSnapshot Normalize(ErrorFeedIdentity identity, IReadOnlyList<string> levels, int maxAgeSeconds, int maxWindowSeconds, int futureSkewSeconds, DateTimeOffset now)
    {
        if (SchemaVersion != 1 || !Complete || !Sanitized) throw new RuleException("Require a complete, producer-sanitized version 1 error snapshot. No complete coverage was established.", 503);
        if (new ErrorFeedIdentity(SourceId, Application, Environment) != identity) throw new RuleException("The error snapshot does not match its configured source, application and environment.", 503);
        if (WindowStart > WindowEnd || WindowEnd > GeneratedAt || GeneratedAt > now.AddSeconds(futureSkewSeconds) ||
            now - GeneratedAt > TimeSpan.FromSeconds(maxAgeSeconds) || WindowEnd - WindowStart > TimeSpan.FromSeconds(maxWindowSeconds))
            throw new RuleException("The error snapshot is expired or has an invalid coverage window/timestamp.", 503);
        if (Groups is null || Groups.Any(x => x is null) || Groups.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Groups.Count) throw new RuleException("Supply distinct error group identities and an explicit group list.", 503);
        var owners = new Dictionary<string, string>(StringComparer.Ordinal); var normalized = new List<ErrorGroup>();
        foreach (var group in Groups)
        {
            var id = Rules.Name(group.Id, "Upstream error group ID");
            if (id != group.Id) throw new RuleException("Upstream group identities cannot have surrounding whitespace.", 503);
            if (group.Frames is null || group.Frames.Any(x => x is null) || group.Occurrences is null || group.Occurrences.Count == 0 || group.Occurrences.Any(x => x is null)) throw new RuleException("Supply explicit error frames and at least one occurrence per group.", 503);
            var frames = group.Frames.Select(frame =>
            {
                var path = frame.Path;
                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(frame.Symbol)) throw new RuleException("Supply the actual relative code path and symbol for each frame.", 503);
                if (path.StartsWith('/') || path.StartsWith('\\') || path.Contains(':') || path.Any(char.IsControl) || path.Split('/', '\\').Any(x => x is ".." or "")) throw new RuleException("Error frames require a relative code path without traversal.", 503);
                if (frame.Line is <= 0) throw new RuleException("Reported error frame lines must be positive.", 503);
                return frame with { Path = path, Symbol = frame.Symbol };
            }).ToArray();
            var events = new Dictionary<string, ErrorOccurrence>(StringComparer.Ordinal);
            foreach (var occurrence in group.Occurrences)
            {
                var eventId = Rules.Name(occurrence.Id, "Upstream event ID");
                if (eventId != occurrence.Id) throw new RuleException("Upstream event identities cannot have surrounding whitespace.", 503);
                if (!levels.Contains(occurrence.Level) || occurrence.OccurredAt < WindowStart || occurrence.OccurredAt > WindowEnd) throw new RuleException("An occurrence has an unconfigured level or falls outside complete coverage.", 503);
                var value = occurrence with { Id = eventId, OccurredAt = occurrence.OccurredAt.ToUniversalTime(), Release = occurrence.Release is null ? null : Rules.Name(occurrence.Release, "Observed release") };
                if (owners.TryGetValue(eventId, out var owner) && owner != id || events.TryGetValue(eventId, out var existing) && existing != value) throw new RuleException("Conflicting event identities cannot establish complete error coverage.", 503);
                owners[eventId] = id; events[eventId] = value;
            }
            var template = group.MessageTemplate;
            AgentRun.Text(template, "Error message template"); if (string.IsNullOrWhiteSpace(template)) throw new RuleException("Supply the upstream error message template.", 503);
            normalized.Add(group with { Id = id, Title = Rules.Name(group.Title), ExceptionType = Rules.Name(group.ExceptionType, "Exception type"), MessageTemplate = template,
                Code = group.Code is null ? null : Rules.Name(group.Code, "Error code"), Frames = frames, Occurrences = events.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray() });
        }
        if (normalized.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != normalized.Count) throw new RuleException("Normalized error group IDs conflict.", 503);
        return this with { Groups = normalized.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(), GeneratedAt = GeneratedAt.ToUniversalTime(), WindowStart = WindowStart.ToUniversalTime(), WindowEnd = WindowEnd.ToUniversalTime() };
    }
}
public sealed record ErrorGroupEvidence(string Kind, string FeedId, ErrorFeedIdentity Identity, ErrorGroup Group);
public sealed record ErrorFeedCoverage(string FeedId, ErrorFeedIdentity Identity, string FileHash, long Bytes, DateTimeOffset GeneratedAt,
    DateTimeOffset WindowStart, DateTimeOffset WindowEnd, int RawEvents, int DistinctEvents, int SelectedEvents, int EligibleGroups);
public sealed class FactoryErrorFeedReceipt
{
    private FactoryErrorFeedReceipt() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid SourceId { get; private set; }
    [JsonIgnore] public string CoverageJson { get; private set; } = "";
    public ErrorFeedCoverage Coverage => JsonSerializer.Deserialize<ErrorFeedCoverage>(CoverageJson, FactoryRules.Json)!;
    public static FactoryErrorFeedReceipt Capture(FactoryIntakeCollection collection, ErrorFeedCoverage coverage) => new()
    { Id = collection.Id, ProjectId = collection.ProjectId, SourceId = collection.SourceId, CoverageJson = FactoryRules.Serialize(coverage) };
}

public sealed record ErrorFeedEventFact(string GroupId, ErrorOccurrence Occurrence);
public sealed class FactoryErrorEventIdentity
{
    private FactoryErrorEventIdentity() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Scope { get; private set; } = "";
    public string EventId { get; private set; } = "";
    public string GroupId { get; private set; } = "";
    public string FactHash { get; private set; } = "";
    public static FactoryErrorEventIdentity Capture(Guid projectId, ErrorFeedIdentity identity, ErrorFeedEventFact fact) => new()
    { Id = Guid.NewGuid(), ProjectId = projectId, Scope = IntakeRules.Fingerprint(identity), EventId = fact.Occurrence.Id, GroupId = fact.GroupId, FactHash = IntakeRules.Fingerprint(fact.Occurrence) };
    public void RequireSame(ErrorFeedEventFact fact)
    {
        if (GroupId != fact.GroupId || FactHash != IntakeRules.Fingerprint(fact.Occurrence)) throw new RuleException($"Event identity '{EventId}' conflicts with earlier complete source evidence. Event ownership and facts must remain immutable.", 503);
    }
}
