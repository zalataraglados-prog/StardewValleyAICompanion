using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class FarmObjectsReadAdapter : ReadAdapterBase
{
    public override string Domain => "farm_objects";
    public override int Priority => 30;

    public override StateAdapterResult Collect(long tick)
    {
        Farm? farm = Context.IsWorldReady ? Game1.getFarm() : null;
        var sampleObjects = farm?.Objects.Pairs
            .Take(50)
            .Select(pair => new
            {
                tile = new { x = pair.Key.X, y = pair.Key.Y },
                id = pair.Value.QualifiedItemId,
                name = pair.Value.DisplayName,
                stack = pair.Value.Stack,
                big_craftable = pair.Value.bigCraftable.Value,
                ready_for_harvest = pair.Value.readyForHarvest.Value
            })
            .ToArray();

        var cropTiles = farm?.terrainFeatures.Pairs
            .Where(pair => pair.Value is StardewValley.TerrainFeatures.HoeDirt dirt && dirt.crop is not null)
            .Take(100)
            .Select(pair =>
            {
                var dirt = (StardewValley.TerrainFeatures.HoeDirt)pair.Value;
                return new
                {
                    tile = new { x = pair.Key.X, y = pair.Key.Y },
                    crop_id = dirt.crop?.indexOfHarvest.Value,
                    phase = dirt.crop?.currentPhase.Value,
                    watered = dirt.state.Value != 0,
                    fertilizer = dirt.fertilizer.Value
                };
            })
            .ToArray();

        return Section("farm", new Dictionary<string, object>
        {
            ["farm_available"] = Field(farm is not null, "Game1.getFarm()", tick),
            ["object_count"] = Field(farm?.Objects.Pairs.Count(), "Farm.Objects", tick),
            ["terrain_feature_count"] = Field(farm?.terrainFeatures.Pairs.Count(), "Farm.terrainFeatures", tick),
            ["building_count"] = Field(farm?.buildings.Count, "Farm.buildings", tick),
            ["sample_objects"] = Field(sampleObjects, "Farm.Objects.Pairs", tick),
            ["crop_tiles_sample"] = Field(cropTiles, "Farm.terrainFeatures.Pairs[HoeDirt.crop]", tick)
        }, new[]
        {
            "farm.full_machine_internals",
            "farm.full_crop_rules",
            "farm.all_map_locations"
        });
    }
}
