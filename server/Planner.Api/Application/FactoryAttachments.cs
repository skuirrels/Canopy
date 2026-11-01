using Planner.Api.Domain;
namespace Planner.Api.Application;

public sealed record EffectiveFactoryAttachment(PipelineVersion Version, FactoryOverrides Overrides, bool Inherited);
// A task uses its own attachment when it has one; otherwise it follows the project's current workflow.
public static class FactoryAttachments
{
    public static EffectiveFactoryAttachment? Effective(WorkspaceData data, Guid itemId)
    {
        var own = data.TaskFactoryConfigurations.SingleOrDefault(x => x.Id == itemId);
        if (own is not null) return new(data.PipelineVersions.Single(x => x.Id == own.PipelineVersionId), own.Overrides, false);
        var item = data.Items.SingleOrDefault(x => x.Id == itemId);
        var setup = data.FactoryProjectSetups.SingleOrDefault(x => x.Id == item?.ProjectId);
        var version = data.PipelineVersions.SingleOrDefault(x => x.Id == setup?.PipelineVersionId);
        if (version is null || data.FactoryPipelines.SingleOrDefault(x => x.Id == version.PipelineId) is not { Archived: false }) return null;
        return new(version, new([]), true);
    }
    public static IReadOnlyList<WorkItem> OpenPrerequisites(WorkspaceData data, Guid itemId) =>
        data.Dependencies.Where(x => x.ItemId == itemId).Select(d => data.Items.Single(i => i.Id == d.DependsOnId))
            .Where(i => !data.Statuses.Single(s => s.Id == i.StatusId).IsCompleted).ToList();
}
