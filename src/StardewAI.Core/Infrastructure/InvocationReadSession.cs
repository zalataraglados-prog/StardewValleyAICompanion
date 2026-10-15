using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace StardewAI.Core.Infrastructure;

public sealed class InvocationReadSession : IDisposable
{
    public const string ReaderVersion = "stardewai.invocation_read_session.v2";

    public const long DefaultMaxRetainedBytes = 32L * 1024 * 1024;
    public const long DefaultMaxEntryRetainedBytes = 24L * 1024 * 1024;
    private const long DynamicLargeEntryAdmissionBytes = 1024L * 1024;

    private static readonly AsyncLocal<InvocationReadSession?> Ambient = new();
    private static readonly StringComparer PathComparer =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly object sync = new();
    private readonly HashSet<string> immutablePaths;
    private readonly HashSet<string> dynamicPaths;
    private readonly HashSet<string> prohibitedPaths;
    private readonly string[] prohibitedRoots;
    private readonly Dictionary<string, Entry> entries;
    private readonly Dictionary<string, long> accessCounts;
    private readonly InvocationReadSession? previous;
    private readonly bool enabled;
    private readonly long maxRetainedBytes;
    private readonly long maxEntryRetainedBytes;
    private long physicalReadCount;
    private long physicalBytesRead;
    private long fullHashCount;
    private long hashBytes;
    private long materializationCount;
    private long cacheHitCount;
    private long dynamicRegistrationCount;
    private long evictionCount;
    private long largeEntryBypassCount;
    private long probationBypassCount;
    private long releasedRawBytes;
    private long releasedStoredTextBytes;
    private long accessSequence;
    private int disposed;

