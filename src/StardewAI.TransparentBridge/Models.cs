using StardewAI.Contracts.State;

namespace StardewAI.TransparentBridge;

public sealed class BridgeConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8765;
    public string PermissionMode { get; set; } = "observer";
}

public sealed class SnapshotEnvelope
{
    public string SchemaVersion { get; set; } = "snapshot.v1";
    public string BridgeVersion { get; set; } = "0.1.0";
    public string SmapiVersion { get; set; } = "unknown";
    public string GameVersion { get; set; } = "unknown";
    public InstalledMod[] InstalledMods { get; set; } = Array.Empty<InstalledMod>();
    public FieldEnvelope<string?> SaveId { get; set; } = new();
    public FieldEnvelope<string?> PlayerId { get; set; } = new();
    public long GameTick { get; set; }
    public FieldEnvelope<int?> InGameTime { get; set; } = new();
    public DateTimeOffset RealTimestamp { get; set; }
    public string StateHash { get; set; } = "unavailable";
    public string Completeness { get; set; } = "partial";
    public string[] UnavailableFields { get; set; } = Array.Empty<string>();
    public object State { get; set; } = new();
}

public sealed record InstalledMod(string Id, string Name, string Version);

public sealed record AuditRecord(
    string EventId,
    string EventType,
    DateTimeOffset RealTimestamp,
    long GameTick,
    string StateHash,
    object? Details);

public sealed record StateAdapterResult(
    string SectionName,
    IReadOnlyDictionary<string, object> Fields,
    IReadOnlyList<string> UnavailableFields,
    string Completeness);
