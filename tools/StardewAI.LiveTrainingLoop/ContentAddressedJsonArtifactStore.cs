using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static class ContentAddressedJsonArtifactStore
{
    public const string PlainMode = "plain";
    public const string ContentAddressedGzipMode = "content_addressed_gzip";
    public const string ManifestSchemaVersion = "stardewai.content_addressed_json.v1";

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
    {
        var stored = File.ReadAllText(logicalPath, Utf8WithoutBom);
        return MaterializeIfNeeded(logicalPath, stored);
    }

    public static async Task<string> ReadAllTextAsync(
        string logicalPath,
        CancellationToken cancellationToken = default)
    {
        var stored = await File.ReadAllTextAsync(
            logicalPath,
            Utf8WithoutBom,
            cancellationToken);
        return MaterializeIfNeeded(logicalPath, stored);
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

    private static string MaterializeIfNeeded(string logicalPath, string stored)
    {
        using var document = JsonDocument.Parse(stored);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schema_version", out var schema) ||
            schema.GetString() != ManifestSchemaVersion)
        {
            return stored;
        }

        if (!root.TryGetProperty("blob_directory", out var blobDirectory) ||
            blobDirectory.GetString() != BlobDirectoryName)
        {
            throw new InvalidDataException(
                "Content-addressed JSON blob directory is invalid.");
        }

        var logicalDirectory = Path.GetDirectoryName(Path.GetFullPath(logicalPath)) ??
            throw new InvalidDataException("Content-addressed JSON directory is unavailable.");
        var blobRoot = Path.Combine(logicalDirectory, BlobDirectoryName);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            foreach (var property in root.GetProperty("root_properties").EnumerateArray())
            {
                var name = RequiredString(property, "name");
                writer.WritePropertyName(name);
                var kind = RequiredString(property, "kind");
                if (kind == "value")
                {
                    writer.WriteRawValue(ReadAndVerifyBlob(blobRoot, property));
                    continue;
                }

                if (kind != "object")
                    throw new InvalidDataException("Unknown content-addressed JSON chunk kind.");
                writer.WriteStartObject();
                foreach (var child in property.GetProperty("properties").EnumerateArray())
                {
                    writer.WritePropertyName(RequiredString(child, "name"));
                    writer.WriteRawValue(ReadAndVerifyBlob(blobRoot, child));
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        var materialized = output.ToArray();
        var expectedBytes = root.GetProperty("logical_bytes").GetInt64();
        var expectedSha256 = RequiredString(root, "logical_sha256");
        var actualSha256 = Convert.ToHexString(
            SHA256.HashData(materialized)).ToLowerInvariant();
        if (materialized.LongLength != expectedBytes ||
            !string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Content-addressed JSON materialization does not match its manifest.");
        }

        return Utf8WithoutBom.GetString(materialized);
    }

    private static byte[] ReadAndVerifyBlob(string blobRoot, JsonElement reference)
    {
        var sha256 = RequiredString(reference, "sha256");
        if (sha256.Length != 64 || sha256.Any(character =>
                !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("Content-addressed JSON blob hash is invalid.");
        }

        var blobPath = Path.Combine(
            blobRoot,
            sha256[..2],
            sha256 + ".json.gz");
        if (!File.Exists(blobPath))
            throw new FileNotFoundException("Content-addressed JSON blob is missing.", blobPath);

        using var input = File.OpenRead(blobPath);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        var bytes = output.ToArray();
        var expectedBytes = reference.GetProperty("uncompressed_bytes").GetInt64();
        var actualSha256 = Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();
        if (bytes.LongLength != expectedBytes ||
            !string.Equals(actualSha256, sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Content-addressed JSON blob does not match its reference.");
        }

        return bytes;
    }

    private static string RequiredString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException(
                "Content-addressed JSON manifest is missing " + propertyName + ".");
}
