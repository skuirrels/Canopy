using System.Text.Json;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

internal static class LocalAgentUsage
{
    // Codex exec --json turn.completed and Claude stream-json result reports.
    // Raw usage counters keep their original names; cache semantics differ.
    // https://learn.chatgpt.com/docs/non-interactive-mode#make-output-machine-readable
    // https://platform.claude.com/cookbook/claude-agent-sdk-07-hosting-the-agent
    public static AgentUsageObservation? Read(string provider, JsonElement root)
    {
        if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return null;
        var name = type.GetString()!;
        if (!(provider == "codex" && name == "turn.completed" || provider == "claude" && name == "result")) return null;
        var present = root.TryGetProperty("usage", out var usage); var payload = present ? usage.GetRawText() : "";
        static long? Count(JsonElement value, string field) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(field, out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt64(out var number) && number >= 0 ? number : null;
        var input = present ? Count(usage, "input_tokens") : null; var output = present ? Count(usage, "output_tokens") : null;
        return new(provider, name, DateTime.UtcNow, !present ? "missing" : input.HasValue && output.HasValue ? "reported" : "invalid", input, output, payload, FactoryArtifact.Hash(payload));
    }
}
