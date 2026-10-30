using System.Diagnostics;
using System.Reflection;
using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Planner.Api.Infrastructure;

if (args.Length == 2)
{
    var options = new DbContextOptionsBuilder<PlannerDb>().UseDuckDB($"Data Source={args[1]}").Options;
    await using var db = new PlannerDb(options);
    if (args[0] == "initialize") { await PlannerDatabase.Initialize(db, default); return; }
    await db.Database.OpenConnectionAsync();
    if (args[0] == "seed")
    {
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("CHECKPOINT");
        await db.Database.ExecuteSqlRawAsync("PRAGMA disable_checkpoint_on_shutdown");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE wal_probe (id INTEGER PRIMARY KEY, request_id VARCHAR NOT NULL, state VARCHAR NOT NULL)");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX wal_probe_request ON wal_probe(request_id)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO wal_probe VALUES (1, 'explicit-test-request', 'running')");
    }
    else if (args[0] == "unsafe-replay") await db.Database.ExecuteSqlRawAsync("SELECT 1");
    else if (args[0] == "update")
    {
        using var original = db.Database.GetDbConnection().CreateCommand(); original.CommandText = "SELECT state FROM wal_probe";
        if ((string?)await original.ExecuteScalarAsync() != "running") throw new Exception("The original WAL-recovered row was not preserved.");
        await db.Database.ExecuteSqlRawAsync("UPDATE wal_probe SET state='interrupted' WHERE id=1");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM wal_probe WHERE id=1");
        using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT count(*) FROM wal_probe";
        if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 0) throw new Exception("Indexed recovered rows were not removed.");
    }
    else throw new Exception("Unknown test mode.");
    return;
}

var directory = Path.Combine(Path.GetTempPath(), "canopy-wal-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    async Task<(int Code, string Output)> Run(string mode, string file)
    {
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); info.ArgumentList.Add(mode); info.ArgumentList.Add(file);
        using var process = Process.Start(info) ?? throw new Exception("Cannot start native WAL test child.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        return (process.ExitCode, await output + await error);
    }
    var unsafeFile = Path.Combine(directory, "control.duckdb"); var safeFile = Path.Combine(directory, "initialized.duckdb");
    foreach (var file in new[] { unsafeFile, safeFile })
    {
        var seeded = await Run("seed", file); if (seeded.Code != 0) throw new Exception(seeded.Output);
        if (!File.Exists(file + ".wal")) throw new Exception("The interruption fixture did not retain a pending WAL.");
    }
    var control = await Run("unsafe-replay", unsafeFile); if (control.Code != 0) throw new Exception(control.Output);
    var broken = await Run("update", unsafeFile);
    if (broken.Code == 0 || !broken.Output.Contains("Failed to delete all rows from index")) throw new Exception("The pinned native failure control did not reproduce: " + broken.Output);
    Console.WriteLine("PASS: pinned native engine reproduces secondary-index corruption after context-free WAL shutdown checkpoint.");
    var initialized = await Run("initialize", safeFile); if (initialized.Code != 0) throw new Exception(initialized.Output);
    var recovered = await Run("update", safeFile); if (recovered.Code != 0) throw new Exception(recovered.Output);
    Console.WriteLine("PASS: actual application initialization preserves WAL-recovered indexes through restart, update and deletion.");
}
finally { Directory.Delete(directory, recursive: true); }
