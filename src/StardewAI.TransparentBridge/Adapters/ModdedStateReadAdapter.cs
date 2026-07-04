using StardewModdingAPI;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class ModdedStateReadAdapter : ReadAdapterBase
{
    private const string RegistrySource = "IModRegistry.GetAll()";
    private const string RegistryAdapter = "smapi_mod_registry";
    private const string UnavailableReason = "arbitrary_mod_private_state_unavailable_without_mod_specific_read_only_api";

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

        return BuildResult(mods, tick);
    }

    public static StateAdapterResult BuildResult(IReadOnlyList<ModdedStateModMetadata> mods, long tick)
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

        return Section("modded_state", new Dictionary<string, object>
        {
            ["installed_count"] = Field(installed.Length, RegistrySource, tick, RegistryAdapter),
            ["installed"] = Field(installed, RegistrySource, tick, RegistryAdapter),
            ["content_pack_count"] = Field(contentPacks.Length, RegistrySource, tick, RegistryAdapter),
            ["content_packs"] = Field(contentPacks, RegistrySource, tick, RegistryAdapter),
            ["private_mod_state"] = Unavailable(UnavailableReason, "arbitrary mod private save/state data", tick, "transparent_unavailable")
        }, new[]
        {
            "modded_state.private_mod_state",
            "modded_state.arbitrary_mod_private_save_data"
        });
    }
}

public sealed record ModdedStateModMetadata(
    string ModId,
    string Name,
    string Version,
    string Author,
    bool IsContentPack);
