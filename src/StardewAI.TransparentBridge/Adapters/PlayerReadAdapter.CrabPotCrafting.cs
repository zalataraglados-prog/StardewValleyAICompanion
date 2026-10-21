using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadCrabPotCraftingContext(Farmer? player)
    {
        var projection = ReadExactSingleOutputCraftingProjection(
            player,
            CrabPotQualifiedItemId,
            "exact_single_crab_pot_output",
            "complete_learned_crab_pot_recipe_projection");
        return new
        {
            schema_version = "crab_pot_crafting.v1",
            projection_status = projection.Status,
            learned_crab_pot_recipe_count = projection.Rows.Length,
            recipe_learned = projection.Rows.Length > 0,
            unclassified_known_recipe_count = projection.Failures.Length,
            unclassified_known_recipes = projection.Failures,
            rows = projection.Rows
        };
    }
}
