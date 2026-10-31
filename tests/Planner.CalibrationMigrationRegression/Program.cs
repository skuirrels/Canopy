using System.Data.Common;
using System.Text.Json;
using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planner.Api.Infrastructure;

var directory = Path.Combine(Path.GetTempPath(), "canopy-calibration-migration-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var options = new DbContextOptionsBuilder<PlannerDb>().UseDuckDB($"Data Source={Path.Combine(directory, "upgrade.duckdb")}").Options;
    var original = new Dictionary<string, object?>
    {
        ["Id"] = Guid.NewGuid(), ["RequestId"] = Guid.NewGuid(), ["ProjectId"] = Guid.NewGuid(), ["ItemId"] = Guid.NewGuid(),
        ["PreviousRunId"] = Guid.NewGuid(), ["Provider"] = "codex", ["Purpose"] = "review", ["State"] = "succeeded",
        ["Title"] = "Prior release record", ["SpecJson"] = "{}", ["ProjectRevision"] = 17L,
        ["RepositoryPath"] = directory, ["BaseRef"] = "fixture-branch", ["Model"] = "fixture-explicit-model", ["Effort"] = "high",
        ["RequestedModel"] = "fixture-requested-model", ["RequestedEffort"] = "medium", ["ProjectInstructions"] = "Project input",
        ["Instructions"] = "Caller input", ["SessionId"] = Guid.NewGuid().ToString(), ["WorkspacePath"] = directory,
        ["BaseCommit"] = "immutable-fixture-commit", ["Output"] = "Retained protocol", ["Result"] = "Retained result",
        ["Diff"] = "Retained diff", ["Error"] = "Retained diagnostic", ["CreatedAt"] = DateTime.UtcNow.AddMinutes(-10),
        ["StartedAt"] = DateTime.UtcNow.AddMinutes(-9), ["FinishedAt"] = DateTime.UtcNow.AddMinutes(-8),
        ["FactoryStepRunId"] = Guid.NewGuid(), ["FactoryPermission"] = "read-only", ["UsageJson"] = ""
    };
    string columns = string.Join(",", original.Keys.Select(key => '"' + key + '"'));
    async Task<string> Read(PlannerDb db)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = $"SELECT {columns} FROM AgentRuns";
        await using var reader = await command.ExecuteReaderAsync(); if (!await reader.ReadAsync()) throw new Exception("Prior record disappeared.");
        return JsonSerializer.Serialize(Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue).ToArray());
    }
    string before;
    await using (var old = new PlannerDb(options))
    {
        await old.GetService<IMigrator>().MigrateAsync("20261031021540_FactoryParallelWorkspaces");
        await old.Database.OpenConnectionAsync();
        await using var insert = old.Database.GetDbConnection().CreateCommand();
        var names = original.Keys.Select((_, i) => "p" + i).ToArray();
        insert.CommandText = $"INSERT INTO AgentRuns ({columns}) VALUES ({string.Join(",", names.Select(x => '$' + x))})";
        foreach (var (entry, i) in original.Select((x, i) => (x, i))) { var param = insert.CreateParameter(); param.ParameterName = names[i]; param.Value = entry.Value ?? DBNull.Value; insert.Parameters.Add(param); }
        await insert.ExecuteNonQueryAsync(); before = await Read(old); await old.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var current = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(current, default); await current.Database.OpenConnectionAsync();
        if (before != await Read(current)) throw new Exception("Migration changed prior agent execution fields.");
        var agent = await current.AgentRuns.SingleAsync();
        if (agent.CalibrationJobId is not null || await current.CalibrationPolicies.AnyAsync() || await current.Calibrations.AnyAsync() || await current.CalibrationTriggers.AnyAsync()) throw new Exception("Migration fabricated calibration authority, execution or receipts.");
        if (JsonSerializer.Serialize(agent, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Contains("calibrationJobId")) throw new Exception("Legacy agent response changed by a missing optional calibration association.");
        if (agent.Usage.Status != "unknown" || agent.Usage.Observations.Count != 0) throw new Exception("Old execution was assigned fabricated usage.");
        agent.Progress("New retained activity", 2000); await current.SaveChangesAsync();
        using var indexes = current.Database.GetDbConnection().CreateCommand(); indexes.CommandText = "SELECT count(*) FROM duckdb_indexes() WHERE table_name='AgentRuns' AND index_name IN ('IX_AgentRuns_RequestId','IX_AgentRuns_ItemId','IX_AgentRuns_FactoryStepRunId')";
        if (Convert.ToInt64(await indexes.ExecuteScalarAsync()) != 3) throw new Exception("Existing indexes were not preserved.");
        await current.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var restarted = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(restarted, default);
        var agent = await restarted.AgentRuns.SingleAsync(); if (!agent.Output.EndsWith("New retained activity") || agent.Usage.Status != "unknown") throw new Exception("Migrated row cannot be updated and restarted intact.");
    }
    Console.WriteLine("PASS: prior native agent fields and indexes survive calibration upgrade, update and restart; optional association stays absent, new calibration tables empty and missing historical usage unknown.");
}
finally { Directory.Delete(directory, true); }
