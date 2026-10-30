using System.Text.Json;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalDeliveryHooks(DeliveryOptions options, IHttpClientFactory clients, AgentOptions agents) : IDeliveryHooks
{
    public IReadOnlyList<DeploymentTarget> Targets() => options.Targets;
    public string ResourceKey(string id)
    { var hook = Find(id); return FactoryArtifact.Hash(new Uri(hook.HealthUrl).AbsoluteUri + "\n" + new Uri(hook.SnapshotUrl).AbsoluteUri); }
    public string? ConfigurationHash(string? id)
    {
        if (id is null) return null;
        var hook = Find(id); var executable = LocalAgentEnvironment.Resolve(hook.Executable) ?? throw new RuleException("The configured hook executable is unavailable.", 409);
        string FileHash(string path)
        {
            try { using var input = File.OpenRead(path); return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { throw new RuleException("A captured hook integrity input is unavailable.", 409); }
        }
        return FactoryArtifact.Hash(JsonSerializer.Serialize(new { hook, executable = Path.GetFullPath(executable), executableHash = FileHash(executable), files = hook.IntegrityFiles.Select(path => new { path, hash = FileHash(path) }).ToArray() }));
    }
    public IReadOnlyList<DeliveryHookInfo> List() => options.Hooks.Select(x => new DeliveryHookInfo(x.Id, x.Name,
        LocalAgentEnvironment.Resolve(x.Executable) is not null && Directory.Exists(x.WorkingDirectory) && x.IntegrityFiles.All(File.Exists),
        LocalAgentEnvironment.Resolve(x.Executable) is null || !Directory.Exists(x.WorkingDirectory) || x.IntegrityFiles.Any(path => !File.Exists(path)) ? "Configured hook executable/directory is unavailable." : null)).ToList();
    public async Task<DeliveryProbe> Probe(string id, CancellationToken ct)
    {
        var hook = Find(id); var health = await Read(hook.HealthUrl, ct); using var expected = JsonDocument.Parse(hook.ExpectedHealthJson);
        if (!Contains(health, expected.RootElement)) throw new RuleException("Post-merge health does not match its configured contract.", 503);
        var identity = health;
        foreach (var part in hook.InstanceJsonPointer[1..].Split('/').Select(x => x.Replace("~1", "/").Replace("~0", "~")))
            if (!identity.TryGetProperty(part, out identity)) throw new RuleException("The configured instance identity is unavailable.", 503);
        if (identity.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) || identity.ToString().Length == 0) throw new RuleException("A real process instance identity is required for restart verification.", 503);
        var revision = health;
        foreach (var part in hook.RevisionJsonPointer[1..].Split('/').Select(x => x.Replace("~1", "/").Replace("~0", "~")))
            if (!revision.TryGetProperty(part, out revision)) throw new RuleException("The running application revision is unavailable.", 503);
        if (revision.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(revision.GetString())) throw new RuleException("The running application must report its actual deployed commit revision.", 503);
        var snapshot = await Read(hook.SnapshotUrl, ct);
        return new(identity.ToString(), revision.GetString()!, Hash(snapshot), Hash(health));
    }
    public Task<DeliveryHookReceipt> Run(string id, FactoryDelivery delivery, DeliveryProbe before, CancellationToken ct) =>
        RunTarget(id, new(delivery.Id, Guid.Empty, delivery.Policy.Repository, delivery.Remote!.MergeSha!, delivery.Remote.MergeSha!, "", delivery.Policy.Environment, false), before, ct);
    public async Task<DeliveryHookReceipt> RunTarget(string id, DeploymentExecution execution, DeliveryProbe before, CancellationToken ct)
    {
        var hook = Find(id); using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(hook.TimeoutSeconds));
        var output = ""; var truncated = false; int? code = null; DeliveryProbe? after = null; string? error = null;
        Task Append(string line) { truncated |= (long)output.Length + line.Length > agents.MaxOutputCharacters; output = AgentRun.Tail(output + line + "\n", agents.MaxOutputCharacters); return Task.CompletedTask; }
        try
        {
            code = await AgentProcess.Run(LocalAgentEnvironment.Resolve(hook.Executable) ?? throw new RuleException("The configured post-merge hook is unavailable."), hook.WorkingDirectory, hook.Arguments, null, Append, Append, deadline.Token,
                new Dictionary<string, string> { ["CANOPY_DELIVERY_ID"] = execution.DeliveryId.ToString(), ["CANOPY_MERGE_SHA"] = execution.MergeSha, ["CANOPY_REPOSITORY"] = execution.Repository,
                    ["CANOPY_DEPLOYMENT_ID"] = execution.DeploymentId.ToString(), ["CANOPY_REVISION"] = execution.Revision, ["CANOPY_TARGET_ID"] = execution.TargetId, ["CANOPY_ENVIRONMENT"] = execution.Environment, ["CANOPY_RECOVERY"] = execution.Recovery ? "true" : "false" });
            after = await Probe(id, ct);
            error = DeploymentVerification.Error(before, after, execution.Revision, code);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { error = "The post-merge hook timed out. Reconcile the actual application before further action."; }
        catch (Exception exception) when (exception is RuleException or IOException or System.ComponentModel.Win32Exception) { error = exception.Message; }
        return new(before, after, code, output, truncated, error);
    }
    private DeliveryHook Find(string id) => options.Hooks.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("The captured post-merge hook is unavailable.", 409);
    private async Task<JsonElement> Read(string url, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
        try
        {
            using var response = await clients.CreateClient("delivery").GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new RuleException("The configured health or persistence probe failed.", 503);
            using var stream = await response.Content.ReadAsStreamAsync(deadline.Token); using var bytes = new MemoryStream(); var buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, deadline.Token)) > 0) { if (bytes.Length + count > options.MaxResponseBytes) throw new RuleException("Probe response exceeds the configured limit; coverage is incomplete.", 503); bytes.Write(buffer, 0, count); }
            using var json = JsonDocument.Parse(bytes.ToArray()); return json.RootElement.Clone();
        }
        catch (Exception error) when (error is HttpRequestException or JsonException) { throw new RuleException("Configured health/persistence observations are unavailable.", 503); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new RuleException("Configured health/persistence probe timed out.", 503); }
    }
    private static bool Contains(JsonElement actual, JsonElement expected) => expected.ValueKind == JsonValueKind.Object
        ? actual.ValueKind == JsonValueKind.Object && expected.EnumerateObject().All(x => actual.TryGetProperty(x.Name, out var value) && Contains(value, x.Value)) : actual.GetRawText() == expected.GetRawText();
    private static string Hash(JsonElement value)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) Canonical(writer, value);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));
    }
    private static void Canonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        { writer.WriteStartObject(); foreach (var p in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Canonical(writer, p.Value); } writer.WriteEndObject(); }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray(); var array = value.EnumerateArray().ToList();
            if (array.All(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)) array = array.OrderBy(x => x.GetProperty("id").GetString(), StringComparer.Ordinal).ToList();
            foreach (var item in array) Canonical(writer, item); writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
