using System.Globalization;
using System.Text.Json;
using Planner.Api.Application;
using Planner.Api.Domain;
namespace Planner.Api.Infrastructure;

public sealed class GitHubIntakeIssues(GitHubApiClient api, DeliveryOptions limits, IntakeLimits intakeLimits) : IIntakeIssues
{
    public string Binding => api.Binding;
    public string Identity => api.Identity;
    public async Task<IReadOnlyList<IntakeSignal>> Collect(IntakeConfiguration configuration, CancellationToken ct)
    {
        var signals = new List<IntakeSignal>(); var seen = new HashSet<string>(); long totalBytes = 0;
        for (var page = 1; page <= limits.MaximumPages; page++)
        {
            var next = false;
            var path = $"repos/{configuration.Repository}/issues?state=all&sort=created&direction=asc&per_page=100&page={page}&labels={Uri.EscapeDataString(string.Join(',', configuration.Labels))}";
            var value = (await api.Send(path, HttpMethod.Get, null, ct, inspect: response => next = response.Headers.TryGetValues("Link", out var links) && links.Any(x => x.Contains("rel=\"next\"", StringComparison.Ordinal))))!.Value;
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 100) throw new RuleException("GitHub issue coverage has an invalid shape.", 503);
            foreach (var row in value.EnumerateArray())
            {
                if (row.TryGetProperty("pull_request", out _)) continue; // The Issues list includes PRs in GitHub's contract.
                var signal = Parse(row, configuration);
                if (!seen.Add(signal.Fingerprint)) throw new RuleException("GitHub issue pagination repeated an identity; collect again for complete coverage.", 503);
                totalBytes += System.Text.Encoding.UTF8.GetByteCount(signal.EvidenceJson);
                if (totalBytes > intakeLimits.MaximumCollectionBytes) throw new RuleException("Configured collection byte limit reached; coverage is incomplete.", 503);
                signals.Add(signal);
            }
            if (!next && value.GetArrayLength() < 100) return signals;
        }
        throw new RuleException("GitHub pagination limit reached; issue coverage is incomplete. No candidates were changed.", 503);
    }
    public async Task<IntakeSignal> Refresh(IntakeSignal signal, IntakeConfiguration configuration, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(signal.EvidenceJson); var number = document.RootElement.GetProperty("number").GetInt32();
        var value = (await api.Send($"repos/{configuration.Repository}/issues/{number}", HttpMethod.Get, null, ct))!.Value;
        if (value.TryGetProperty("pull_request", out _)) throw new RuleException("The source now identifies a pull request; collect again.", 409);
        var fresh = Parse(value, configuration);
        if (fresh.Fingerprint != signal.Fingerprint) throw new RuleException("GitHub issue identity changed. Collect again before triage.", 409);
        return fresh;
    }
    private IntakeSignal Parse(JsonElement row, IntakeConfiguration configuration)
    {
        try
        {
            var id = row.GetProperty("id").GetInt64(); var node = Text(row, "node_id"); var number = row.GetProperty("number").GetInt32();
            var title = Text(row, "title"); var state = Text(row, "state"); var url = Text(row, "html_url");
            var created = Text(row, "created_at"); var updated = Text(row, "updated_at");
            if (id < 1 || number < 1 || node.Length == 0 || title.Length == 0 || state is not ("open" or "closed") || !DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) || !DateTimeOffset.TryParse(updated, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new InvalidOperationException();
            var uri = DeliveryOptions.Endpoint(url);
            if (!uri.Host.Equals(limits.GitHubHost, StringComparison.OrdinalIgnoreCase) || !uri.AbsolutePath.Equals($"/{configuration.Repository}/issues/{number}", StringComparison.OrdinalIgnoreCase) || uri.Query.Length > 0) throw new InvalidOperationException();
            var labels = row.GetProperty("labels").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : Text(x, "name")).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            var body = row.GetProperty("body").ValueKind == JsonValueKind.Null ? "" : row.GetProperty("body").GetString()!;
            var author = row.GetProperty("user").ValueKind == JsonValueKind.Null ? null : Text(row.GetProperty("user"), "login");
            var evidence = JsonSerializer.Serialize(new { id, nodeId = node, number, repository = configuration.Repository, title, body, state, labels, author, createdAt = created, updatedAt = updated, url }, FactoryRules.Json);
            return new(IntakeRules.Fingerprint("githubIssues", Identity, id), title, body, $"{configuration.Repository}#{number}", url,
                state == "open" && configuration.Labels.All(label => labels.Contains(label, StringComparer.OrdinalIgnoreCase)), evidence);
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new RuleException("GitHub issue evidence is malformed or has the wrong repository identity. No complete collection was established.", 503); }
    }
    private static string Text(JsonElement value, string key) => value.GetProperty(key).GetString() ?? throw new InvalidOperationException();
}
