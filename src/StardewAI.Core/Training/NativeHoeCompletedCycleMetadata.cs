using System.IO;

namespace StardewAI.Core.Training;

/// <summary>
/// Pure metadata arithmetic for one completed normal native Hoe power-zero
/// begin/release animation with stable facing and no interruption or other use.
/// Callers must independently verify the cycle, tool, actor and supplied inputs.
/// </summary>
public static class NativeHoeCompletedCycleMetadata
{
    public static int ExpectedSpriteIndex(int initialParentTileIndex, int facingDirection)
    {
        // Pinned Tool.beginUsing 0x060015bb, Tool.endUsing 0x060015ba,
        // FarmerSprite.animateOnce 0x060009e4 and Tool.Update 0x060015b4.
        // On south completion, doneWithAnimation -> Halt -> StopAnimation resets
        // the animation index before the final Tool.Update; east/west retain +2.
        var offset = facingDirection switch
        {
            0 => 4,
            1 => 2,
            2 => 0,
            3 => 2,
            _ => throw new InvalidDataException("Completed Hoe cycle requires facing direction 0 through 3.")
        };
        return unchecked(initialParentTileIndex + offset);
    }

    // Pinned Tool.endUsing 0x060015ba increments once, with native int wrap.
    public static int ExpectedSwingTicker(int before) => unchecked(before + 1);

    /// <summary>
    /// equippedDuringCycle must represent an independently verified native Equip
    /// of this tool as the new item, not merely inventory slot selection.
    /// </summary>
    public static bool ExpectedInventoryHistory(bool before, bool equippedDuringCycle)
        // Pinned Farmer.Update 0x06000953 calls Equip 0x06000941, which sets history true.
        => before || equippedDuringCycle;
}
