using StardewModdingAPI;
using StardewValley;
using StardewValley.Mods;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class ModdedStateReadAdapter : ReadAdapterBase
{
    private const string RegistrySource = "IModRegistry.GetAll()";
    private const string RegistryAdapter = "smapi_mod_registry";

    private readonly IModRegistry modRegistry;

    public ModdedStateReadAdapter(IModRegistry modRegistry)
    {
        this.modRegistry = modRegistry;
    }

    public override string Domain => "modded_state";
    public override int Priority => 80;

    public override StateAdapterResult Collect(long tick)
    {
        var mods = modRegistry.GetAll()
            .Select(mod => new ModdedStateModMetadata(
                mod.Manifest.UniqueID,
                mod.Manifest.Name,
                mod.Manifest.Version.ToString(),
                mod.Manifest.Author,
                mod.IsContentPack))
            .ToArray();

        return BuildResult(mods, tick, ReadSaveDataEntries(), ReadPublicModData());
    }

    public static StateAdapterResult BuildResult(
        IReadOnlyList<ModdedStateModMetadata> mods,
        long tick,
        IReadOnlyList<object>? saveDataEntries = null,
        IReadOnlyList<object>? publicModData = null)
    {
        var installed = mods
            .Select(mod => new
            {
                mod_id = mod.ModId,
                name = mod.Name,
                version = mod.Version,
                author = mod.Author,
                is_content_pack = mod.IsContentPack
            })
            .ToArray();

        var contentPacks = installed
            .Where(mod => mod.is_content_pack)
            .ToArray();

        var saveData = saveDataEntries ?? Array.Empty<object>();
        var publicData = publicModData ?? Array.Empty<object>();

        return Section("modded_state", new Dictionary<string, object>
        {
            ["installed_count"] = Field(installed.Length, RegistrySource, tick, RegistryAdapter),
            ["installed"] = Field(installed, RegistrySource, tick, RegistryAdapter),
            ["content_pack_count"] = Field(contentPacks.Length, RegistrySource, tick, RegistryAdapter),
            ["content_packs"] = Field(contentPacks, RegistrySource, tick, RegistryAdapter),
            ["arbitrary_mod_private_save_data"] = Field(new
            {
                source = "Game1.CustomData smapi/mod-data/* raw JSON strings",
                entry_count = saveData.Count,
                entries = saveData
            }, "Game1.CustomData entries prefixed smapi/mod-data/", tick, "smapi_save_data"),
            ["private_mod_state"] = Field(new
            {
                public_mod_data_entry_count = publicData.Count,
                public_mod_data = publicData,
                save_data_entry_count = saveData.Count,
                save_data = saveData,
                runtime_private_fields_exposed = false,
                runtime_private_fields_reason = "CLR private fields inside arbitrary mods are not a stable game data surface; transparent read covers SMAPI save data and public IHaveModData dictionaries."
            }, "Game1.CustomData; Game1.player/currentLocation/getFarm().modData", tick, "smapi_mod_state")
        }, Array.Empty<string>(), "complete");
    }

    private static object[] ReadSaveDataEntries()
    {
        if (!Context.IsWorldReady || Game1.CustomData is null)
        {
            return Array.Empty<object>();
        }

        const string prefix = "smapi/mod-data/";
        return Game1.CustomData
            .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(pair =>
            {
                var remainder = pair.Key[prefix.Length..];
                var slash = remainder.IndexOf('/');
                var modId = slash >= 0 ? remainder[..slash] : remainder;
                var key = slash >= 0 ? remainder[(slash + 1)..] : string.Empty;
                return new
                {
                    raw_key = pair.Key,
                    mod_id = modId,
                    data_key = key,
                    raw_json = pair.Value
                };
            })
            .OrderBy(entry => entry.mod_id, StringComparer.Ordinal)
            .ThenBy(entry => entry.data_key, StringComparer.Ordinal)
            .Cast<object>()
            .ToArray();
    }

    private static object[] ReadPublicModData()
    {
        if (!Context.IsWorldReady)
        {
            return Array.Empty<object>();
        }

        var entries = new List<object>();
        AddModData(entries, "player", "Game1.player", Game1.player);
        AddModData(entries, "current_location", "Game1.currentLocation", Game1.currentLocation);
        AddModData(entries, "farm", "Game1.getFarm()", Game1.getFarm());
        return entries.ToArray();
    }

    private static void AddModData(List<object> entries, string ownerKind, string ownerPath, IHaveModData? owner)
    {
        if (owner is null || owner.modData.Length == 0)
        {
            return;
        }

        entries.Add(new
        {
            owner_kind = ownerKind,
            owner_path = ownerPath,
            entry_count = owner.modData.Length,
            entries = ToSortedDictionary(owner.modData)
        });
    }

    private static Dictionary<string, string> ToSortedDictionary(ModDataDictionary source)
    {
        return source.Pairs
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}

public sealed record ModdedStateModMetadata(
    string ModId,
    string Name,
    string Version,
    string Author,
    bool IsContentPack);
