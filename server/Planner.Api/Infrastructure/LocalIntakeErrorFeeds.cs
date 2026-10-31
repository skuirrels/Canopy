using System.Security.Cryptography;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalIntakeErrorFeeds(IntakeLimits limits) : IIntakeErrorFeeds
{
    public bool Supported => LocalIntakeSnapshotFile.Supported;
    public IReadOnlyList<ErrorFeedChoice> Catalogue() => limits.ErrorFeeds.Select(x => new ErrorFeedChoice(x.Id, x.Name, x.Identity, x.Levels, x.MaxAgeSeconds, x.MaxWindowSeconds)).ToArray();
    private ErrorFeedDefinition Resolve(IntakeConfiguration configuration)
    {
        var feed = limits.ErrorFeeds.SingleOrDefault(x => x.Id == configuration.ErrorFeedId) ?? throw new RuleException("This error feed is not configured on the backend. Configure it explicitly before collection.", 503);
        if (configuration.Levels is null || configuration.Levels.Any(x => !feed.Levels.Contains(x))) throw new RuleException("Choose levels from the configured error feed.");
        return feed;
    }
    public (string Binding, string Identity) Bind(IntakeConfiguration configuration)
    {
        var feed = Resolve(configuration);
        return (IntakeRules.Fingerprint(feed, limits.MaximumErrorEvents, limits.MaximumFutureSkewSeconds, limits.MaximumCollectionBytes), IntakeRules.Fingerprint(feed.Identity));
    }
    public void RequireFresh(ErrorFeedCoverage coverage, IntakeConfiguration configuration)
    {
        var feed = Resolve(configuration);
        if (coverage.FeedId != feed.Id || coverage.Identity != feed.Identity || DateTimeOffset.UtcNow - coverage.GeneratedAt > TimeSpan.FromSeconds(feed.MaxAgeSeconds)) throw new RuleException("Current, unexpired complete error-feed coverage is required. Collect again before triage.", 409);
    }
    public async Task<ErrorFeedInspection> Collect(IntakeConfiguration configuration, CancellationToken ct)
    {
        var feed = Resolve(configuration);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(limits.CollectionTimeoutSeconds)); ct = timeout.Token;
        var body = await LocalIntakeSnapshot.Read(feed.Path, limits.MaximumCollectionBytes, ct);
        var snapshot = LocalIntakeSnapshot.Parse<ErrorFeedSnapshot>(body);
        if (snapshot.Groups is null || snapshot.Groups.Count > limits.MaximumErrorEvents || snapshot.Groups.Any(x => x is null || x.Occurrences is null) || snapshot.Groups.Sum(x => (long)x.Occurrences.Count) > limits.MaximumErrorEvents) throw new RuleException("Error event/group limit reached or incomplete event lists supplied. Coverage is unavailable.", 503);
        var raw = snapshot.Groups.Sum(x => x.Occurrences.Count);
        snapshot = snapshot.Normalize(feed.Identity, feed.Levels, feed.MaxAgeSeconds, feed.MaxWindowSeconds, limits.MaximumFutureSkewSeconds, DateTimeOffset.UtcNow);
        var distinct = snapshot.Groups.Sum(x => x.Occurrences.Count);
        var selected = snapshot.Groups.Select(group => group with { Occurrences = group.Occurrences.Where(x => configuration.Levels!.Contains(x.Level)).ToArray() }).ToArray();
        var signals = selected.Where(x => x.Occurrences.Count >= configuration.MinimumOccurrences).Select(group => new IntakeSignal(
            IntakeRules.Fingerprint("applicationErrors", feed.Identity, group.Id), group.Title,
            $"{group.ExceptionType}: {group.MessageTemplate}\n{group.Occurrences.Count} distinct selected occurrences in the captured window. Operator reproduction and relevance verification are still required.",
            $"{feed.Name} · {feed.Application} · {feed.Environment}", null, true,
            FactoryRules.Serialize(new ErrorGroupEvidence("applicationErrors", feed.Id, feed.Identity, group)))).ToArray();
        return new(signals, new(feed.Id, feed.Identity, Convert.ToHexString(SHA256.HashData(body)), body.LongLength,
            snapshot.GeneratedAt, snapshot.WindowStart, snapshot.WindowEnd, raw, distinct, selected.Sum(x => x.Occurrences.Count), signals.Length),
            snapshot.Groups.SelectMany(group => group.Occurrences.Select(occurrence => new ErrorFeedEventFact(group.Id, occurrence))).ToArray());
    }
    public async Task<IntakeSignal?> Refresh(IntakeSignal signal, ErrorFeedCoverage coverage, IntakeConfiguration configuration, CancellationToken ct)
    {
        RequireFresh(coverage, configuration); var current = await Collect(configuration, ct);
        if (current.Coverage.FileHash != coverage.FileHash) throw new RuleException("The error snapshot changed. Collect current complete coverage before triage.", 409);
        return current.Signals.SingleOrDefault(x => x.Fingerprint == signal.Fingerprint);
    }
}
