using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.GameData.WildTrees;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private const string WildTreeChopNativeContract =
        "Axe native tool lifecycle -> Tree.performToolAction -> Tree.performTreeFall(trunk,stump) -> Tree.tickUpdate falling settlement; locked Data/WildTrees DropWoodOnChop, DropHardwoodOnLumberChop, ChopItems, SeedItemId, and SeedOnChopChance; complete stochastic output domain; no direct tree, RNG, debris, inventory, stats, or skill mutation";

    private static WildTreeChopProjection ProjectWildTreeChop(
        Vector2 tile,
        Tree tree,
        int? axeSlotIndex,
        Axe? axe,
        int? expectedHits)
    {
        var data = tree.GetData();
        var dataStatus = ValidateBaseWildTreeChopData(tree.treeType.Value, data);
        var protectedStatus = WildTreeChopProtectionStatus(tile, tree);
        var guaranteedOutputs = data is null
            ? Array.Empty<WildTreeChopOutputMinimum>()
            : ProjectWildTreeChopGuaranteedOutputs(tile, tree, data);
        var optionalOutputDomain = data is null
            ? Array.Empty<object>()
            : ProjectWildTreeChopOptionalOutputDomain(tree, data, axe, expectedHits ?? 1);
        var experienceBefore = Game1.player.experiencePoints[Farmer.foragingSkill];
        var experienceDelta = tree.growthStage.Value >= Tree.treeStage && !tree.stump.Value ? 16 : 0;
        var treesChoppedBefore = (long)Game1.player.stats.Get("TreesChopped");
        var energyPerSwing = axe?.isEfficient.Value == true
            ? 0d
            : Math.Max(0d, 2d - Game1.player.ForagingLevel * 0.1d);
        var energyCost = expectedHits.HasValue ? energyPerSwing * expectedHits.Value : (double?)null;
        var status = tree.GetType() != typeof(Tree)
            ? "blocked_custom_tree_runtime_type"
            : data is null
                ? "blocked_wild_tree_data_missing"
                : dataStatus != "exact_locked_base_1.6.15_chop"
                    ? "blocked_wild_tree_chop_data_contract_drift"
                    : protectedStatus != "unprotected"
                        ? protectedStatus
                        : tree.growthStage.Value < Tree.treeStage
                            ? "blocked_tree_not_mature"
                            : tree.stump.Value
                                ? "blocked_tree_is_stump"
                                : tree.tapped.Value
                                    ? "blocked_tree_is_tapped"
                                    : tree.hasSeed.Value
                                        ? "blocked_tree_seed_must_be_harvested_first"
                                        : tree.hasMoss.Value
                                            ? "blocked_tree_moss_must_be_harvested_first"
                                            : tree.falling.Value
                                                ? "blocked_tree_fall_in_progress"
                                                : tree.maxShake != 0f
                                                    ? "blocked_tree_shake_in_progress"
                                                    : axe is null || !axeSlotIndex.HasValue
                                                        ? "blocked_axe_missing"
                                                        : axe.GetType() != typeof(Axe)
                                                            ? "blocked_custom_axe_runtime_type"
                                                            : !expectedHits.HasValue
                                                                ? "blocked_tree_hit_budget_unavailable"
                                                                : guaranteedOutputs.Length == 0
                                                                    ? "blocked_tree_chop_guaranteed_output_projection_missing"
                                                                    : "ready";

        return new WildTreeChopProjection(
            status,
            dataStatus,
            protectedStatus,
            axeSlotIndex,
            axe?.QualifiedItemId ?? string.Empty,
            axe?.UpgradeLevel,
            axe?.isEfficient.Value,
            expectedHits,
            energyPerSwing,
            energyCost,
            guaranteedOutputs,
            optionalOutputDomain,
            "complete_stochastic_native_branch_domain_no_rng_consumed",
            tree.GetType() == typeof(Tree) && dataStatus == "exact_locked_base_1.6.15_chop"
                ? "exact_live_tree_and_locked_wild_tree_chop_domain"
                : "unavailable_unverified_runtime_or_data",
            experienceBefore,
            experienceDelta,
            checked(experienceBefore + experienceDelta),
            treesChoppedBefore,
            tree.stump.Value ? 0 : 1,
            checked(treesChoppedBefore + (tree.stump.Value ? 0 : 1)));
    }

    private static string WildTreeChopProtectionStatus(Vector2 tile, Tree tree)
    {
        var location = tree.Location ?? Game1.currentLocation;
        if (location is not Town || tile.X >= 100f || tree.isTemporaryGreenRainTree.Value)
        {
            return "unprotected";
        }

        var pathTile = location.getTileIndexAt((int)tile.X, (int)tile.Y, "Paths");
        return pathTile is 9 or 10 or 11
            ? "blocked_native_town_tree_protection"
            : "unprotected";
    }

    private static string ValidateBaseWildTreeChopData(string treeType, WildTreeData? data)
    {
        if (data is null)
        {
            return "missing";
        }

        var expected = treeType switch
        {
            "1" => new WildTreeChopDataExpectation(true, true, "(O)309", 0.75f, new[] { StumpRow("(O)92") }),
            "2" => new WildTreeChopDataExpectation(true, true, "(O)310", 0.75f, new[] { StumpRow("(O)92") }),
            "3" => new WildTreeChopDataExpectation(true, true, "(O)311", 0.75f, new[] { StumpRow("(O)92") }),
            "6" => new WildTreeChopDataExpectation(true, true, "(O)88", 0.75f, Array.Empty<WildTreeChopRowExpectation>()),
            "7" => new WildTreeChopDataExpectation(false, false, "(O)891", 0f, new[]
            {
                new WildTreeChopRowExpectation("(O)420", 3, 3, false, 1f, 1, -1),
                new WildTreeChopRowExpectation("(O)420", 5, null, false, 1f, 5, -1),
                new WildTreeChopRowExpectation("(H)42", 5, null, false, 0.01f, 1, -1),
                StumpRow("(O)420")
            }),
            "8" => new WildTreeChopDataExpectation(false, true, "(O)292", 0.5625f, new[]
            {
                new WildTreeChopRowExpectation("(O)709", 5, null, false, 1f, 7, 11),
                StumpRow("(O)709")
            }),
            "9" => new WildTreeChopDataExpectation(true, true, "(O)88", 0.75f, Array.Empty<WildTreeChopRowExpectation>()),
            "10" => new WildTreeChopDataExpectation(true, true, "MossySeed", 0.05f, new[] { StumpRow("(O)92") }),
            "11" => new WildTreeChopDataExpectation(true, true, "MossySeed", 0.05f, new[] { StumpRow("(O)92") }),
            "12" => new WildTreeChopDataExpectation(false, false, "MossySeed", 0.05f, new[]
            {
                new WildTreeChopRowExpectation("(O)259", 5, null, false, 1f, 5, -1),
                StumpRow("(O)259")
            }),
            "13" => new WildTreeChopDataExpectation(false, true, "MysticTreeSeed", 0f, new[]
            {
                new WildTreeChopRowExpectation("(O)709", 5, null, false, 1f, 7, 11),
                StumpRow("(O)709")
            }),
            _ => null
        };
        if (expected is null ||
            data.DropWoodOnChop != expected.DropWood ||
            data.DropHardwoodOnLumberChop != expected.DropHardwood ||
            data.SeedItemId != expected.SeedItemId ||
            Math.Abs(data.SeedOnChopChance - expected.SeedOnChopChance) > 0.000001f)
        {
            return "drifted_chop_flags_or_seed_chance";
        }

        var rows = data.ChopItems ?? new List<WildTreeChopItemData>();
        if (rows.Count != expected.Rows.Length)
        {
            return "drifted_chop_item_count";
        }
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var contract = expected.Rows[index];
            if (!HasLockedBaseWildTreeChopItemFields(row, contract.ItemId) ||
                row.RandomItemId is not null ||
                (int?)row.MinSize != contract.MinSize ||
                (int?)row.MaxSize != contract.MaxSize ||
                row.ForStump != contract.ForStump ||
                Math.Abs(row.Chance - contract.Chance) > 0.000001f ||
                row.Season is not null ||
                row.Condition is not null ||
                row.MinStack != contract.MinStack ||
                row.MaxStack != contract.MaxStack)
            {
                return "drifted_chop_item_fields";
            }
        }
        return "exact_locked_base_1.6.15_chop";
    }

    private static bool HasLockedBaseWildTreeChopItemFields(WildTreeChopItemData row, string itemId) =>
        row.Id == itemId &&
        row.ItemId == itemId &&
        row.MaxItems is null &&
        row.Quality == -1 &&
        row.ObjectInternalName is null &&
        row.ObjectDisplayName is null &&
        row.ObjectColor is null &&
        row.ToolUpgradeLevel == -1 &&
        !row.IsRecipe &&
        row.StackModifiers is null &&
        (int)row.StackModifierMode == 0 &&
        row.QualityModifiers is null &&
        (int)row.QualityModifierMode == 0 &&
        row.ModData is null &&
        row.PerItemCondition is null;

    private static WildTreeChopOutputMinimum[] ProjectWildTreeChopGuaranteedOutputs(
        Vector2 tile,
        Tree tree,
        WildTreeData data)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        if (data.DropWoodOnChop)
        {
            AddMinimum(totals, "(O)388", ProjectWildTreeGuaranteedWoodMinimum(tree));
            AddMinimum(totals, "(O)92", 5);
        }
        foreach (var stump in new[] { false, true })
        {
            foreach (var row in data.ChopItems ?? Enumerable.Empty<WildTreeChopItemData>())
            {
                if (row.Chance < 1f || !row.IsValidForGrowthStage(Tree.treeStage, stump))
                {
                    continue;
                }
                var itemId = row.ItemId;
                if (stump && itemId == "(O)420" && tile.X % 7f == 0f)
                {
                    itemId = "(O)422";
                }
                AddMinimum(totals, ItemRegistry.Create(itemId).QualifiedItemId, Math.Max(1, row.MinStack));
            }
        }
        return totals.OrderBy(row => row.Key, StringComparer.Ordinal)
            .Select(row => new WildTreeChopOutputMinimum(row.Key, 0, row.Value))
            .ToArray();
    }

    private static object[] ProjectWildTreeChopOptionalOutputDomain(
        Tree tree,
        WildTreeData data,
        Axe? axe,
        int maxSwings)
    {
        var rows = new List<object>();
        var exactIds = new HashSet<string>(StringComparer.Ordinal);
        void AddExact(string itemId, string branch, int? quantityMax = null)
        {
            var qualifiedId = ItemRegistry.Create(itemId).QualifiedItemId;
            if (exactIds.Add(qualifiedId + "|" + branch))
            {
                rows.Add(new { kind = "exact", qualified_item_id = qualifiedId, quality = 0, quantity_max = quantityMax, branch });
            }
        }

        foreach (var output in ProjectWildTreeChopGuaranteedOutputs(tree.Tile, tree, data))
        {
            AddExact(output.QualifiedItemId, "quantity_above_guaranteed");
        }
        if (data.DropHardwoodOnLumberChop && Game1.player.professions.Contains(14))
        {
            AddExact("(O)709", "lumberjack_profession");
        }
        foreach (var row in data.ChopItems ?? Enumerable.Empty<WildTreeChopItemData>())
        {
            if (row.Chance < 1f && row.IsValidForGrowthStage(Tree.treeStage, false))
            {
                var maximum = row.MaxStack >= row.MinStack ? row.MaxStack : Math.Max(1, row.MinStack);
                AddExact(row.ItemId, "data_chop_item", maximum);
            }
        }
        if (Game1.player.getEffectiveSkillLevel(Farmer.foragingSkill) >= 1 && data.SeedOnChopChance > 0f)
        {
            AddExact(data.SeedItemId, "seed_on_chop", 2);
        }
        if (axe?.hasEnchantmentOfType<ShavingEnchantment>() == true)
        {
            AddExact(tree.treeType.Value switch
            {
                "12" => "(O)259",
                "7" => "(O)420",
                "8" => "(O)709",
                _ => "(O)388"
            }, "shaving_enchantment", maxSwings);
        }
        if ((tree.Location ?? Game1.currentLocation).HasUnlockedAreaSecretNotes(Game1.player))
        {
            AddExact((tree.Location ?? Game1.currentLocation).InIslandContext() ? "(O)842" : "(O)79", "unseen_secret_note", maxSwings);
        }
        if (Game1.MasterPlayer.mailReceived.Contains("sawQiPlane"))
        {
            AddExact(Game1.player.stats.Get(StardewValley.Constants.StatKeys.Mastery(2)) != 0 ? "(O)GoldenMysteryBox" : "(O)MysteryBox", "mystery_box", maxSwings);
        }
        if (Game1.player.stats.Get("TreesChopped") > 20 && !Game1.player.mailReceived.Contains("GotWoodcuttingBook"))
        {
            AddExact("(O)Book_Woodcutting", "woodcutting_book", 1);
        }
        if (Game1.player.stats.Get(StardewValley.Constants.StatKeys.Mastery(0)) != 0)
        {
            AddExact("(O)GoldenAnimalCracker", "rare_object", maxSwings);
        }
        if (Game1.stats.DaysPlayed > 2)
        {
            rows.Add(new { kind = "family", family = "native_cosmetic_item", quality = 0, quantity_max = maxSwings, branch = "rare_object" });
            rows.Add(new { kind = "range", qualified_item_id_prefix = "(O)SkillBook_", min_suffix = 0, max_suffix = 4, quality = 0, quantity_max = maxSwings, branch = "rare_object" });
        }
        if (Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS"))
        {
            AddExact("(O)890", "qi_bean", 1);
        }
        return rows.ToArray();
    }

    private static int ProjectWildTreeGuaranteedWoodMinimum(Tree tree)
    {
        var deterministicExtra = tree.treeType.Value == "3" ? 1 : 0;
        var multiplier = Game1.player.professions.Contains(12) ? 1.25d : 1d;
        var trunk = (int)(multiplier * (12 + deterministicExtra));
        var stump = (int)(multiplier * (Game1.IsMultiplayer ? 4 : 5 + deterministicExtra));
        return checked(trunk + stump);
    }

    private static void AddMinimum(IDictionary<string, int> totals, string itemId, int quantity)
    {
        totals[itemId] = totals.TryGetValue(itemId, out var current)
            ? checked(current + quantity)
            : quantity;
    }

    private static WildTreeChopRowExpectation StumpRow(string itemId) =>
        new(itemId, null, null, true, 1f, 1, -1);

    private sealed record WildTreeChopDataExpectation(
        bool DropWood,
        bool DropHardwood,
        string SeedItemId,
        float SeedOnChopChance,
        WildTreeChopRowExpectation[] Rows);

    private sealed record WildTreeChopRowExpectation(
        string ItemId,
        int? MinSize,
        int? MaxSize,
        bool? ForStump,
        float Chance,
        int MinStack,
        int MaxStack);

    private sealed record WildTreeChopOutputMinimum(
        string QualifiedItemId,
        int Quality,
        int QuantityMin);

    private sealed record WildTreeChopProjection(
        string Status,
        string DataContractStatus,
        string ProtectionStatus,
        int? AxeSlotIndex,
        string AxeQualifiedItemId,
        int? AxeUpgradeLevel,
        bool? AxeIsEfficient,
        int? ExpectedHits,
        double EnergyPerSwing,
        double? EnergyCost,
        WildTreeChopOutputMinimum[] GuaranteedMinimumOutputs,
        object[] OptionalOutputDomain,
        string OutputDistributionStatus,
        string ProjectionStatus,
        int ForagingExperienceBefore,
        int ForagingExperienceDelta,
        int ForagingExperienceAfter,
        long TreesChoppedBefore,
        int TreesChoppedDelta,
        long TreesChoppedAfter);
}
