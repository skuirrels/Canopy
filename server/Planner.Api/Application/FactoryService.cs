using Planner.Api.Domain;
using System.Text.Json.Serialization;

namespace Planner.Api.Application;

public sealed record FactoryTemplate(string Id, string Name, string Description, string Source, FactoryDefinition Definition);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateFactoryPipeline(long ExpectedRevision, string Name, string Description, string? TemplateId = null, FactoryDefinition? Definition = null, string? Source = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditFactoryDraft(long ExpectedRevision, long ExpectedDraftRevision, string Name, string Description, FactoryDefinition Definition);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeFactoryPipeline(long ExpectedRevision, long ExpectedDraftRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AttachFactoryPipeline(long ExpectedRevision, Guid PipelineVersionId, FactoryOverrides Overrides);
public sealed record FactoryPipelineDetail(long ProjectRevision, FactoryPipeline Pipeline, IReadOnlyList<PipelineVersion> Versions);
public sealed record FactoryPipelineList(long ProjectRevision, IReadOnlyList<FactoryPipeline> Pipelines);
public sealed record TaskFactoryDetail(long ProjectRevision, TaskFactoryConfiguration? Configuration, PipelineVersion? Version, FactoryDefinition? ResolvedDefinition, bool UpdateAvailable, bool Archived);

public sealed class FactoryService(IWorkspaceStore store, IReadOnlyList<FactoryTemplate> templates)
{
    public IReadOnlyList<FactoryTemplate> Templates() => templates;
    public FactoryValidation Validate(FactoryDefinition definition)
    {
        FactoryRules.Serialize(definition);
        return FactoryRules.Validate(definition);
    }
    public async Task<FactoryPipelineList> List(Guid projectId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); var graph = data.Graph(projectId);
        return new(graph.Project.Revision, data.FactoryPipelines.Where(x => x.ProjectId == projectId).ToList());
    }
    public async Task<FactoryPipelineDetail> Read(Guid id, CancellationToken ct) => Detail(await store.ReadAsync(ct), id);
    public async Task<FactoryPipelineDetail> Create(Guid projectId, CreateFactoryPipeline input, CancellationToken ct)
    {
        if ((input.TemplateId is null) == (input.Definition is null)) throw new RuleException("Supply exactly one templateId or definition.");
        var template = input.TemplateId is null ? null : templates.SingleOrDefault(x => x.Id == input.TemplateId) ?? throw new RuleException("Factory template not found.", 404);
        if (template is not null && input.Source is not null) throw new RuleException("Template provenance is supplied by configuration; do not override its source.");
        var pipeline = FactoryPipeline.Create(projectId, input.Name, input.Description, template?.Source ?? input.Source!, template?.Definition ?? input.Definition!);
        var data = await store.WriteAsync(data =>
        {
            var graph = data.Graph(projectId, input.ExpectedRevision); data.FactoryPipelines.Add(pipeline); graph.Project.Touch();
        }, ct);
        return Detail(data, pipeline.Id);
    }
    public async Task<FactoryPipelineDetail> Edit(Guid id, EditFactoryDraft input, CancellationToken ct)
    {
        var data = await store.WriteAsync(data =>
        {
            var pipeline = Find(data, id); var graph = data.Graph(pipeline.ProjectId, input.ExpectedRevision);
            pipeline.Edit(input.ExpectedDraftRevision, input.Name, input.Description, input.Definition); graph.Project.Touch();
        }, ct);
        return Detail(data, id);
    }
    public async Task<FactoryPipelineDetail> Publish(Guid id, ChangeFactoryPipeline input, CancellationToken ct)
    {
        var data = await store.WriteAsync(data =>
        {
            var pipeline = Find(data, id); var graph = data.Graph(pipeline.ProjectId, input.ExpectedRevision);
            var number = data.PipelineVersions.Where(x => x.PipelineId == id).Select(x => x.Number).DefaultIfEmpty().Max() + 1;
            data.PipelineVersions.Add(pipeline.Publish(input.ExpectedDraftRevision, number)); graph.Project.Touch();
        }, ct);
        return Detail(data, id);
    }
    public async Task<FactoryPipelineDetail> Archive(Guid id, ChangeFactoryPipeline input, CancellationToken ct)
    {
        var data = await store.WriteAsync(data =>
        {
            var pipeline = Find(data, id); var graph = data.Graph(pipeline.ProjectId, input.ExpectedRevision);
            pipeline.Archive(input.ExpectedDraftRevision); graph.Project.Touch();
        }, ct);
        return Detail(data, id);
    }
    public async Task<PipelineVersion> Version(Guid id, CancellationToken ct) => FindVersion(await store.ReadAsync(ct), id);
    public async Task<TaskFactoryDetail> TaskConfiguration(Guid id, CancellationToken ct) => TaskDetail(await store.ReadAsync(ct), id);
    public async Task<TaskFactoryDetail> Attach(Guid id, AttachFactoryPipeline input, CancellationToken ct)
    {
        var data = await store.WriteAsync(data =>
        {
            var graph = data.ForItem(id, input.ExpectedRevision);
            if ((data.AgentRuns.Any(x => x.ItemId == id && x.Active) || data.FactoryRuns.Any(x => x.ItemId == id && x.Active))) throw new RuleException("Wait for this task's active run before changing its factory configuration.", 409);
            var version = FindVersion(data, input.PipelineVersionId); Find(data, version.PipelineId).RequireActive();
            var configuration = data.TaskFactoryConfigurations.SingleOrDefault(x => x.Id == id);
            if (configuration is null) { configuration = TaskFactoryConfiguration.Create(id, graph.Project.Id); data.TaskFactoryConfigurations.Add(configuration); }
            configuration.Attach(version, input.Overrides); graph.Project.Touch();
        }, ct);
        return TaskDetail(data, id);
    }
    public async Task<TaskFactoryDetail> Detach(Guid id, long revision, CancellationToken ct)
    {
        var data = await store.WriteAsync(data =>
        {
            var graph = data.ForItem(id, revision);
            if ((data.AgentRuns.Any(x => x.ItemId == id && x.Active) || data.FactoryRuns.Any(x => x.ItemId == id && x.Active))) throw new RuleException("Wait for this task's active run before changing its factory configuration.", 409);
            data.TaskFactoryConfigurations.RemoveAll(x => x.Id == id); graph.Project.Touch();
        }, ct);
        return TaskDetail(data, id);
    }
    private static FactoryPipeline Find(WorkspaceData data, Guid id) => data.FactoryPipelines.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Factory pipeline not found.", 404);
    private static PipelineVersion FindVersion(WorkspaceData data, Guid id) => data.PipelineVersions.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Published pipeline version not found.", 404);
    private static FactoryPipelineDetail Detail(WorkspaceData data, Guid id)
    {
        var pipeline = Find(data, id);
        return new(data.Graph(pipeline.ProjectId).Project.Revision, pipeline, data.PipelineVersions.Where(x => x.PipelineId == id).OrderBy(x => x.Number).ToList());
    }
    private static TaskFactoryDetail TaskDetail(WorkspaceData data, Guid id)
    {
        var item = data.Items.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Task not found.", 404);
        var revision = data.Graph(item.ProjectId).Project.Revision;
        var config = data.TaskFactoryConfigurations.SingleOrDefault(x => x.Id == id);
        if (config is null) return new(revision, null, null, null, false, false);
        var version = FindVersion(data, config.PipelineVersionId); var pipeline = Find(data, version.PipelineId);
        return new(revision, config, version, FactoryRules.Resolve(version.Definition, config.Overrides),
            data.PipelineVersions.Any(x => x.PipelineId == pipeline.Id && x.Number > version.Number), pipeline.Archived);
    }
}
