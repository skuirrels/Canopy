using System.Text.Json.Serialization;
using Planner.Api.Domain;
namespace Planner.Api.Application;

public sealed class FactorySetupOptions
{
    public string DefaultTemplateId { get; set; } = "";
    public Dictionary<string, FactorySkill[]> SuggestedSkills { get; set; } = [];
    public int DefaultCheckTimeoutSeconds { get; set; }
    // Common build and test commands offered when the repository has nothing to detect yet.
    public StarterCheckGroup[] StarterChecks { get; set; } = [];
}
public sealed class StarterCheckGroup
{
    public string Ecosystem { get; set; } = "";
    public string[] Commands { get; set; } = [];
}
public sealed record SuggestedCheckGroup(string Ecosystem, string Evidence, FactoryCommand[] Checks);
public interface IRepositoryChecks { IReadOnlyList<SuggestedCheckGroup> Suggest(string repositoryPath, int timeoutSeconds); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfigureFactorySetup(long ExpectedRevision, string TemplateId, string Name, FactoryCommand[] Commands,
    Dictionary<string, FactorySkill[]> Skills, Guid? ItemId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UseFactorySetup(long ExpectedRevision, Guid PipelineVersionId);
public sealed record FactorySetupDetail(long ProjectRevision, FactoryProjectSetup? Setup, PipelineVersion? Version,
    bool Available, ProjectAgentConnection? Connection, IReadOnlyList<AgentProvider> Providers,
    IReadOnlyList<FactoryTemplate> Templates, FactorySetupOptions Defaults, IReadOnlyList<SuggestedCheckGroup> SuggestedChecks);

public sealed class FactorySetupService(IWorkspaceStore store, IReadOnlyList<FactoryTemplate> templates,
    FactorySetupOptions options, IFactorySkillCatalogue catalogue, IAgentEnvironment agents, IRepositoryChecks repositoryChecks)
{
    private FactorySetupDetail Detail(WorkspaceData data, Guid id)
    {
        var graph = data.Graph(id); var setup = data.FactoryProjectSetups.SingleOrDefault(x => x.Id == id);
        var version = data.PipelineVersions.SingleOrDefault(x => x.Id == setup?.PipelineVersionId);
        var pipeline = data.FactoryPipelines.SingleOrDefault(x => x.Id == version?.PipelineId);
        var connection = data.AgentConnections.SingleOrDefault(x => x.ProjectId == id);
        return new(graph.Project.Revision, setup, version, pipeline is { Archived: false }, connection, agents.Providers(), templates, options,
            connection is null ? [] : repositoryChecks.Suggest(connection.RepositoryPath, options.DefaultCheckTimeoutSeconds));
    }
    public async Task<FactorySetupDetail> Read(Guid id, CancellationToken ct) => Detail(await store.ReadAsync(ct), id);
    public async Task<FactoryDefinition> Preview(Guid id, ConfigureFactorySetup input, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); data.Graph(id, input.ExpectedRevision);
        return await Definition(data, id, input, ct);
    }
    private async Task<FactoryDefinition> Definition(WorkspaceData data, Guid id, ConfigureFactorySetup input, CancellationToken ct)
    {
        var template = templates.SingleOrDefault(x => x.Id == input.TemplateId) ?? throw new RuleException("Choose a configured workflow template.");
        var connection = data.AgentConnections.SingleOrDefault(x => x.ProjectId == id) ?? throw new RuleException("Connect the project repository and agent first.");
        if (!agents.Providers().Any(p => p.Id == connection.Provider && p.Available)) throw new RuleException("The project's selected agent is unavailable on this backend.");
        var definition = FactoryProjectSetup.Assemble(template.Definition, input.Commands, input.Skills);
        var references = definition.Stages.SelectMany(s => s.Steps).SelectMany(s => s.Skills ?? []).Distinct().ToList();
        if (references.Count > 0)
        {
            var found = await catalogue.Read(connection.RepositoryPath, ct);
            foreach (var reference in references) found.Resolve(reference);
        }
        return definition;
    }
    public async Task<FactorySetupDetail> Configure(Guid id, ConfigureFactorySetup input, CancellationToken ct)
    {
        var observed = await store.ReadAsync(ct); observed.Graph(id, input.ExpectedRevision);
        var definition = await Definition(observed, id, input, ct);
        var template = templates.Single(x => x.Id == input.TemplateId);
        var data = await store.WriteAsync(data =>
        {
            var graph = data.Graph(id, input.ExpectedRevision);
            var setup = data.FactoryProjectSetups.SingleOrDefault(x => x.Id == id);
            var version = data.PipelineVersions.SingleOrDefault(x => x.Id == setup?.PipelineVersionId);
            var pipeline = data.FactoryPipelines.SingleOrDefault(x => x.Id == version?.PipelineId);
            var name = Rules.Name(input.Name);
            if (pipeline is { Archived: false } && setup?.TemplateId == input.TemplateId && pipeline.Source == template.Source)
            {
                if (pipeline.DraftRevision != pipeline.PublishedDraftRevision || data.PipelineVersions.Where(x => x.PipelineId == pipeline.Id).Max(x => x.Number) != version!.Number)
                    throw new RuleException("The managed workflow changed in the advanced designer. Review it there before replacing project setup.", 409);
                if (name != version.Name || FactoryRules.Serialize(definition) != FactoryRules.Serialize(version.Definition))
                {
                    pipeline.Edit(pipeline.DraftRevision, name, template.Description, definition);
                    version = pipeline.Publish(pipeline.DraftRevision, version.Number + 1); data.PipelineVersions.Add(version);
                }
            }
            else
            {
                pipeline = FactoryPipeline.Create(id, name, template.Description, template.Source, definition);
                version = pipeline.Publish(pipeline.DraftRevision, 1);
                data.FactoryPipelines.Add(pipeline); data.PipelineVersions.Add(version);
            }
            if (setup is null) { setup = FactoryProjectSetup.Create(id); data.FactoryProjectSetups.Add(setup); }
            setup.Adopt(input.TemplateId, version!);
            if (input.ItemId is { } itemId) Attach(data, id, itemId, version!);
            graph.Project.Touch();
        }, ct);
        return Detail(data, id);
    }
    private static void Attach(WorkspaceData data, Guid project, Guid item, PipelineVersion version)
    {
        if (!data.Items.Any(x => x.Id == item && x.ProjectId == project)) throw new RuleException("The task must belong to this project.", 404);
        if (data.AgentRuns.Any(x => x.ItemId == item && x.Active) || data.FactoryRuns.Any(x => x.ItemId == item && x.Active)) throw new RuleException("Wait for active task work before attaching a workflow.", 409);
        var current = data.TaskFactoryConfigurations.SingleOrDefault(x => x.Id == item);
        if (current is not null)
        {
            if (current.PipelineVersionId == version.Id && current.Overrides.Steps.Length == 0) return;
            throw new RuleException("This task already has a workflow. Change it explicitly in Advanced configuration.", 409);
        }
        current = TaskFactoryConfiguration.Create(item, project); current.Attach(version, new([])); data.TaskFactoryConfigurations.Add(current);
    }
    public async Task<TaskFactoryDetail> Use(Guid item, UseFactorySetup input, CancellationToken ct)
    {
        await store.WriteAsync(data =>
        {
            var graph = data.ForItem(item, input.ExpectedRevision);
            var setup = data.FactoryProjectSetups.SingleOrDefault(x => x.Id == graph.Project.Id);
            if (setup?.PipelineVersionId != input.PipelineVersionId) throw new RuleException("Project setup changed. Review the current workflow before using it.", 409);
            var version = data.PipelineVersions.Single(x => x.Id == input.PipelineVersionId);
            data.FactoryPipelines.Single(x => x.Id == version.PipelineId).RequireActive();
            Attach(data, graph.Project.Id, item, version); graph.Project.Touch();
        }, ct);
        return await new FactoryService(store, templates).TaskConfiguration(item, ct);
    }
}
