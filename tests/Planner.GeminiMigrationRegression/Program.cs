using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planner.Api.Domain;
using Planner.Api.Infrastructure;

var directory = Path.Combine(Path.GetTempPath(), "canopy-gemini-migration-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var options = new DbContextOptionsBuilder<PlannerDb>().UseDuckDB($"Data Source={Path.Combine(directory, "upgrade.duckdb")}").Options;
    var id = Guid.NewGuid(); var projectId = Guid.NewGuid();
    await using (var old = new PlannerDb(options))
    {
        var previous = old.Database.GetMigrations().TakeWhile(x => !x.EndsWith("_GeminiAgent", StringComparison.Ordinal)).Last();
        await old.GetService<IMigrator>().MigrateAsync(previous);
        await old.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentConnections" ("Id", "ProjectId", "RepositoryPath", "BaseRef", "Provider", "CodexModel", "CodexEffort", "ClaudeModel", "ClaudeEffort", "Instructions")
            VALUES ({id}, {projectId}, {directory}, {"HEAD"}, {"claude"}, {"fixture-codex"}, {"high"}, {"fixture-claude"}, {"low"}, {"Retain project instructions"})
            """);
        await old.Database.ExecuteSqlRawAsync("CHECKPOINT");
        await old.GetService<IMigrator>().MigrateAsync("20261031102552_GeminiAgent");
        await old.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AgentConnections\" SET \"GeminiModel\" = {"fixture-existing-gemini"}");
        await old.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var current = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(current, default);
        var connection = await current.AgentConnections.SingleAsync();
        if (connection.Id != id || connection.ProjectId != projectId || connection.RepositoryPath != directory || connection.BaseRef != "HEAD" || connection.Provider != "claude" || connection.Instructions != "Retain project instructions"
            || connection.For("codex") != new AgentExecutionSettings("fixture-codex", "high") || connection.For("claude") != new AgentExecutionSettings("fixture-claude", "low") || connection.For("gemini") != new AgentExecutionSettings("fixture-existing-gemini", null))
            throw new Exception("Upgrade changed the existing connection or fabricated Gemini settings.");
        connection.Configure(directory, "HEAD", "gemini", "fixture-gemini", connection.Instructions, new Dictionary<string, AgentExecutionSettings> { ["gemini"] = new("fixture-gemini", "medium") });
        await current.SaveChangesAsync();
        await current.Database.ExecuteSqlRawAsync("CHECKPOINT");
    }
    await using (var restarted = new PlannerDb(options))
    {
        await PlannerDatabase.Initialize(restarted, default);
        var connection = await restarted.AgentConnections.SingleAsync();
        if (connection.Provider != "gemini" || connection.Model != "fixture-gemini" || connection.For("gemini").Effort != "medium" || connection.For("codex").Model != "fixture-codex" || connection.For("claude").Model != "fixture-claude")
            throw new Exception("Provider defaults did not survive update/checkpoint/restart.");
        await restarted.Database.OpenConnectionAsync();
        using var indexes = restarted.Database.GetDbConnection().CreateCommand();
        indexes.CommandText = "SELECT count(*) FROM duckdb_indexes() WHERE table_name='AgentConnections' AND index_name='IX_AgentConnections_ProjectId' AND is_unique";
        if (Convert.ToInt64(await indexes.ExecuteScalarAsync()) != 1) throw new Exception("Unique connection index was lost.");
    }
    Console.WriteLine("PASS: checkpointed connections retain all Codex/Claude fields; Gemini defaults, updates and unique index survive restart.");
}
finally { Directory.Delete(directory, true); }
