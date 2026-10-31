using System.Text.Json;
using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planner.Api.Domain;
using Planner.Api.Infrastructure;

var directory = Path.Combine(Path.GetTempPath(), "canopy-parallel-migration-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var options = new DbContextOptionsBuilder<PlannerDb>().UseDuckDB($"Data Source={Path.Combine(directory, "upgrade.duckdb")}").Options;
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var project = Project.Create("Prior native workspace", "Explicit migration fixture", "#477fa1");
    var status = WorkflowStatus.Create(project.Id, "Prior workflow input", "#477fa1", 0);
    var item = WorkItem.Create(project.Id, "Prior task", null, status.Id, 0);
    const string priorPolicyJson = "{\"enabled\":true,\"routes\":[],\"maxAgentInvocations\":0,\"maxCommandInvocations\":0,\"maxElapsedSeconds\":120,\"allowUnmeteredCost\":false,\"maxTokens\":null,\"maxCost\":null,\"currency\":null}";
    var policy = JsonSerializer.Deserialize<FactoryExecutionPolicy>(priorPolicyJson, json)!;
    if (FactoryRules.Serialize(policy) != priorPolicyJson) throw new Exception("Optional parallelism changed legacy policy bytes or hash.");
    var adopted = ProjectFactoryExecutionPolicy.Adopt(project.Id, policy);
    var definition = new FactoryDefinition([new("prior", "Prior stage", "Check", [], [new("gate", "Prior manual", "manual", "Caller gate")])]);
    var snapshot = new FactoryRunSnapshot(Guid.NewGuid(), 1, "Prior pipeline", "Caller fixture", item.Title, item.SpecJson, project.Revision,
        new([]), new(directory, "HEAD", "prior-immutable-commit", "Caller instructions", "codex", definition, []), [], DateTime.UtcNow,
        Execution: new(policy, FactoryArtifact.Hash(priorPolicyJson), []));
    var run = FactoryRun.Prepare(Guid.NewGuid(), item, snapshot);
    var stage = FactoryStageRun.Prepare(run.Id, definition.Stages[0]); var step = FactoryStepRun.Prepare(run.Id, stage.Id, definition.Stages[0].Steps[0]);
    run.Queue(run.Revision, Guid.NewGuid()); run.Begin(directory); stage.Begin(); step.Begin(); step.Wait(); stage.Wait(); run.Wait();
    async Task<string> Evidence(PlannerDb db) => JsonSerializer.Serialize(new {
        Projects = await db.Projects.ToArrayAsync(), Items = await db.Items.ToArrayAsync(), Policies = await db.ExecutionPolicies.ToArrayAsync(),
        Runs = await db.FactoryRuns.ToArrayAsync(), Stages = await db.FactoryStageRuns.ToArrayAsync(), Steps = await db.FactoryStepRuns.ToArrayAsync()
    }, json);
    string before;
    await using (var old = new PlannerDb(options))
    {
        await old.GetService<IMigrator>().MigrateAsync("20261031011605_FactoryAnalyticsIntake");
        old.AddRange(project, status, item, adopted, run, stage, step); await old.SaveChangesAsync(); old.ChangeTracker.Clear();
        before = await Evidence(old); await old.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var current = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(current, default);
        if (before != await Evidence(current)) throw new Exception("Parallel upgrade changed prior native project, policy, work order, stage or decision records.");
        if (await current.ParallelRuns.AnyAsync() || await current.ParallelStages.AnyAsync()) throw new Exception("Migration invented parallel execution evidence.");
        var retained = await current.FactoryRuns.SingleAsync();
        if (FactoryParallel.Enabled(retained) || retained.Snapshot.Execution!.PolicyHash != FactoryArtifact.Hash(priorPolicyJson)) throw new Exception("Prior policy no longer resolves as intact serial execution.");
        retained.Continue(); await current.SaveChangesAsync(); await current.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var restarted = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(restarted, default);
        if ((await restarted.FactoryRuns.SingleAsync()).State != "running" || (await restarted.ExecutionPolicies.SingleAsync()).PolicyJson != priorPolicyJson)
            throw new Exception("Migrated serial records cannot be updated and restarted intact.");
    }
    Console.WriteLine("PASS: prior native work orders, policies, hashes, gates and workflow survive parallel upgrade; new tables remain empty; serial records stay editable across restart.");
}
finally { Directory.Delete(directory, true); }
