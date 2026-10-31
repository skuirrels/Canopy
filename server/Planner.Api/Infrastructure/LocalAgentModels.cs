using System.Text.Json;
using Planner.Api.Application;

namespace Planner.Api.Infrastructure;

public static class AgentModelParser
{
    public static IReadOnlyList<AgentModel> Parse(string provider, string output) => provider switch
    {
        "codex" => Codex(output),
        "gemini" => Gemini(output),
        _ => []
    };
    // `codex debug models` prints the catalogue the CLI itself offers; hidden entries are internal to Codex.
    private static List<AgentModel> Codex(string output)
    {
        var start = output.IndexOf('{');
        if (start < 0) return [];
        try
        {
            using var document = JsonDocument.Parse(output[start..]);
            if (!document.RootElement.TryGetProperty("models", out var entries) || entries.ValueKind != JsonValueKind.Array) return [];
            return entries.EnumerateArray()
                .Where(x => x.TryGetProperty("slug", out var slug) && slug.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(slug.GetString())
                    && (!x.TryGetProperty("visibility", out var visibility) || visibility.GetString() == "list"))
                .OrderBy(x => x.TryGetProperty("priority", out var priority) && priority.TryGetInt32(out var value) ? value : int.MaxValue)
                .Select(x => new AgentModel(x.GetProperty("slug").GetString()!, x.TryGetProperty("display_name", out var name) && name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString()) ? name.GetString()! : x.GetProperty("slug").GetString()!))
                .DistinctBy(x => x.Id).ToList();
        }
        catch (JsonException) { return []; }
    }
    // `agy models` prints one tab-separated "id<TAB>display name" line per model after a progress line.
    private static List<AgentModel> Gemini(string output) => output.Split('\n')
        .Select(line => line.TrimEnd('\r').Split('\t', 2))
        .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !parts[0].Contains(' '))
        .Select(parts => new AgentModel(parts[0].Trim(), string.IsNullOrWhiteSpace(parts[1]) ? parts[0].Trim() : parts[1].Trim()))
        .DistinctBy(x => x.Id).ToList();
}
