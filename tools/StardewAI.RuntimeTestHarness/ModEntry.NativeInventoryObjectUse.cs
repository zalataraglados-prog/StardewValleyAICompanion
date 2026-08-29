using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static NativeInventoryObjectUseResult UseInventoryObjectNative(
        StardewValley.Object item,
        int slotIndex)
    {
        var stackBefore = item.Stack;
        Game1.player.CurrentToolIndex = slotIndex;
        var used = item.performUseAction(Game1.player.currentLocation);
        if (used)
        {
            Game1.player.reduceActiveItemByOne();
        }
        var remaining = slotIndex >= 0 && slotIndex < Game1.player.Items.Count
            ? Game1.player.Items[slotIndex]
            : null;
        return new NativeInventoryObjectUseResult(
            used,
            stackBefore,
            remaining is StardewValley.Object remainingObject &&
                string.Equals(remainingObject.QualifiedItemId, item.QualifiedItemId, StringComparison.Ordinal)
                    ? remainingObject.Stack
                    : 0);
    }

    private sealed record NativeInventoryObjectUseResult(
        bool Used,
        int StackBefore,
        int StackAfter);
}
