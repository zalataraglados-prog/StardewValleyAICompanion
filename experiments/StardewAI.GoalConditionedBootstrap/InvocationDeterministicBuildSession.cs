using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class InvocationDeterministicBuildSession : IDisposable
{
    public const string SessionVersion =
        "stardewai.invocation_deterministic_build_session.v1";

    private static readonly AsyncLocal<
        InvocationDeterministicBuildSession?> Ambient = new();

    private readonly Dictionary<string, byte[]> entries =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> referenceEntries =
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

    public static T GetOrBuild<T>(
        string producerId,
        string producerVersion,
        IEnumerable<DeterministicBuildIdentityPart> identity,
        Func<T> factory)
    {
        var session = Ambient.Value;
        if (session is null || !session.enabled || session.disposed != 0)
            return factory();

        var identityParts = identity.ToArray();
        using var operation = ValidationTraceSession.BeginOperation(
            "deterministic_build",
            producerId,
            producerVersion,
            identityParts
                .Where(part => part.IsFile)
                .Select(part => part.Identity)
                .ToArray());
        var key = BuildKey(producerId, producerVersion, identityParts);
        operation.AddArtifactId(key);
        if (session.entries.TryGetValue(key, out var cached))
        {
            session.cacheHitCount++;
            operation.MarkCacheHit();
            return JsonSerializer.Deserialize<T>(cached, JsonDefaults.Options)
                ?? throw new InvalidDataException(
                    "Deterministic build cache entry is null: " +
                    producerId);
        }

        var result = factory();
        session.entries.Add(
            key,
            JsonSerializer.SerializeToUtf8Bytes(
                result,
                JsonDefaults.Options));
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
        using var operation = ValidationTraceSession.BeginOperation(
            "deterministic_reference_build",
            producerId,
            producerVersion,
            identityParts
                .Where(part => part.IsFile)
                .Select(part => part.Identity)
                .ToArray());
        var key = BuildKey(producerId, producerVersion, identityParts);
        operation.AddArtifactId(key);
        if (session.referenceEntries.TryGetValue(key, out var cached))
        {
            operation.MarkCacheHit();
            return (T)cached;
        }

        var result = factory();
        session.referenceEntries.Add(key, result);
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

    public static DeterministicBuildIdentityPart Value(
        string name,
        object? value) => new(
            name,
            JsonSerializer.Serialize(value, JsonDefaults.Compact),
            IsFile: false);

    public InvocationDeterministicBuildSessionSnapshot Snapshot() => new(
        SessionVersion,
        enabled,
        buildCount,
        cacheHitCount,
        entries.Sum(pair => (long)pair.Value.LongLength));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        entries.Clear();
        referenceEntries.Clear();
        if (ReferenceEquals(Ambient.Value, this))
            Ambient.Value = previous;
    }

    private static DeterministicBuildIdentityPart IdentityPart(
        string name,
        object? value,
        Type type)
    {
        if (type == typeof(string) &&
            name.EndsWith("Path", StringComparison.Ordinal) &&
            value is string path)
        {
            return File(name, path);
        }
        return Value(name, value);
    }

    private static string BuildKey(
        string producerId,
        string producerVersion,
        IEnumerable<DeterministicBuildIdentityPart> identity)
    {
        var builder = new StringBuilder()
            .Append(SessionVersion).Append('\n')
            .Append(producerId).Append('\n')
            .Append(producerVersion).Append('\n');
        foreach (var part in identity)
        {
            builder.Append(part.Name).Append('=');
            if (part.IsFile)
            {
                var fullPath = Path.GetFullPath(part.Identity);
                builder.Append(fullPath).Append('#');
                if (System.IO.File.Exists(fullPath))
                {
                    builder.Append(InvocationReadSession.HashFile(
                        fullPath,
                        out _));
                }
                else
                {
                    builder.Append("missing");
                }
            }
            else
            {
                builder.Append(part.Identity);
            }
            builder.Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }
}

internal readonly record struct DeterministicBuildIdentityPart(
    string Name,
    string Identity,
    bool IsFile);

internal sealed record InvocationDeterministicBuildSessionSnapshot(
    string SessionVersion,
    bool Enabled,
    int BuildCount,
    int CacheHitCount,
    long CachedBytes);
