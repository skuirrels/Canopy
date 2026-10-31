using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

public sealed record AgentExecutionSettings(string? Model, string? Effort)
{
    public static string? Normalize(string? value, string label) => string.IsNullOrWhiteSpace(value) ? null : Rules.Name(value, label);
    public AgentExecutionSettings Normalize() => new(Normalize(Model, "Model"), Normalize(Effort, "Effort"));
}

public sealed class ProjectAgentConnection
{
    private ProjectAgentConnection() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string RepositoryPath { get; private set; } = "";
    public string BaseRef { get; private set; } = "";
    public string Provider { get; private set; } = "";
    [JsonIgnore] public string? CodexModel { get; private set; }
    [JsonIgnore] public string? CodexEffort { get; private set; }
    [JsonIgnore] public string? ClaudeModel { get; private set; }
    [JsonIgnore] public string? ClaudeEffort { get; private set; }
    public string? Model => For(Provider).Model;
    public IReadOnlyDictionary<string, AgentExecutionSettings> Settings => new Dictionary<string, AgentExecutionSettings>
    { ["codex"] = new(CodexModel, CodexEffort), ["claude"] = new(ClaudeModel, ClaudeEffort) };
    public string Instructions { get; private set; } = "";
    public static ProjectAgentConnection Create(Guid projectId) => new() { Id = Guid.NewGuid(), ProjectId = projectId };
    public AgentExecutionSettings For(string provider)
    { AgentRun.RequireProvider(provider); return provider == "codex" ? new(CodexModel, CodexEffort) : new(ClaudeModel, ClaudeEffort); }
    public void Configure(string repositoryPath, string baseRef, string provider, string? model, string instructions, IReadOnlyDictionary<string, AgentExecutionSettings>? settings = null)
    {
        AgentRun.RequireProvider(provider);
        RepositoryPath = repositoryPath; BaseRef = Rules.Name(baseRef, "Base Git ref"); Provider = provider;
        if (settings is null || !settings.ContainsKey(provider)) Set(provider, new(model, For(provider).Effort));
        if (settings is not null) foreach (var (agent, value) in settings) Set(agent, value ?? throw new RuleException("Agent settings cannot be null."));
        Instructions = AgentRun.Text(instructions, "Project instructions");
    }
    private void Set(string provider, AgentExecutionSettings settings)
    {
        AgentRun.RequireProvider(provider); var normalized = settings.Normalize();
        if (provider == "codex") { CodexModel = normalized.Model; CodexEffort = normalized.Effort; }
        else { ClaudeModel = normalized.Model; ClaudeEffort = normalized.Effort; }
    }
}

