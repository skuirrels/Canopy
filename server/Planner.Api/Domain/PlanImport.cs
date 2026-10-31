namespace Planner.Api.Domain;

// An import identity survives edits to task text, status and hierarchy.
public sealed class PlanImportEntry
{
    private PlanImportEntry() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public string Source { get; private set; } = "";
    public string SourceHash { get; private set; } = "";
    public string Key { get; private set; } = "";
    public static PlanImportEntry Create(WorkItem item, string source, string hash, string key) => new()
    {
        Id = Guid.NewGuid(), ProjectId = item.ProjectId, ItemId = item.Id,
        Source = Rules.Name(source, "Plan source"), SourceHash = hash, Key = Rules.Name(key, "Plan item key")
    };
}
