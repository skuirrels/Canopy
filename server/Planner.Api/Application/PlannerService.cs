using Planner.Api.Domain;
namespace Planner.Api.Application;

public sealed class PlannerService(IWorkspaceStore store, IReadOnlyList<WorkflowDefault> defaults)
{
    public Task<WorkspaceData> Read(CancellationToken ct) => store.ReadAsync(ct);
    public Task<WorkspaceData> AddProject(CreateProject input, CancellationToken ct) => store.WriteAsync(data =>
    {
        if (defaults.Count == 0) throw new InvalidOperationException("Configure at least one default workflow status.");
        var project = Project.Create(input.Name, input.Description, input.Color); data.Projects.Add(project);
        data.Statuses.AddRange(defaults.Select((s, i) => WorkflowStatus.Create(project.Id, s.Name, s.Color, i, s.IsCompleted)));
    }, ct);
    public Task<WorkspaceData> UpdateProject(Guid id, EditProject input, CancellationToken ct) => store.WriteAsync(data =>
    { var graph = data.Graph(id, input.ExpectedRevision); graph.Project.Edit(input.Name, input.Description, input.Color); graph.Project.Touch(); }, ct);
    public Task<WorkspaceData> DeleteProject(Guid id, long revision, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.Graph(id, revision);
        if ((data.AgentRuns.Any(x => x.ProjectId == id && x.Active) || data.FactoryRuns.Any(x => x.ProjectId == id && x.Active))) throw new RuleException("Stop active agent runs before deleting this project.", 409);
        data.RemoveFactoryRuns(data.FactoryRuns.Where(x => x.ProjectId == id).Select(x => x.Id));
        data.AgentConnections.RemoveAll(x => x.ProjectId == id); data.AgentRuns.RemoveAll(x => x.ProjectId == id);
        data.SpecFiles.RemoveAll(x => x.ProjectId == id);
        data.TaskFactoryConfigurations.RemoveAll(x => x.ProjectId == id);
        data.PipelineVersions.RemoveAll(x => x.ProjectId == id); data.FactoryPipelines.RemoveAll(x => x.ProjectId == id);
        data.AcceptanceCriteria.RemoveAll(x => x.ProjectId == id);
        data.Dependencies.RemoveAll(x => x.ProjectId == id); data.Items.RemoveAll(x => x.ProjectId == id);
        data.Statuses.RemoveAll(x => x.ProjectId == id); data.Projects.Remove(graph.Project);
    }, ct);
    public Task<WorkspaceData> AddItem(Guid projectId, CreateItem input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.Graph(projectId, input.ExpectedRevision); var item = graph.Add(input.Title, input.ParentId, input.StatusId);
        data.Items.Add(item); graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> UpdateItem(Guid id, EditItem input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.ForItem(id, input.ExpectedRevision); graph.RequireStatus(input.StatusId);
        if (input.AssigneeIds is not null && input.AssigneeId.HasValue && input.AssigneeIds.FirstOrDefault() != input.AssigneeId)
            throw new RuleException("Supply assigneeIds or the legacy assigneeId, not conflicting values.");
        var assignees = input.AssigneeIds ?? (input.AssigneeId.HasValue ? [input.AssigneeId.Value] : new List<Guid>());
        var links = graph.SetDependencies(id, input.DependencyIds ?? throw new RuleException("Dependency IDs are required."));
        var item = graph.Item(id); item.Edit(input.Title, input.SpecJson, input.StatusId, input.Priority, input.StartDate, input.DueDate);
        item.Assign(assignees, data.People);
        if (input.HasEstimatedHours) item.Estimate(input.EstimatedHours);
        data.Dependencies.RemoveAll(x => x.ItemId == id); data.Dependencies.AddRange(links); graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> Move(Guid id, MoveItem input, CancellationToken ct) => store.WriteAsync(data =>
    { var graph = data.ForItem(id, input.ExpectedRevision); graph.Move(id, input.ParentId, input.StatusId, input.BeforeId); graph.Project.Touch(); }, ct);
    public Task<WorkspaceData> ReorderSiblings(Guid id, OrderSiblings input, CancellationToken ct) => store.WriteAsync(data =>
    { var graph = data.Graph(id, input.ExpectedRevision); graph.ReorderSiblings(input.ParentId, input.ItemIds); graph.Project.Touch(); }, ct);
    public Task<WorkspaceData> DeleteItem(Guid id, long revision, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.ForItem(id, revision); var deleted = graph.Subtree(id);
        if ((data.AgentRuns.Any(x => deleted.Contains(x.ItemId) && x.Active) || data.FactoryRuns.Any(x => deleted.Contains(x.ItemId) && x.Active))) throw new RuleException("Stop active agent runs before deleting these tasks.", 409);
        data.RemoveFactoryRuns(data.FactoryRuns.Where(x => deleted.Contains(x.ItemId)).Select(x => x.Id));
        data.AgentRuns.RemoveAll(x => deleted.Contains(x.ItemId));
        data.SpecFiles.RemoveAll(x => deleted.Contains(x.Id));
        data.AcceptanceCriteria.RemoveAll(x => deleted.Contains(x.Id));
        data.TaskFactoryConfigurations.RemoveAll(x => deleted.Contains(x.Id));
        data.Dependencies.RemoveAll(x => deleted.Contains(x.ItemId) || deleted.Contains(x.DependsOnId)); data.Items.RemoveAll(x => deleted.Contains(x.Id)); graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> Layout(Guid id, SaveLayout input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.Graph(id, input.ExpectedRevision);
        foreach (var position in input.Positions ?? throw new RuleException("Map positions are required.")) graph.Item(position.Id).Place(position.X, position.Y);
        graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> ReorderStatuses(Guid projectId, OrderStatuses input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.Graph(projectId, input.ExpectedRevision);
        if (input.StatusIds is null || input.StatusIds.Count != graph.Statuses.Count || !input.StatusIds.ToHashSet().SetEquals(graph.Statuses.Select(x => x.Id)))
            throw new RuleException("Include every workflow column exactly once.");
        for (var i = 0; i < input.StatusIds.Count; i++) graph.Statuses.Single(x => x.Id == input.StatusIds[i]).Order(i);
        graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> AddStatus(Guid projectId, CreateStatus input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.Graph(projectId, input.ExpectedRevision);
        data.Statuses.Add(WorkflowStatus.Create(projectId, input.Name, input.Color, graph.Statuses.Max(x => x.Position) + 1)); graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> UpdateStatus(Guid id, EditStatus input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var status = data.Statuses.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Column not found.", 404);
        var graph = data.Graph(status.ProjectId, input.ExpectedRevision); status.Edit(input.Name, input.Color, input.IsCompleted); graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> DeleteStatus(Guid id, long revision, CancellationToken ct) => store.WriteAsync(data =>
    {
        var status = data.Statuses.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Column not found.", 404);
        var graph = data.Graph(status.ProjectId, revision);
        if (graph.Statuses.Count <= 1) throw new RuleException("Keep at least one workflow column.");
        if (graph.Items.Any(x => x.StatusId == id)) throw new RuleException("Move this column’s tasks before deleting it.");
        data.Statuses.Remove(status); graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> RemovePerson(Guid id, CancellationToken ct) => store.WriteAsync(data =>
    {
        var person = data.People.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Assignee not found.", 404);
        var affected = data.Items.Where(x => x.AssigneeIds.Contains(id)).ToList();
        foreach (var item in affected) item.Unassign(id);
        foreach (var projectId in affected.Select(x => x.ProjectId).Distinct()) data.Graph(projectId).Project.Touch();
        data.People.Remove(person);
    }, ct);
    public Task<WorkspaceData> UpdatePerson(Guid id, EditPerson input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var person = data.People.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Assignee not found.", 404);
        person.Rename(input.Name, data.People);
    }, ct);
    public Task<WorkspaceData> AddPerson(CreatePerson input, CancellationToken ct) => store.WriteAsync(data =>
    {
        data.People.Add(Person.Create(input.Name, data.People));
    }, ct);
}
