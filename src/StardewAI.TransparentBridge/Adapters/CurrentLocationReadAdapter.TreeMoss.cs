using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private const string TreeMossHarvestNativeContract =
        "MeleeWeapon(scythe) native tool lifecycle -> Tree.performToolAction -> Tree.CreateMossItem -> Game1.createMultipleItemDebris(Item.Stack=1 side effect) -> Tree.shake -> growthStage=11, seedless exact base Tree, no direct tree, RNG, debris, inventory, stat, or skill mutation";

    private static TreeMossHarvestProjection ProjectTreeMossHarvest(Tree tree)
    {
        var scytheEntry = Game1.player.Items
            .Select((item, index) => new { Item = item, Index = index })
            .FirstOrDefault(entry => entry.Item is MeleeWeapon weapon && weapon.isScythe());
        var mossStack = tree.hasMoss.Value ? ProjectMossStack() : 0;
        var mossHarvestedBefore = (long)Game1.stats.Get("mossHarvested");
        var foragingExperienceBefore = Game1.player.experiencePoints[Farmer.foragingSkill];
        var outputItems = mossStack > 0
            ? new[] { ClearanceOutputItemProjection.FromStandard("(O)Moss", mossStack) }
            : Array.Empty<ClearanceOutputItemProjection>();
        var status = tree.GetType() != typeof(Tree)
            ? "blocked_custom_tree_runtime_type"
            : tree.growthStage.Value < Tree.treeStage
                ? "blocked_tree_not_mature"
                : tree.stump.Value
                    ? "blocked_tree_is_stump"
                    : !tree.hasMoss.Value
                        ? "blocked_tree_has_no_moss"
                        : tree.hasSeed.Value
                            ? "blocked_tree_seed_must_be_shaken_first"
                            : tree.maxShake != 0f
                                ? "blocked_tree_shake_in_progress"
                                : scytheEntry is null
                                    ? "blocked_scythe_missing"
                                    : outputItems.Length != 1
                                        ? "blocked_moss_output_projection_missing"
                                        : "ready";

        return new TreeMossHarvestProjection(
            status,
            "tree_moss_removed",
            scytheEntry?.Index,
            "scythe",
            mossStack,
            outputItems,
            mossHarvestedBefore,
            checked(mossHarvestedBefore + 1),
            foragingExperienceBefore,
            checked(foragingExperienceBefore + mossStack),
            tree.growthStage.Value,
            mossStack > 0 ? 11 : tree.growthStage.Value,
            tree.health.Value,
            tree.hasMoss.Value,
            tree.hasSeed.Value,
            tree.wasShakenToday.Value,
            "exact_seedless_native_scythe_moss_branch",
            TreeMossHarvestNativeContract);
    }

    private sealed record TreeMossHarvestProjection(
        string Status,
        string CompletionMode,
        int? ToolSlotIndex,
        string RequiredToolKind,
        int MossQuantity,
        ClearanceOutputItemProjection[] OutputItems,
        long MossHarvestedBefore,
        long MossHarvestedAfter,
        int ForagingExperienceBefore,
        int ForagingExperienceAfter,
        int GrowthStageBefore,
        int GrowthStageAfter,
        float Health,
        bool HasMossBefore,
        bool HasSeedBefore,
        bool WasShakenTodayBefore,
        string ProjectionStatus,
        string NativeContract);
}
