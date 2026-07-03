using StardewModdingAPI;
using StardewValley;
using StardewAI.TransparentBridge.Adapters;
using StardewAI.Contracts.State;

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

    public SnapshotEnvelope BuildSnapshot()
    {
        var tick = unchecked((long)Game1.ticks);
        var player = Context.IsWorldReady ? Game1.player : null;
        var sections = new Dictionary<string, object>();
        var unavailableFields = new List<string>();

        foreach (var adapter in adapters)
        {
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

        EnsureCanonicalSection(sections, unavailableFields, "quests", tick);
        EnsureCanonicalSection(sections, unavailableFields, "world_progress", tick);
        EnsureCanonicalSection(sections, unavailableFields, "menus", tick);
        EnsureCanonicalSection(sections, unavailableFields, "modded_state", tick, fieldMap: false);

        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = bridgeVersion,
            SmapiVersion = Constants.ApiVersion.ToString(),
            GameVersion = "unknown",
            InstalledMods = modRegistry.GetAll()
                .Select(mod => new InstalledMod(mod.Manifest.UniqueID, mod.Manifest.Name, mod.Manifest.Version.ToString()))
                .ToArray(),
            SaveId = Field(player?.farmName.Value, "Game1.player.farmName", tick),
            PlayerId = Field(player?.UniqueMultiplayerID.ToString(), "Game1.player.UniqueMultiplayerID", tick),
            GameTick = tick,
            InGameTime = Field(Context.IsWorldReady ? (int?)Game1.timeOfDay : null, "Game1.timeOfDay", tick),
            RealTimestamp = DateTimeOffset.UtcNow,
            Completeness = unavailableFields.Count == 0 ? "complete" : "partial",
            UnavailableFields = unavailableFields.Distinct().OrderBy(field => field).ToArray(),
            State = sections
        };
    }

    private static FieldEnvelope<T> Field<T>(T value, string source, long readAtTick) => new()
    {
        Value = value,
        Status = value is null ? "unavailable" : "available",
        Source = new SourceRef { Kind = value is null ? "unavailable" : "game_object", Path = source },
        Adapter = "vanilla_1_6",
        ReadAtTick = readAtTick,
        Confidence = value is null ? 0.0 : 1.0,
        Reason = value is null ? "value_unavailable" : null
    };

    private static void EnsureCanonicalSection(
        IDictionary<string, object> sections,
        ICollection<string> unavailableFields,
        string sectionName,
        long tick,
        bool fieldMap = true)
    {
        if (sections.ContainsKey(sectionName))
        {
            return;
        }

        sections[sectionName] = fieldMap
            ? new Dictionary<string, object>
            {
                ["status"] = new FieldEnvelope<object?>
                {
                    Value = null,
                    Status = "unavailable",
                    Source = new SourceRef { Kind = "unavailable", Path = $"state.{sectionName}" },
                    Adapter = "not_connected",
                    ReadAtTick = tick,
                    Confidence = 0.0,
                    Reason = $"{sectionName}_reader_not_connected"
                }
            }
            : new Dictionary<string, object>();

        unavailableFields.Add(sectionName);
    }
}
