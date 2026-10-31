using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planner.Api.Domain;
using Planner.Api.Infrastructure;

var directory = Path.Combine(Path.GetTempPath(), "canopy-setup-upgrade-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var options = new DbContextOptionsBuilder<PlannerDb>().UseDuckDB($"Data Source={Path.Combine(directory, "upgrade.duckdb")}").Options;
    var project = Project.Create("Prior project", "Existing user description", "#477fa1");
    await using (var prior = new PlannerDb(options))
    {
        var migrations = prior.Database.GetMigrations().ToArray();
        var setupIndex = Array.FindIndex(migrations, x => x.EndsWith("_FactoryProjectSetup", StringComparison.Ordinal));
        if (setupIndex < 1) throw new Exception("Cannot identify setup's predecessor migration.");
        await prior.GetService<IMigrator>().MigrateAsync(migrations[setupIndex - 1]);
        prior.Projects.Add(project); await prior.SaveChangesAsync();
        await prior.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var current = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(current, default);
        var retained = await current.Projects.SingleAsync();
        if (retained.Id != project.Id || retained.Name != project.Name || retained.Description != project.Description || retained.Color != project.Color || retained.Revision != project.Revision)
            throw new Exception("Setup upgrade changed an existing project.");
        if (await current.FactoryProjectSetups.AnyAsync() || await current.FactoryRuns.AnyAsync() || await current.AgentRuns.AnyAsync()) throw new Exception("Upgrade invented setup or execution state.");
    }
    Console.WriteLine("PASS: checkpointed native database upgraded without changing existing project data or enabling setup/execution.");
}
finally { Directory.Delete(directory, true); }
