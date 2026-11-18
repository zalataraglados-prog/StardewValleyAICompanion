using System.IO;

namespace StardewAI.Core.Training;

public enum NativeToolSpriteKind { Other, FishingRod, WateringCan }

// One actual Tool.Update call (1.6.15 token0x060015b4), not an animation-cycle forecast.
public static class NativeToolUpdateMetadata
{
    public static int ApplySpriteUpdate(int initial, int current, int direction, int farmerMotionFrame,
        int actorFacing, NativeToolSpriteKind kind, string qualifiedToolId)
    {
        var offset = kind switch
        {
            NativeToolSpriteKind.WateringCan => direction switch { 0 => 4, 1 or 3 => 2, _ => 0 },
            NativeToolSpriteKind.FishingRod => direction == 0 ? 3 : 0,
            NativeToolSpriteKind.Other => direction switch { 0 => 3, 1 or 3 => 2, _ => 0 },
            _ => throw new InvalidDataException("Native tool sprite category is unavailable.")
        };
        if (qualifiedToolId != "(T)WateringCan")
        {
            if (farmerMotionFrame < 1) current = initial;
            else if (actorFacing == 0 || actorFacing == 2 && farmerMotionFrame >= 2) current = unchecked(initial + 1);
        }
        else if (farmerMotionFrame < 5 || direction == 0) current = initial;
        else current = unchecked(initial + 1);
        return unchecked(current + offset);
    }
}