public sealed class AgentRun
{
    private AgentRun() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? FactoryStepRunId { get; private set; }
    public string? FactoryPermission { get; private set; }
    public string ExecutionPermission => FactoryPermission ?? (Purpose == "implementation" ? "workspace-write" : "read-only");
    public Guid? PreviousRunId { get; private set; }
    public string Provider { get; private set; } = "";
    public string Purpose { get; private set; } = "";
    public string State { get; private set; } = "queued";
    public string Title { get; private set; } = "";
    public string SpecJson { get; private set; } = "";
    public long ProjectRevision { get; private set; }
    public string RepositoryPath { get; private set; } = "";
    public string BaseRef { get; private set; } = "";
    public string? Model { get; private set; }
    public string? Effort { get; private set; }
    public string? RequestedModel { get; private set; }
    public string? RequestedEffort { get; private set; }
    public string ProjectInstructions { get; private set; } = "";
    public string Instructions { get; private set; } = "";
    public string? SessionId { get; private set; }
    public string? WorkspacePath { get; private set; }
    public string? BaseCommit { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public string UsageJson { get; private set; } = "";
    public AgentUsage Usage => UsageJson.Length == 0 ? AgentUsage.Empty : System.Text.Json.JsonSerializer.Deserialize<AgentUsage>(UsageJson, FactoryRules.Json)!;
    public void ObserveUsage(AgentUsageObservation? observation, int maxEvents, int maxCharacters)
    { var before = Usage; var after = before.Append(observation, maxEvents, maxCharacters); if (!ReferenceEquals(before, after)) UsageJson = FactoryRules.Serialize(after); }
    public string Output { get; private set; } = "";
    public string Result { get; private set; } = "";
    public string Diff { get; private set; } = "";
    public string? Error { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public bool Active => State is "queued" or "running" or "cancelling";
    public static void RequireProvider(string provider)
    { if (provider is not ("codex" or "claude")) throw new RuleException("Choose Codex or Claude."); }
    public static string Text(string? value, string label)
    {
        var text = value?.Trim() ?? "";
        if (text.Length > 100_000) throw new RuleException($"{label} must contain at most 100,000 characters.");
        return text;
    }
    public static AgentRun Create(Guid requestId, WorkItem item, ProjectAgentConnection connection, long revision,
        string provider, string purpose, string? instructions, AgentRun? previous, string? model = null, string? effort = null)
    {
        RequireProvider(provider);
        if (requestId == Guid.Empty) throw new RuleException("A unique request ID is required.");
        if (purpose is not ("spec" or "documentation" or "implementation")) throw new RuleException("Choose a specification, documentation or implementation run.");
        if (previous?.FactoryStepRunId is not null) throw new RuleException("Factory step sessions belong to their work order. Start independent work or prepare a new factory run.", 409);
        var prompt = Text(instructions, "Run instructions");
        if (purpose == "implementation" && !HasSpec(item.SpecJson)) throw new RuleException("Write a specification before running an implementation.");
        if (purpose != "implementation" && prompt.Length == 0) throw new RuleException("Describe what you want the agent to write.");
        if (previous is not null && (previous.ItemId != item.Id || previous.Provider != provider || previous.Purpose != purpose
            || previous.RepositoryPath != connection.RepositoryPath || previous.SessionId is null || previous.WorkspacePath is null || previous.Active))
            throw new RuleException("Continue a finished run for this task with the same agent, purpose and repository.");
        var defaults = previous is null ? connection.For(provider) : new AgentExecutionSettings(previous.Model, previous.Effort);
        var resolvedModel = model is null ? defaults.Model : AgentExecutionSettings.Normalize(model, "Model");
        var resolvedEffort = effort is null ? defaults.Effort : AgentExecutionSettings.Normalize(effort, "Effort");
        return new()
        {
            Id = Guid.NewGuid(), RequestId = requestId, ProjectId = item.ProjectId, ItemId = item.Id, PreviousRunId = previous?.Id,
            Provider = provider, Purpose = purpose, Title = item.Title, SpecJson = item.SpecJson, ProjectRevision = revision,
            RepositoryPath = connection.RepositoryPath, BaseRef = connection.BaseRef, Model = resolvedModel, Effort = resolvedEffort,
            RequestedModel = model?.Trim(), RequestedEffort = effort?.Trim(),
            ProjectInstructions = connection.Instructions, Instructions = prompt, CreatedAt = DateTime.UtcNow,
            SessionId = previous?.SessionId, WorkspacePath = previous?.WorkspacePath, BaseCommit = previous?.BaseCommit
        };
    }
    public static AgentRun ForFactory(FactoryRun run, FactoryStepRun stepRun, FactoryStep step, string instructions, string? executionBase = null)
    {
        if (step.Kind != "agent" || step.Provider is null || step.Permission is not ("read-only" or "workspace-write") || run.WorkspacePath is null)
            throw new RuleException("The factory agent step is not ready.");
        if (step.Role == "review" && step.Permission != "read-only") throw new RuleException("Review requires read-only permissions.");
        RequireProvider(step.Provider);
        var snapshot = run.Snapshot;
        return new() { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), FactoryStepRunId = stepRun.Id, FactoryPermission = step.Permission,
            ProjectId = run.ProjectId, ItemId = run.ItemId, Provider = step.Provider, Purpose = step.Role == "specification" ? "spec" : step.Role!,
            Title = snapshot.Title, SpecJson = snapshot.SpecJson, ProjectRevision = snapshot.ProjectRevision,
            RepositoryPath = snapshot.Environment.RepositoryPath, BaseRef = executionBase ?? snapshot.Environment.BaseCommit, BaseCommit = executionBase ?? snapshot.Environment.BaseCommit,
            WorkspacePath = run.WorkspacePath, Model = step.Model, Effort = step.Effort,
            ProjectInstructions = snapshot.Environment.ProjectInstructions, Instructions = instructions, CreatedAt = DateTime.UtcNow };
    }
    internal static bool HasSpec(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return HasText(document.RootElement);
        static bool HasText(System.Text.Json.JsonElement node) => node.ValueKind == System.Text.Json.JsonValueKind.Object &&
            ((node.TryGetProperty("text", out var text) && text.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(text.GetString())) ||
             (node.TryGetProperty("content", out var content) && content.ValueKind == System.Text.Json.JsonValueKind.Array && content.EnumerateArray().Any(HasText)));
    }
    public void Start(string workspace, string? commit)
    {
        if (State != "queued") throw new RuleException("This run is no longer queued.", 409);
        WorkspacePath = workspace; BaseCommit = commit; State = "running"; StartedAt = DateTime.UtcNow;
    }
    public void RecordSession(string sessionId) { if (Guid.TryParse(sessionId, out _)) SessionId = sessionId; }
    public void Progress(string output, int maxCharacters) => Output = Tail(Output + output, maxCharacters);
    public void Cancel()
    {
        if (State == "queued") Finish("cancelled", "", "", null);
        else if (State == "running") State = "cancelling";
    }
    public void Finish(string state, string result, string diff, string? error)
    {
        if (!Active) return;
        if (state is not ("succeeded" or "failed" or "cancelled" or "interrupted")) throw new RuleException("Invalid run outcome.");
        State = State == "cancelling" && state != "interrupted" ? "cancelled" : state;
        Result = result; Diff = diff; Error = error; FinishedAt = DateTime.UtcNow;
    }
    public static string Tail(string text, int maxCharacters) => text.Length > maxCharacters ? text[^maxCharacters..] : text;
}
