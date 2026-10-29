using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed record DeliveryHook(string Id, string Name, string Executable, string[] Arguments, string WorkingDirectory,
    int TimeoutSeconds, string[] IntegrityFiles, string HealthUrl, string ExpectedHealthJson, string InstanceJsonPointer, string RevisionJsonPointer, string SnapshotUrl);
public sealed class DeliveryOptions
{
    public string ApiBaseUrl { get; set; } = "";
    public string ApiVersion { get; set; } = "";
    public string GitHubHost { get; set; } = "";
    public string GitHubExecutable { get; set; } = "";
    public string? TokenEnvironmentVariable { get; set; }
    public int RequestTimeoutSeconds { get; set; }
    public int MaxResponseBytes { get; set; }
    public int MaximumPages { get; set; }
    public List<DeliveryHook> Hooks { get; set; } = [];
    public void Validate()
    {
        Endpoint(ApiBaseUrl);
        if (string.IsNullOrWhiteSpace(ApiVersion) || string.IsNullOrWhiteSpace(GitHubHost) || RequestTimeoutSeconds < 1 || MaxResponseBytes < 1 || MaximumPages < 1) throw new InvalidOperationException("Configure Delivery provider, request limits and API version.");
        if (Hooks.Select(x => x.Id).Distinct().Count() != Hooks.Count) throw new InvalidOperationException("Delivery hook IDs must be unique.");
        foreach (var hook in Hooks)
        {
            Rules.Name(hook.Id, "Hook ID"); Rules.Name(hook.Name, "Hook name"); Endpoint(hook.HealthUrl); Endpoint(hook.SnapshotUrl);
            if (hook.IntegrityFiles is null || hook.IntegrityFiles.Any(x => !Path.IsPathRooted(x)) || hook.TimeoutSeconds < 1 || !Path.IsPathRooted(hook.WorkingDirectory) || string.IsNullOrWhiteSpace(hook.Executable) || hook.Arguments is null || (!hook.InstanceJsonPointer.StartsWith('/') || !hook.RevisionJsonPointer.StartsWith('/'))) throw new InvalidOperationException("Configure an explicit hook command, directory, timeout and instance JSON pointer.");
            using var document = System.Text.Json.JsonDocument.Parse(hook.ExpectedHealthJson);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object || !document.RootElement.EnumerateObject().Any()) throw new InvalidOperationException("Configure expected health JSON fields.");
        }
    }
    public static Uri Endpoint(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0 || !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)) throw new InvalidOperationException("Delivery endpoints require HTTPS or explicit loopback HTTP, without embedded credentials.");
        return uri;
    }
}
