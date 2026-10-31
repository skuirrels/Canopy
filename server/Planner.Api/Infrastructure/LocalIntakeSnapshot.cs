using System.Text.Json;
using System.Text.Json.Serialization;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

internal static class LocalIntakeSnapshot
{
    private static readonly JsonSerializerOptions Json = Options();
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(FactoryRules.Json) { PropertyNameCaseInsensitive = false, AllowDuplicateProperties = false, RespectRequiredConstructorParameters = true, NumberHandling = JsonNumberHandling.Strict };
        options.Converters.Add(new ExplicitTimestamp()); return options;
    }
    private sealed class ExplicitTimestamp : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException();
            var text = reader.GetString()!;
            var offset = text.EndsWith('Z') || text.Length >= 6 && (text[^6] is '+' or '-') && text[^3] == ':';
            if (!offset || !reader.TryGetDateTimeOffset(out var instant)) throw new JsonException("Require explicit timestamp offsets.");
            return instant;
        }
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }
    public static T Parse<T>(byte[] body)
    {
        try { return JsonSerializer.Deserialize<T>(body, Json) ?? throw new JsonException(); }
        catch (JsonException) { throw new RuleException("Malformed or unsupported complete snapshot. Unknown/raw context fields cannot establish coverage.", 503); }
    }
    public static async Task<byte[]> Read(string configuredPath, int maximumBytes, CancellationToken ct)
    {
        for (var path = new FileInfo(configuredPath) as FileSystemInfo; path is not null; path = path is FileInfo f ? f.Directory : ((DirectoryInfo)path).Parent)
            if (path.LinkTarget is not null) throw new RuleException("Complete snapshots cannot traverse symbolic links.", 503);
        var file = new FileInfo(configuredPath);
        if (!file.Exists || (file.Attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0) throw new RuleException("The configured complete snapshot is unavailable.", 503);
        await using var stream = LocalIntakeSnapshotFile.Read(configuredPath);
        if (!stream.CanSeek || stream.Length > maximumBytes) throw new RuleException("The snapshot exceeds configured bounds or is not a regular file.", 503);
        var beforeLength = stream.Length; var beforeWrite = file.LastWriteTimeUtc;
        using var bytes = new MemoryStream(); var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct); if (read == 0) break;
            if (bytes.Length + read > maximumBytes) throw new RuleException("Snapshot byte limit reached; coverage is incomplete.", 503);
            bytes.Write(buffer, 0, read);
        }
        file.Refresh();
        if (bytes.Length != beforeLength || stream.Length != beforeLength || file.Length != beforeLength || file.LastWriteTimeUtc != beforeWrite) throw new RuleException("The snapshot changed while being read. Publish an atomic complete snapshot and collect again.", 503);
        return bytes.ToArray();
    }
}
