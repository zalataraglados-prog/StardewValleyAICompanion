using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object ReadTerrainFeatureDetails(Vector2 tile, TerrainFeature feature)
    {
        if (feature is not Tree tree)
        {
            return new
            {
                tile_x = (int)tile.X,
                tile_y = (int)tile.Y,
                type = feature.GetType().FullName
            };
        }

        var bestAxe = Game1.player.Items.OfType<Axe>()
            .OrderByDescending(axe => axe.UpgradeLevel)
            .FirstOrDefault();
        var mossExperience = tree.hasMoss.Value ? ProjectMossStack() : 0;
        var foragingExperience = tree.growthStage.Value >= 5
            ? (tree.stump.Value ? 2 : 16) + mossExperience
            : 0;
        var expectedHits = bestAxe is null || tree.tapped.Value
            ? (int?)null
            : ExpectedTreeAxeHits(tree, bestAxe.UpgradeLevel);

        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            type = feature.GetType().FullName,
            tree_type = tree.treeType.Value,
            growth_stage = tree.growthStage.Value,
            health = tree.health.Value,
            stump = tree.stump.Value,
            tapped = tree.tapped.Value,
            has_moss = tree.hasMoss.Value,
            moss_stack_on_next_axe_hit = mossExperience,
            best_axe_upgrade_level = bestAxe?.UpgradeLevel,
            expected_axe_hits_to_clear = expectedHits,
            tree_clear_executor_status = tree.GetType() != typeof(Tree)
                ? "blocked_custom_tree_runtime_type"
                : tree.tapped.Value
                    ? "blocked_tapped_tree"
                    : bestAxe is null
                        ? "blocked_axe_missing"
                        : "ready",
            harvest_experience_skill_id = "foraging",
            harvest_experience_skill_index = Farmer.foragingSkill,
            harvest_experience_on_success_min = foragingExperience,
            harvest_experience_on_success_max = foragingExperience,
            harvest_experience_condition = tree.growthStage.Value >= 5
                ? tree.stump.Value
                    ? "native_player_axe_destroys_mature_tree_stump_including_current_moss"
                    : "native_player_axe_fells_mature_tree_and_destroys_resulting_stump_including_current_moss"
                : "native_young_tree_clear_has_no_skill_experience",
            harvest_experience_projection_status = tree.GetType() == typeof(Tree)
                ? "exact_from_decompiled_native_tree_branches"
                : "unavailable_custom_tree_runtime_type",
            source = "Tree live net fields; Tree.performToolAction/performTreeFall/CreateMossItem"
        };
    }

    private static int ExpectedTreeAxeHits(Tree tree, int axeUpgradeLevel)
    {
        if (tree.growthStage.Value < 3)
        {
            return 1;
        }

        var matureDamage = axeUpgradeLevel switch
        {
            0 => 1f,
            1 => 1.25f,
            2 => 1.67f,
            3 => 2.5f,
            4 => 5f,
            _ => axeUpgradeLevel + 1f
        };
        var youngTreeDamage = axeUpgradeLevel switch
        {
            0 => 2f,
            1 => 2.5f,
            2 => 3.34f,
            3 => 5f,
            4 => 10f,
            _ => 10f + (axeUpgradeLevel - 4)
        };
        var damage = tree.growthStage.Value >= 5 ? matureDamage : youngTreeDamage;
        var currentPhaseHits = Math.Max(1, (int)Math.Ceiling(Math.Max(0f, tree.health.Value) / damage));
        if (tree.growthStage.Value < 5 || tree.stump.Value)
        {
            return currentPhaseHits;
        }

        return currentPhaseHits + Math.Max(1, (int)Math.Ceiling(5f / matureDamage));
    }

    private static int ProjectMossStack()
    {
        var random = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.Get("mossHarvested") * 50);
        return random.Next(1, 3);
    }
}
