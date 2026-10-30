using Microsoft.EntityFrameworkCore;

namespace Planner.Api.Infrastructure;

public static class PlannerDatabase
{
    public static async Task Initialize(PlannerDb db, CancellationToken ct)
    {
        // Keep a client context alive across WAL recovery and migration. The pinned
        // native engine can serialize stale, lazily bound secondary indexes when
        // shutdown checkpoints a replayed WAL without an active context.
        // https://github.com/duckdb/duckdb/pull/23848
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("CHECKPOINT", ct);
            await db.Database.MigrateAsync(ct);
            await db.Database.ExecuteSqlRawAsync("CHECKPOINT", ct);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
