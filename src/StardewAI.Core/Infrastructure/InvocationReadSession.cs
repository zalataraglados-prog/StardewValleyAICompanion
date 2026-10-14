using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;

namespace StardewAI.Core.Infrastructure;

public sealed class InvocationReadSession : IDisposable
{
    public const string ReaderVersion = "stardewai.invocation_read_session.v1";

    private static readonly AsyncLocal<InvocationReadSession?> Ambient = new();
    private static readonly StringComparer PathComparer =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly object sync = new();
    private readonly HashSet<string> immutablePaths;
    private readonly Dictionary<string, Entry> entries;
    private readonly InvocationReadSession? previous;
    private readonly bool enabled;
    private long physicalReadCount;
    private long physicalBytesRead;
    private long fullHashCount;
    private long hashBytes;
    private long materializationCount;
    private long cacheHitCount;
    private long releasedRawBytes;
    private long releasedStoredTextBytes;
    private int disposed;

    private InvocationReadSession(
        IEnumerable<string> immutableInputPaths,
        IEnumerable<string> prohibitedPaths,
        IEnumerable<string> prohibitedRoots,
        bool enabled)
    {
        this.enabled = enabled;
        var prohibited = new HashSet<string>(
            prohibitedPaths.Select(CanonicalPath),
            PathComparer);
        var roots = prohibitedRoots
            .Select(CanonicalPath)
            .Select(TrimDirectorySeparators)
            .Distinct(PathComparer)
            .ToArray();
        immutablePaths = immutableInputPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(CanonicalPath)
            .Where(File.Exists)
            .Where(path => !prohibited.Contains(path))
            .Where(path => !roots.Any(root => IsDescendant(path, root)))
            .ToHashSet(PathComparer);
        entries = new Dictionary<string, Entry>(PathComparer);
        previous = Ambient.Value;
        Ambient.Value = this;
    }

    public static InvocationReadSession Start(
        IEnumerable<string> immutableInputPaths,
        IEnumerable<string>? prohibitedPaths = null,
        IEnumerable<string>? prohibitedRoots = null,
        bool enabled = true) => new(
            immutableInputPaths ?? Array.Empty<string>(),
            prohibitedPaths ?? Array.Empty<string>(),
            prohibitedRoots ?? Array.Empty<string>(),
            enabled);

    public static bool IsPathReusable(string path)
    {
        var session = Ambient.Value;
        return session is not null && session.CanReuse(CanonicalPath(path));
    }

    public static string HashFile(
        string path,
        out InvocationReadObservation observation)
    {
        var fullPath = CanonicalPath(path);
        var session = Ambient.Value;
        if (session is null || !session.CanReuse(fullPath))
            return HashWithoutReuse(fullPath, out observation);
        return session.HashReusableFile(fullPath, out observation);
    }

    internal static string ReadAllText(
        string path,
        Encoding encoding,
        out InvocationReadObservation observation)
    {
        var fullPath = CanonicalPath(path);
        var session = Ambient.Value;
        if (session is null || !session.CanReuse(fullPath))
        {
            var text = File.ReadAllText(fullPath, encoding);
            observation = new InvocationReadObservation(
                SafeLength(fullPath),
                0,
                CacheHit: false);
            return text;
        }
        return session.ReadReusableText(fullPath, encoding, out observation);
    }

    internal static bool TryGetMaterializedText(
        string path,
        out string text,
        out string? logicalSha256)
    {
        text = string.Empty;
        logicalSha256 = null;
        var fullPath = CanonicalPath(path);
        var session = Ambient.Value;
        if (session is null || !session.CanReuse(fullPath))
            return false;

        lock (session.sync)
        {
            if (!session.entries.TryGetValue(fullPath, out var entry) ||
                entry.MaterializedText is null)
            {
                return false;
            }
            text = entry.MaterializedText;
            logicalSha256 = entry.LogicalSha256;
            session.cacheHitCount++;
            return true;
        }
    }

