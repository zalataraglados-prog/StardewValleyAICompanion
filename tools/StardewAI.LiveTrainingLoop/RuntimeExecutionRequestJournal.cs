using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

namespace StardewAI.LiveTrainingLoop;

public static partial class RuntimeExecutionRequestJournal
{
    public const string SchemaVersion = "stardewai.execution_request_journal.v1";
    public const int MaximumRequestBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static async Task<RuntimeExecutionRequestRecord> WriteAsync(
        string directory, int iteration, int ordinal, string requestJson,
        string executorUrl, string beforeSnapshotPath)
    {
        if (iteration < 0 || ordinal < 1) throw new ArgumentOutOfRangeException(nameof(ordinal));
        var bytes = Utf8.GetBytes(requestJson);
        if (bytes.Length == 0 || bytes.Length > MaximumRequestBytes)
            throw new InvalidDataException("Execution request exceeds the journal byte budget.");
        var path = Path.Combine(Path.GetFullPath(directory), $"execution-request-{iteration:D4}-item-{ordinal:D4}.json");
        var record = Describe(path, requestJson, executorUrl, beforeSnapshotPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) throw new IOException("Execution request journal already exists; refusing overwrite.");
        await using (var file = CreateNew(path + ".gz"))
        {
            await using (var gzip = new GZipStream(file, CompressionLevel.Fastest, leaveOpen: true))
                await gzip.WriteAsync(bytes);
            await file.FlushAsync();
            file.Flush(flushToDisk: true);
        }
        await using (var file = CreateNew(path))
        {
            await JsonSerializer.SerializeAsync(file, record, JsonOptions);
            await file.FlushAsync();
            file.Flush(flushToDisk: true);
        }
        return record;
    }

    private static FileStream CreateNew(string path) => new(path, new FileStreamOptions
    {
        Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
        Options = FileOptions.Asynchronous | FileOptions.WriteThrough
    });

    private static RuntimeExecutionRequestRecord Describe(
        string path, string requestJson, string executorUrl, string beforeSnapshotPath)
    {
        var node = JsonNode.Parse(requestJson) as JsonObject ?? throw new InvalidDataException("Execution request must be an object.");
        var request = node.Deserialize<TrainingExecutionRequest>(JsonOptions) ?? throw new InvalidDataException("Execution request is missing.");
        if (!Guid.TryParseExact(request.RequestNonce, "N", out _) || string.IsNullOrWhiteSpace(request.RunId) ||
            string.IsNullOrWhiteSpace(request.QueueId) || string.IsNullOrWhiteSpace(request.QueueItemId))
            throw new InvalidDataException("Execution request journal identity is missing.");
        return new RuntimeExecutionRequestRecord
        {
            JournalPath = path, BlobFileName = Path.GetFileName(path) + ".gz",
            RawRequestSha256 = Hash(Utf8.GetBytes(requestJson)), ProductRequestSha256 = Hash(Utf8.GetBytes(node.ToJsonString())),
            RequestBytes = Utf8.GetByteCount(requestJson), RunId = request.RunId, QueueId = request.QueueId,
            QueueItemId = request.QueueItemId, RequestNonce = request.RequestNonce, OptionId = request.OptionId,
            SourceStateHash = request.BeforeStateHash, ExecutorUrl = executorUrl, BeforeSnapshotPath = Path.GetFullPath(beforeSnapshotPath)
        };
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public sealed record RuntimeExecutionRequestRecord
{
    public string SchemaVersion { get; init; } = RuntimeExecutionRequestJournal.SchemaVersion;
    [JsonIgnore] public string JournalPath { get; init; } = string.Empty;
    public string BlobFileName { get; init; } = string.Empty;
    public string RawRequestSha256 { get; init; } = string.Empty;
    public string ProductRequestSha256 { get; init; } = string.Empty;
    public int RequestBytes { get; init; }
    public string RunId { get; init; } = string.Empty;
    public string QueueId { get; init; } = string.Empty;
    public string QueueItemId { get; init; } = string.Empty;
    public string RequestNonce { get; init; } = string.Empty;
    public string OptionId { get; init; } = string.Empty;
    public string SourceStateHash { get; init; } = string.Empty;
    public string ExecutorUrl { get; init; } = string.Empty;
    public string BeforeSnapshotPath { get; init; } = string.Empty;
}
