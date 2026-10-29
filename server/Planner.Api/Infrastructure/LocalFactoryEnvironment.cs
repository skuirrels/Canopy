using System.ComponentModel;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed record FactorySkillLocation(string Id, string Source, string Path);

// Local filesystem observations are confined to the explicitly single-process deployment.
// Provider credentials/model access and execution success are never inferred from a binary.
public sealed class LocalFactoryEnvironment(LocalAgentEnvironment agents, IMarkdownFiles markdown, IConfiguration configuration) : IFactoryEnvironment
{
    public async Task<FactoryInspection> Inspect(FactoryDefinition definition, ProjectAgentConnection? connection, CancellationToken ct)
    {
        var checks = new List<FactoryReadinessCheck>();
        void Check(string code, string severity, string message, string? stage = null, string? step = null) => checks.Add(new(code, severity, message, stage, step));
        if (connection is null) return new(null, [new("repository.required", "error", "Connect this project to a repository first.")]);
        string root, commit;
        try
        {
            root = await agents.ValidateRepository(connection.RepositoryPath, connection.BaseRef, ct);
            commit = (await agents.Git(root, ["rev-parse", "--verify", "--end-of-options", connection.BaseRef + "^{commit}"], ct)).Trim();
            Check("repository.available", "info", "Repository and base commit resolved.");
        }
        catch (Exception error) when (error is RuleException or IOException or UnauthorizedAccessException or Win32Exception)
        { return new(null, [new("repository.unavailable", "error", error.Message)]); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return new(null, [new("repository.timeout", "error", "Repository inspection exceeded the configured Git timeout.")]); }
        var locations = configuration.GetSection("Factory:Skills").Get<List<FactorySkillLocation>>() ?? [];
        var skills = new List<FactorySkillSnapshot>();
        var resolved = new List<FactoryStage>();
        foreach (var stage in definition.Stages)
        {
            var steps = new List<FactoryStep>();
            foreach (var step in stage.Steps)
            {
                var effective = step;
                if (step.Kind == "agent")
                {
                    var provider = step.Provider ?? connection.Provider; var settings = connection.For(provider);
                    effective = step with { Provider = provider };
                    try { effective = effective with { Model = step.Model is null ? settings.Model : AgentExecutionSettings.Normalize(step.Model, "Model"),
                        Effort = step.Effort is null ? settings.Effort : AgentExecutionSettings.Normalize(step.Effort, "Effort") }; }
                    catch (RuleException error) { Check("agent.settings", "error", error.Message, stage.Id, step.Id); }
                    var available = agents.Providers().Single(x => x.Id == provider);
                    var executableAvailable = false;
                    try { executableAvailable = available.Available && CanExecute(agents.Executable(provider)); } catch (RuleException) { /* The executable may disappear during inspection. */ }
                    Check("agent.executable", executableAvailable ? "info" : "error", executableAvailable ? $"{available.Name} executable is available." : $"{available.Name} executable is unavailable.", stage.Id, step.Id);
                    if (effective.Effort is not null && !available.EffortLevels.Contains(effective.Effort)) Check("agent.effort", "error", "The effort level is not configured for this provider.", stage.Id, step.Id);
                    Check("agent.access", "warning", "Provider credentials and model access have not been verified; no model call was made.", stage.Id, step.Id);
                    foreach (var skill in step.Skills ?? [])
                    {
                        var matches = locations.Where(x => x is not null && x.Id == skill.Id && x.Source == skill.Source).ToList();
                        if (matches.Count != 1) { Check("skill.unresolved", "error", $"Configure one Factory:Skills path for '{skill.Id}' with its exact source.", stage.Id, step.Id); continue; }
                        try
                        {
                            var file = markdown.Read(Path.GetFullPath(matches[0].Path, root));
                            if (!skills.Any(x => x.Id == skill.Id && x.Source == skill.Source)) skills.Add(new(skill.Id, skill.Source, file.Path, file.Hash));
                            Check("skill.available", "info", $"Skill '{skill.Id}' resolved to readable Markdown; its content hash is captured.", stage.Id, step.Id);
                        }
                        catch (Exception error) when (error is RuleException or ArgumentException or IOException or UnauthorizedAccessException)
                        { Check("skill.unavailable", "error", $"Skill '{skill.Id}': {error.Message}", stage.Id, step.Id); }
                    }
                }
                if (step.Kind == "command" && step.Command is { } command)
                {
                    try
                    {
                        var directory = Path.GetFullPath(command.WorkingDirectory, root);
                        // Reject symlink traversal, even when the lexical relative path is valid.
                        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
                            if (current.LinkTarget is not null) throw new RuleException("Use a real working directory without symbolic links.");
                        if (!Directory.Exists(directory)) throw new RuleException("The command working directory does not exist.");
                        var binary = command.Executable.Contains('/') || command.Executable.Contains('\\') ? Path.GetFullPath(command.Executable, directory) : command.Executable;
                        Check("command.executable", CanExecute(binary) ? "info" : "error", CanExecute(binary) ? "Command executable is available; it has not been run." : "Command executable is unavailable.", stage.Id, step.Id);
                        foreach (var reference in command.EnvironmentReferences ?? [])
                            if (Environment.GetEnvironmentVariable(reference.Value) is null) Check("command.environment", "error", $"Environment reference '{reference.Key}' is unavailable.", stage.Id, step.Id);
                    }
                    catch (Exception error) when (error is RuleException or ArgumentException or IOException or UnauthorizedAccessException)
                    { Check("command.directory", "error", error.Message, stage.Id, step.Id); }
                }
                steps.Add(effective);
            }
            resolved.Add(stage with { Steps = steps.ToArray() });
        }
        Check("execution.pending", "warning", "Preparation records the work order only. Execution and human approvals have not occurred.");
        return new(new(root, connection.BaseRef, commit, connection.Instructions, connection.Provider, definition with { Stages = resolved.ToArray() }, skills), checks);
    }
    private static bool CanExecute(string executable)
    {
        var path = LocalAgentEnvironment.Resolve(executable);
        if (path is null) return false;
        try { return OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }
}
