using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class ContentInventoryVerifier
{
    public static ContentInventoryRehashReport Verify(string manifestPath, string contentRoot)
    {
        var manifestFullPath = Path.GetFullPath(manifestPath);
        var root = Path.GetFullPath(contentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(root) || Path.GetPathRoot(root)?.TrimEnd('\\', '/') == root)
            throw new DirectoryNotFoundException("Content root is missing or resolves to a drive root: " + root);

        using var document = JsonDocument.Parse(File.ReadAllText(manifestFullPath));
        var manifest = document.RootElement;
        var expected = manifest.GetProperty("contentFiles").EnumerateArray()
            .Select(value => new ContentRecord(
                RequiredString(value, "relativePath").Replace('/', Path.DirectorySeparatorChar),
                value.GetProperty("bytes").GetInt64(),
                RequiredString(value, "sha256")))
            .OrderBy(value => value.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expectedPaths = expected.Select(value => value.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        var sizeMismatch = new List<string>();
        var hashMismatch = new List<string>();
        var verified = 0;
        using var aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var item in expected)
        {
            var path = ResolveDescendant(root, item.RelativePath);
            if (!File.Exists(path))
            {
                missing.Add(item.RelativePath.Replace('\\', '/'));
                continue;
            }
            var info = new FileInfo(path);
            if (info.Length != item.Bytes)
            {
                sizeMismatch.Add(item.RelativePath.Replace('\\', '/'));
                continue;
            }
            var hash = HashFile(path);
            if (!string.Equals(hash, item.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                hashMismatch.Add(item.RelativePath.Replace('\\', '/'));
                continue;
            }
            var aggregateLine = item.RelativePath.Replace('\\', '/') + "\0" + item.Bytes + "\0" + hash + "\n";
            aggregate.AppendData(Encoding.UTF8.GetBytes(aggregateLine));
            verified++;
        }

        var unexpected = Directory.EnumerateFiles(root, "*.xnb", SearchOption.AllDirectories)
            .Select(path => path[(root.Length + 1)..])
            .Where(path => !expectedPaths.Contains(path))
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var passed = verified == expected.Length && unexpected.Length == 0;
        return new ContentInventoryRehashReport
        {
            Status = passed ? "pass" : "blocked",
            ManifestPath = manifestFullPath,
            ManifestSha256 = HashFile(manifestFullPath),
            ContentRoot = root,
            ExpectedFiles = expected.Length,
            VerifiedFiles = verified,
            MissingFiles = missing.ToArray(),
            UnexpectedFiles = unexpected,
            SizeMismatchFiles = sizeMismatch.ToArray(),
            HashMismatchFiles = hashMismatch.ToArray(),
            VerifiedAggregateSha256 = Convert.ToHexString(aggregate.GetHashAndReset()).ToLowerInvariant()
        };
    }

    private static string ResolveDescendant(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidDataException("Content manifest path is rooted: " + relativePath);
        var result = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!result.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Content manifest path escapes root: " + relativePath);
        return result;
    }

    private static string RequiredString(JsonElement value, string property)
    {
        var result = value.GetProperty(property).GetString();
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidDataException("Required manifest string is empty: " + property)
            : result;
    }

    internal static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record ContentRecord(string RelativePath, long Bytes, string Sha256);
}

public sealed class ContentInventoryRehashReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "content_inventory_rehash.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("content_root")]
    public string ContentRoot { get; set; } = string.Empty;

    [JsonPropertyName("expected_files")]
    public int ExpectedFiles { get; set; }

    [JsonPropertyName("verified_files")]
    public int VerifiedFiles { get; set; }

    [JsonPropertyName("missing_files")]
    public string[] MissingFiles { get; set; } = Array.Empty<string>();

    [JsonPropertyName("unexpected_files")]
    public string[] UnexpectedFiles { get; set; } = Array.Empty<string>();

    [JsonPropertyName("size_mismatch_files")]
    public string[] SizeMismatchFiles { get; set; } = Array.Empty<string>();

    [JsonPropertyName("hash_mismatch_files")]
    public string[] HashMismatchFiles { get; set; } = Array.Empty<string>();

    [JsonPropertyName("verified_aggregate_sha256")]
    public string VerifiedAggregateSha256 { get; set; } = string.Empty;
}
