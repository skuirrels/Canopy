using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Api.Domain;

public sealed record ParallelStepCheckpoint(Guid StepRunId, VerificationContext Context, string IndexHash, ParallelTree Files);
public sealed record ParallelTree(string Tree, IReadOnlyDictionary<string, int> Modes);
public sealed class FactoryParallelRun
{
    private FactoryParallelRun() { }
    public Guid Id { get; private set; }
    public string State { get; private set; } = "preparing";
    public string Head { get; private set; } = "";
    public string IndexHash { get; private set; } = "";
    [JsonIgnore] public string InitialJson { get; private set; } = "";
    [JsonIgnore] public string InitialContextJson { get; private set; } = "";
    [JsonIgnore] public string FinalJson { get; private set; } = "";
    [JsonIgnore] public string FinalContextJson { get; private set; } = "";
    public ParallelTree? Initial => Decode<ParallelTree>(InitialJson);
    public VerificationContext? InitialContext => Decode<VerificationContext>(InitialContextJson);
    public ParallelTree? Final => Decode<ParallelTree>(FinalJson);
    public VerificationContext? FinalContext => Decode<VerificationContext>(FinalContextJson);
    public string? Error { get; private set; }
    public static FactoryParallelRun Begin(Guid runId) => new() { Id = runId };
    public void Ready(string head, string indexHash, ParallelTree tree, VerificationContext context)
    { if (State != "preparing") throw new RuleException("Parallel workspace is not preparing."); Head = head; IndexHash = indexHash; InitialJson = FactoryRules.Serialize(tree); InitialContextJson = FactoryRules.Serialize(context); State = "running"; }
    public void Integrating() { if (State != "running") throw new RuleException("Parallel workspace is not ready for final integration."); State = "integrating"; }
    public void Complete(ParallelTree tree, VerificationContext context)
    { if (State != "integrating") throw new RuleException("Parallel integration has no current intent."); FinalJson = FactoryRules.Serialize(tree); FinalContextJson = FactoryRules.Serialize(context); State = "complete"; }
    public void Stop(string state, string? error) { if (State == "complete") return; if (state is not ("failed" or "interrupted" or "cancelled")) throw new RuleException("Invalid parallel outcome."); State = state; Error = error; }
    private static T? Decode<T>(string value) => value.Length == 0 ? default : JsonSerializer.Deserialize<T>(value, FactoryRules.Json);
}
public sealed class FactoryParallelStage
{
    private FactoryParallelStage() { }
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public string WorkspacePath { get; private set; } = "";
    public string State { get; private set; } = "materializing";
    [JsonIgnore] public string InputJson { get; private set; } = "";
    [JsonIgnore] public string OutputJson { get; private set; } = "";
    [JsonIgnore] public string CheckpointJson { get; private set; } = "";
    public ParallelTree? Input => InputJson.Length == 0 ? null : JsonSerializer.Deserialize<ParallelTree>(InputJson, FactoryRules.Json);
    public ParallelTree? Output => OutputJson.Length == 0 ? null : JsonSerializer.Deserialize<ParallelTree>(OutputJson, FactoryRules.Json);
    public VerificationContext? Checkpoint => CheckpointJson.Length == 0 ? null : JsonSerializer.Deserialize<VerificationContext>(CheckpointJson, FactoryRules.Json);
    [JsonIgnore] public string StepsJson { get; private set; } = "[]";
    public IReadOnlyList<ParallelStepCheckpoint> StepCheckpoints => JsonSerializer.Deserialize<ParallelStepCheckpoint[]>(StepsJson, FactoryRules.Json)!;
    public void CaptureStep(ParallelStepCheckpoint checkpoint)
    { if (State != "running" || StepCheckpoints.Any(x => x.StepRunId == checkpoint.StepRunId)) throw new RuleException("Step checkpoint is not a new running-stage observation."); StepsJson = FactoryRules.Serialize(StepCheckpoints.Append(checkpoint).ToArray()); }
    public string? Error { get; private set; }
    public static FactoryParallelStage Begin(Guid runId, Guid stageId, string workspace) => new() { Id = stageId, RunId = runId, WorkspacePath = workspace };
    public void Ready(ParallelTree input, VerificationContext context)
    { if (State != "materializing") throw new RuleException("Stage materialization has no current intent."); InputJson = FactoryRules.Serialize(input); CheckpointJson = FactoryRules.Serialize(context); State = "running"; }
    public void Wait(VerificationContext context) { if (State != "running") throw new RuleException("Stage is not running."); CheckpointJson = FactoryRules.Serialize(context); State = "waiting"; }
    public void Continue() { if (State != "waiting") throw new RuleException("Stage is not waiting."); State = "running"; }
    public void Complete(ParallelTree output, VerificationContext context)
    { if (State != "running") throw new RuleException("Stage is not running."); OutputJson = FactoryRules.Serialize(output); CheckpointJson = FactoryRules.Serialize(context); State = "complete"; }
    public void Stop(string state, string? error) { if (State == "complete") return; State = state; Error = error; }
}
public static class FactoryParallel
{
    public static bool Enabled(FactoryRun run) => run.Snapshot.Execution?.Policy.MaxConcurrentStages is > 1;
    public static IReadOnlyList<FactoryStage> Ordered(FactoryDefinition definition, string? ancestorsOf = null)
    {
        var remaining = definition.Stages.ToList(); var result = new List<FactoryStage>();
        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(stage => stage.DependsOn.All(id => result.Any(x => x.Id == id))) ?? throw new RuleException("Parallel dependencies are invalid.");
            result.Add(next); remaining.Remove(next);
        }
        if (ancestorsOf is null) return result;
        var required = new HashSet<string>(); var queue = new Queue<string>(definition.Stages.Single(x => x.Id == ancestorsOf).DependsOn);
        while (queue.TryDequeue(out var id)) if (required.Add(id)) foreach (var parent in definition.Stages.Single(x => x.Id == id).DependsOn) queue.Enqueue(parent);
        return result.Where(x => required.Contains(x.Id)).ToList();
    }
    public static Dictionary<string, int> MergeModes(IReadOnlyDictionary<string, int> current, ParallelTree input, ParallelTree output)
    {
        var merged = new Dictionary<string, int>(current, StringComparer.Ordinal);
        foreach (var path in input.Modes.Keys.Union(output.Modes.Keys))
        {
            int? before = input.Modes.TryGetValue(path, out var b) ? b : null, after = output.Modes.TryGetValue(path, out var a) ? a : null, actual = merged.TryGetValue(path, out var c) ? c : null;
            if (before == after) continue;
            if (actual != before && actual != after) throw new RuleException($"Parallel file-mode conflict at '{path}'. Inspect retained branches; no winner was selected.");
            if (after.HasValue) merged[path] = after.Value; else merged.Remove(path);
        }
        return merged;
    }
}
