using System.Reflection;
using System.Text.Json;
using System.Threading;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class InvocationDeterministicBuildSession : IDisposable
{
    public const string SessionVersion =
        "stardewai.invocation_deterministic_build_session.v2";

    private static readonly AsyncLocal<
        InvocationDeterministicBuildSession?> Ambient = new();

    private readonly Dictionary<string, byte[]> entries =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> referenceEntries =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProducerStatistics> producerStatistics =
        new(StringComparer.Ordinal);
    private readonly InvocationDeterministicBuildSession? previous;
    private readonly bool enabled;
    private int disposed;
    private int buildCount;
    private int cacheHitCount;

    private InvocationDeterministicBuildSession(bool enabled)
    {
        this.enabled = enabled;
        previous = Ambient.Value;
        Ambient.Value = this;
    }

    public static InvocationDeterministicBuildSession Start(
        bool enabled = true) => new(enabled);

    internal static bool IsReuseEnabled
    {
        get
        {
            var session = Ambient.Value;
            return session is not null && session.enabled &&
                session.disposed == 0;
        }
    }

    internal static InvocationDeterministicBuildProducerSnapshot?
        CurrentProducerSnapshot(string producerId)
    {
        var session = Ambient.Value;
        if (session is null || session.disposed != 0)
            return null;
        return session.Snapshot().Producers.SingleOrDefault(producer =>
            producer.ProducerId == producerId);
    }

    public static T GetOrBuild<T>(
        string producerId,
        string producerVersion,
        IEnumerable<DeterministicBuildIdentityPart> identity,
        Func<T> factory,
        string artifactKind = "deterministic_derived_artifact")
    {
        var session = Ambient.Value;
        if (session is null || !session.enabled || session.disposed != 0)
            return factory();

        var identityParts = identity.ToArray();
        var buildIdentity = BuildIdentity(
            producerId,
            producerVersion,
            identityParts);
        using var operation = ValidationTraceSession.BeginOperation(
            "deterministic_build",
            producerId,
            producerVersion,
            identityParts
                .Where(part => part.IsFile)
                .Select(part => part.Identity)
                .ToArray());
        using var dependency = ValidationTraceSession.BeginDependencyNode(
            buildIdentity.Key,
            artifactKind,
            producerId,
            producerVersion,
            buildIdentity.Inputs);
        var key = buildIdentity.Key;
        operation.AddArtifactId(key);
        if (session.entries.TryGetValue(key, out var cached))
        {
            session.cacheHitCount++;
            session.Statistics(producerId).RecordHit(cached.LongLength);
            operation.MarkCacheHit();
            dependency.MarkCacheHit();
            dependency.SetOutputArtifact(key, Sha256(cached));
            return JsonSerializer.Deserialize<T>(cached, JsonDefaults.Options)
                ?? throw new InvalidDataException(
                    "Deterministic build cache entry is null: " +
                    producerId);
        }

        var result = factory();
        var serialized = JsonSerializer.SerializeToUtf8Bytes(
            result,
            JsonDefaults.Options);
        session.entries.Add(key, serialized);
        session.Statistics(producerId).RecordBuild(serialized.LongLength);
        dependency.SetOutputArtifact(key, Sha256(serialized));
        session.buildCount++;
        return result;
    }

    public static T GetOrBuildReference<T>(
        string producerId,
        string producerVersion,
        IEnumerable<DeterministicBuildIdentityPart> identity,
        Func<T> factory)
        where T : class
    {
        var session = Ambient.Value;
        if (session is null || !session.enabled || session.disposed != 0)
            return factory();

        var identityParts = identity.ToArray();
        var buildIdentity = BuildIdentity(
            producerId,
            producerVersion,
            identityParts);
        using var operation = ValidationTraceSession.BeginOperation(
            "deterministic_reference_build",
            producerId,
            producerVersion,
            identityParts
                .Where(part => part.IsFile)
                .Select(part => part.Identity)
                .ToArray());
        using var dependency = ValidationTraceSession.BeginDependencyNode(
            buildIdentity.Key,
            "deterministic_reference_index",
            producerId,
            producerVersion,
            buildIdentity.Inputs);
        var key = buildIdentity.Key;
        operation.AddArtifactId(key);
        if (session.referenceEntries.TryGetValue(key, out var cached))
        {
            session.Statistics(producerId).RecordReferenceHit();
            operation.MarkCacheHit();
            dependency.MarkCacheHit();
            var typed = (T)cached;
            dependency.SetOutputArtifact(
                key,
                dependency.IsEnabled ? ContentSha256(typed) : string.Empty);
            return typed;
        }

        var result = factory();
        session.referenceEntries.Add(key, result);
        session.Statistics(producerId).RecordReferenceBuild();
        dependency.SetOutputArtifact(
            key,
            dependency.IsEnabled ? ContentSha256(result) : string.Empty);
        return result;
    }

    public static T Observe<T>(
        string artifactKind,
        string producerId,
        string producerVersion,
        IEnumerable<DeterministicBuildIdentityPart> identity,
        Func<T> factory,
        Func<T, object?>? contentProjection = null)
    {
        if (!ValidationTraceSession.IsActive)
            return factory();

        var buildIdentity = BuildIdentity(
            producerId,
            producerVersion,
            identity.ToArray());
        using var dependency = ValidationTraceSession.BeginDependencyNode(
            buildIdentity.Key,
            artifactKind,
            producerId,
            producerVersion,
            buildIdentity.Inputs);
        var result = factory();
        dependency.SetOutputArtifact(
            buildIdentity.Key,
            ContentSha256(contentProjection is null
                ? result
                : contentProjection(result)));
        return result;
    }

    public static DeterministicBuildIdentityPart[] Describe(object value) =>
        value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => IdentityPart(
                property.Name,
                property.GetValue(value),
                property.PropertyType))
            .ToArray();

    public static DeterministicBuildIdentityPart File(
        string name,
        string path) => new(name, path, IsFile: true);

    public static DeterministicBuildIdentityPart VerifiedTransitiveFile(
        string name,
        string path,
        string contentSha256)
    {
        if (contentSha256.Length != 64 ||
            contentSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                "Verified transitive file identity requires SHA-256: " +
                name);
        }
        return new DeterministicBuildIdentityPart(
            name,
            path,
            IsFile: true,
            contentSha256.ToLowerInvariant());
    }

    public static DeterministicBuildIdentityPart Value(
        string name,
        object? value) => new(
            name,
            JsonSerializer.Serialize(value, JsonDefaults.Compact),
            IsFile: false);

    public InvocationDeterministicBuildSessionSnapshot Snapshot()
    {
        var cachedBytes = entries.Sum(pair => (long)pair.Value.LongLength);
        return new InvocationDeterministicBuildSessionSnapshot(
            SessionVersion,
            enabled,
            buildCount,
            cacheHitCount,
            entries.Count,
            referenceEntries.Count,
            cachedBytes,
            entries.Count == 0
                ? 0
                : entries.Max(pair => (long)pair.Value.LongLength),
            producerStatistics
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value.Snapshot(pair.Key))
                .ToArray());
    }

    private ProducerStatistics Statistics(string producerId)
    {
        if (!producerStatistics.TryGetValue(producerId, out var statistics))
        {
            statistics = new ProducerStatistics();
            producerStatistics.Add(producerId, statistics);
        }
        return statistics;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        entries.Clear();
        referenceEntries.Clear();
        producerStatistics.Clear();
        if (ReferenceEquals(Ambient.Value, this))
            Ambient.Value = previous;
    }

    private sealed class ProducerStatistics
    {
        private long buildCount;
        private long cacheHitCount;
        private long cachedBytes;
        private long maxEntryBytes;
        private long referenceBuildCount;
        private long referenceHitCount;

        public void RecordBuild(long bytes)
        {
            buildCount++;
            cachedBytes += bytes;
            maxEntryBytes = Math.Max(maxEntryBytes, bytes);
        }

        public void RecordHit(long bytes)
        {
            cacheHitCount++;
            maxEntryBytes = Math.Max(maxEntryBytes, bytes);
        }

        public void RecordReferenceBuild() => referenceBuildCount++;

        public void RecordReferenceHit() => referenceHitCount++;

        public InvocationDeterministicBuildProducerSnapshot Snapshot(
            string producerId) => new(
                producerId,
                buildCount,
                cacheHitCount,
                cachedBytes,
                maxEntryBytes,
                referenceBuildCount,
                referenceHitCount);
    }
}

internal readonly record struct DeterministicBuildIdentityPart(
    string Name,
    string Identity,
    bool IsFile,
    string? VerifiedContentSha256 = null);

internal sealed record InvocationDeterministicBuildSessionSnapshot(
    string SessionVersion,
    bool Enabled,
    int BuildCount,
    int CacheHitCount,
    int EntryCount,
    int ReferenceEntryCount,
    long CachedBytes,
    long MaxEntryBytes,
    InvocationDeterministicBuildProducerSnapshot[] Producers);

internal sealed record InvocationDeterministicBuildProducerSnapshot(
    string ProducerId,
    long BuildCount,
    long CacheHitCount,
    long CachedBytes,
    long MaxEntryBytes,
    long ReferenceBuildCount,
    long ReferenceHitCount);
