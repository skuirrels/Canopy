using Microsoft.EntityFrameworkCore;
using Planner.Api.Domain;
namespace Planner.Api.Infrastructure;

public sealed class PlannerDb(DbContextOptions<PlannerDb> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> Items => Set<WorkItem>();
    public DbSet<WorkflowStatus> Statuses => Set<WorkflowStatus>();
    public DbSet<Dependency> Dependencies => Set<Dependency>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<WorkItemAssignee> Assignments => Set<WorkItemAssignee>();
    public DbSet<ProjectAgentConnection> AgentConnections => Set<ProjectAgentConnection>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<SpecFileLink> SpecFiles => Set<SpecFileLink>();
    public DbSet<FactoryPipeline> FactoryPipelines => Set<FactoryPipeline>();
    public DbSet<PipelineVersion> PipelineVersions => Set<PipelineVersion>();
    public DbSet<TaskFactoryConfiguration> TaskFactoryConfigurations => Set<TaskFactoryConfiguration>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Project>().HasKey(x => x.Id);
        model.Entity<WorkItem>().HasKey(x => x.Id);
        model.Entity<WorkflowStatus>().HasKey(x => x.Id);
        model.Entity<Dependency>().HasKey(x => x.Id);
        model.Entity<Person>().HasKey(x => x.Id);
        model.Entity<ProjectAgentConnection>().HasKey(x => x.Id);
        model.Entity<ProjectAgentConnection>().Ignore(x => x.Model).Ignore(x => x.Settings);
        model.Entity<ProjectAgentConnection>().HasIndex(x => x.ProjectId).IsUnique();
        model.Entity<AgentRun>().HasKey(x => x.Id);
        model.Entity<SpecFileLink>().HasKey(x => x.Id);
        model.Entity<FactoryPipeline>().HasKey(x => x.Id);
        model.Entity<FactoryPipeline>().Ignore(x => x.Definition);
        model.Entity<FactoryPipeline>().Property(x => x.CreatedAt).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        model.Entity<FactoryPipeline>().HasIndex(x => x.ProjectId);
        model.Entity<PipelineVersion>().HasKey(x => x.Id);
        model.Entity<PipelineVersion>().Ignore(x => x.Definition);
        model.Entity<PipelineVersion>().Property(x => x.PublishedAt).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        model.Entity<PipelineVersion>().HasIndex(x => new { x.PipelineId, x.Number }).IsUnique();
        model.Entity<TaskFactoryConfiguration>().HasKey(x => x.Id);
        model.Entity<TaskFactoryConfiguration>().Ignore(x => x.Overrides);
        model.Entity<TaskFactoryConfiguration>().HasIndex(x => x.ProjectId);
        model.Entity<AgentRun>().Ignore(x => x.Active);
        model.Entity<AgentRun>().HasIndex(x => x.RequestId).IsUnique();
        model.Entity<AgentRun>().HasIndex(x => x.ItemId);
        model.Entity<WorkItem>().Ignore(x => x.AssigneeId).Ignore(x => x.AssigneeIds);
        model.Entity<WorkItemAssignee>().HasKey(x => x.Id);
        model.Entity<WorkItemAssignee>().HasIndex(x => new { x.ItemId, x.PersonId }).IsUnique();
        model.Entity<WorkItem>().HasMany(x => x.Assignments).WithOne().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.ClientCascade);
        model.Entity<WorkItem>().Navigation(x => x.Assignments).HasField("assignments").UsePropertyAccessMode(PropertyAccessMode.Field);
        foreach (var entity in model.Model.GetEntityTypes())
            entity.FindProperty("Id")!.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        model.Entity<WorkItem>().HasIndex(x => x.ProjectId);
        model.Entity<Dependency>().HasIndex(x => x.ProjectId);
        model.Entity<WorkflowStatus>().HasIndex(x => x.ProjectId);
        // Relationships are guarded by ProjectGraph. DuckDB cannot reliably update referenced
        // principals when physical FKs exist; this aggregate is committed under a single writer.
    }
}
