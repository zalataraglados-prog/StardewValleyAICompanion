using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

internal static class ResourceClumpToolTracePatch
{
    private static readonly object Sync = new();
    private static ResourceClump? target;
    private static readonly List<string> entries = new();

    public static void Begin(ResourceClump clump)
    {
        lock (Sync)
        {
            target = clump;
            entries.Clear();
        }
    }

    public static string[] Complete(ResourceClump clump)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(target, clump))
            {
                return Array.Empty<string>();
            }
            target = null;
            return entries.ToArray();
        }
    }

    public static void Prefix(
        ResourceClump __instance,
        Tool t,
        out float __state)
    {
        __state = __instance.health.Value;
        if (!ReferenceEquals(target, __instance))
        {
            return;
        }

        lock (Sync)
        {
            entries.Add(
                "before:health=" + __state.ToString("0.###") +
                ":swing_ticker=" + t.swingTicker +
                ":base_upgrade=" + t.UpgradeLevel +
                ":additional_power=" + AdditionalPower(t));
        }
    }

    public static void Postfix(
        ResourceClump __instance,
        Tool t,
        bool __result,
        float __state)
    {
        if (!ReferenceEquals(target, __instance))
        {
            return;
        }

        lock (Sync)
        {
            entries.Add(
                "after:health=" +
                __instance.health.Value.ToString("0.###") +
                ":destroyed=" + __result.ToString().ToLowerInvariant() +
                ":swing_ticker=" + t.swingTicker +
                ":health_delta=" +
                (__instance.health.Value - __state).ToString("0.###"));
        }
    }

    private static int AdditionalPower(Tool tool)
    {
        return tool switch
        {
            Axe axe => axe.additionalPower.Value,
            Pickaxe pickaxe => pickaxe.additionalPower.Value,
            _ => 0
        };
    }
}
