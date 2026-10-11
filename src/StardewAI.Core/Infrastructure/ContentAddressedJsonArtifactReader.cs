using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

public static class ContentAddressedJsonArtifactReader
{
    public const string ManifestSchemaVersion =
        "stardewai.content_addressed_json.v1";

    private const string BlobDirectoryName = "_snapshot-blobs";
    private const string TraceProducerId =
        "StardewAI.Core.ContentAddressedJsonArtifactReader";
    private const string TraceProducerVersion = "v2";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string ReadAllText(string logicalPath)
    {
        using var operation = ValidationTraceSession.BeginOperation(
            "content_addressed_json_read",
            TraceProducerId,
            TraceProducerVersion,
            logicalPath);
        if (InvocationReadSession.TryGetMaterializedText(
                logicalPath,
                out var cached,
                out var cachedLogicalSha256))
        {
            operation.MarkCacheHit();
            operation.AddArtifactId(cachedLogicalSha256);
            return cached;
        }

        var stored = InvocationReadSession.ReadAllText(
            logicalPath,
            Utf8WithoutBom,
            out var observation);
        operation.AddBytesRead(observation.PhysicalBytesRead);
        if (observation.CacheHit)
            operation.MarkCacheHit();
        var materialized = MaterializeIfNeeded(
            logicalPath,
            stored,
            operation,
            out var logicalSha256);
        InvocationReadSession.StoreMaterializedText(
            logicalPath,
            materialized,
            logicalSha256);
        return materialized;
    }

    public static string MaterializeIfNeeded(
        string logicalPath,
        string stored)
    {
        using var operation = ValidationTraceSession.BeginOperation(
            "content_addressed_json_materialize",
            TraceProducerId,
            TraceProducerVersion,
            logicalPath);
        return MaterializeIfNeeded(
            logicalPath,
            stored,
            operation,
            out _);
    }

    private static string MaterializeIfNeeded(
        string logicalPath,
        string stored,
        ValidationTraceOperation operation,
        out string? logicalSha256)
    {
        logicalSha256 = null;
        var parseClock = operation.IsEnabled ? Stopwatch.StartNew() : null;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(stored);
        }
        finally
        {
            parseClock?.Stop();
            if (parseClock is not null)
            {
                operation.AddJsonParseMilliseconds(
                    parseClock.Elapsed.TotalMilliseconds);
            }
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schema_version", out var schema) ||
                schema.GetString() != ManifestSchemaVersion)
            {
                return stored;
            }

            var materializeClock = operation.IsEnabled
                ? Stopwatch.StartNew()
                : null;
            try
            {

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
                            writer.WriteRawValue(ReadAndVerifyBlob(
                                blobRoot,
                                property,
                                operation));
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
                            writer.WriteRawValue(ReadAndVerifyBlob(
                                blobRoot,
                                child,
                                operation));
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }

                var materialized = output.ToArray();
                var expectedBytes = root.GetProperty("logical_bytes").GetInt64();
                var expectedSha256 = RequiredString(root, "logical_sha256");
                var actualSha256 = Sha256(materialized);
                logicalSha256 = actualSha256;
                operation.AddHashBytes(materialized.LongLength);
                operation.AddArtifactId(expectedSha256);
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
            finally
            {
                materializeClock?.Stop();
                if (materializeClock is not null)
                {
                    operation.AddMaterializeMilliseconds(
                        materializeClock.Elapsed.TotalMilliseconds);
                }
            }
        }
    }

    private static byte[] ReadAndVerifyBlob(
        string blobRoot,
        JsonElement reference,
        ValidationTraceOperation operation)
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
        operation.AddBytesRead(input.Length);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        var bytes = output.ToArray();
        var expectedBytes = reference.GetProperty("uncompressed_bytes")
            .GetInt64();
        var actualSha256 = Sha256(bytes);
        operation.AddHashBytes(bytes.LongLength);
        operation.AddArtifactId(sha256);
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
