using Planner.Api.Domain;
using System.Text.Json.Serialization;
namespace Planner.Api.Application;

public sealed record CreateProject(string Name, string Description, string Color);
public sealed record EditProject(long ExpectedRevision, string Name, string Description, string Color);
public sealed record CreateItem(long ExpectedRevision, string Title, Guid? ParentId, Guid? StatusId);
public sealed record EditItem(long ExpectedRevision, string Title, string SpecJson, Guid StatusId, string Priority, Guid? AssigneeId, DateOnly? StartDate, DateOnly? DueDate, List<Guid> DependencyIds, List<Guid>? AssigneeIds = null)
{
    private double? estimatedHours;
    [JsonIgnore] public bool HasEstimatedHours { get; private set; }
    // Track explicit null separately from omission, so older clients preserve estimates.
    public double? EstimatedHours { get => estimatedHours; init { estimatedHours = value; HasEstimatedHours = true; } }
}
public sealed record MoveItem(long ExpectedRevision, Guid? ParentId, Guid StatusId, Guid? BeforeId);
public sealed record LayoutPosition(Guid Id, double X, double Y);
public sealed record SaveLayout(long ExpectedRevision, List<LayoutPosition> Positions);
public sealed record OrderStatuses(long ExpectedRevision, List<Guid> StatusIds);
public sealed record OrderSiblings(long ExpectedRevision, Guid? ParentId, List<Guid> ItemIds);
public sealed record CreateStatus(long ExpectedRevision, string Name, string Color);
public sealed record EditStatus(long ExpectedRevision, string Name, string Color, bool IsCompleted);
public sealed record CreatePerson(string Name);
public sealed record EditPerson(string Name);
public sealed record WorkflowDefault(string Name, string Color, bool IsCompleted);
public sealed class WorkspaceData
{
    [JsonIgnore] public List<ProjectAgentConnection> AgentConnections { get; init; } = [];
    [JsonIgnore] public List<AgentRun> AgentRuns { get; init; } = [];
    public List<Project> Projects { get; init; } = [];
    public List<WorkItem> Items { get; init; } = [];
    public List<WorkflowStatus> Statuses { get; init; } = [];
    public List<Dependency> Dependencies { get; init; } = [];
    public List<Person> People { get; init; } = [];
    public ProjectGraph Graph(Guid projectId, long? revision = null)
    {
        var project = Projects.SingleOrDefault(p => p.Id == projectId) ?? throw new RuleException("Project not found.", 404);
        if (revision.HasValue) Rules.Revision(revision.Value, project.Revision);
        return new(project, Items.Where(x => x.ProjectId == projectId).ToList(), Statuses.Where(x => x.ProjectId == projectId).ToList(), Dependencies.Where(x => x.ProjectId == projectId).ToList());
    }
    public ProjectGraph ForItem(Guid itemId, long revision)
    {
        var item = Items.SingleOrDefault(x => x.Id == itemId) ?? throw new RuleException("Task not found.", 404);
        return Graph(item.ProjectId, revision);
    }
}
public interface IWorkspaceStore
{
    Task<WorkspaceData> ReadAsync(CancellationToken cancellationToken);
    Task<WorkspaceData> WriteAsync(Action<WorkspaceData> change, CancellationToken cancellationToken);
}