    internal static void StoreMaterializedText(
        string path,
        string text,
        string? logicalSha256)
    {
        var fullPath = CanonicalPath(path);
        var session = Ambient.Value;
        if (session is null || !session.CanReuse(fullPath))
            return;

        lock (session.sync)
        {
            var entry = session.GetOrCreate(fullPath);
            if (entry.MaterializedText is not null &&
                (!string.Equals(entry.MaterializedText, text,
                     StringComparison.Ordinal) ||
                 !string.Equals(entry.LogicalSha256, logicalSha256,
                     StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    "Immutable artifact materialization changed during one invocation: " +
                    fullPath);
            }
            if (entry.MaterializedText is null)
                session.materializationCount++;
            entry.MaterializedText = text;
            entry.LogicalSha256 = logicalSha256;
            session.ReleaseRedundantRepresentations(entry);
        }
    }

    public InvocationReadSessionSnapshot Snapshot()
    {
        lock (sync)
        {
            var retainedEntries = entries
                .Select(pair => MeasureRetainedEntry(pair.Key, pair.Value))
                .OrderByDescending(value => value.TotalRetainedBytes)
                .ThenBy(value => value.Path, PathComparer)
                .ToArray();
            return new InvocationReadSessionSnapshot(
                ReaderVersion,
                enabled,
                immutablePaths.Count,
                entries.Count,
                physicalReadCount,
                physicalBytesRead,
                fullHashCount,
                hashBytes,
                materializationCount,
                cacheHitCount,
                retainedEntries.Sum(value => value.RawBytes),
                retainedEntries.Sum(value => value.StoredTextBytes),
                retainedEntries.Sum(value => value.MaterializedTextBytes),
                retainedEntries.Sum(value => value.HashTextBytes),
                retainedEntries.Sum(value => value.TotalRetainedBytes),
                retainedEntries.FirstOrDefault()?.TotalRetainedBytes ?? 0,
                releasedRawBytes,
                releasedStoredTextBytes,
                retainedEntries.Take(20).ToArray());
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        if (ReferenceEquals(Ambient.Value, this))
            Ambient.Value = previous;
        lock (sync)
        {
            entries.Clear();
            immutablePaths.Clear();
        }
    }

    private bool CanReuse(string fullPath) =>
        enabled && disposed == 0 && immutablePaths.Contains(fullPath);

    private string ReadReusableText(
        string fullPath,
        Encoding encoding,
        out InvocationReadObservation observation)
    {
        lock (sync)
        {
            var entry = GetOrCreate(fullPath);
            var reused = entry.StoredText is not null ||
                entry.RawBytes is not null;
            var bytesRead = 0L;
            if (entry.StoredText is null)
            {
                bytesRead = EnsureRawBytes(fullPath, entry);
                using var stream = new MemoryStream(
                    entry.RawBytes!,
                    writable: false);
                using var reader = new StreamReader(
                    stream,
                    encoding,
                    detectEncodingFromByteOrderMarks: true);
                entry.StoredText = reader.ReadToEnd();
            }
            if (reused)
                cacheHitCount++;
            observation = new InvocationReadObservation(
                bytesRead,
                0,
                reused);
            var text = entry.StoredText;
            ReleaseRedundantRepresentations(entry);
            return text;
        }
    }

    private string HashReusableFile(
        string fullPath,
        out InvocationReadObservation observation)
    {
        lock (sync)
        {
            var entry = GetOrCreate(fullPath);
            var reusedBytes = entry.RawBytes is not null;
            var bytesRead = 0L;
            var hashWork = 0L;
            var reusedHash = entry.StoredSha256 is not null;
            if (entry.StoredSha256 is null)
            {
                bytesRead = EnsureRawBytes(fullPath, entry);
                entry.StoredSha256 = Sha256(entry.RawBytes!);
                hashWork = entry.RawBytes!.LongLength;
                fullHashCount++;
                hashBytes += hashWork;
            }
            ReleaseRedundantRepresentations(entry);
            if (reusedBytes || reusedHash)
                cacheHitCount++;
            observation = new InvocationReadObservation(
                bytesRead,
                hashWork,
                reusedBytes || reusedHash);
            return entry.StoredSha256;
        }
    }

    private long EnsureRawBytes(string fullPath, Entry entry)
    {
        if (entry.RawBytes is not null)
            return 0;
        entry.RawBytes = File.ReadAllBytes(fullPath);
        physicalReadCount++;
        physicalBytesRead += entry.RawBytes.LongLength;
        return entry.RawBytes.LongLength;
    }

    private Entry GetOrCreate(string fullPath)
    {
        if (!entries.TryGetValue(fullPath, out var entry))
        {
            entry = new Entry();
            entries.Add(fullPath, entry);
        }
        return entry;
    }

    private void ReleaseRedundantRepresentations(Entry entry)
    {
        if (entry.StoredSha256 is not null &&
            entry.StoredText is not null &&
            entry.RawBytes is not null)
        {
            releasedRawBytes += entry.RawBytes.LongLength;
            entry.RawBytes = null;
        }

        if (entry.StoredSha256 is not null &&
            entry.MaterializedText is not null &&
            entry.StoredText is not null &&
            !ReferenceEquals(entry.StoredText, entry.MaterializedText))
        {
            releasedStoredTextBytes += StringBytes(entry.StoredText);
            entry.StoredText = null;
        }
    }

    private static string HashWithoutReuse(
        string fullPath,
        out InvocationReadObservation observation)
    {
        using var stream = File.OpenRead(fullPath);
        var length = stream.Length;
        using var sha256 = SHA256.Create();
        var hash = Hex(sha256.ComputeHash(stream));
        observation = new InvocationReadObservation(
            length,
            length,
            CacheHit: false);
        return hash;
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha256 = SHA256.Create();
        return Hex(sha256.ComputeHash(bytes));
    }

    private static string Hex(byte[] bytes) =>
        BitConverter.ToString(bytes).Replace("-", string.Empty)
            .ToLowerInvariant();

    private static string CanonicalPath(string path) =>
        Path.GetFullPath(path);

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static string TrimDirectorySeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsDescendant(string path, string root) =>
        path.StartsWith(
            root + Path.DirectorySeparatorChar,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static InvocationReadRetainedEntry MeasureRetainedEntry(
        string path,
        Entry entry)
    {
        var rawBytes = entry.RawBytes?.LongLength ?? 0;
        var storedTextBytes = StringBytes(entry.StoredText);
        var materializedTextBytes = ReferenceEquals(
            entry.StoredText,
            entry.MaterializedText)
                ? 0
                : StringBytes(entry.MaterializedText);
        var hashTextBytes = StringBytes(entry.StoredSha256) +
            StringBytes(entry.LogicalSha256);
        return new InvocationReadRetainedEntry(
            path,
            rawBytes,
            storedTextBytes,
            materializedTextBytes,
            hashTextBytes,
            rawBytes + storedTextBytes + materializedTextBytes +
                hashTextBytes);
    }

    private static long StringBytes(string? value) =>
        value is null ? 0 : (long)value.Length * sizeof(char);

    private sealed class Entry
    {
        public byte[]? RawBytes { get; set; }
        public string? StoredText { get; set; }
        public string? StoredSha256 { get; set; }
        public string? MaterializedText { get; set; }
        public string? LogicalSha256 { get; set; }
    }
}

public readonly record struct InvocationReadObservation(
    long PhysicalBytesRead,
    long HashBytes,
    bool CacheHit);

public sealed record InvocationReadSessionSnapshot(
    [property: JsonPropertyName("reader_version")] string ReaderVersion,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("registered_path_count")]
        int RegisteredPathCount,
    [property: JsonPropertyName("entry_count")] int EntryCount,
    [property: JsonPropertyName("physical_read_count")]
        long PhysicalReadCount,
    [property: JsonPropertyName("physical_bytes_read")]
        long PhysicalBytesRead,
    [property: JsonPropertyName("full_hash_count")] long FullHashCount,
    [property: JsonPropertyName("hash_bytes")] long HashBytes,
    [property: JsonPropertyName("materialization_count")]
        long MaterializationCount,
    [property: JsonPropertyName("cache_hit_count")] long CacheHitCount,
    [property: JsonPropertyName("raw_bytes_retained")]
        long RawBytesRetained,
    [property: JsonPropertyName("stored_text_bytes_retained")]
        long StoredTextBytesRetained,
    [property: JsonPropertyName("materialized_text_bytes_retained")]
        long MaterializedTextBytesRetained,
    [property: JsonPropertyName("hash_text_bytes_retained")]
        long HashTextBytesRetained,
    [property: JsonPropertyName("total_retained_bytes")]
        long TotalRetainedBytes,
    [property: JsonPropertyName("max_entry_retained_bytes")]
        long MaxEntryRetainedBytes,
    [property: JsonPropertyName("released_raw_bytes")]
        long ReleasedRawBytes,
    [property: JsonPropertyName("released_stored_text_bytes")]
        long ReleasedStoredTextBytes,
    [property: JsonPropertyName("largest_entries")]
        InvocationReadRetainedEntry[] LargestEntries);

public sealed record InvocationReadRetainedEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("raw_bytes")] long RawBytes,
    [property: JsonPropertyName("stored_text_bytes")] long StoredTextBytes,
    [property: JsonPropertyName("materialized_text_bytes")]
        long MaterializedTextBytes,
    [property: JsonPropertyName("hash_text_bytes")] long HashTextBytes,
    [property: JsonPropertyName("total_retained_bytes")]
        long TotalRetainedBytes);
