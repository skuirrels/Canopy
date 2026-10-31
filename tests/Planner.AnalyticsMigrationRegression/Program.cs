using System.Text.Json;
using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planner.Api.Domain;
using Planner.Api.Infrastructure;

var directory = Path.Combine(Path.GetTempPath(), "canopy-analytics-migration-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var options = new DbContextOptionsBuilder<PlannerDb>().UseDuckDB($"Data Source={Path.Combine(directory, "upgrade.duckdb")}").Options;
    var projectId = Guid.NewGuid();
    var config = new IntakeConfiguration("Prior source", "applicationErrors", true, "", [], 1, [], "prior-error-feed", ["error"]).Validate();
    var observedAt = DateTimeOffset.UtcNow;
    var identity = new ErrorFeedIdentity("prior-source", "Prior application", "Prior environment");
    var occurrence = new ErrorOccurrence("prior-event", observedAt.AddMinutes(-1), "error", "prior-release");
    var group = new ErrorGroup("prior-group", "Prior failure", "PriorFailure", "The configured operation failed.", [new("src/prior.cs", "Handle", 1)], [occurrence]);
    var serializer = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var source = FactoryIntakeSource.Create(projectId, config, "prior-provider-binding", IntakeRules.Fingerprint(identity));
    if (source.ConfigurationJson.Contains("analytics", StringComparison.OrdinalIgnoreCase)) throw new Exception("Optional analytics settings changed the prior source representation.");
    var batch = FactoryIntakeCollection.Begin(source, Guid.NewGuid(), "prior-request-hash"); batch.Finish("complete", null, 1);
    var candidate = FactoryIntakeCandidate.Create(projectId, IntakeRules.Fingerprint("applicationErrors", identity, group.Id));
    var evidence = JsonSerializer.Serialize(new ErrorGroupEvidence("applicationErrors", config.ErrorFeedId!, identity, group), serializer);
    var observation = IntakeObservation.Create(candidate.Id, batch.Id, new(candidate.Fingerprint, group.Title, group.MessageTemplate, "Configured prior provider", null, true, evidence));
    candidate.Observe(observation); candidate.Decide("verify", "Prior operator rationale", observation, null);
    var decision = IntakeDecision.Create(Guid.NewGuid(), "prior-decision-hash", candidate, "verify", "Prior operator rationale");
    var snapshot = new ErrorFeedSnapshot(1, identity.SourceId, identity.Application, identity.Environment, observedAt, observedAt.AddMinutes(-2), observedAt.AddSeconds(-1), true, true, [group]);
    var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, serializer);
    var coverage = new ErrorFeedCoverage(config.ErrorFeedId!, identity, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), bytes.Length, snapshot.GeneratedAt, snapshot.WindowStart, snapshot.WindowEnd, 1, 1, 1, 1);
    var receipt = FactoryErrorFeedReceipt.Capture(batch, coverage);
    var eventIdentity = FactoryErrorEventIdentity.Capture(projectId, identity, new(group.Id, occurrence));
    async Task<string> Evidence(PlannerDb db) => JsonSerializer.Serialize(new {
        Sources = await db.IntakeSources.ToArrayAsync(), Collections = await db.IntakeCollections.ToArrayAsync(),
        Candidates = await db.IntakeCandidates.ToArrayAsync(), Observations = await db.IntakeObservations.ToArrayAsync(), Decisions = await db.IntakeDecisions.ToArrayAsync(), ErrorReceipts = await db.ErrorFeedReceipts.ToArrayAsync(), ErrorEvents = await db.ErrorEventIdentities.ToArrayAsync()
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    string before;
    await using (var old = new PlannerDb(options))
    {
        await old.GetService<IMigrator>().MigrateAsync("20261031004524_FactoryErrorFeedReceipts");
        old.AddRange(source, batch, candidate, observation, decision, receipt, eventIdentity); await old.SaveChangesAsync();
        old.ChangeTracker.Clear(); before = await Evidence(old); await old.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var current = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(current, default);
        if (before != await Evidence(current)) throw new Exception("Analytics upgrade changed prior intake identity, hashes, error receipts or event identities.");
        if (await current.AnalyticsReceipts.AnyAsync() || await current.AnalyticsReportIdentities.AnyAsync()) throw new Exception("Migration fabricated analytics coverage or reports.");
        var retained = await current.IntakeSources.SingleAsync();
        if (retained.ConfigurationJson != source.ConfigurationJson) throw new Exception("Stored source JSON changed during upgrade.");
        retained.Edit(retained.Revision, retained.Configuration with { Name = "Explicitly edited after upgrade" }, retained.ProviderBinding, IntakeRules.Fingerprint(identity));
        await current.SaveChangesAsync(); await current.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var restarted = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(restarted, default);
        if ((await restarted.IntakeSources.SingleAsync()).Configuration.Name != "Explicitly edited after upgrade" || (await restarted.IntakeCandidates.SingleAsync()).State != "verified") throw new Exception("Prior intake cannot be edited and restarted intact.");
    }
    Console.WriteLine("PASS: prior error-source JSON, scope, verification, observations, decisions, file receipt and event identity survive analytics upgrade; analytics tables stay empty; existing sources remain editable across restart.");
}
finally { Directory.Delete(directory, true); }
