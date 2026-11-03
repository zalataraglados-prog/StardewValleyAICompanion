using System.IO.Compression;
using System.Text.Json;

namespace StardewAI.LiveTrainingLoop;

public static partial class RuntimeExecutionRequestJournal
{
    public static async Task<(RuntimeExecutionRequestRecord Record, string RequestJson)> ReadAsync(string path)
    {
        path = Path.GetFullPath(path);
        RejectReparsePoint(path);
        var directory = Path.GetDirectoryName(path)!;
        RejectReparsePoint(directory);
        var record = JsonSerializer.Deserialize<RuntimeExecutionRequestRecord>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidDataException("Execution request journal is missing.");
        if (record.SchemaVersion != SchemaVersion || record.RequestBytes < 1 || record.RequestBytes > MaximumRequestBytes ||
            record.BlobFileName != Path.GetFileName(path) + ".gz")
            throw new InvalidDataException("Execution request journal scope or byte budget is invalid.");
        var blob = Path.Combine(directory, record.BlobFileName);
        RejectReparsePoint(blob);
        await using var file = File.OpenRead(blob);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        var bytes = new byte[record.RequestBytes];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var count = await gzip.ReadAsync(bytes.AsMemory(offset));
            if (count == 0) throw new InvalidDataException("Execution request gzip is truncated.");
            offset += count;
        }
        if (gzip.ReadByte() != -1) throw new InvalidDataException("Execution request gzip exceeds its declared byte budget.");
        if (Hash(bytes) != record.RawRequestSha256) throw new InvalidDataException("Execution request gzip hash mismatch.");
        var json = Utf8.GetString(bytes);
        record = record with { JournalPath = path };
        if (record != Describe(path, json, record.ExecutorUrl, record.BeforeSnapshotPath))
            throw new InvalidDataException("Execution request journal identity or payload binding drifted.");
        return (record, json);
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Execution request journal must not traverse a reparse point.");
    }
}
