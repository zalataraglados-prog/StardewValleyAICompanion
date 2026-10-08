using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;

namespace StardewAI.LiveTrainingLoop;

public static class ContentAddressedJsonArtifactStore
{
    public const string PlainMode = "plain";
    public const string ContentAddressedGzipMode = "content_addressed_gzip";
    public const string ManifestSchemaVersion =
        ContentAddressedJsonArtifactReader.ManifestSchemaVersion;

    private const string BlobDirectoryName = "_snapshot-blobs";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static bool IsSupportedMode(string value) =>
        value is PlainMode or ContentAddressedGzipMode;

    public static async Task WriteAsync(
        string logicalPath,
        string json,
        string mode,
        CancellationToken cancellationToken = default)
    {
        if (mode == PlainMode)
        {
            await File.WriteAllTextAsync(
                logicalPath,
                json,
                Utf8WithoutBom,
                cancellationToken);
            return;
        }

        if (mode != ContentAddressedGzipMode)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unsupported JSON artifact mode.");
        }

        var fullLogicalPath = Path.GetFullPath(logicalPath);
        var logicalDirectory = Path.GetDirectoryName(fullLogicalPath) ??
            throw new InvalidOperationException("JSON artifact directory is unavailable.");
        Directory.CreateDirectory(logicalDirectory);
        var blobRoot = Path.Combine(logicalDirectory, BlobDirectoryName);
        Directory.CreateDirectory(blobRoot);

        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Content-addressed JSON root must be an object.");
        var canonicalJson = JsonSerializer.Serialize(parsed.RootElement);
        using var source = JsonDocument.Parse(canonicalJson);

        var rootProperties = new JsonArray();
        foreach (var property in source.RootElement.EnumerateObject())
        {
            if (property.NameEquals("state") &&
                property.Value.ValueKind == JsonValueKind.Object)
            {
                var children = new JsonArray();
                foreach (var child in property.Value.EnumerateObject())
                {
                    children.Add(await WritePropertyReferenceAsync(
                        blobRoot,
                        child.Name,
                        child.Value,
                        cancellationToken));
                }

                rootProperties.Add(new JsonObject
                {
                    ["name"] = property.Name,
                    ["kind"] = "object",
                    ["properties"] = children
                });
                continue;
            }

            var reference = await WritePropertyReferenceAsync(
                blobRoot,
                property.Name,
                property.Value,
                cancellationToken);
            reference["kind"] = "value";
            rootProperties.Add(reference);
        }

        var logicalBytes = Utf8WithoutBom.GetBytes(canonicalJson);
        var manifest = new JsonObject
        {
            ["schema_version"] = ManifestSchemaVersion,
            ["content_kind"] = "transparent_snapshot",
            ["compression"] = "gzip",
            ["chunking"] = "root_and_state_properties",
            ["blob_directory"] = BlobDirectoryName,
            ["logical_sha256"] = Convert.ToHexString(
                SHA256.HashData(logicalBytes)).ToLowerInvariant(),
            ["logical_bytes"] = logicalBytes.LongLength,
            ["root_properties"] = rootProperties
        };

        var manifestJson = manifest.ToJsonString(ManifestJsonOptions);
        var temporaryPath = fullLogicalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                manifestJson,
                Utf8WithoutBom,
                cancellationToken);
            File.Move(temporaryPath, fullLogicalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static string ReadAllText(string logicalPath)
        => ContentAddressedJsonArtifactReader.ReadAllText(logicalPath);

    public static async Task<string> ReadAllTextAsync(
        string logicalPath,
        CancellationToken cancellationToken = default)
    {
        var stored = await File.ReadAllTextAsync(
            logicalPath,
            Utf8WithoutBom,
            cancellationToken);
        return ContentAddressedJsonArtifactReader.MaterializeIfNeeded(
            logicalPath,
            stored);
    }

    private static async Task<JsonObject> WritePropertyReferenceAsync(
        string blobRoot,
        string name,
        JsonElement value,
        CancellationToken cancellationToken)
    {
        var bytes = Utf8WithoutBom.GetBytes(value.GetRawText());
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var shardDirectory = Path.Combine(blobRoot, sha256[..2]);
        Directory.CreateDirectory(shardDirectory);
        var blobPath = Path.Combine(shardDirectory, sha256 + ".json.gz");
        if (!File.Exists(blobPath))
        {
            var temporaryPath = blobPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    useAsync: true))
                await using (var gzip = new GZipStream(
                    output,
                    CompressionLevel.SmallestSize,
                    leaveOpen: false))
                {
                    await gzip.WriteAsync(bytes, cancellationToken);
                }

                try
                {
                    File.Move(temporaryPath, blobPath);
                }
                catch (IOException) when (File.Exists(blobPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        return new JsonObject
        {
            ["name"] = name,
            ["sha256"] = sha256,
            ["uncompressed_bytes"] = bytes.LongLength,
            ["compressed_bytes"] = new FileInfo(blobPath).Length
        };
    }

}
