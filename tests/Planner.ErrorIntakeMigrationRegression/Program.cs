using System.Text.Json;
using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planner.Api.Domain;
using Planner.Api.Infrastructure;

var directory = Path.Combine(Path.GetTempPath(), "canopy-error-migration-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var options = new DbContextOptionsBuilder<PlannerDb>().UseDuckDB($"Data Source={Path.Combine(directory, "upgrade.duckdb")}").Options;
    var projectId = Guid.NewGuid();
    var config = new IntakeConfiguration("Prior source", "githubIssues", true, "fixture/issues", [], 0, []).Validate();
    var source = FactoryIntakeSource.Create(projectId, config, "prior-provider-binding", "prior-provider-identity");
    if (source.ConfigurationJson != "{\"name\":\"Prior source\",\"kind\":\"githubIssues\",\"enabled\":true,\"repository\":\"fixture/issues\",\"labels\":[],\"minimumOccurrences\":0,\"categories\":[]}") throw new Exception("Optional error settings changed the prior source representation.");
    var batch = FactoryIntakeCollection.Begin(source, Guid.NewGuid(), "prior-request-hash"); batch.Finish("complete", null, 1);
    var candidate = FactoryIntakeCandidate.Create(projectId, "prior-fingerprint");
    var observation = IntakeObservation.Create(candidate.Id, batch.Id, new(candidate.Fingerprint, "Prior issue", "Retained description", "Configured prior provider", null, true, "{\"retained\":true}"));
    candidate.Observe(observation); candidate.Decide("verify", "Prior operator rationale", observation, null);
    var decision = IntakeDecision.Create(Guid.NewGuid(), "prior-decision-hash", candidate, "verify", "Prior operator rationale");
    async Task<string> Evidence(PlannerDb db) => JsonSerializer.Serialize(new {
        Sources = await db.IntakeSources.ToArrayAsync(), Collections = await db.IntakeCollections.ToArrayAsync(),
        Candidates = await db.IntakeCandidates.ToArrayAsync(), Observations = await db.IntakeObservations.ToArrayAsync(), Decisions = await db.IntakeDecisions.ToArrayAsync()
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    string before;
    await using (var old = new PlannerDb(options))
    {
        await old.GetService<IMigrator>().MigrateAsync("20261030231425_FactoryExecutionPolicy");
        old.AddRange(source, batch, candidate, observation, decision); await old.SaveChangesAsync();
        old.ChangeTracker.Clear(); before = await Evidence(old); await old.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var current = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(current, default);
        if (before != await Evidence(current)) throw new Exception("Error-feed upgrade changed prior intake identity, hashes or verified evidence.");
        if (await current.ErrorFeedReceipts.AnyAsync() || await current.ErrorEventIdentities.AnyAsync()) throw new Exception("Migration fabricated error-feed coverage or events.");
        var retained = await current.IntakeSources.SingleAsync();
        if (retained.ConfigurationJson != source.ConfigurationJson) throw new Exception("Stored source JSON changed during upgrade.");
        retained.Edit(retained.Revision, retained.Configuration with { Name = "Explicitly edited after upgrade" }, retained.ProviderBinding, "prior-provider-identity");
        await current.SaveChangesAsync(); await current.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var restarted = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(restarted, default);
        if ((await restarted.IntakeSources.SingleAsync()).Configuration.Name != "Explicitly edited after upgrade" || (await restarted.IntakeCandidates.SingleAsync()).State != "verified") throw new Exception("Prior intake cannot be edited and restarted intact.");
    }
    Console.WriteLine("PASS: prior source JSON, scope, hashes, observations, decisions and verification survive native upgrade; new error tables stay empty; old sources remain editable across restart.");
}
finally { Directory.Delete(directory, true); }
