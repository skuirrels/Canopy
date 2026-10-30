using System.Text.Json;
using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Planner.Api.Domain;
using Planner.Api.Infrastructure;

var directory = Path.Combine(Path.GetTempPath(), "canopy-store-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var options = new DbContextOptionsBuilder<PlannerDb>()
        .UseDuckDB($"Data Source={Path.Combine(directory, "workspace.duckdb")}").Options;
    var factory = new TestFactory(options);
    await using (var db = factory.CreateDbContext()) await db.Database.MigrateAsync();
    using var store = new DuckDbWorkspaceStore(factory);
    var template = FactoryTemplateLoader.Load(args.Single()).Single(x => x.Id == "specification");
    var project = Project.Create("Persistence regression", "Temporary native database", "#477fa1");
    await store.WriteAsync(data => data.Projects.Add(project), default);
    var instant = DateTime.UtcNow;
    var microsecondTicks = instant.Ticks - instant.Ticks % TimeSpan.TicksPerMicrosecond;
    // Exercise sub-microsecond values on every OS, including clocks that only
    // generate microsecond DateTimes. The public provider determines storage precision.
    for (var remainder = 1; remainder < TimeSpan.TicksPerMicrosecond; remainder++)
    {
        var pipeline = FactoryPipeline.Create(project.Id, "Timestamp round trip", "Test only", template.Source, template.Definition);
        var version = pipeline.Publish(pipeline.DraftRevision, 1);
        var supplied = new DateTime(microsecondTicks + remainder, DateTimeKind.Utc);
        typeof(PipelineVersion).GetProperty(nameof(PipelineVersion.PublishedAt))!.SetValue(version, supplied);
        var saved = await store.WriteAsync(data =>
        {
            data.FactoryPipelines.Add(pipeline);
            data.PipelineVersions.Add(version);
        }, default);
        var returned = saved.PipelineVersions.Single(x => x.Id == version.Id);
        var persisted = (await store.ReadAsync(default)).PipelineVersions.Single(x => x.Id == version.Id);
        if (JsonSerializer.Serialize(returned) != JsonSerializer.Serialize(persisted))
            throw new Exception($"Write response differs from persisted version: {returned.PublishedAt:O} != {persisted.PublishedAt:O}");
        if (persisted.PublishedAt.Kind != DateTimeKind.Utc)
            throw new Exception("Persisted publication time lost UTC identity.");
        Console.WriteLine($"PASS: write/read timestamp identity for sub-microsecond remainder {remainder}.");
    }
    var before = (await store.ReadAsync(default)).Projects.Single().Revision;
    try
    {
        await store.WriteAsync(data => { data.Projects.Single().Touch(); throw new RuleException("Explicit rollback fixture"); }, default);
        throw new Exception("Invalid write did not fail.");
    }
    catch (RuleException) { }
    if ((await store.ReadAsync(default)).Projects.Single().Revision != before)
        throw new Exception("Invalid write changed persisted revision.");
    Console.WriteLine("PASS: failed write retains the previous persisted revision.");
}
finally { Directory.Delete(directory, recursive: true); }

sealed class TestFactory(DbContextOptions<PlannerDb> options) : IDbContextFactory<PlannerDb>
{
    public PlannerDb CreateDbContext() => new(options);
}
