using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Planner.Api.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactorySkill(string Id, string Source);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactoryCommand(string Executable, string[] Arguments, string WorkingDirectory, int TimeoutSeconds,
    Dictionary<string, string>? EnvironmentReferences = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactoryStep(string Id, string Name, string Kind, string? Instructions = null,
    string? Role = null, string? Permission = null, string? Provider = null, string? Model = null, string? Effort = null,
    FactorySkill[]? Skills = null, FactoryCommand? Command = null, string[]? Consumes = null, string[]? Produces = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactoryStage(string Id, string Name, string Group, string[] DependsOn, FactoryStep[] Steps);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactoryDefinition(FactoryStage[] Stages, int SchemaVersion = 1);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactoryStepOverride(string StageId, string StepId, string? Instructions = null,
    string? Provider = null, string? Model = null, string? Effort = null, FactorySkill[]? Skills = null, FactoryCommand? Command = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactoryOverrides(FactoryStepOverride[] Steps);
public sealed record FactoryDiagnostic(string Code, string Severity, string Message, string Field, string? StageId = null, string? StepId = null);
public sealed record FactoryValidation(bool Valid, string ExecutionReadiness, IReadOnlyList<FactoryDiagnostic> Diagnostics);

public static partial class FactoryRules
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // Configuration documents follow the existing rich specification size boundary.
    private const int MaxDocumentBytes = 2_000_000;
    public static string Serialize<T>(T document)
    {
        if (document is null) throw new RuleException("Factory configuration is required.");
        var json = JsonSerializer.Serialize(document, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxDocumentBytes) throw new RuleException("Factory configuration must be smaller than 2 MB.");
        return json;
    }
    public static FactoryValidation Validate(FactoryDefinition? definition)
    {
        var diagnostics = new List<FactoryDiagnostic>();
        void Error(string code, string message, string field, string? stage = null, string? step = null) =>
            diagnostics.Add(new(code, "error", message, field, stage, step));
        if (definition is not null && definition.SchemaVersion != 1) Error("definition.schema", "Only factory definition schema version 1 is supported.", "schemaVersion");
        if (definition?.Stages is not { Length: > 0 } stages)
        {
            Error("stages.required", "Include at least one stage.", "stages");
            return new(false, "notEvaluated", diagnostics);
        }
        var byId = new Dictionary<string, FactoryStage>(StringComparer.Ordinal);
        foreach (var stage in stages)
        {
            if (stage is null) { Error("stage.required", "Stages cannot contain null.", "stages"); continue; }
            if (!Identifier(stage.Id)) Error("stage.id", "Use a nonempty stage ID containing letters, numbers, dots, underscores or hyphens.", "id", stage.Id);
            else if (!byId.TryAdd(stage.Id, stage)) Error("stage.duplicate", "Stage IDs must be unique.", "id", stage.Id);
            if (!Name(stage.Name)) Error("stage.name", "Stage names must contain 1–200 characters.", "name", stage.Id);
            if (!Name(stage.Group)) Error("stage.group", "A display group is required (1–200 characters).", "group", stage.Id);
            if (stage.DependsOn is null) Error("dependencies.required", "Supply the dependency list, using [] for a root stage.", "dependsOn", stage.Id);
            if (stage.Steps is not { Length: > 0 }) { Error("steps.required", "Include at least one step.", "steps", stage.Id); continue; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in stage.Steps)
            {
                if (step is null) { Error("step.required", "Steps cannot contain null.", "steps", stage.Id); continue; }
                void StepError(string code, string message, string field) => Error(code, message, field, stage.Id, step.Id);
                if (!Identifier(step.Id) || !ids.Add(step.Id)) StepError("step.id", "Step IDs must be valid and unique within their stage.", "id");
                if (!Name(step.Name)) StepError("step.name", "Step names must contain 1–200 characters.", "name");
                if (step.Kind is not ("agent" or "command" or "manual" or "artifact")) StepError("step.kind", "Choose agent, command, manual or artifact.", "kind");
                if (step.Kind == "agent")
                {
                    if (string.IsNullOrWhiteSpace(step.Instructions)) StepError("agent.instructions", "Agent instructions are required.", "instructions");
                    if (step.Role is not ("specification" or "implementation" or "verification" or "review" or "documentation")) StepError("agent.role", "Choose an agent role.", "role");
                    if (step.Permission is not ("read-only" or "workspace-write")) StepError("agent.permission", "Choose read-only or workspace-write.", "permission");
                    if (step.Role == "review" && step.Permission != "read-only") StepError("review.permission", "Review must use read-only permissions.", "permission");
                    if (step.Provider is not (null or "codex" or "claude")) StepError("agent.provider", "Choose codex, claude or inherit the project setting with null.", "provider");
                    var skills = step.Skills ?? [];
                    if (skills.Any(s => s is null || !Name(s.Id) || !Name(s.Source)) || skills.Where(s => s is not null).Select(s => s.Id).Distinct().Count() != skills.Length)
                        StepError("skills.invalid", "Skills require unique IDs and explicit sources (1–200 characters each).", "skills");
                }
                else if (step.Role is not null || step.Permission is not null || step.Provider is not null || step.Model is not null || step.Effort is not null || step.Skills is { Length: > 0 })
                    StepError("step.agentFields", "Only agent steps accept agent settings and skills.", "kind");
                if (step.Kind == "command")
                {
                    var command = step.Command;
                    if (command is null) StepError("command.required", "Configure the actual verification executable and arguments.", "command");
                    else
                    {
                        if (string.IsNullOrWhiteSpace(command.Executable) || command.Executable.Any(char.IsControl)) StepError("command.executable", "Provide an executable, separately from its arguments.", "command.executable");
                        if (command.Arguments is null || command.Arguments.Any(a => a is null || a.Contains('\0'))) StepError("command.arguments", "Supply an argument array containing no nulls or NUL characters.", "command.arguments");
                        if (!RelativePath(command.WorkingDirectory)) StepError("command.directory", "Use a repository-relative working directory without parent traversal.", "command.workingDirectory");
                        if (command.TimeoutSeconds <= 0) StepError("command.timeout", "Supply a positive timeout in seconds.", "command.timeoutSeconds");
                        if (command.EnvironmentReferences?.Any(e => !EnvironmentName().IsMatch(e.Key) || e.Value is null || !EnvironmentName().IsMatch(e.Value)) == true)
                            StepError("command.environment", "Environment values must reference environment variable names, never inline credentials.", "command.environmentReferences");
                    }
                }
                else if (step.Command is not null) StepError("step.commandFields", "Only command steps accept a command.", "command");
                if (step.Kind == "manual" && string.IsNullOrWhiteSpace(step.Instructions)) StepError("manual.instructions", "Describe the required human decision.", "instructions");
                if (step.Kind == "artifact" && step.Consumes is not { Length: > 0 }) StepError("artifact.inputs", "Artifact checks require at least one input.", "consumes");
                foreach (var (values, field) in new[] { (step.Consumes, "consumes"), (step.Produces, "produces") })
                    if (values is not null && (values.Any(v => !Identifier(v)) || values.Distinct().Count() != values.Length)) StepError("artifact.id", "Artifact IDs must be valid and unique in each list.", field);
            }
        }
        foreach (var stage in byId.Values)
        {
            var deps = stage.DependsOn ?? [];
            if (deps.Distinct().Count() != deps.Length) Error("dependency.duplicate", "Include each dependency once.", "dependsOn", stage.Id);
            foreach (var dependency in deps)
                if (dependency is null || dependency == stage.Id || !byId.ContainsKey(dependency)) Error("dependency.invalid", $"Dependency '{dependency}' on '{stage.Id}' must identify a different stage in this pipeline.", "dependsOn", stage.Id);
        }
        // Kahn's algorithm avoids recursion for user-supplied graphs.
        var remaining = byId.ToDictionary(p => p.Key, p => (p.Value.DependsOn ?? []).Where(d => d is not null && byId.ContainsKey(d)).Distinct().Count());
        var dependents = byId.Keys.ToDictionary(id => id, _ => new List<string>());
        foreach (var stage in byId.Values)
            foreach (var dep in (stage.DependsOn ?? []).Where(d => d is not null && byId.ContainsKey(d)).Distinct()) dependents[dep].Add(stage.Id);
        var ready = new Queue<string>(remaining.Where(p => p.Value == 0).Select(p => p.Key));
        var ordered = new List<string>();
        while (ready.TryDequeue(out var id))
        {
            ordered.Add(id);
            foreach (var next in dependents[id]) if (--remaining[next] == 0) ready.Enqueue(next);
        }
        if (ordered.Count != byId.Count) Error("graph.cycle", "Stage dependencies must form an acyclic graph.", "dependsOn");
        if (diagnostics.All(d => d.Code is "command.required" or "agent.instructions" or "manual.instructions"))
        {
            var available = new Dictionary<string, HashSet<string>>();
            var producers = new HashSet<string>();
            foreach (var id in ordered)
            {
                var stage = byId[id];
                var artifacts = new HashSet<string>((stage.DependsOn ?? []).SelectMany(d => available[d]));
                foreach (var step in stage.Steps)
                {
                    foreach (var input in step.Consumes ?? []) if (!artifacts.Contains(input)) Error("artifact.unavailable", $"Artifact '{input}' must be produced by an earlier step or dependency.", "consumes", id, step.Id);
                    foreach (var output in step.Produces ?? [])
                    {
                        if (!producers.Add(output)) Error("artifact.duplicate", $"Artifact '{output}' has more than one producer.", "produces", id, step.Id);
                        artifacts.Add(output);
                    }
                }
                available[id] = artifacts;
            }
        }
        return new(diagnostics.Count == 0, "notEvaluated", diagnostics);
    }
    public static FactoryDefinition Resolve(FactoryDefinition definition, FactoryOverrides overrides)
    {
        if (overrides?.Steps is null) throw new RuleException("Supply an overrides steps array (use [] to inherit all settings).");
        var keys = new HashSet<(string, string)>();
        foreach (var item in overrides.Steps)
        {
            if (item is null || !keys.Add((item.StageId, item.StepId))) throw new RuleException("Include each step override once.");
            var step = definition.Stages.SingleOrDefault(s => s.Id == item.StageId)?.Steps.SingleOrDefault(s => s.Id == item.StepId);
            if (step is null) throw new RuleException("Override references a stage or step outside the selected version.");
        }
        return new(definition.Stages.Select(stage => stage with { Steps = stage.Steps.Select(step =>
        {
            var change = overrides.Steps.SingleOrDefault(o => o.StageId == stage.Id && o.StepId == step.Id);
            return change is null ? step : step with { Instructions = change.Instructions ?? step.Instructions, Provider = change.Provider ?? step.Provider,
                Model = change.Model ?? step.Model, Effort = change.Effort ?? step.Effort, Skills = change.Skills ?? step.Skills, Command = change.Command ?? step.Command };
        }).ToArray() }).ToArray());
    }
    public static string RequireStructure(FactoryDefinition definition)
    {
        var json = Serialize(definition);
        // Incomplete drafts may omit command details, but never persist broken graph structure.
        var structural = Validate(definition).Diagnostics.Where(d => d.Code is not ("command.required" or "agent.instructions" or "manual.instructions")).ToList();
        if (structural.Count > 0) throw new RuleException(structural[0].Message);
        return json;
    }
    public static void RequirePublishable(FactoryDefinition definition)
    {
        Serialize(definition);
        var validation = Validate(definition);
        if (!validation.Valid) throw new RuleException(validation.Diagnostics[0].Message);
    }
    private static bool Name(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 200;
    private static bool Identifier(string? value) => value is not null && value.Length <= 200 && IdentifierPattern().IsMatch(value);
    private static bool RelativePath(string? path) => !string.IsNullOrWhiteSpace(path) && !path.Any(char.IsControl) && !path.Contains(':') && !path.StartsWith('/') && !path.StartsWith('\\') && !path.Split('/', '\\').Contains("..");
    [GeneratedRegex("^[A-Za-z0-9_.-]+\\z")] private static partial Regex IdentifierPattern();
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*\\z")] private static partial Regex EnvironmentName();
}

public sealed class FactoryPipeline
{
    private FactoryPipeline() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Source { get; private set; } = "";
    [JsonIgnore] public string DefinitionJson { get; private set; } = "";
    public FactoryDefinition Definition => JsonSerializer.Deserialize<FactoryDefinition>(DefinitionJson, FactoryRules.Json)!;
    public long DraftRevision { get; private set; }
    public long PublishedDraftRevision { get; private set; }
    public bool Archived { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public static FactoryPipeline Create(Guid project, string name, string description, string source, FactoryDefinition definition) => new()
    { Id = Guid.NewGuid(), ProjectId = project, Name = Rules.Name(name), Description = AgentRun.Text(description, "Description"), Source = Rules.Name(source, "Configuration source"), DefinitionJson = FactoryRules.RequireStructure(definition), DraftRevision = 1, CreatedAt = DateTime.UtcNow };
    public void Edit(long revision, string name, string description, FactoryDefinition definition)
    {
        RequireRevision(revision); RequireActive();
        Name = Rules.Name(name); Description = AgentRun.Text(description, "Description"); DefinitionJson = FactoryRules.RequireStructure(definition); DraftRevision++;
    }
    public PipelineVersion Publish(long revision, int number)
    {
        RequireRevision(revision); RequireActive();
        if (PublishedDraftRevision == DraftRevision) throw new RuleException("This draft has already been published. Edit it before publishing again.", 409);
        FactoryRules.RequirePublishable(Definition);
        PublishedDraftRevision = DraftRevision;
        return PipelineVersion.Create(this, number);
    }
    public void Archive(long revision) { RequireRevision(revision); RequireActive(); Archived = true; DraftRevision++; }
    public void RequireActive() { if (Archived) throw new RuleException("This pipeline is archived.", 409); }
    private void RequireRevision(long revision) { if (revision != DraftRevision) throw new RuleException("The pipeline draft changed. Refresh before saving.", 409); }
}

public sealed class PipelineVersion
{
    private PipelineVersion() { }
    public Guid Id { get; private set; }
    public Guid PipelineId { get; private set; }
    public Guid ProjectId { get; private set; }
    public int Number { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Source { get; private set; } = "";
    [JsonIgnore] public string DefinitionJson { get; private set; } = "";
    public FactoryDefinition Definition => JsonSerializer.Deserialize<FactoryDefinition>(DefinitionJson, FactoryRules.Json)!;
    public DateTime PublishedAt { get; private set; }
    internal static PipelineVersion Create(FactoryPipeline pipeline, int number) => new()
    { Id = Guid.NewGuid(), PipelineId = pipeline.Id, ProjectId = pipeline.ProjectId, Number = number, Name = pipeline.Name, Description = pipeline.Description, Source = pipeline.Source, DefinitionJson = pipeline.DefinitionJson, PublishedAt = DateTime.UtcNow };
}

public sealed class TaskFactoryConfiguration
{
    private TaskFactoryConfiguration() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid PipelineVersionId { get; private set; }
    [JsonIgnore] public string OverridesJson { get; private set; } = "";
    public FactoryOverrides Overrides => JsonSerializer.Deserialize<FactoryOverrides>(OverridesJson, FactoryRules.Json)!;
    public static TaskFactoryConfiguration Create(Guid item, Guid project) => new() { Id = item, ProjectId = project };
    public void Attach(PipelineVersion version, FactoryOverrides overrides)
    {
        if (version.ProjectId != ProjectId) throw new RuleException("Choose a published version from this task's project.");
        FactoryRules.RequirePublishable(FactoryRules.Resolve(version.Definition, overrides));
        PipelineVersionId = version.Id; OverridesJson = FactoryRules.Serialize(overrides);
    }
}
