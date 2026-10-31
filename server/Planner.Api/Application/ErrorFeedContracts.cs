using Planner.Api.Domain;

namespace Planner.Api.Application;

public sealed class ErrorFeedDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Application { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Path { get; set; } = "";
    public List<string> Levels { get; set; } = [];
    public int MaxAgeSeconds { get; set; }
    public int MaxWindowSeconds { get; set; }
    public ErrorFeedIdentity Identity => new(SourceId, Application, Environment);
    public void Validate()
    {
        Id = Rules.Name(Id, "Error feed ID"); Name = Rules.Name(Name); SourceId = Rules.Name(SourceId, "Source identity");
        Application = Rules.Name(Application, "Application"); Environment = Rules.Name(Environment, "Environment");
        if (!System.IO.Path.IsPathFullyQualified(Path) || Path.Any(char.IsControl) || MaxAgeSeconds < 1 || MaxWindowSeconds < 1 || Levels is null || Levels.Count == 0) throw new InvalidOperationException("Configure each error feed's absolute path, explicit age/window limits and supported levels.");
        Levels = Levels.Select(x => Rules.Name(x, "Error level")).ToList();
        if (Levels.Distinct(StringComparer.Ordinal).Count() != Levels.Count) throw new InvalidOperationException("Error feed levels must be distinct.");
    }
}
public sealed record ErrorFeedChoice(string Id, string Name, ErrorFeedIdentity Identity, IReadOnlyList<string> Levels, int MaxAgeSeconds, int MaxWindowSeconds);
public sealed record ErrorFeedCatalogue(IReadOnlyList<ErrorFeedChoice> Feeds, bool Supported);
public sealed record ErrorFeedInspection(IReadOnlyList<IntakeSignal> Signals, ErrorFeedCoverage Coverage, IReadOnlyList<ErrorFeedEventFact> Events);
public interface IIntakeErrorFeeds
{
    bool Supported { get; }
    IReadOnlyList<ErrorFeedChoice> Catalogue();
    (string Binding, string Identity) Bind(IntakeConfiguration configuration);
    void RequireFresh(ErrorFeedCoverage coverage, IntakeConfiguration configuration);
    Task<ErrorFeedInspection> Collect(IntakeConfiguration configuration, CancellationToken ct);
    Task<IntakeSignal?> Refresh(IntakeSignal signal, ErrorFeedCoverage coverage, IntakeConfiguration configuration, CancellationToken ct);
}
