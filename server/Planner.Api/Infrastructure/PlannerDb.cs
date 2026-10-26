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
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Project>().HasKey(x => x.Id);
        model.Entity<WorkItem>().HasKey(x => x.Id);
        model.Entity<WorkflowStatus>().HasKey(x => x.Id);
        model.Entity<Dependency>().HasKey(x => x.Id);
        model.Entity<Person>().HasKey(x => x.Id);
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
