using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadRainTotemCraftingContext(Farmer? player)
    {
        var projection = ReadExactSingleOutputCraftingProjection(
            player,
            "(O)681",
            "exact_single_rain_totem_output",
            "complete_learned_rain_totem_recipe_projection");
        return new
        {
            schema_version = "rain_totem_crafting.v1",
            projection_status = projection.Status,
            learned_rain_totem_recipe_count = projection.Rows.Length,
            recipe_learned = projection.Rows.Length > 0,
            unclassified_known_recipe_count = projection.Failures.Length,
            unclassified_known_recipes = projection.Failures,
            rows = projection.Rows
        };
    }
}
