using StardewValley;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

internal static class NativeToolPowerProjection
{
    public static int AdditionalPower(Tool tool)
    {
        return tool switch
        {
            Axe axe => axe.additionalPower.Value,
            Pickaxe pickaxe => pickaxe.additionalPower.Value,
            _ => 0
        };
    }

    public static int EffectiveUpgradeLevel(Tool tool)
    {
        return tool.UpgradeLevel + AdditionalPower(tool);
    }

    public static float ResourceClumpDamage(Tool tool)
    {
        return Math.Max(
            1f,
            (EffectiveUpgradeLevel(tool) + 1) * 0.75f);
    }
}
