namespace Planner.Api.Domain;

// A project owns one default workflow. Published task attachments remain immutable.
public sealed class FactoryProjectSetup
{
    private FactoryProjectSetup() { }
    public Guid Id { get; private set; }
    public string TemplateId { get; private set; } = "";
    public Guid PipelineVersionId { get; private set; }
    public static FactoryProjectSetup Create(Guid project) => new() { Id = project };
    public void Adopt(string template, PipelineVersion version)
    {
        if (version.ProjectId != Id) throw new RuleException("The project workflow must belong to this project.");
        TemplateId = Rules.Name(template, "Template ID"); PipelineVersionId = version.Id;
    }

    public static FactoryDefinition Assemble(FactoryDefinition template, FactoryCommand[] commands, Dictionary<string, FactorySkill[]> skills)
    {
        if (commands is null || commands.Any(x => x is null) || skills is null || skills.Any(x => !FactoryRules.AgentRoles.Contains(x.Key) || x.Value is null))
            throw new RuleException("Supply verification commands and skills for valid agent roles.");
        var roles = template.Stages.SelectMany(s => s.Steps).Where(s => s.Kind == "agent").Select(s => s.Role).ToHashSet();
        if (skills.Any(x => x.Value.Length > 0 && !roles.Contains(x.Key))) throw new RuleException("Selected skills must belong to an agent role present in this workflow.");
        var slots = template.Stages.SelectMany(s => s.Steps).Count(s => s.Kind == "command");
        if (slots > 1) throw new RuleException("This template has multiple command groups. Configure it in the advanced designer.");
        if (slots == 1 && commands.Length == 0) throw new RuleException("Add the project's actual verification command before creating its workflow.");
        if (slots == 0 && commands.Length > 0) throw new RuleException("This workflow has no command step. Choose an implementation or verification workflow to use checks.");
        var definition = template with { Stages = template.Stages.Select(stage => stage with { Steps = stage.Steps.SelectMany(step =>
            step.Kind == "command" ? commands.Select((command, index) => step with
            { Id = commands.Length == 1 ? step.Id : step.Id + "-" + (index + 1), Name = commands.Length == 1 ? step.Name : step.Name + " " + (index + 1), Command = command,
                Produces = index == commands.Length - 1 ? step.Produces : [] }) :
            new[] { step.Kind == "agent" && skills.TryGetValue(step.Role!, out var selected) ? step with { Skills = selected } : step }).ToArray() }).ToArray() };
        FactoryRules.RequirePublishable(definition);
        return definition;
    }
}
