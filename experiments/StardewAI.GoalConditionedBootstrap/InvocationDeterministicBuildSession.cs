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
    private readonly Dictionary<string, ProducerMetrics> producerMetrics =
        new(StringComparer.Ordinal);
    private readonly InvocationDeterministicBuildSession? previous;
    private readonly bool enabled;
    private int disposed;
    private int buildCount;
    private int cacheHitCount;
    private int referenceBuildCount;
    private int referenceCacheHitCount;

    private InvocationDeterministicBuildSession(bool enabled)
    {
        this.enabled = enabled;
        previous = Ambient.Value;
        Ambient.Value = this;
    }

    public static InvocationDeterministicBuildSession Start(
        bool enabled = true) => new(enabled);

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
        var metrics = session.GetProducerMetrics(producerId, producerVersion);
        operation.AddArtifactId(key);
        if (session.entries.TryGetValue(key, out var cached))
        {
            session.cacheHitCount++;
            metrics.CacheHitCount++;
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
        metrics.BuildCount++;
        metrics.CachedBytes += serialized.LongLength;
        metrics.MaxEntryBytes = Math.Max(
            metrics.MaxEntryBytes,
            serialized.LongLength);
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
        var metrics = session.GetProducerMetrics(producerId, producerVersion);
        operation.AddArtifactId(key);
        if (session.referenceEntries.TryGetValue(key, out var cached))
        {
            session.referenceCacheHitCount++;
            metrics.ReferenceCacheHitCount++;
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
        session.referenceBuildCount++;
        metrics.ReferenceBuildCount++;
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
        var cachedBytes = entries.Sum(pair => pair.Value.LongLength);
        return new InvocationDeterministicBuildSessionSnapshot(
            SessionVersion,
            enabled,
            buildCount,
            cacheHitCount,
            cachedBytes,
            entries.Count,
            referenceEntries.Count,
            referenceBuildCount,
            referenceCacheHitCount,
            entries.Count == 0
                ? 0
                : entries.Max(pair => pair.Value.LongLength),
            producerMetrics.Values
                .OrderBy(value => value.ProducerId, StringComparer.Ordinal)
                .ThenBy(
                    value => value.ProducerVersion,
                    StringComparer.Ordinal)
                .Select(value =>
                    new ValidationTraceDeterministicProducerMetrics
                    {
                        ProducerId = value.ProducerId,
                        ProducerVersion = value.ProducerVersion,
                        BuildCount = value.BuildCount,
                        CacheHitCount = value.CacheHitCount,
                        ReferenceBuildCount = value.ReferenceBuildCount,
                        ReferenceCacheHitCount =
                            value.ReferenceCacheHitCount,
                        CachedBytes = value.CachedBytes,
                        MaxEntryBytes = value.MaxEntryBytes
                    })
                .ToArray());
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        entries.Clear();
        referenceEntries.Clear();
        producerMetrics.Clear();
        if (ReferenceEquals(Ambient.Value, this))
            Ambient.Value = previous;
    }

    private ProducerMetrics GetProducerMetrics(
        string producerId,
        string producerVersion)
    {
        var key = producerId + "\u001f" + producerVersion;
        if (producerMetrics.TryGetValue(key, out var metrics))
            return metrics;

        metrics = new ProducerMetrics(producerId, producerVersion);
        producerMetrics.Add(key, metrics);
        return metrics;
    }

    private sealed class ProducerMetrics
    {
        public ProducerMetrics(string producerId, string producerVersion)
        {
            ProducerId = producerId;
            ProducerVersion = producerVersion;
        }

        public string ProducerId { get; }
        public string ProducerVersion { get; }
        public int BuildCount { get; set; }
        public int CacheHitCount { get; set; }
        public int ReferenceBuildCount { get; set; }
        public int ReferenceCacheHitCount { get; set; }
        public long CachedBytes { get; set; }
        public long MaxEntryBytes { get; set; }
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
    long CachedBytes,
    int SerializedEntryCount,
    int ReferenceEntryCount,
    int ReferenceBuildCount,
    int ReferenceCacheHitCount,
    long MaxEntryBytes,
    ValidationTraceDeterministicProducerMetrics[] Producers);
