using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Planner.Api.Domain;

public sealed class RuleException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

public static partial class Rules
{
    public static string Name(string? value, string label = "Name")
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 1 or > 200) throw new RuleException($"{label} must contain 1–200 characters.");
        return name;
    }
    public static string Color(string? color) => color is not null && HexColor().IsMatch(color) ? color : throw new RuleException("Choose a valid hex colour.");
    public static void Revision(long expected, long actual)
    {
        if (expected != actual) throw new RuleException("This project changed since you loaded it. Your latest changes have not been saved. Refresh and try again.", 409);
    }
    public static string Spec(string? json)
    {
        if (json is null) throw new RuleException("The specification is required.");
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 2_000_000) throw new RuleException("This specification is too large (maximum 2 MB).");
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("type", out var type) || type.GetString() != "doc")
                throw new RuleException("The specification must be a rich-text document.");
        }
        catch (JsonException) { throw new RuleException("The specification contains invalid JSON."); }
        return json;
    }
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")] private static partial Regex HexColor();
}

public sealed class Project
{
    private Project() { }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Color { get; private set; } = "";
    public long Revision { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public static Project Create(string name, string description, string color) => new()
    { Id = Guid.NewGuid(), Name = Rules.Name(name), Description = (description ?? "").Trim(), Color = Rules.Color(color), CreatedAt = DateTime.UtcNow };
    public void Edit(string name, string description, string color) { Name = Rules.Name(name); Description = (description ?? "").Trim(); Color = Rules.Color(color); }
    public void Touch() => Revision++;
}

public sealed class WorkflowStatus
{
    private WorkflowStatus() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = "";
    public string Color { get; private set; } = "";
    public int Position { get; private set; }
    public bool IsCompleted { get; private set; }
    public static WorkflowStatus Create(Guid projectId, string name, string color, int position, bool completed = false) => new()
    { Id = Guid.NewGuid(), ProjectId = projectId, Name = Rules.Name(name), Color = Rules.Color(color), Position = position, IsCompleted = completed };
    public void Order(int position) => Position = position;
    public void Edit(string name, string color, bool completed) { Name = Rules.Name(name); Color = Rules.Color(color); IsCompleted = completed; }
}

public sealed class Person
{
    private Person() { }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public static Person Create(string name, IEnumerable<Person> people)
    {
        var person = new Person { Id = Guid.NewGuid() };
        person.Rename(name, people);
        return person;
    }
    public void Rename(string name, IEnumerable<Person> people)
    {
        var value = Rules.Name(name);
        if (people.Any(person => person.Id != Id && string.Equals(person.Name, value, StringComparison.OrdinalIgnoreCase)))
            throw new RuleException("That person already exists.");
        Name = value;
    }
}

public sealed class WorkItem
{
    private WorkItem() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? ParentId { get; private set; }
    public Guid StatusId { get; private set; }
    public string Title { get; private set; } = "";
    public string SpecJson { get; private set; } = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}";
    public string Priority { get; private set; } = "none";
    private readonly List<WorkItemAssignee> assignments = [];
    [JsonIgnore] public IReadOnlyCollection<WorkItemAssignee> Assignments => assignments.AsReadOnly();
    public IReadOnlyList<Guid> AssigneeIds => assignments.OrderBy(x => x.Position).Select(x => x.PersonId).ToArray();
    // Compatibility for existing API clients. The assignment collection is authoritative.
    public Guid? AssigneeId => AssigneeIds.Count == 0 ? null : AssigneeIds[0];
    public double? EstimatedHours { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public int Position { get; private set; }
    public double? X { get; private set; }
    public int SiblingPosition { get; private set; }
    public double? Y { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public static WorkItem Create(Guid project, string title, Guid? parent, Guid status, int position) => new()
    { Id = Guid.NewGuid(), ProjectId = project, Title = Rules.Name(title, "Title"), ParentId = parent, StatusId = status, Position = position, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    public void Edit(string title, string specJson, Guid status, string priority, DateOnly? start, DateOnly? due)
    {
        if (priority is not ("none" or "low" or "medium" or "high" or "urgent")) throw new RuleException("Choose a valid priority.");
        if (start.HasValue && due.HasValue && start > due) throw new RuleException("The due date must be on or after the start date.");
        Title = Rules.Name(title, "Title"); SpecJson = Rules.Spec(specJson); StatusId = status; Priority = priority; StartDate = start; DueDate = due; UpdatedAt = DateTime.UtcNow;
    }
    public void Estimate(double? hours)
    {
        if (hours.HasValue && (!double.IsFinite(hours.Value) || hours.Value < 0)) throw new RuleException("Estimated hours must be a finite number of zero or more.");
        EstimatedHours = hours; UpdatedAt = DateTime.UtcNow;
    }
    public void Move(Guid? parent, Guid status, int position) { if (ParentId != parent) { X = null; Y = null; } ParentId = parent; StatusId = status; Position = position; UpdatedAt = DateTime.UtcNow; }
    public void Order(int position) => Position = position;
    public void OrderSibling(int position) => SiblingPosition = position;
    public void Assign(IReadOnlyList<Guid> ids, IReadOnlyCollection<Person> people)
    {
        if (ids.Count != ids.Distinct().Count()) throw new RuleException("Include each assignee only once.");
        if (ids.Any(id => !people.Any(person => person.Id == id))) throw new RuleException("Assignee not found.");
        assignments.RemoveAll(assignment => !ids.Contains(assignment.PersonId));
        for (var position = 0; position < ids.Count; position++)
        {
            var existing = assignments.SingleOrDefault(assignment => assignment.PersonId == ids[position]);
            if (existing is null) assignments.Add(WorkItemAssignee.Create(Id, ids[position], position));
            else existing.Order(position);
        }
        UpdatedAt = DateTime.UtcNow;
    }
    public void Unassign(Guid personId) { assignments.RemoveAll(assignment => assignment.PersonId == personId); UpdatedAt = DateTime.UtcNow; }
    public void Place(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new RuleException("Map coordinates must be finite numbers.");
        X = x; Y = y;
    }
}

public sealed class Dependency
{
    private Dependency() { }
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid DependsOnId { get; private set; }
    public Guid ProjectId { get; private set; }
    public static Dependency Create(Guid project, Guid item, Guid dependsOn) => new() { Id = Guid.NewGuid(), ProjectId = project, ItemId = item, DependsOnId = dependsOn };
}

public sealed class WorkItemAssignee
{
    private WorkItemAssignee() { }
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid PersonId { get; private set; }
    public int Position { get; private set; }
    internal static WorkItemAssignee Create(Guid itemId, Guid personId, int position) => new()
    { Id = Guid.NewGuid(), ItemId = itemId, PersonId = personId, Position = position };
    internal void Order(int position) => Position = position;
}
