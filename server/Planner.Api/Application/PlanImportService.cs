using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Planner.Api.Domain;

namespace Planner.Api.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlanDraftItem(string Key, string Title, string? ParentKey, string SpecJson, string Priority,
    IReadOnlyList<string> DependencyKeys, IReadOnlyList<AcceptanceCriterion> Criteria, Guid? ExistingItemId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportPlan(long ExpectedRevision, string Source, string SourceHash, IReadOnlyList<PlanDraftItem> Items);

public sealed class PlanImportService(IWorkspaceStore store)
{
    // One store transaction: malformed branches or cycles cannot leave a partial backlog.
    public Task<WorkspaceData> Import(Guid projectId, ImportPlan input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.Graph(projectId, input.ExpectedRevision);
        var source = Rules.Name(input.Source, "Plan source");
        if (input.SourceHash is null || !Regex.IsMatch(input.SourceHash, "^[a-f0-9]{64}$")) throw new RuleException("Supply the SHA-256 hash of the imported Markdown.");
        if (input.Items is null || input.Items.Count == 0 || input.Items.Any(x => x is null)) throw new RuleException("Supply a nonempty plan draft.");
        var entries = input.Items.ToList();
        foreach (var entry in entries)
        {
            if (Rules.Name(entry.Key, "Plan item key") != entry.Key) throw new RuleException("Plan item keys must be trimmed.");
            _ = Rules.Name(entry.Title, "Title"); _ = Rules.Spec(entry.SpecJson);
            if (entry.DependencyKeys is null || entry.Criteria is null) throw new RuleException("Supply dependencies and acceptance criteria for each draft item.");
        }
        if (entries.Select(x => x.Key).Distinct().Count() != entries.Count) throw new RuleException("Plan item keys must be unique.");
        var byKey = entries.ToDictionary(x => x.Key);
        foreach (var entry in entries)
        {
            if (entry.ParentKey is not null && !byKey.ContainsKey(entry.ParentKey)) throw new RuleException($"Unknown parent: {entry.ParentKey}.");
            if (entry.DependencyKeys.Any(key => key is null || !byKey.ContainsKey(key))) throw new RuleException($"Unknown dependency in {entry.Key}.");
        }
        var mapped = new Dictionary<string, WorkItem>();
        var created = new HashSet<string>();
        var remaining = entries.ToList();
        while (remaining.Count > 0)
        {
            var ready = remaining.Where(x => x.ParentKey is null || mapped.ContainsKey(x.ParentKey)).ToList();
            if (ready.Count == 0) throw new RuleException("The plan hierarchy contains a cycle.");
            foreach (var entry in ready)
            {
                var receipt = data.PlanImports.SingleOrDefault(x => x.ProjectId == projectId && x.Source == source && x.Key == entry.Key);
                WorkItem item;
                if (receipt is not null)
                {
                    item = graph.Items.SingleOrDefault(x => x.Id == receipt.ItemId) ?? throw new RuleException("A previously imported item is missing. Refresh the draft.", 409);
                    if (entry.ExistingItemId.HasValue && entry.ExistingItemId != item.Id) throw new RuleException("This plan key already refers to another task.", 409);
                }
                else
                {
                    if (entry.ExistingItemId.HasValue) item = graph.Item(entry.ExistingItemId.Value);
                    else
                    {
                        item = graph.Add(entry.Title, entry.ParentKey is null ? null : mapped[entry.ParentKey].Id, null);
                        item.Edit(entry.Title, entry.SpecJson, item.StatusId, entry.Priority, null, null);
                        data.Items.Add(item); created.Add(entry.Key);
                        if (entry.Criteria.Count > 0)
                        {
                            var criteria = TaskAcceptanceCriteria.Create(item); criteria.Replace(entry.Criteria); data.AcceptanceCriteria.Add(criteria);
                        }
                    }
                    if (data.PlanImports.Any(x => x.ProjectId == projectId && x.Source == source && x.ItemId == item.Id)) throw new RuleException("Two plan keys cannot refer to the same existing task.");
                    data.PlanImports.Add(PlanImportEntry.Create(item, source, input.SourceHash, entry.Key));
                }
                mapped.Add(entry.Key, item); remaining.Remove(entry);
            }
        }
        foreach (var entry in entries.Where(x => created.Contains(x.Key)))
        {
            var links = graph.SetDependencies(mapped[entry.Key].Id, entry.DependencyKeys.Select(key => mapped[key].Id).ToList());
            graph.Dependencies.AddRange(links); data.Dependencies.AddRange(links);
        }
        graph.Project.Touch();
    }, ct);
}
