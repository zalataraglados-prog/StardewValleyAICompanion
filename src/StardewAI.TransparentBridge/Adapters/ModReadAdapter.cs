using StardewModdingAPI;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class ModReadAdapter : ReadAdapterBase
{
    private readonly IModRegistry modRegistry;

    public ModReadAdapter(IModRegistry modRegistry)
    {
        this.modRegistry = modRegistry;
    }

    public override string Domain => "mods";
    public override int Priority => 70;

    public override StateAdapterResult Collect(long tick)
    {
        var mods = modRegistry.GetAll()
            .Select(mod => new
            {
                mod_id = mod.Manifest.UniqueID,
                name = mod.Manifest.Name,
                version = mod.Manifest.Version.ToString(),
                author = mod.Manifest.Author
            })
            .ToArray();

        return Section("mods", new Dictionary<string, object>
        {
            ["installed_count"] = Field(mods.Length, "IModRegistry.GetAll()", tick, "smapi_mod_registry"),
            ["installed_mods"] = Field(mods, "IModRegistry.GetAll()", tick, "smapi_mod_registry"),
            ["mod_specific_adapters"] = Unavailable("per_mod_adapters_not_implemented", "SMAPI mod integration contracts", tick)
        }, new[]
        {
            "mods.per_mod_state",
            "mods.compatibility_contracts"
        });
    }
}
