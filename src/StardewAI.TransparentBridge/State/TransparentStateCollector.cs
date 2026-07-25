using StardewModdingAPI;
using StardewValley;
using StardewAI.TransparentBridge.Adapters;
using StardewAI.Contracts.State;
using System.Text.Json;

namespace StardewAI.TransparentBridge.State;

public sealed class TransparentStateCollector
{
    private readonly string bridgeVersion;
    private readonly IModRegistry modRegistry;
    private readonly IReadOnlyList<IStateAdapter> adapters;

    public TransparentStateCollector(string bridgeVersion, IModRegistry modRegistry, IEnumerable<IStateAdapter> adapters)
    {
        this.bridgeVersion = bridgeVersion;
        this.modRegistry = modRegistry;
        this.adapters = adapters.OrderBy(adapter => adapter.Priority).ToArray();
    }

    public IReadOnlyList<IStateAdapter> Adapters => adapters;

    public SnapshotEnvelope BuildSnapshot(ISet<string>? allowedDomains = null)
    {
        var tick = unchecked((long)Game1.ticks);
        var player = Context.IsWorldReady ? Game1.player : null;
        var sections = new Dictionary<string, object>();
        var unavailableFields = new List<string>();

        foreach (var adapter in adapters)
        {
            if (allowedDomains is not null && !allowedDomains.Contains(adapter.Domain))
            {
                continue;
            }

            var result = adapter.Collect(tick);
            if (sections.TryGetValue(result.SectionName, out var existing) &&
                existing is Dictionary<string, object> existingFields)
            {
                foreach (var field in result.Fields)
                {
                    existingFields[field.Key] = field.Value;
                }
            }
            else
            {
                sections[result.SectionName] = new Dictionary<string, object>(result.Fields);
            }

            unavailableFields.AddRange(result.UnavailableFields);
        }

        sections["environment"] = new Dictionary<string, object>
        {
            ["game_version"] = Field(Game1.version, "Game1.version", tick),
            ["smapi_version"] = Field(Constants.ApiVersion.ToString(), "StardewModdingAPI.Constants.ApiVersion", tick, "smapi_constants"),
            ["bridge_version"] = Field(bridgeVersion, "IModManifest.Version", tick, "bridge_manifest"),
            ["training_mode"] = Field(Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE"), "Environment.STARDEWAI_TRAINING_MODE", tick, "process_environment"),
            ["training_run_id"] = Field(Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID"), "Environment.STARDEWAI_TRAINING_RUN_ID", tick, "process_environment"),
            ["save_isolation_path"] = Field(Environment.GetEnvironmentVariable("STARDEWAI_SAVE_ISOLATION_PATH"), "Environment.STARDEWAI_SAVE_ISOLATION_PATH", tick, "process_environment"),
            ["installed_mods"] = Field(modRegistry.GetAll()
                .Select(mod => new
                {
                    id = mod.Manifest.UniqueID,
                    name = mod.Manifest.Name,
                    version = mod.Manifest.Version.ToString()
                })
                .ToArray(), "IModRegistry.GetAll()", tick, "smapi_mod_registry")
        };

        sections["identity"] = new Dictionary<string, object>
        {
            ["save_id"] = Field(player?.farmName.Value, "Game1.player.farmName", tick),
            ["player_id"] = Field(player?.UniqueMultiplayerID.ToString(), "Game1.player.UniqueMultiplayerID", tick)
        };

        var state = sections.ToDictionary(
            item => item.Key,
            item => JsonSerializer.SerializeToElement(item.Value, JsonOptions));

        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = bridgeVersion,
            SmapiVersion = Constants.ApiVersion.ToString(),
            GameVersion = Game1.version,
            InstalledMods = modRegistry.GetAll()
                .Select(mod => new InstalledModRef(mod.Manifest.UniqueID, mod.Manifest.Name, mod.Manifest.Version.ToString()))
                .ToArray(),
            SaveId = Field(player?.farmName.Value, "Game1.player.farmName", tick),
            PlayerId = Field(player?.UniqueMultiplayerID.ToString(), "Game1.player.UniqueMultiplayerID", tick),
            GameTick = tick,
            InGameTime = Field(Context.IsWorldReady ? (int?)Game1.timeOfDay : null, "Game1.timeOfDay", tick),
            RealTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            Completeness = unavailableFields.Count == 0 ? "complete" : "partial",
            UnavailableFields = unavailableFields.Distinct().OrderBy(field => field).ToArray(),
            State = state
        };

        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static FieldEnvelope<T> Field<T>(T value, string source, long readAtTick, string adapter = "vanilla_1_6") => new()
    {
        Value = value,
        Status = value is null ? FieldStatus.Unavailable : FieldStatus.Available,
        Source = new SourceRef { Kind = value is null ? "unavailable" : "game_object", Path = source },
        Adapter = adapter,
        ReadAtTick = readAtTick,
        Confidence = value is null ? 0.0 : 1.0,
        Reason = value is null ? "value_unavailable" : null
    };
}
