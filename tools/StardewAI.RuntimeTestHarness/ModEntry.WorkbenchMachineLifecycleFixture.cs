using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static WorkbenchMachineLifecycleFixture?
        SetupWorkbenchMachineLifecycleFixture(
            TrainingExecutionRequest request,
            Farm farm,
            Point machineTarget,
            CraftingRecipe recipe,
            out string reason)
    {
        reason = string.Empty;
        if (!request.InteractionTileX.HasValue ||
            !request.InteractionTileY.HasValue)
        {
            reason = "workbench_tile_required";
            return null;
        }

        var workbenchTile = new Vector2(
            request.InteractionTileX.Value,
            request.InteractionTileY.Value);
        var chestTile = workbenchTile + new Vector2(-1f, 0f);
        var standTile = workbenchTile + new Vector2(1f, 0f);
        if (workbenchTile.ToPoint() == machineTarget ||
            chestTile.ToPoint() == machineTarget ||
            standTile.ToPoint() == machineTarget)
        {
            reason = "workbench_fixture_overlaps_machine_target";
            return null;
        }

        foreach (var offset in NativeWorkbenchChestOffsets)
        {
            var tile = workbenchTile + offset;
            farm.objects.Remove(tile);
            farm.terrainFeatures.Remove(tile);
        }
        farm.objects.Remove(workbenchTile);
        farm.terrainFeatures.Remove(workbenchTile);

        foreach (var ingredient in recipe.recipeList)
        {
            for (var slot = 0;
                 slot < Game1.player.Items.Count;
                 slot++)
            {
                if (CraftingRecipe.ItemMatchesForCrafting(
                        Game1.player.Items[slot],
                        ingredient.Key))
                {
                    Game1.player.Items[slot] = null;
                }
            }
        }

        var chest = CreateOwnedChest(chestTile, "130");
        chest.Items.Clear();
        farm.objects[chestTile] = chest;
        farm.objects[workbenchTile] =
            new Workbench(workbenchTile);

        var locationId = farm.NameOrUniqueName;
        var accessPointId =
            "access:workbench:" +
            EscapeMaterialNodePart(locationId) +
            ":" + (int)workbenchTile.X +
            "," + (int)workbenchTile.Y;
        var chestNodeId =
            WorkbenchChestNodeId(locationId, chestTile);
        return new WorkbenchMachineLifecycleFixture(
            chest,
            accessPointId,
            chestNodeId);
    }

    private static bool WorkbenchFixtureHasRecipeIngredients(
        CraftingRecipe recipe,
        Chest chest)
    {
        foreach (var ingredient in recipe.recipeList)
        {
            var available = chest.Items
                .Where(item =>
                    CraftingRecipe.ItemMatchesForCrafting(
                        item,
                        ingredient.Key))
                .Sum(item => item?.Stack ?? 0);
            if (available < ingredient.Value)
            {
                return false;
            }
        }
        return true;
    }

    private sealed record WorkbenchMachineLifecycleFixture(
        Chest Chest,
        string AccessPointId,
        string ChestNodeId);
}