    private InvocationReadSession(
        IEnumerable<string> immutableInputPaths,
        IEnumerable<string> prohibitedPaths,
        IEnumerable<string> prohibitedRoots,
        bool enabled,
        long maxRetainedBytes,
        long maxEntryRetainedBytes)
    {
        if (maxRetainedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetainedBytes));
        if (maxEntryRetainedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntryRetainedBytes));
        this.enabled = enabled;
        this.maxRetainedBytes = maxRetainedBytes;
        this.maxEntryRetainedBytes = maxEntryRetainedBytes;
        this.prohibitedPaths = new HashSet<string>(
            prohibitedPaths.Select(CanonicalPath),
            PathComparer);
        this.prohibitedRoots = prohibitedRoots
            .Select(CanonicalPath)
            .Select(TrimDirectorySeparators)
            .Distinct(PathComparer)
            .ToArray();
        immutablePaths = immutableInputPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(CanonicalPath)
            .Where(File.Exists)
            .Where(path => !this.prohibitedPaths.Contains(path))
            .Where(path => !this.prohibitedRoots.Any(root =>
                IsDescendant(path, root)))
            .ToHashSet(PathComparer);
        entries = new Dictionary<string, Entry>(PathComparer);
        dynamicPaths = new HashSet<string>(PathComparer);
        accessCounts = new Dictionary<string, long>(PathComparer);
        previous = Ambient.Value;
        Ambient.Value = this;
    }

    public static InvocationReadSession Start(
        IEnumerable<string> immutableInputPaths,
        IEnumerable<string>? prohibitedPaths = null,
        IEnumerable<string>? prohibitedRoots = null,
        bool enabled = true,
        long maxRetainedBytes = DefaultMaxRetainedBytes,
        long maxEntryRetainedBytes = DefaultMaxEntryRetainedBytes) => new(
            immutableInputPaths ?? Array.Empty<string>(),
            prohibitedPaths ?? Array.Empty<string>(),
            prohibitedRoots ?? Array.Empty<string>(),
            enabled,
            maxRetainedBytes,
            maxEntryRetainedBytes);

    public static bool IsPathReusable(string path)
    {
        var session = Ambient.Value;
        return session is not null && session.IsRegistered(CanonicalPath(path));
    }

    public static string HashFile(
        string path,
        out InvocationReadObservation observation)
    {
        var fullPath = CanonicalPath(path);
        var session = Ambient.Value;
        if (session is null || !session.CanReuse(
                fullPath,
                allowDynamicRegistration: false))
            return HashWithoutReuse(fullPath, out observation);
        return session.HashReusableFile(fullPath, out observation);
    }

    public static string HashImmutableInputFile(
        string path,
        out InvocationReadObservation observation)
    {
        var fullPath = CanonicalPath(path);
        var session = Ambient.Value;
        if (session is null || !session.CanReuse(
                fullPath,
                allowDynamicRegistration: true))
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
        if (session is null || !session.CanReuse(
                fullPath,
                allowDynamicRegistration: true))
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
        if (session is null || !session.CanReuse(
                fullPath,
                allowDynamicRegistration: true))
            return false;

        lock (session.sync)
        {
            session.accessCounts.TryGetValue(fullPath, out var accessCount);
            session.accessCounts[fullPath] = accessCount + 1;
            if (!session.entries.TryGetValue(fullPath, out var entry) ||
                entry.MaterializedText is null)
            {
                return false;
            }
            text = entry.MaterializedText;
            logicalSha256 = entry.LogicalSha256;
            entry.LastUse = ++session.accessSequence;
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
        if (session is null || !session.CanReuse(
                fullPath,
                allowDynamicRegistration: true))
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
            entry.LastUse = ++session.accessSequence;
            if (!ReferenceEquals(entry.StoredText, text))
            {
                session.releasedStoredTextBytes += Utf16Bytes(entry.StoredText);
                entry.StoredText = null;
            }
            session.ReleaseRawBytes(entry);
            if (session.dynamicPaths.Contains(fullPath) &&
                session.accessCounts.GetValueOrDefault(fullPath) <= 1 &&
                EntryRetainedBytes(entry) > DynamicLargeEntryAdmissionBytes)
            {
                session.entries.Remove(fullPath);
                session.probationBypassCount++;
                return;
            }
            session.EnforceBudget(fullPath);
        }
    }

    public InvocationReadSessionSnapshot Snapshot()
    {
        lock (sync)
        {
            var retainedRawBytes = entries.Values.Sum(entry =>
                entry.RawBytes?.LongLength ?? 0);
            var retainedStoredTextBytes = entries.Values.Sum(entry =>
                Utf16Bytes(entry.StoredText));
            var retainedMaterializedTextBytes = entries.Values.Sum(entry =>
                ReferenceEquals(entry.MaterializedText, entry.StoredText)
                    ? 0
                    : Utf16Bytes(entry.MaterializedText));
            var retainedHashTextBytes = entries.Values.Sum(entry =>
                Utf16Bytes(entry.StoredSha256) +
                (ReferenceEquals(entry.LogicalSha256, entry.StoredSha256)
                    ? 0
                    : Utf16Bytes(entry.LogicalSha256)));
            var retainedBytes = retainedRawBytes + retainedStoredTextBytes +
                retainedMaterializedTextBytes + retainedHashTextBytes;
            var maxEntryRetainedBytes = entries.Count == 0
                ? 0
                : entries.Values.Max(EntryRetainedBytes);
            var largestEntries = entries
                .Select(pair => new InvocationReadEntrySnapshot(
                    pair.Key,
                    EntryRetainedBytes(pair.Value),
                    accessCounts.GetValueOrDefault(pair.Key)))
                .OrderByDescending(entry => entry.RetainedBytes)
                .ThenBy(entry => entry.Path, PathComparer)
                .Take(10)
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
                retainedRawBytes,
                retainedStoredTextBytes,
                retainedMaterializedTextBytes,
                retainedHashTextBytes,
                retainedBytes,
                maxEntryRetainedBytes,
                this.maxRetainedBytes,
                this.maxEntryRetainedBytes,
                dynamicRegistrationCount,
                evictionCount,
                largeEntryBypassCount,
                probationBypassCount,
                releasedRawBytes,
                releasedStoredTextBytes,
                largestEntries);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        if (ReferenceEquals(Ambient.Value, this))
            Ambient.Value = previous;
    }

    private bool CanReuse(
        string fullPath,
        bool allowDynamicRegistration)
    {
        if (!enabled || disposed != 0)
            return false;
        lock (sync)
        {
            if (immutablePaths.Contains(fullPath))
                return true;
            if (!allowDynamicRegistration || !File.Exists(fullPath) ||
                prohibitedPaths.Contains(fullPath) ||
                prohibitedRoots.Any(root => IsDescendant(fullPath, root)))
            {
                return false;
            }
            immutablePaths.Add(fullPath);
            dynamicPaths.Add(fullPath);
            dynamicRegistrationCount++;
            return true;
        }
    }

    private string ReadReusableText(
        string fullPath,
        Encoding encoding,
        out InvocationReadObservation observation)
    {
        lock (sync)
        {
            var entry = GetOrCreate(fullPath);
            var reused = entry.RawBytes is not null || entry.StoredText is not null;
            var bytesRead = 0L;
            var hashWork = 0L;
            if (entry.StoredText is null)
            {
                bytesRead = EnsureRawBytes(fullPath, entry);
                hashWork = EnsureStoredHash(entry);
                using var stream = new MemoryStream(
                    entry.RawBytes!,
                    writable: false);
                using var reader = new StreamReader(
                    stream,
                    encoding,
                    detectEncodingFromByteOrderMarks: true);
                entry.StoredText = reader.ReadToEnd();
            }
            entry.LastUse = ++accessSequence;
            ReleaseRawBytes(entry);
            EnforceBudget(fullPath);
            if (reused)
                cacheHitCount++;
            observation = new InvocationReadObservation(
                bytesRead,
                hashWork,
                reused);
            return entry.StoredText;
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
            var reusedHash = entry.StoredSha256 is not null;
            var bytesRead = 0L;
            var hashWork = 0L;
            if (!reusedHash)
            {
                bytesRead = EnsureRawBytes(fullPath, entry);
                hashWork = EnsureStoredHash(entry);
            }
            entry.LastUse = ++accessSequence;
            ReleaseRawBytes(entry);
            EnforceBudget(fullPath);
            if (reusedBytes || reusedHash)
                cacheHitCount++;
            observation = new InvocationReadObservation(
                bytesRead,
                hashWork,
                reusedBytes || reusedHash);
            return entry.StoredSha256!;
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

    private long EnsureStoredHash(Entry entry)
    {
        if (entry.StoredSha256 is not null)
            return 0;
        entry.StoredSha256 = Sha256(entry.RawBytes!);
        var hashWork = entry.RawBytes!.LongLength;
        fullHashCount++;
        hashBytes += hashWork;
        return hashWork;
    }

    private bool IsRegistered(string fullPath)
    {
        if (!enabled || disposed != 0)
            return false;
        lock (sync)
            return immutablePaths.Contains(fullPath);
    }

    private void ReleaseRawBytes(Entry entry)
    {
        if (entry.RawBytes is null || entry.StoredSha256 is null)
            return;
        releasedRawBytes += entry.RawBytes.LongLength;
        entry.RawBytes = null;
    }

    private void EnforceBudget(string currentPath)
    {
        if (!entries.TryGetValue(currentPath, out var current))
            return;
        if (EntryRetainedBytes(current) > maxEntryRetainedBytes)
        {
            entries.Remove(currentPath);
            largeEntryBypassCount++;
            return;
        }

        while (entries.Values.Sum(EntryRetainedBytes) > maxRetainedBytes &&
               entries.Count > 0)
        {
            var victim = entries
                .OrderBy(pair => accessCounts.GetValueOrDefault(pair.Key))
                .ThenBy(pair => pair.Value.LastUse)
                .ThenBy(pair => pair.Key, PathComparer)
                .First();
            entries.Remove(victim.Key);
            evictionCount++;
        }
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

    private static long EntryRetainedBytes(Entry entry) =>
        (entry.RawBytes?.LongLength ?? 0) +
        Utf16Bytes(entry.StoredText) +
        (ReferenceEquals(entry.MaterializedText, entry.StoredText)
            ? 0
            : Utf16Bytes(entry.MaterializedText)) +
        Utf16Bytes(entry.StoredSha256) +
        (ReferenceEquals(entry.LogicalSha256, entry.StoredSha256)
            ? 0
            : Utf16Bytes(entry.LogicalSha256));

    private static long Utf16Bytes(string? value) =>
        value is null ? 0 : (long)value.Length * sizeof(char);

    private static string TrimDirectorySeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsDescendant(string path, string root) =>
        path.StartsWith(
            root + Path.DirectorySeparatorChar,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private sealed class Entry
    {
        public byte[]? RawBytes { get; set; }
        public string? StoredText { get; set; }
        public string? StoredSha256 { get; set; }
        public string? MaterializedText { get; set; }
        public string? LogicalSha256 { get; set; }
        public long LastUse { get; set; }
    }
}

public readonly record struct InvocationReadObservation(
    long PhysicalBytesRead,
    long HashBytes,
    bool CacheHit);

public sealed record InvocationReadEntrySnapshot(
    string Path,
    long RetainedBytes,
    long AccessCount);

public sealed record InvocationReadSessionSnapshot(
    string ReaderVersion,
    bool Enabled,
    int RegisteredPathCount,
    int EntryCount,
    long PhysicalReadCount,
    long PhysicalBytesRead,
    long FullHashCount,
    long HashBytes,
    long MaterializationCount,
    long CacheHitCount,
    long RetainedRawBytes,
    long RetainedStoredTextBytes,
    long RetainedMaterializedTextBytes,
    long RetainedHashTextBytes,
    long RetainedBytes,
    long MaxEntryRetainedBytes,
    long RetentionBudgetBytes,
    long EntryRetentionLimitBytes,
    long DynamicRegistrationCount,
    long EvictionCount,
    long LargeEntryBypassCount,
    long ProbationBypassCount,
    long ReleasedRawBytes,
    long ReleasedStoredTextBytes,
    InvocationReadEntrySnapshot[] LargestEntries);
