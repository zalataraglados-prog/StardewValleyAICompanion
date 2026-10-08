using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

public static class ContentAddressedJsonArtifactReader
{
    public const string ManifestSchemaVersion =
        "stardewai.content_addressed_json.v1";

    private const string BlobDirectoryName = "_snapshot-blobs";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string ReadAllText(string logicalPath)
    {
        var stored = File.ReadAllText(logicalPath, Utf8WithoutBom);
        return MaterializeIfNeeded(logicalPath, stored);
    }

    public static string MaterializeIfNeeded(
        string logicalPath,
        string stored)
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

        var logicalDirectory = Path.GetDirectoryName(
            Path.GetFullPath(logicalPath)) ?? throw new InvalidDataException(
                "Content-addressed JSON directory is unavailable.");
        var blobRoot = Path.Combine(logicalDirectory, BlobDirectoryName);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            foreach (var property in root.GetProperty("root_properties")
                         .EnumerateArray())
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
                {
                    throw new InvalidDataException(
                        "Unknown content-addressed JSON chunk kind.");
                }
                writer.WriteStartObject();
                foreach (var child in property.GetProperty("properties")
                             .EnumerateArray())
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
        var actualSha256 = Sha256(materialized);
        if (materialized.LongLength != expectedBytes ||
            !string.Equals(
                actualSha256,
                expectedSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Content-addressed JSON materialization does not match its manifest.");
        }

        return Utf8WithoutBom.GetString(materialized);
    }

    private static byte[] ReadAndVerifyBlob(
        string blobRoot,
        JsonElement reference)
    {
        var sha256 = RequiredString(reference, "sha256");
        if (sha256.Length != 64 || sha256.Any(character =>
                !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                "Content-addressed JSON blob hash is invalid.");
        }

        var blobPath = Path.Combine(
            blobRoot,
            sha256[..2],
            sha256 + ".json.gz");
        if (!File.Exists(blobPath))
        {
            throw new FileNotFoundException(
                "Content-addressed JSON blob is missing.",
                blobPath);
        }

        using var input = File.OpenRead(blobPath);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        var bytes = output.ToArray();
        var expectedBytes = reference.GetProperty("uncompressed_bytes")
            .GetInt64();
        var actualSha256 = Sha256(bytes);
        if (bytes.LongLength != expectedBytes ||
            !string.Equals(actualSha256, sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Content-addressed JSON blob does not match its reference.");
        }

        return bytes;
    }

    private static string RequiredString(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException(
                "Content-addressed JSON manifest is missing " +
                propertyName + ".");

    private static string Sha256(byte[] bytes)
    {
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(bytes))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
