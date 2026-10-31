using System.Security.Cryptography;
using System.Text;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalIntakeAnalyticsFeeds(IntakeLimits limits) : IIntakeAnalyticsFeeds
{
    public bool Supported => LocalIntakeSnapshotFile.Supported;
    public IReadOnlyList<AnalyticsFeedChoice> Catalogue() => limits.AnalyticsFeeds.Select(x => new AnalyticsFeedChoice(x.Id, x.Name, x.Identity, x.MaxAgeSeconds, x.MaxWindowSeconds, x.DimensionNames, x.Rules)).ToArray();
    private AnalyticsFeedDefinition Resolve(IntakeConfiguration config)
    {
        var feed = limits.AnalyticsFeeds.SingleOrDefault(x => x.Id == config.AnalyticsFeedId) ?? throw new RuleException("This analytics feed is not configured on the backend.", 503);
        if (config.AnalyticsRuleIds is null || config.AnalyticsRuleIds.Any(id => !feed.Rules.Any(rule => rule.Id == id))) throw new RuleException("Choose rules from this configured analytics feed.");
        return feed;
    }
    public (string Binding, string Identity) Bind(IntakeConfiguration config)
    {
        var feed = Resolve(config); return (IntakeRules.Fingerprint(feed, limits.MaximumAnalyticsMetrics, limits.MaximumAnalyticsSignals, limits.MaximumFutureSkewSeconds, limits.MaximumCollectionBytes), IntakeRules.Fingerprint(feed.Identity));
    }
    public void RequireFresh(AnalyticsCoverage coverage, IntakeConfiguration config)
    {
        var feed = Resolve(config);
        if (coverage.FeedId != feed.Id || coverage.Identity != feed.Identity || DateTimeOffset.UtcNow - coverage.GeneratedAt > TimeSpan.FromSeconds(feed.MaxAgeSeconds)) throw new RuleException("Current unexpired analytics coverage is required. Collect again before triage.", 409);
    }
    public async Task<AnalyticsInspection> Collect(IntakeConfiguration config, CancellationToken ct)
    {
        var feed = Resolve(config); using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(limits.CollectionTimeoutSeconds)); ct = deadline.Token;
        var bytes = await LocalIntakeSnapshot.Read(feed.Path, limits.MaximumCollectionBytes, ct); var report = LocalIntakeSnapshot.Parse<AnalyticsSnapshot>(bytes);
        if (report.Metrics is null || report.Metrics.Count > limits.MaximumAnalyticsMetrics) throw new RuleException("Analytics metric limit reached or complete metric list missing.", 503);
        report = report.Normalize(feed.Identity, feed.Rules, feed.DimensionNames, feed.MaxAgeSeconds, feed.MaxWindowSeconds, limits.MaximumFutureSkewSeconds, DateTimeOffset.UtcNow);
        var rules = feed.Rules.Where(x => config.AnalyticsRuleIds!.Contains(x.Id)).ToArray();
        var selected = report.Metrics.Where(metric => rules.Any(rule => rule.Condition.Describes(metric))).ToArray();
        var signals = new List<IntakeSignal>(); long evidenceBytes = 0;
        foreach (var metric in selected)
            foreach (var rule in rules)
            {
                ct.ThrowIfCancellationRequested();
                if (!rule.Condition.Matches(metric)) continue;
                if (signals.Count >= limits.MaximumAnalyticsSignals) throw new RuleException("Analytics signal limit reached; coverage is incomplete.", 503);
                var evidence = FactoryRules.Serialize(new AnalyticsEvidence("analytics", feed.Id, feed.Identity, rule, report.WindowStart, report.WindowEnd, metric));
                evidenceBytes += Encoding.UTF8.GetByteCount(evidence);
                if (evidenceBytes > limits.MaximumCollectionBytes) throw new RuleException("Analytics evidence byte limit reached; coverage is incomplete.", 503);
                signals.Add(new(IntakeRules.Fingerprint("analytics", feed.Identity, rule.Id, metric.MetricId, metric.Unit, metric.Statistic, metric.Id, metric.Dimensions), metric.Name,
                    $"{metric.Value} {metric.Unit} ({metric.Statistic}); {metric.Samples} reported samples. Rule {rule.Name}: {rule.Condition.Comparison} {rule.Condition.Threshold}, minimum {rule.Condition.MinimumSamples} samples. Verify relevance and aggregation provenance before filing.",
                    $"{feed.Name} · {feed.Application} · {feed.Environment}", null, true, evidence));
            }
        return new(signals, new(feed.Id, feed.Identity, report.ReportId, IntakeRules.Fingerprint(report), Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength,
            report.GeneratedAt, report.WindowStart, report.WindowEnd, report.Metrics.Count, selected.Length, selected.Count(x => x.Value is not null), signals.Count));
    }
    public async Task<IntakeSignal?> Refresh(IntakeSignal signal, AnalyticsCoverage coverage, IntakeConfiguration config, CancellationToken ct)
    {
        RequireFresh(coverage, config); var current = await Collect(config, ct);
        if (current.Coverage.FileHash != coverage.FileHash) throw new RuleException("Analytics report changed. Collect complete current coverage before triage.", 409);
        return current.Signals.SingleOrDefault(x => x.Fingerprint == signal.Fingerprint);
    }
}
