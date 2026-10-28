using Microsoft.EntityFrameworkCore;
using Planner.Api.Application;
namespace Planner.Api.Infrastructure;

public sealed class DuckDbWorkspaceStore(IDbContextFactory<PlannerDb> factory) : IWorkspaceStore, IDisposable
{
    private readonly SemaphoreSlim writer = new(1, 1);
    public async Task<WorkspaceData> ReadAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var data = await Load(db, ct); await transaction.CommitAsync(ct); return data;
    }
    public async Task<WorkspaceData> WriteAsync(Action<WorkspaceData> change, CancellationToken ct)
    {
        await writer.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var data = await Load(db, ct);
            var projects = data.Projects.ToList(); var items = data.Items.ToList(); var statuses = data.Statuses.ToList(); var dependencies = data.Dependencies.ToList(); var people = data.People.ToList();
            var connections = data.AgentConnections.ToList(); var runs = data.AgentRuns.ToList();
            var specFiles = data.SpecFiles.ToList();
            change(data);
            Sync(db, projects, data.Projects); Sync(db, items, data.Items); Sync(db, statuses, data.Statuses); Sync(db, dependencies, data.Dependencies); Sync(db, people, data.People);
            Sync(db, connections, data.AgentConnections); Sync(db, runs, data.AgentRuns);
            Sync(db, specFiles, data.SpecFiles);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return data;
        }
        finally { writer.Release(); }
    }
    private static void Sync<T>(PlannerDb db, List<T> before, List<T> after) where T : class
    { db.RemoveRange(before.Except(after)); db.AddRange(after.Except(before)); }
    private static async Task<WorkspaceData> Load(PlannerDb db, CancellationToken ct) => new()
    {
        Projects = await db.Projects.OrderBy(x => x.CreatedAt).ToListAsync(ct),
        SpecFiles = await db.SpecFiles.ToListAsync(ct),
        AgentConnections = await db.AgentConnections.ToListAsync(ct), AgentRuns = await db.AgentRuns.OrderBy(x => x.CreatedAt).ToListAsync(ct),
        Items = await db.Items.Include(x => x.Assignments).AsSplitQuery().OrderBy(x => x.Position).ThenBy(x => x.CreatedAt).ToListAsync(ct),
        Statuses = await db.Statuses.OrderBy(x => x.Position).ToListAsync(ct),
        Dependencies = await db.Dependencies.ToListAsync(ct), People = await db.People.OrderBy(x => x.Name).ToListAsync(ct)
    };
    public void Dispose() => writer.Dispose();
}
