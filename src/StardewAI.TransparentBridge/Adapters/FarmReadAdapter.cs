using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewAI.TransparentBridge.State;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter : ReadAdapterBase
{
    private const int MaxMachineInputProbeMachinesPerRefresh = 16;
    private const int MaxMachineInputProbeSlotsPerMachine = 16;
    private const long MachineProbeCacheMaxAgeTicks = 240;

    private static readonly object MachineProbeCacheLock = new();
    private static object[] cachedMachineProbeRows = Array.Empty<object>();
    private static long cachedMachineProbeTick = -1;

    private static readonly string[] FarmFields =
    {
        "farm_type",
        "farm_identity",
        "crop_catalog",
        "shipping_bins",
        "buildings",
        "crops",
        "terrain_features",
        "objects",
        "machines",
        "chests",
        "material_inventory_graph",
        "animals",
        "pets",
        "pet_bowls",
        "resource_clumps",
        "debris",
        "warps",
        "grandpa_score"
    };

    public override string Domain => "farm";
    public override int Priority => 30;

    public override StateAdapterResult Collect(long tick)
    {
        var farm = Context.IsWorldReady ? Game1.getFarm() : null;
        if (farm is null)
        {
            return Section(
                "farm",
                FarmFields.ToDictionary(
                    field => field,
                    field => (object)Unavailable("world_not_ready_or_farm_unavailable", "Context.IsWorldReady; Game1.getFarm()", tick, "vanilla_1_6_farm")),
                FarmFields.Select(field => "farm." + field).ToArray(),
                "unavailable");
        }

        if (string.Equals(SnapshotProfileContext.Current, "machine", StringComparison.OrdinalIgnoreCase))
        {
            return Section("farm", new Dictionary<string, object>
            {
                ["machines"] = Field(ReadCachedMachineProbeRowsOrFallback(farm), "FarmReadAdapter.RefreshMachineProbeCache on SMAPI UpdateTicked; Utility.ForEachLocation(includeInteriors:true, includeGenerated:false) plus native farm/home ownership topology; GameLocation.objects[*] machine-shaped objects", tick, "transparent_bridge_main_thread_cache")
            });
        }

        return Section("farm", new Dictionary<string, object>
        {
            ["farm_type"] = Field(Game1.whichFarm, "Game1.whichFarm", tick, "vanilla_1_6_farm"),
            ["farm_identity"] = Field(new
            {
                location_name = farm.Name,
                location_id = farm.NameOrUniqueName,
                is_farm = farm.IsFarm,
                greenhouse_unlocked = farm.greenhouseUnlocked.Value
            }, "Game1.getFarm().Name/NameOrUniqueName/IsFarm/greenhouseUnlocked", tick, "vanilla_1_6_farm"),
            ["crop_catalog"] = Field(ReadCropCatalog(), "Game1.cropData (Data\\Crops)", tick, "vanilla_1_6_crop_data"),
            ["shipping_bins"] = Field(ReadShippingBins(farm), "Game1.getFarm().buildings as ShippingBin", tick, "vanilla_1_6_farm"),
            ["grandpa_score"] = Field(farm.grandpaScore.Value, "Game1.getFarm().grandpaScore.Value", tick, "vanilla_1_6_farm"),
            ["buildings"] = Field(ReadBuildings(farm), "Game1.getFarm().buildings", tick, "vanilla_1_6_farm"),
            ["crops"] = Field(ReadCrops(farm), "Game1.getFarm().terrainFeatures[*] as HoeDirt.crop", tick, "vanilla_1_6_farm"),
            ["terrain_features"] = Field(ReadTerrainFeatures(farm), "Game1.getFarm().terrainFeatures", tick, "vanilla_1_6_farm"),
            ["objects"] = Field(ReadObjects(farm), "Game1.getFarm().objects", tick, "vanilla_1_6_farm"),
            ["machines"] = Field(ReadCachedMachineProbeRowsOrFallback(farm), "FarmReadAdapter.RefreshMachineProbeCache on SMAPI UpdateTicked; Utility.ForEachLocation(includeInteriors:true, includeGenerated:false) plus native farm/home ownership topology; GameLocation.objects[*] machine-shaped objects", tick, "transparent_bridge_main_thread_cache"),
            ["chests"] = Field(ReadChests(farm), "Game1.getFarm().objects[*] as Chest", tick, "vanilla_1_6_farm"),
            ["material_inventory_graph"] = Field(ReadMaterialInventoryGraph(farm, Game1.player), "Game1.player.Items; Utility.ForEachLocation(includeInteriors:true, includeGenerated:false); Chest.GetItemsForPlayer/GetActualCapacity/GetMutex; GameLocation.GetFridge/GetFridgePosition; Workbench.checkForAction adjacency; Object heldObject machine/auto-grabber buffers", tick, "vanilla_1_6_material_inventory_graph"),
            ["animals"] = Field(ReadAnimals(farm), "Game1.locations[*].animals plus Game1.getFarm().buildings[*].GetIndoors().animals", tick, "vanilla_1_6_farm_and_animal_houses"),
            ["pets"] = Field(ReadPets(), "Utility.getAllPets(); Pet fields; Data/Pets; Pet.checkAction and Pet.dayUpdate projections", tick, "vanilla_1_6_pet"),
            ["pet_bowls"] = Field(ReadPetBowls(farm), "Game1.locations[*].buildings as PetBowl; PetBowl.performToolAction and Pet.dayUpdate projections", tick, "vanilla_1_6_pet_bowl"),
            ["resource_clumps"] = Field(ReadResourceClumps(farm), "Game1.getFarm().resourceClumps", tick, "vanilla_1_6_farm"),
            ["debris"] = Field(ReadDebris(farm), "Game1.getFarm().debris", tick, "vanilla_1_6_farm"),
            ["warps"] = Field(ReadWarps(farm), "Game1.getFarm().warps", tick, "vanilla_1_6_farm")
        });
    }

    public static void RefreshMachineProbeCache()
    {
        if (!Context.IsWorldReady || Game1.getFarm() is not { } farm)
        {
            SetMachineProbeCache(Array.Empty<object>(), unchecked((long)Game1.ticks));
            return;
        }

        var tick = unchecked((long)Game1.ticks);
        SetMachineProbeCache(ReadMachines(farm, includeLoadableInputs: true, minimalMachineProfile: true, machineProbeCacheTick: tick), tick);
    }

}
