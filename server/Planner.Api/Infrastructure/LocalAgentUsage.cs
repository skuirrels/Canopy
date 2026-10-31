using System.Text.Json;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

internal static class LocalAgentUsage
{
    // Raw counters retain provider names and scope; resumed Antigravity result
    // usage is cumulative for that conversation and must not be summed as turns.
    // https://antigravity.google/docs/cli/headless/
    public static AgentUsageObservation? Read(string provider, JsonElement root)
    {
        string? name;
        if (provider == "gemini")
        {
            if (!root.TryGetProperty("event", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "result"
                || !root.TryGetProperty("result", out root) || root.ValueKind != JsonValueKind.Object) return null;
            name = "result";
        }
        else
        {
            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return null;
            name = type.GetString();
            if (!(provider == "codex" && name == "turn.completed" || provider == "claude" && name == "result")) return null;
        }
        var present = root.TryGetProperty("usage", out var usage); var payload = present ? usage.GetRawText() : "";
        static long? Count(JsonElement value, string field) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(field, out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt64(out var number) && number >= 0 ? number : null;
        var input = present ? Count(usage, "input_tokens") : null; var output = present ? Count(usage, "output_tokens") : null;
        return new(provider, name!, DateTime.UtcNow, !present ? "missing" : input.HasValue && output.HasValue ? "reported" : "invalid", input, output, payload, FactoryArtifact.Hash(payload));
    }
}
