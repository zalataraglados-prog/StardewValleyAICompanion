using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool CanPlaceInventoryObjectNative(
        GameLocation location,
        StardewValley.Object inventoryItem,
        int slotIndex,
        Point target)
    {
        var previousSlot = Game1.player.CurrentToolIndex;
        try
        {
            Game1.player.CurrentToolIndex = slotIndex;
            return ReferenceEquals(Game1.player.ActiveObject, inventoryItem) &&
                Utility.playerCanPlaceItemHere(
                    location,
                    inventoryItem,
                    target.X * Game1.tileSize,
                    target.Y * Game1.tileSize,
                    Game1.player);
        }
        finally
        {
            if (previousSlot >= 0 && previousSlot < Game1.player.Items.Count)
            {
                Game1.player.CurrentToolIndex = previousSlot;
            }
        }
    }

    private static NativeObjectPlacementAttempt PlaceInventoryObjectNative(
        GameLocation location,
        StardewValley.Object inventoryItem,
        int slotIndex,
        Point target)
    {
        var previousSlot = Game1.player.CurrentToolIndex;
        var stackBefore = inventoryItem.Stack;
        bool placed;
        try
        {
            Game1.player.CurrentToolIndex = slotIndex;
            if (!ReferenceEquals(Game1.player.ActiveObject, inventoryItem))
            {
                return new NativeObjectPlacementAttempt(false, null, null, stackBefore, stackBefore);
            }
            placed = Utility.tryToPlaceItem(
                location,
                inventoryItem,
                target.X * Game1.tileSize,
                target.Y * Game1.tileSize);
        }
        finally
        {
            if (previousSlot >= 0 && previousSlot < Game1.player.Items.Count)
            {
                Game1.player.CurrentToolIndex = previousSlot;
            }
        }

        location.objects.TryGetValue(new Vector2(target.X, target.Y), out var placedObject);
        location.terrainFeatures.TryGetValue(new Vector2(target.X, target.Y), out var placedTerrainFeature);
        var stackAfter = Game1.player.Items.ElementAtOrDefault(slotIndex)?.Stack ?? 0;
        return new NativeObjectPlacementAttempt(placed, placedObject, placedTerrainFeature, stackBefore, stackAfter);
    }

    private sealed record NativeObjectPlacementAttempt(
        bool Placed,
        StardewValley.Object? PlacedObject,
        TerrainFeature? PlacedTerrainFeature,
        int StackBefore,
        int StackAfter);
}
