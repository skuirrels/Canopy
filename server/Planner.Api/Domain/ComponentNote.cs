namespace Planner.Api.Domain;

// Notes record what was discussed about a component, such as a call. They are kept
// apart from the specification so meeting history never rewrites the plan itself.
public sealed class ComponentNote
{
    public const int MaxMarkdownBytes = 2_000_000;
    private ComponentNote() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public string Title { get; private set; } = "";
    public DateOnly? HeldOn { get; private set; }
    public string Markdown { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public static ComponentNote Create(Guid projectId, Guid itemId, string? title, DateOnly? heldOn, string? markdown)
    {
        var note = new ComponentNote { Id = Guid.NewGuid(), ProjectId = projectId, ItemId = itemId, CreatedAt = DateTime.UtcNow };
        note.Edit(title, heldOn, markdown);
        return note;
    }
    public void Edit(string? title, DateOnly? heldOn, string? markdown)
    {
        var name = title?.Trim() ?? "";
        if (name.Length > 200) throw new RuleException("Note titles must contain at most 200 characters.");
        if (markdown is null) throw new RuleException("The note text is required. Send an empty string for a blank note.");
        if (System.Text.Encoding.UTF8.GetByteCount(markdown) > MaxMarkdownBytes) throw new RuleException("This note is too large (maximum 2 MB).");
        Title = name; HeldOn = heldOn; Markdown = markdown; UpdatedAt = DateTime.UtcNow;
    }
}
