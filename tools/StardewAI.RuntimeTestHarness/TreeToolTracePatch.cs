using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

internal static class TreeToolTracePatch
{
    private static readonly object Sync = new();
    private static GameLocation? targetLocation;
    private static Tree? target;
    private static readonly List<string> entries = new();

    public static void Begin(GameLocation location, Tree tree)
    {
        lock (Sync)
        {
            if (targetLocation is not null)
            {
                targetLocation.terrainFeatures.OnValueRemoved -= OnTerrainFeatureRemoved;
            }
            targetLocation = location;
            target = tree;
            entries.Clear();
            entries.Add("begin");
            targetLocation.terrainFeatures.OnValueRemoved += OnTerrainFeatureRemoved;
        }
    }

    public static string[] Complete(Tree tree)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(target, tree))
            {
                return Array.Empty<string>();
            }

            if (targetLocation is not null)
            {
                targetLocation.terrainFeatures.OnValueRemoved -= OnTerrainFeatureRemoved;
            }
            targetLocation = null;
            target = null;
            return entries.ToArray();
        }
    }

    private static void OnTerrainFeatureRemoved(Vector2 tile, TerrainFeature feature)
    {
        if (!ReferenceEquals(target, feature))
        {
            return;
        }

        lock (Sync)
        {
            entries.Add(
                "removed:tile=" + tile.X + "," + tile.Y +
                ":stack=" + string.Join(
                    ">",
                    Environment.StackTrace
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Skip(1)
                        .Take(8)
                        .Select(line => line.Trim())));
        }
    }

    public static void Prefix(
        Tree __instance,
        Tool t,
        int explosion,
        Vector2 tileLocation,
        out TreeToolTraceState __state)
    {
        __state = new TreeToolTraceState(
            __instance.hasMoss.Value,
            __instance.hasSeed.Value,
            __instance.growthStage.Value,
            __instance.health.Value,
            Game1.player.experiencePoints[Farmer.foragingSkill],
            Game1.stats.Get("mossHarvested"));
        if (!ReferenceEquals(target, __instance))
        {
            return;
        }

        lock (Sync)
        {
            entries.Add(
                "before:tool=" + t.QualifiedItemId +
                ":type=" + t.GetType().Name +
                ":scythe=" + (t is MeleeWeapon weapon && weapon.isScythe()).ToString().ToLowerInvariant() +
                ":explosion=" + explosion +
                ":tile=" + tileLocation.X + "," + tileLocation.Y +
                ":moss=" + __state.HasMoss.ToString().ToLowerInvariant() +
                ":seed=" + __state.HasSeed.ToString().ToLowerInvariant() +
                ":growth=" + __state.GrowthStage +
                ":health=" + __state.Health.ToString("0.###"));
        }
    }

    public static void Postfix(
        Tree __instance,
        bool __result,
        TreeToolTraceState __state)
    {
        if (!ReferenceEquals(target, __instance))
        {
            return;
        }

        lock (Sync)
        {
            entries.Add(
                "after:remove=" + __result.ToString().ToLowerInvariant() +
                ":moss=" + __instance.hasMoss.Value.ToString().ToLowerInvariant() +
                ":seed=" + __instance.hasSeed.Value.ToString().ToLowerInvariant() +
                ":growth=" + __instance.growthStage.Value +
                ":health=" + __instance.health.Value.ToString("0.###") +
                ":foraging_delta=" +
                (Game1.player.experiencePoints[Farmer.foragingSkill] - __state.ForagingExperience) +
                ":moss_stat_delta=" +
                ((long)Game1.stats.Get("mossHarvested") - __state.MossHarvested));
        }
    }

    internal readonly record struct TreeToolTraceState(
        bool HasMoss,
        bool HasSeed,
        int GrowthStage,
        float Health,
        int ForagingExperience,
        long MossHarvested);

}
