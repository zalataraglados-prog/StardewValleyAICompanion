using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

public static class ExecutionRequestJournalReader
{
    public const string SchemaVersion = "stardewai.execution_request_journal.v1";
    public const int MaximumRequestBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static VerifiedExecutionRequestJournal Read(string path) => ReadAsync(path).GetAwaiter().GetResult();

    public static async Task<VerifiedExecutionRequestJournal> ReadAsync(string path)
    {
        path = Path.GetFullPath(path);
        RejectReparsePoint(path);
        var directory = Path.GetDirectoryName(path)!;
        RejectReparsePoint(directory);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
        var metadata = document.RootElement;
        if (metadata.ValueKind != JsonValueKind.Object || String(metadata, "schema_version") != SchemaVersion ||
            !metadata.TryGetProperty("request_bytes", out var size) || size.ValueKind != JsonValueKind.Number || !size.TryGetInt32(out var count) ||
            count < 1 || count > MaximumRequestBytes || String(metadata, "blob_file_name") != Path.GetFileName(path) + ".gz")
            throw new InvalidDataException("Execution request journal scope or byte budget is invalid.");
        var blob = Path.Combine(directory, String(metadata, "blob_file_name"));
        RejectReparsePoint(blob);
        var bytes = new byte[count];
        using (var file = File.OpenRead(blob))
        using (var gzip = new GZipStream(file, CompressionMode.Decompress))
        {
            var offset = 0;
            while (offset < bytes.Length)
            {
                var read = await gzip.ReadAsync(bytes, offset, bytes.Length - offset).ConfigureAwait(false);
                if (read == 0) throw new InvalidDataException("Execution request gzip is truncated.");
                offset += read;
            }
            if (gzip.ReadByte() != -1) throw new InvalidDataException("Execution request gzip exceeds its declared byte budget.");
        }
        if (Hash(bytes) != String(metadata, "raw_request_sha256"))
            throw new InvalidDataException("Execution request gzip hash mismatch.");
        var json = Utf8.GetString(bytes);
        var node = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Execution request must be an object.");
        var request = node.Deserialize<TrainingExecutionRequest>(JsonOptions) ?? throw new InvalidDataException("Execution request is missing.");
        var beforePath = String(metadata, "before_snapshot_path");
        if (!Guid.TryParseExact(request.RequestNonce, "N", out _) || string.IsNullOrWhiteSpace(request.RunId) ||
            string.IsNullOrWhiteSpace(request.QueueId) || string.IsNullOrWhiteSpace(request.QueueItemId) ||
            String(metadata, "run_id") != request.RunId || String(metadata, "queue_id") != request.QueueId ||
            String(metadata, "queue_item_id") != request.QueueItemId || String(metadata, "request_nonce") != request.RequestNonce ||
            String(metadata, "option_id") != request.OptionId || String(metadata, "source_state_hash") != request.BeforeStateHash ||
            String(metadata, "product_request_sha256") != Hash(Utf8.GetBytes(node.ToJsonString())) ||
            string.IsNullOrWhiteSpace(beforePath) || beforePath != Path.GetFullPath(beforePath))
            throw new InvalidDataException("Execution request journal identity or payload binding drifted.");
        return new VerifiedExecutionRequestJournal(path, metadata.Clone(), json, request);
    }

    private static string String(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Execution request journal must not traverse a reparse point.");
    }
}

public sealed class VerifiedExecutionRequestJournal
{
    internal VerifiedExecutionRequestJournal(string path, JsonElement metadata, string requestJson, TrainingExecutionRequest request)
    {
        JournalPath = path;
        Metadata = metadata;
        RequestJson = requestJson;
        Request = request;
    }

    public string JournalPath { get; }
    public JsonElement Metadata { get; }
    public string RequestJson { get; }
    public TrainingExecutionRequest Request { get; }
}
