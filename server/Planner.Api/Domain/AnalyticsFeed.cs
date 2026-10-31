using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Planner.Api.Domain;

public static class AnalyticsQuantity
{
    public static decimal Decimal(string? text)
    {
        if (text is null || text.Length > 64 || !Regex.IsMatch(text, @"^-?(0|[1-9][0-9]*)(\.[0-9]+)?$") ||
            !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw new RuleException("Supply an exact fixed-point decimal string without exponent notation.", 503);
        var normalized = text.Contains('.') ? text.TrimEnd('0').TrimEnd('.') : text; if (normalized == "-0") normalized = "0";
        if (Format(value) != normalized) throw new RuleException("A reported decimal cannot be rounded to establish analytics evidence.", 503);
        return value;
    }
    public static string Format(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    public static long Count(string? text)
    {
        if (text is null || text.Length > 19 || !Regex.IsMatch(text, @"^(0|[1-9][0-9]*)$") || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) throw new RuleException("Supply an exact nonnegative 64-bit sample-count string.", 503);
        return value;
    }
    public static string Identity(string value, string label, int statusCode = 503)
    { var normalized = Rules.Name(value, label); if (normalized != value) throw new RuleException($"{label} cannot have surrounding whitespace.", statusCode); return value; }
}
public sealed record AnalyticsIdentity(string SourceId, string Application, string Environment);
public sealed record AnalyticsCondition(string MetricId, string Unit, string Statistic, string Comparison, string Threshold, string MinimumSamples)
{
    public AnalyticsCondition Validate()
    {
        AnalyticsQuantity.Identity(MetricId, "Metric identity"); AnalyticsQuantity.Identity(Unit, "Metric unit"); AnalyticsQuantity.Identity(Statistic, "Metric statistic");
        if (Comparison is not ("above" or "atLeast" or "below" or "atMost")) throw new RuleException("Choose above, atLeast, below or atMost for an analytics rule.");
        if (AnalyticsQuantity.Count(MinimumSamples) < 1) throw new RuleException("Explicitly configure a positive minimum sample count.");
        return this with { Threshold = AnalyticsQuantity.Format(AnalyticsQuantity.Decimal(Threshold)) };
    }
    public bool Describes(AnalyticsMetric metric) => metric.MetricId == MetricId && metric.Unit == Unit && metric.Statistic == Statistic;
    public bool Matches(AnalyticsMetric metric)
    {
        if (!Describes(metric) || metric.Value is null || AnalyticsQuantity.Count(metric.Samples) < AnalyticsQuantity.Count(MinimumSamples)) return false;
        var value = AnalyticsQuantity.Decimal(metric.Value); var threshold = AnalyticsQuantity.Decimal(Threshold);
        return Comparison switch { "above" => value > threshold, "atLeast" => value >= threshold, "below" => value < threshold, "atMost" => value <= threshold, _ => throw new RuleException("Unsupported analytics comparison.", 503) };
    }
}
public sealed record AnalyticsRule(string Id, string Name, AnalyticsCondition Condition);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnalyticsDimension(string Name, string Value);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnalyticsMetric(string Id, string MetricId, string Name, string Unit, string Statistic, string? Value, string Samples, IReadOnlyList<AnalyticsDimension> Dimensions);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnalyticsSnapshot([property: JsonRequired] int SchemaVersion, string ReportId, string SourceId, string Application, string Environment,
    [property: JsonRequired] DateTimeOffset GeneratedAt, [property: JsonRequired] DateTimeOffset WindowStart, [property: JsonRequired] DateTimeOffset WindowEnd,
    [property: JsonRequired] bool Complete, [property: JsonRequired] bool Sanitized, IReadOnlyList<AnalyticsMetric> Metrics)
{
    public AnalyticsSnapshot Normalize(AnalyticsIdentity identity, IReadOnlyList<AnalyticsRule> rules, IReadOnlyList<string> dimensionNames, int maxAge, int maxWindow, int futureSkew, DateTimeOffset now)
    {
        if (SchemaVersion != 1 || !Complete || !Sanitized) throw new RuleException("Require a complete, producer-sanitized version 1 analytics report.", 503);
        if (new AnalyticsIdentity(SourceId, Application, Environment) != identity) throw new RuleException("Analytics report scope does not match its configured source/application/environment.", 503);
        AnalyticsQuantity.Identity(ReportId, "Report identity");
        if (WindowStart > WindowEnd || WindowEnd > GeneratedAt || GeneratedAt > now.AddSeconds(futureSkew) || now - GeneratedAt > TimeSpan.FromSeconds(maxAge) || WindowEnd - WindowStart > TimeSpan.FromSeconds(maxWindow)) throw new RuleException("Analytics report is expired or its complete window/timestamps are invalid.", 503);
        if (Metrics is null || Metrics.Any(x => x is null) || Metrics.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Metrics.Count) throw new RuleException("Supply distinct metric series identities and an explicit metric list.", 503);
        var normalized = Metrics.Select(metric =>
        {
            AnalyticsQuantity.Identity(metric.Id, "Metric series identity"); AnalyticsQuantity.Identity(metric.MetricId, "Metric identity");
            if (!rules.Any(rule => rule.Condition.Describes(metric))) throw new RuleException("A reported metric has unconfigured identity, unit or aggregation statistic.", 503);
            var samples = AnalyticsQuantity.Count(metric.Samples);
            if ((samples == 0) != (metric.Value is null)) throw new RuleException("Zero samples require an unavailable value; measured values require actual reported samples.", 503);
            if (metric.Dimensions is null || metric.Dimensions.Any(x => x is null) || metric.Dimensions.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != metric.Dimensions.Count) throw new RuleException("Supply explicit, distinct configured dimensions.", 503);
            foreach (var dimension in metric.Dimensions) { if (!dimensionNames.Contains(dimension.Name)) throw new RuleException("An analytics dimension is not configured for this feed.", 503); AnalyticsQuantity.Identity(dimension.Value, "Dimension value"); }
            return metric with { Name = Rules.Name(metric.Name), Value = metric.Value is null ? null : AnalyticsQuantity.Format(AnalyticsQuantity.Decimal(metric.Value)), Dimensions = metric.Dimensions.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray() };
        }).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        return this with { GeneratedAt = GeneratedAt.ToUniversalTime(), WindowStart = WindowStart.ToUniversalTime(), WindowEnd = WindowEnd.ToUniversalTime(), Metrics = normalized };
    }
}
public sealed record AnalyticsEvidence(string Kind, string FeedId, AnalyticsIdentity Identity, AnalyticsRule Rule, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, AnalyticsMetric Metric);
public sealed record AnalyticsCoverage(string FeedId, AnalyticsIdentity Identity, string ReportId, string ReportHash, string FileHash, long Bytes,
    DateTimeOffset GeneratedAt, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, int ReportedSeries, int SelectedSeries, int AvailableSeries, int QualifyingSignals);
public sealed class FactoryAnalyticsReceipt
{
    private FactoryAnalyticsReceipt() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid SourceId { get; private set; }
    [JsonIgnore] public string CoverageJson { get; private set; } = "";
    public AnalyticsCoverage Coverage => JsonSerializer.Deserialize<AnalyticsCoverage>(CoverageJson, FactoryRules.Json)!;
    public static FactoryAnalyticsReceipt Capture(FactoryIntakeCollection batch, AnalyticsCoverage coverage) => new() { Id = batch.Id, ProjectId = batch.ProjectId, SourceId = batch.SourceId, CoverageJson = FactoryRules.Serialize(coverage) };
}
public sealed class FactoryAnalyticsReportIdentity
{
    private FactoryAnalyticsReportIdentity() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Scope { get; private set; } = "";
    public string ReportId { get; private set; } = "";
    public string ReportHash { get; private set; } = "";
    public static FactoryAnalyticsReportIdentity Capture(Guid projectId, AnalyticsCoverage coverage) => new() { Id = Guid.NewGuid(), ProjectId = projectId, Scope = IntakeRules.Fingerprint(coverage.Identity), ReportId = coverage.ReportId, ReportHash = coverage.ReportHash };
    public void RequireSame(AnalyticsCoverage coverage) { if (ReportHash != coverage.ReportHash) throw new RuleException("A retained analytics report ID has conflicting content. Publish a new immutable report identity.", 503); }
}
