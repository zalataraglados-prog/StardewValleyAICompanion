using System.Text;
using System.Text.Json;

namespace StardewAI.LiveTrainingLoop;

public static class ContentAddressedJsonArtifactRetention
{
    private const string BlobDirectoryName = "_snapshot-blobs";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static ContentAddressedJsonArtifactPruneResult PruneUnreferencedBlobs(
        string logicalDirectory)
    {
        var fullLogicalDirectory = Path.GetFullPath(logicalDirectory);
        if (!Directory.Exists(fullLogicalDirectory))
            return ContentAddressedJsonArtifactPruneResult.CompletedEmpty;

        RejectReparsePoint(
            fullLogicalDirectory,
            "content_addressed_prune_logical_directory_is_reparse_point");
        var blobRoot = Path.GetFullPath(Path.Combine(
            fullLogicalDirectory,
            BlobDirectoryName));
        if (!IsStrictDescendant(blobRoot, fullLogicalDirectory) ||
            !string.Equals(
                Path.GetFileName(blobRoot),
                BlobDirectoryName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "content_addressed_prune_blob_directory_not_scoped");
        }
        if (!Directory.Exists(blobRoot))
            return ContentAddressedJsonArtifactPruneResult.CompletedEmpty;

        RejectReparsePoint(
            blobRoot,
            "content_addressed_prune_blob_directory_is_reparse_point");
        var referencedHashes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var logicalPath in Directory.EnumerateFiles(
            fullLogicalDirectory,
            "*.json",
            SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(logicalPath) & FileAttributes.ReparsePoint) != 0)
            {
                return ContentAddressedJsonArtifactPruneResult.Skipped(
                    "logical_json_is_reparse_point:" +
                    Path.GetFileName(logicalPath));
            }
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(
                    logicalPath,
                    Utf8WithoutBom));
            }
            catch (JsonException)
            {
                return ContentAddressedJsonArtifactPruneResult.Skipped(
                    "logical_json_invalid:" + Path.GetFileName(logicalPath));
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("schema_version", out var schema) ||
                    schema.GetString() !=
                        ContentAddressedJsonArtifactStore.ManifestSchemaVersion)
                {
                    continue;
                }

                if (!TryCollectReferencedHashes(
                        root,
                        blobRoot,
                        referencedHashes,
                        out var reason))
                {
                    return ContentAddressedJsonArtifactPruneResult.Skipped(
                        reason + ":" + Path.GetFileName(logicalPath));
                }
            }
        }

        var shardDirectories = Directory.EnumerateDirectories(
                blobRoot,
                "*",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .ToArray();
        foreach (var shardDirectory in shardDirectories)
        {
            if (!IsStrictDescendant(shardDirectory, blobRoot))
            {
                throw new InvalidOperationException(
                    "content_addressed_prune_shard_not_scoped");
            }
            RejectReparsePoint(
                shardDirectory,
                "content_addressed_prune_shard_is_reparse_point");
        }

        var deletedBlobCount = 0;
        long deletedBytes = 0;
        foreach (var shardDirectory in shardDirectories)
        {
            foreach (var blobPath in Directory.EnumerateFiles(
                         shardDirectory,
                         "*.json.gz",
                         SearchOption.TopDirectoryOnly))
            {
                var fullBlobPath = Path.GetFullPath(blobPath);
                if (!IsStrictDescendant(fullBlobPath, shardDirectory))
                {
                    throw new InvalidOperationException(
                        "content_addressed_prune_blob_not_scoped");
                }
                RejectReparsePoint(
                    fullBlobPath,
                    "content_addressed_prune_blob_is_reparse_point");

                var fileName = Path.GetFileName(fullBlobPath);
                var hash = fileName.EndsWith(".json.gz", StringComparison.Ordinal)
                    ? fileName[..^8]
                    : string.Empty;
                if (!IsSha256(hash) ||
                    !string.Equals(
                        Path.GetFileName(shardDirectory),
                        hash[..2],
                        StringComparison.OrdinalIgnoreCase) ||
                    referencedHashes.Contains(hash))
                {
                    continue;
                }

                var blobInfo = new FileInfo(fullBlobPath);
                deletedBytes += blobInfo.Length;
                File.Delete(fullBlobPath);
                deletedBlobCount++;
            }
        }

        foreach (var shardDirectory in shardDirectories)
        {
            if (!Directory.EnumerateFileSystemEntries(shardDirectory).Any())
                Directory.Delete(shardDirectory);
        }

        return new ContentAddressedJsonArtifactPruneResult
        {
            Completed = true,
            ReferencedBlobCount = referencedHashes.Count,
            DeletedBlobCount = deletedBlobCount,
            DeletedBytes = deletedBytes
        };
    }

    private static bool TryCollectReferencedHashes(
        JsonElement manifest,
        string blobRoot,
        HashSet<string> referencedHashes,
        out string reason)
    {
        reason = string.Empty;
        if (!manifest.TryGetProperty("blob_directory", out var blobDirectory) ||
            blobDirectory.ValueKind != JsonValueKind.String ||
            blobDirectory.GetString() != BlobDirectoryName ||
            !manifest.TryGetProperty("root_properties", out var rootProperties) ||
            rootProperties.ValueKind != JsonValueKind.Array)
        {
            reason = "content_addressed_manifest_invalid";
            return false;
        }

        foreach (var property in rootProperties.EnumerateArray())
        {
            if (!property.TryGetProperty("kind", out var kindElement) ||
                kindElement.ValueKind != JsonValueKind.String)
            {
                reason = "content_addressed_manifest_kind_missing";
                return false;
            }

            var kind = kindElement.GetString();
            if (kind == "value")
            {
                if (!TryCollectReference(
                        property,
                        blobRoot,
                        referencedHashes,
                        out reason))
                {
                    return false;
                }
                continue;
            }

            if (kind != "object" ||
                !property.TryGetProperty("properties", out var children) ||
                children.ValueKind != JsonValueKind.Array)
            {
                reason = "content_addressed_manifest_children_invalid";
                return false;
            }
            foreach (var child in children.EnumerateArray())
            {
                if (!TryCollectReference(
                        child,
                        blobRoot,
                        referencedHashes,
                        out reason))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryCollectReference(
        JsonElement reference,
        string blobRoot,
        HashSet<string> referencedHashes,
        out string reason)
    {
        reason = string.Empty;
        if (!reference.TryGetProperty("sha256", out var hashElement) ||
            hashElement.ValueKind != JsonValueKind.String ||
            !IsSha256(hashElement.GetString()))
        {
            reason = "content_addressed_manifest_hash_invalid";
            return false;
        }

        var hash = hashElement.GetString()!.ToLowerInvariant();
        var blobPath = Path.Combine(
            blobRoot,
            hash[..2],
            hash + ".json.gz");
        if (!File.Exists(blobPath))
        {
            reason = "content_addressed_manifest_blob_missing";
            return false;
        }

        referencedHashes.Add(hash);
        return true;
    }

    private static void RejectReparsePoint(string path, string message)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException(message);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsStrictDescendant(string path, string parent)
    {
        var relative = Path.GetRelativePath(parent, path);
        return relative.Length > 0 &&
            relative != "." &&
            !relative.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }
}

public sealed class ContentAddressedJsonArtifactPruneResult
{
    public static ContentAddressedJsonArtifactPruneResult CompletedEmpty =>
        new() { Completed = true };

    public bool Completed { get; init; }

    public int ReferencedBlobCount { get; init; }

    public int DeletedBlobCount { get; init; }

    public long DeletedBytes { get; init; }

    public string SkipReason { get; init; } = string.Empty;

    public static ContentAddressedJsonArtifactPruneResult Skipped(string reason) =>
        new() { SkipReason = reason };
}
