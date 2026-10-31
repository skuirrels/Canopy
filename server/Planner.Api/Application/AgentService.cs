using Planner.Api.Domain;

namespace Planner.Api.Application;

public sealed record SaveAgentConnection(long ExpectedRevision, string RepositoryPath, string BaseRef, string Provider, string? Model, string Instructions, Dictionary<string, AgentExecutionSettings>? Settings = null);
public sealed record StartAgentRun(Guid RequestId, long ExpectedRevision, string Provider, string Purpose, string Instructions, Guid? PreviousRunId, string? Model = null, string? Effort = null);
public sealed record AgentProvider(string Id, string Name, bool Available, IReadOnlyList<string> EffortLevels);
public interface IAgentEnvironment
{
    int PollIntervalMs { get; }
    IReadOnlyList<AgentProvider> Providers();
    Task<string> ValidateRepository(string path, string gitRef, CancellationToken ct);
    void Cancel(Guid runId);
}

public sealed class AgentService(IWorkspaceStore store, IAgentEnvironment environment)
{
    public object Providers() => new { providers = environment.Providers(), pollIntervalMs = environment.PollIntervalMs };
    public async Task<ProjectAgentConnection?> Connection(Guid projectId, CancellationToken ct)
    { var data = await store.ReadAsync(ct); data.Graph(projectId); return data.AgentConnections.SingleOrDefault(x => x.ProjectId == projectId); }
    public async Task<ProjectAgentConnection> SaveConnection(Guid projectId, SaveAgentConnection input, CancellationToken ct)
    {
        AgentRun.RequireProvider(input.Provider);
        var path = await environment.ValidateRepository(input.RepositoryPath, input.BaseRef, ct);
        var data = await store.WriteAsync(data =>
        {
            var graph = data.Graph(projectId, input.ExpectedRevision);
            if (data.Calibrations.Any(x => x.ProjectId == projectId && (x.Active || x.State == "needsAttention"))) throw new RuleException("Finish or reconcile project calibration before changing its connection.", 409);
            if (((data.Deliveries.Any(x => x.ProjectId == projectId && x.Open) || data.RepairLoops.Any(x => x.ProjectId == projectId && x.Open)) || data.AgentRuns.Any(x => x.ProjectId == projectId && x.Active) || data.FactoryRuns.Any(x => x.ProjectId == projectId && x.Active))) throw new RuleException("Stop active runs and repair loops, and close open deliveries before changing the connection.", 409);
            var connection = data.AgentConnections.SingleOrDefault(x => x.ProjectId == projectId);
            if (connection is null) { connection = ProjectAgentConnection.Create(projectId); data.AgentConnections.Add(connection); }
            connection.Configure(path, input.BaseRef, input.Provider, input.Model, input.Instructions, input.Settings);
            foreach (var (provider, settings) in connection.Settings) RequireEffort(provider, settings.Effort);
            graph.Project.Touch();
        }, ct);
        return data.AgentConnections.Single(x => x.ProjectId == projectId);
    }
    public async Task Disconnect(Guid projectId, long revision, CancellationToken ct) => await store.WriteAsync(data =>
    {
        var graph = data.Graph(projectId, revision);
        if (data.Calibrations.Any(x => x.ProjectId == projectId && (x.Active || x.State == "needsAttention"))) throw new RuleException("Finish or reconcile project calibration before disconnecting.", 409);
        if (((data.Deliveries.Any(x => x.ProjectId == projectId && x.Open) || data.RepairLoops.Any(x => x.ProjectId == projectId && x.Open)) || data.AgentRuns.Any(x => x.ProjectId == projectId && x.Active) || data.FactoryRuns.Any(x => x.ProjectId == projectId && x.Active))) throw new RuleException("Stop active runs and repair loops, and close open deliveries before disconnecting.", 409);
        data.AgentConnections.RemoveAll(x => x.ProjectId == projectId); graph.Project.Touch();
    }, ct);
    public async Task<IReadOnlyList<AgentRun>> Runs(Guid itemId, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct);
        if (!data.Items.Any(x => x.Id == itemId)) throw new RuleException("Task not found.", 404);
        return data.AgentRuns.Where(x => x.ItemId == itemId && x.FactoryStepRunId == null && x.CalibrationJobId == null).OrderByDescending(x => x.CreatedAt).ToList();
    }
    public async Task<AgentRun> Run(Guid id, CancellationToken ct) => Find(await store.ReadAsync(ct), id);
    public async Task<AgentRun> Start(Guid itemId, StartAgentRun input, CancellationToken ct)
    {
        var data = await store.WriteAsync(data =>
        {
            var duplicate = data.AgentRuns.SingleOrDefault(x => x.RequestId == input.RequestId);
            if (duplicate is not null)
            {
                if (duplicate.FactoryStepRunId is not null || duplicate.CalibrationJobId is not null || duplicate.ItemId != itemId || duplicate.Provider != input.Provider || duplicate.Purpose != input.Purpose || duplicate.Instructions != (input.Instructions ?? "").Trim() || duplicate.PreviousRunId != input.PreviousRunId || duplicate.RequestedModel != input.Model?.Trim() || duplicate.RequestedEffort != input.Effort?.Trim())
                    throw new RuleException("That request ID was already used for different work.", 409);
                return;
            }
            var graph = data.ForItem(itemId, input.ExpectedRevision);
            var connection = data.AgentConnections.SingleOrDefault(x => x.ProjectId == graph.Project.Id) ?? throw new RuleException("Connect this project to a repository first.");
            AgentRun.RequireProvider(input.Provider);
            if (!environment.Providers().Any(x => x.Id == input.Provider && x.Available)) throw new RuleException("The selected agent executable is unavailable. Install it or configure Agents in the backend.", 503);
            if (data.CalibrationBusy(itemId)) throw new RuleException("Finish or reconcile task calibration first.", 409);
            if ((data.AgentRuns.Any(x => x.ItemId == itemId && x.Active) || (data.Deliveries.Any(x => x.ItemId == itemId && x.Open) || data.RepairLoops.Any(x => x.ItemId == itemId && x.Open)) || data.FactoryRuns.Any(x => x.ItemId == itemId && x.Active))) throw new RuleException("This task already has an active run.", 409);
            var previous = input.PreviousRunId.HasValue ? Find(data, input.PreviousRunId.Value) : null;
            var run = AgentRun.Create(input.RequestId, graph.Item(itemId), connection, graph.Project.Revision, input.Provider, input.Purpose, input.Instructions, previous, input.Model, input.Effort);
            RequireEffort(run.Provider, run.Effort);
            if (input.Purpose == "implementation" && data.Dependencies.Where(x => x.ItemId == itemId).Any(d => !data.Statuses.Single(s => s.Id == data.Items.Single(i => i.Id == d.DependsOnId).StatusId).IsCompleted))
                throw new RuleException("Complete prerequisite tasks before running this implementation.", 409);
            data.AgentRuns.Add(run);
        }, ct);
        return data.AgentRuns.Single(x => x.RequestId == input.RequestId);
    }
    public async Task<AgentRun> Cancel(Guid id, CancellationToken ct)
    {
        var data = await store.WriteAsync(data => Find(data, id).Cancel(), ct);
        environment.Cancel(id); return Find(data, id);
    }
    public static AgentRun Find(WorkspaceData data, Guid id) => data.AgentRuns.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Agent run not found.", 404);
    private void RequireEffort(string provider, string? effort)
    {
        if (effort is not null && !environment.Providers().Single(x => x.Id == provider).EffortLevels.Contains(effort))
            throw new RuleException("Choose an effort level offered for the selected agent.");
    }
}
