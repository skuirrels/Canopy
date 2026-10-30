using Planner.Api.Domain;
namespace Planner.Api.Application;

public interface IFactorySkillCatalogue
{
    Task<FactorySkillCatalogue> Read(string? repositoryRoot, CancellationToken ct);
}
public sealed class FactorySkillService(IWorkspaceStore store, IFactorySkillCatalogue catalogue)
{
    public async Task<FactorySkillCatalogue> Read(Guid projectId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct); _ = data.Graph(projectId);
        return await catalogue.Read(data.AgentConnections.SingleOrDefault(x => x.ProjectId == projectId)?.RepositoryPath, ct);
    }
}
