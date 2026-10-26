namespace Planner.Api.Domain;

// One project is the consistency boundary for hierarchy, ordering and dependency rules.
public sealed class ProjectGraph(Project project, List<WorkItem> items, List<WorkflowStatus> statuses, List<Dependency> dependencies)
{
    public Project Project { get; } = project;
    public List<WorkItem> Items { get; } = items;
    public List<WorkflowStatus> Statuses { get; } = statuses;
    public List<Dependency> Dependencies { get; } = dependencies;
    public WorkItem Item(Guid id) => Items.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Task not found.", 404);
    public void RequireStatus(Guid id)
    {
        if (!Statuses.Any(x => x.Id == id)) throw new RuleException("Choose a column in this project.");
    }
    public void RequireParent(Guid? id) { if (id.HasValue) _ = Item(id.Value); }
    public HashSet<Guid> Subtree(Guid id)
    {
        _ = Item(id);
        var found = new HashSet<Guid> { id };
        var queue = new Queue<Guid>(); queue.Enqueue(id);
        while (queue.TryDequeue(out var parent))
            foreach (var child in Items.Where(x => x.ParentId == parent))
                if (found.Add(child.Id)) queue.Enqueue(child.Id);
        return found;
    }
    public WorkItem Add(string title, Guid? parent, Guid? status)
    {
        RequireParent(parent);
        var column = status ?? Statuses.OrderBy(x => x.Position).First().Id;
        RequireStatus(column);
        var position = Items.Count(x => x.ParentId == parent && x.StatusId == column);
        var item = WorkItem.Create(Project.Id, title, parent, column, position);
        item.OrderSibling(NextSiblingPosition(parent)); Items.Add(item); return item;
    }
    public void Move(Guid id, Guid? parent, Guid status, Guid? before)
    {
        var item = Item(id); RequireParent(parent); RequireStatus(status);
        if (parent.HasValue && Subtree(id).Contains(parent.Value)) throw new RuleException("A task cannot be moved inside itself or one of its subitems.");
        var siblings = Items.Where(x => x.Id != id && x.ParentId == parent && x.StatusId == status).OrderBy(x => x.Position).ThenBy(x => x.CreatedAt).ToList();
        var index = before.HasValue ? siblings.FindIndex(x => x.Id == before) : siblings.Count;
        if (index < 0) throw new RuleException("The drop target is no longer in this column.");
        if (item.ParentId != parent) item.OrderSibling(NextSiblingPosition(parent));
        item.Move(parent, status, index); siblings.Insert(index, item);
        for (var i = 0; i < siblings.Count; i++) siblings[i].Order(i);
    }
    private int NextSiblingPosition(Guid? parent) => Items.Where(x => x.ParentId == parent).Select(x => x.SiblingPosition).DefaultIfEmpty(-1).Max() + 1;
    public void ReorderSiblings(Guid? parent, IReadOnlyList<Guid>? ids)
    {
        RequireParent(parent);
        var siblings = Items.Where(x => x.ParentId == parent).ToList();
        if (ids is null || ids.Count != siblings.Count || !ids.ToHashSet().SetEquals(siblings.Select(x => x.Id)))
            throw new RuleException("Include every component in this branch exactly once.");
        var byId = siblings.ToDictionary(x => x.Id);
        for (var i = 0; i < ids.Count; i++) byId[ids[i]].OrderSibling(i);
    }
    public List<Dependency> SetDependencies(Guid id, IReadOnlyList<Guid> targetIds)
    {
        _ = Item(id);
        var targets = targetIds.Distinct().ToList();
        foreach (var target in targets)
        {
            _ = Item(target);
            if (target == id) throw new RuleException("A task cannot depend on itself.");
        }
        var proposed = Dependencies.Where(x => x.ItemId != id).Select(x => (x.ItemId, x.DependsOnId)).Concat(targets.Select(x => (id, x))).ToList();
        var edges = proposed.GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToList());
        var visiting = new HashSet<Guid>(); var visited = new HashSet<Guid>();
        bool Cycle(Guid node)
        {
            if (visiting.Contains(node)) return true;
            if (!visited.Add(node)) return false;
            visiting.Add(node);
            if (edges.TryGetValue(node, out var outgoing) && outgoing.Any(Cycle)) return true;
            visiting.Remove(node); return false;
        }
        if (Items.Any(x => Cycle(x.Id))) throw new RuleException("This dependency would create a circular chain.");
        return targets.Select(x => Dependency.Create(Project.Id, id, x)).ToList();
    }
}
