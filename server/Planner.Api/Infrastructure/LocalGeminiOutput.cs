using System.Text.Json;

namespace Planner.Api.Infrastructure;

// Antigravity CLI's NDJSON protocol uses event/step_update/result envelopes.
// Only the terminal response is authoritative; activity deltas can be partial.
internal sealed class LocalGeminiOutput(int maxCharacters)
{
    public string Result { get; private set; } = "";
    public string? SessionId { get; private set; }
    public string? Error { get; private set; }
    public bool Completed { get; private set; }
    public string Read(JsonElement root)
    {
        if (root.TryGetProperty("conversation_id", out var conversation)) SessionId = conversation.GetString();
        if (!root.TryGetProperty("event", out var eventType)) return root.GetRawText() + "\n";
        var type = eventType.GetString();
        if (type == "step_update" && root.TryGetProperty("step_update", out var step))
        {
            if (step.TryGetProperty("conversation_id", out var stepConversation)) SessionId = stepConversation.GetString();
            if (step.TryGetProperty("text_delta", out var text)) return text.GetString() ?? "";
        }
        if (type == "result" && root.TryGetProperty("result", out var result))
        {
            if (result.TryGetProperty("conversation_id", out var resultConversation)) SessionId = resultConversation.GetString();
            var response = result.TryGetProperty("response", out var content) ? content.GetString() ?? "" : "";
            Result = response[..Math.Min(response.Length, maxCharacters + 1)];
            Completed = result.TryGetProperty("status", out var status) && status.GetString() == "SUCCESS";
            if (!Completed) Error = result.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString() : result.GetRawText();
        }
        return root.GetRawText() + "\n";
    }
}
