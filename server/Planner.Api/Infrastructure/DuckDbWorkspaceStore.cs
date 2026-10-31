using Microsoft.EntityFrameworkCore;
using Planner.Api.Application;
namespace Planner.Api.Infrastructure;

public sealed class DuckDbWorkspaceStore(IDbContextFactory<PlannerDb> factory) : IWorkspaceStore, IDisposable
{
    private readonly SemaphoreSlim writer = new(1, 1);
    public async Task<WorkspaceData> ReadAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var data = await Load(db, ct); await transaction.CommitAsync(ct); return data;
    }
    public async Task<WorkspaceData> WriteAsync(Action<WorkspaceData> change, CancellationToken ct)
    {
        await writer.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var data = await Load(db, ct);
            var deployments = data.Deployments.ToList(); var deploymentOperations = data.DeploymentOperations.ToList();
            var projects = data.Projects.ToList(); var items = data.Items.ToList(); var statuses = data.Statuses.ToList(); var dependencies = data.Dependencies.ToList(); var people = data.People.ToList();
            var connections = data.AgentConnections.ToList(); var runs = data.AgentRuns.ToList();
            var intakeSources = data.IntakeSources.ToList(); var intakeCollections = data.IntakeCollections.ToList(); var intakeCandidates = data.IntakeCandidates.ToList(); var intakeObservations = data.IntakeObservations.ToList(); var intakeDecisions = data.IntakeDecisions.ToList();
            var deliveryPolicies = data.DeliveryPolicies.ToList(); var deliveries = data.Deliveries.ToList(); var deliveryOperations = data.DeliveryOperations.ToList();
            var repairPolicies = data.RepairPolicies.ToList(); var repairLoops = data.RepairLoops.ToList(); var repairAttempts = data.RepairAttempts.ToList();
            var reviewRounds = data.ReviewRounds.ToList(); var reviewFindings = data.ReviewFindings.ToList();
            var criteria = data.AcceptanceCriteria.ToList(); var receipts = data.VerificationReceipts.ToList(); var verdicts = data.CriterionVerdicts.ToList();
            var executionPolicies = data.ExecutionPolicies.ToList();
            var artifacts = data.FactoryArtifacts.ToList();
            var factoryRuns = data.FactoryRuns.ToList(); var stageRuns = data.FactoryStageRuns.ToList(); var stepRuns = data.FactoryStepRuns.ToList();
            var specFiles = data.SpecFiles.ToList();
            var pipelines = data.FactoryPipelines.ToList(); var versions = data.PipelineVersions.ToList(); var taskFactory = data.TaskFactoryConfigurations.ToList();
            change(data);
            Sync(db, deployments, data.Deployments); Sync(db, deploymentOperations, data.DeploymentOperations);
            Sync(db, projects, data.Projects); Sync(db, items, data.Items); Sync(db, statuses, data.Statuses); Sync(db, dependencies, data.Dependencies); Sync(db, people, data.People);
            Sync(db, connections, data.AgentConnections); Sync(db, runs, data.AgentRuns);
            Sync(db, intakeSources, data.IntakeSources); Sync(db, intakeCollections, data.IntakeCollections); Sync(db, intakeCandidates, data.IntakeCandidates); Sync(db, intakeObservations, data.IntakeObservations); Sync(db, intakeDecisions, data.IntakeDecisions);
            Sync(db, deliveryPolicies, data.DeliveryPolicies); Sync(db, deliveries, data.Deliveries); Sync(db, deliveryOperations, data.DeliveryOperations);
            Sync(db, repairPolicies, data.RepairPolicies); Sync(db, repairLoops, data.RepairLoops); Sync(db, repairAttempts, data.RepairAttempts);
            Sync(db, reviewRounds, data.ReviewRounds); Sync(db, reviewFindings, data.ReviewFindings);
            Sync(db, criteria, data.AcceptanceCriteria); Sync(db, receipts, data.VerificationReceipts); Sync(db, verdicts, data.CriterionVerdicts);
            Sync(db, executionPolicies, data.ExecutionPolicies);
            Sync(db, artifacts, data.FactoryArtifacts);
            Sync(db, factoryRuns, data.FactoryRuns); Sync(db, stageRuns, data.FactoryStageRuns); Sync(db, stepRuns, data.FactoryStepRuns);
            Sync(db, specFiles, data.SpecFiles);
            Sync(db, pipelines, data.FactoryPipelines); Sync(db, versions, data.PipelineVersions); Sync(db, taskFactory, data.TaskFactoryConfigurations);
            await db.SaveChangesAsync(ct);
            // Return the database's actual representation, including timestamp precision,
            // from the same transaction. Tracked pre-save values can differ on Linux.
            db.ChangeTracker.Clear();
            var persisted = await Load(db, ct);
            await transaction.CommitAsync(ct); return persisted;
        }
        finally { writer.Release(); }
    }
    private static void Sync<T>(PlannerDb db, List<T> before, List<T> after) where T : class
    { db.RemoveRange(before.Except(after)); db.AddRange(after.Except(before)); }
    private static async Task<WorkspaceData> Load(PlannerDb db, CancellationToken ct) => new()
    {
        Deployments = await db.Deployments.ToListAsync(ct), DeploymentOperations = await db.DeploymentOperations.ToListAsync(ct),
        IntakeSources = await db.IntakeSources.ToListAsync(ct), IntakeCollections = await db.IntakeCollections.ToListAsync(ct), IntakeCandidates = await db.IntakeCandidates.ToListAsync(ct), IntakeObservations = await db.IntakeObservations.ToListAsync(ct), IntakeDecisions = await db.IntakeDecisions.ToListAsync(ct),
        DeliveryPolicies = await db.DeliveryPolicies.ToListAsync(ct), Deliveries = await db.Deliveries.ToListAsync(ct), DeliveryOperations = await db.DeliveryOperations.ToListAsync(ct),
        RepairPolicies = await db.RepairPolicies.ToListAsync(ct), RepairLoops = await db.RepairLoops.ToListAsync(ct), RepairAttempts = await db.RepairAttempts.ToListAsync(ct),
        ReviewRounds = await db.ReviewRounds.ToListAsync(ct), ReviewFindings = await db.ReviewFindings.ToListAsync(ct),
        AcceptanceCriteria = await db.AcceptanceCriteria.ToListAsync(ct),
        VerificationReceipts = await db.VerificationReceipts.ToListAsync(ct), CriterionVerdicts = await db.CriterionVerdicts.ToListAsync(ct),
        ExecutionPolicies = await db.ExecutionPolicies.ToListAsync(ct),
        FactoryArtifacts = await db.FactoryArtifacts.ToListAsync(ct),
        FactoryRuns = await db.FactoryRuns.OrderBy(x => x.CreatedAt).ToListAsync(ct),
        FactoryStageRuns = await db.FactoryStageRuns.ToListAsync(ct), FactoryStepRuns = await db.FactoryStepRuns.ToListAsync(ct),
        Projects = await db.Projects.OrderBy(x => x.CreatedAt).ToListAsync(ct),
        SpecFiles = await db.SpecFiles.ToListAsync(ct),
        FactoryPipelines = await db.FactoryPipelines.OrderBy(x => x.CreatedAt).ToListAsync(ct),
        PipelineVersions = await db.PipelineVersions.OrderBy(x => x.Number).ToListAsync(ct),
        TaskFactoryConfigurations = await db.TaskFactoryConfigurations.ToListAsync(ct),
        AgentConnections = await db.AgentConnections.ToListAsync(ct), AgentRuns = await db.AgentRuns.OrderBy(x => x.CreatedAt).ToListAsync(ct),
        Items = await db.Items.Include(x => x.Assignments).AsSplitQuery().OrderBy(x => x.Position).ThenBy(x => x.CreatedAt).ToListAsync(ct),
        Statuses = await db.Statuses.OrderBy(x => x.Position).ToListAsync(ct),
        Dependencies = await db.Dependencies.ToListAsync(ct), People = await db.People.OrderBy(x => x.Name).ToListAsync(ct)
    };
    public void Dispose() => writer.Dispose();
}
