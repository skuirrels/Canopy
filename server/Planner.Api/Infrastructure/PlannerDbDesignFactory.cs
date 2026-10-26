using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Planner.Api.Infrastructure;

// EF tooling inspects the same model without starting the web host or locking the live file.
public sealed class PlannerDbDesignFactory : IDesignTimeDbContextFactory<PlannerDb>
{
    public PlannerDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PlannerDb>()
        .UseDuckDB("Data Source=:memory:").Options);
}
