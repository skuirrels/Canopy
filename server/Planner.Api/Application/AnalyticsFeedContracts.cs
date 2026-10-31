using Planner.Api.Domain;

namespace Planner.Api.Application;

public sealed class AnalyticsFeedDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Application { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Path { get; set; } = "";
    public int MaxAgeSeconds { get; set; }
    public int MaxWindowSeconds { get; set; }
    public List<string> DimensionNames { get; set; } = [];
    public List<AnalyticsRule> Rules { get; set; } = [];
    public AnalyticsIdentity Identity => new(SourceId, Application, Environment);
    public void Validate()
    {
        Id = AnalyticsQuantity.Identity(Id, "Analytics feed identity"); Name = Domain.Rules.Name(Name);
        SourceId = AnalyticsQuantity.Identity(SourceId, "Source identity"); Application = Domain.Rules.Name(Application); Environment = Domain.Rules.Name(Environment);
        if (!System.IO.Path.IsPathFullyQualified(Path) || Path.Any(char.IsControl) || MaxAgeSeconds < 1 || MaxWindowSeconds < 1 || DimensionNames is null || Rules is null || Rules.Count == 0 || Rules.Any(x => x is null || x.Condition is null)) throw new InvalidOperationException("Configure each analytics feed's absolute path, age/window limits, allowed dimensions and explicit metric rules.");
        DimensionNames = DimensionNames.Select(x => AnalyticsQuantity.Identity(x, "Dimension name")).ToList();
        Rules = Rules.Select(x => x with { Id = AnalyticsQuantity.Identity(x.Id, "Rule identity"), Name = Domain.Rules.Name(x.Name), Condition = x.Condition.Validate() }).ToList();
        if (DimensionNames.Distinct(StringComparer.Ordinal).Count() != DimensionNames.Count || Rules.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Rules.Count) throw new InvalidOperationException("Analytics dimension names and rule identities must be distinct.");
    }
}
public sealed record AnalyticsFeedChoice(string Id, string Name, AnalyticsIdentity Identity, int MaxAgeSeconds, int MaxWindowSeconds, IReadOnlyList<string> DimensionNames, IReadOnlyList<AnalyticsRule> Rules);
public sealed record AnalyticsFeedCatalogue(IReadOnlyList<AnalyticsFeedChoice> Feeds, bool Supported);
public sealed record AnalyticsInspection(IReadOnlyList<IntakeSignal> Signals, AnalyticsCoverage Coverage);
public interface IIntakeAnalyticsFeeds
{
    bool Supported { get; }
    IReadOnlyList<AnalyticsFeedChoice> Catalogue();
    (string Binding, string Identity) Bind(IntakeConfiguration config);
    void RequireFresh(AnalyticsCoverage coverage, IntakeConfiguration config);
    Task<AnalyticsInspection> Collect(IntakeConfiguration config, CancellationToken ct);
    Task<IntakeSignal?> Refresh(IntakeSignal signal, AnalyticsCoverage coverage, IntakeConfiguration config, CancellationToken ct);
}
