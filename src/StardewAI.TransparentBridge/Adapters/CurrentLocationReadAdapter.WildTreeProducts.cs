using StardewValley;
using StardewValley.GameData.WildTrees;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private const string WildTreeProductNativeContract =
        "GameLocation.checkAction -> Tree.performUseAction -> Tree.shake; exact base Data/WildTrees seed branch; no direct tree, RNG, debris, inventory, or skill mutation";

    private static WildTreeProductProjection ProjectWildTreeProduct(Tree tree)
    {
        var data = tree.GetData();
        var dataStatus = ValidateBaseWildTreeProductData(tree.treeType.Value, data);
        var safeSlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .Cast<int?>()
            .FirstOrDefault(index => Game1.player.Items[index!.Value] is null);
        var branch = WildTreeProductBranch(tree);
        var primaryId = PrimaryWildTreeProductId(tree, data?.SeedItemId);
        var primaryItem = string.IsNullOrWhiteSpace(primaryId) ? null : ItemRegistry.Create(primaryId);
        var primaryQualifiedId = primaryItem?.QualifiedItemId ?? string.Empty;
        var primaryQuality = primaryItem is not null && Game1.player.professions.Contains(16) && primaryItem.HasContextTag("forage_item")
            ? 4
            : 0;
        var guaranteed = string.IsNullOrWhiteSpace(primaryQualifiedId)
            ? Array.Empty<object>()
            : new object[]
            {
                new
                {
                    qualified_item_id = primaryQualifiedId,
                    quality = primaryQuality,
                    quantity = 1,
                    branch
                }
            };
        var optional = ProjectWildTreeOptionalOutputDomain(tree);
        var status = tree.GetType() != typeof(Tree)
            ? "blocked_custom_tree_runtime_type"
            : data is null
                ? "blocked_wild_tree_data_missing"
                : !string.Equals(dataStatus, "exact_locked_base_1.6.15", StringComparison.Ordinal)
                    ? "blocked_wild_tree_data_contract_drift"
                    : tree.growthStage.Value < Tree.treeStage
                        ? "blocked_tree_not_mature"
                        : tree.stump.Value
                            ? "blocked_tree_is_stump"
                            : tree.tapped.Value
                                ? "blocked_tree_is_tapped"
                                : !tree.hasSeed.Value
                                    ? "blocked_tree_has_no_seed"
                                    : !(Game1.IsMultiplayer || Game1.player.ForagingLevel >= 1)
                                        ? "blocked_foraging_level_below_native_seed_gate"
                                        : tree.maxShake != 0f
                                            ? "blocked_tree_shake_in_progress"
                                            : safeSlot is null
                                                ? "blocked_empty_toolbar_slot_required"
                                                : guaranteed.Length == 0
                                                    ? "blocked_guaranteed_output_projection_missing"
                                                    : "ready";

        return new WildTreeProductProjection(
            status,
            dataStatus,
            branch,
            data?.SeedItemId ?? string.Empty,
            primaryQualifiedId,
            primaryQuality,
            guaranteed.Length == 0 ? 0 : 1,
            guaranteed,
            optional,
            "complete_stochastic_native_branch_domain_no_rng_consumed",
            tree.GetType() == typeof(Tree) && string.Equals(dataStatus, "exact_locked_base_1.6.15", StringComparison.Ordinal)
                ? "exact_from_native_tree_performUseAction_shake_and_locked_wild_tree_data"
                : "unavailable_unverified_runtime_or_data",
            safeSlot,
            Game1.player.CurrentToolIndex);
    }

    private static string ValidateBaseWildTreeProductData(string treeType, WildTreeData? data)
    {
        if (data is null || data.ShakeItems is not null)
        {
            return data is null ? "missing" : "drifted_shake_items_present";
        }

        var expected = treeType switch
        {
            "1" => ("(O)309", 0.05f, "none"),
            "2" => ("(O)310", 0.05f, "hazelnut"),
            "3" => ("(O)311", 0.05f, "none"),
            "6" => ("(O)88", 0.05f, "golden_coconut"),
            "7" => ("(O)891", 0f, "none"),
            "8" => ("(O)292", 0.05f, "none"),
            "9" => ("(O)88", 0.15f, "golden_coconut"),
            "10" or "11" or "12" => ("MossySeed", 0.05f, "none"),
            "13" => ("MysticTreeSeed", 0f, "none"),
            _ => (string.Empty, -1f, "unsupported")
        };
        if (expected.Item3 == "unsupported" ||
            !string.Equals(data.SeedItemId, expected.Item1, StringComparison.Ordinal) ||
            Math.Abs(data.SeedOnShakeChance - expected.Item2) > 0.000001f)
        {
            return "drifted_seed_identity_or_chance";
        }

        var drops = data.SeedDropItems;
        if (expected.Item3 == "none")
        {
            return drops is null || drops.Count == 0
                ? "exact_locked_base_1.6.15"
                : "drifted_unexpected_seed_drop_items";
        }
        if (drops is null || drops.Count != 1)
        {
            return "drifted_seed_drop_item_count";
        }

        var row = drops[0];
        var exact = expected.Item3 == "hazelnut"
            ? row.Id == "Hazelnut" && row.ItemId == "(O)408" && row.Season == Season.Fall &&
              Math.Abs(row.Chance - 1f) < 0.000001f && !row.ContinueOnDrop &&
              row.Condition == "DAY_OF_MONTH 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28"
            : row.Id == "GoldenCoconut" && row.ItemId == "(O)791" && row.Season is null &&
              Math.Abs(row.Chance - 0.1f) < 0.000001f && row.ContinueOnDrop &&
              row.Condition == "LOCATION_CONTEXT Target Island";
        return exact ? "exact_locked_base_1.6.15" : "drifted_seed_drop_item_fields";
    }

    private static string WildTreeProductBranch(Tree tree)
    {
        if (tree.treeType.Value == "2" && tree.Location?.GetSeason() == Season.Fall && Game1.dayOfMonth >= 14)
        {
            return "fall_hazelnut_replaces_seed";
        }
        return tree.treeType.Value is "6" or "9" ? "island_palm_seed" : "default_seed";
    }

    private static string PrimaryWildTreeProductId(Tree tree, string? seedItemId)
    {
        return tree.treeType.Value == "2" && tree.Location?.GetSeason() == Season.Fall && Game1.dayOfMonth >= 14
            ? "(O)408"
            : seedItemId ?? string.Empty;
    }

    private static object[] ProjectWildTreeOptionalOutputDomain(Tree tree)
    {
        var rows = new List<object>();
        if (tree.treeType.Value is "6" or "9" && tree.Location?.InIslandContext() == true)
        {
            rows.Add(new { kind = "exact", qualified_item_id = "(O)791", quality = WildTreeProductQuality("(O)791"), quantity_max = 1, branch = "golden_coconut" });
        }
        if (Game1.MasterPlayer.mailReceived.Contains("sawQiPlane"))
        {
            rows.Add(new
            {
                kind = "exact",
                qualified_item_id = Game1.player.stats.Get(StardewValley.Constants.StatKeys.Mastery(2)) != 0 ? "(O)GoldenMysteryBox" : "(O)MysteryBox",
                quality = 0,
                quantity_max = 1,
                branch = "mystery_box"
            });
        }
        if (Game1.player.stats.Get(StardewValley.Constants.StatKeys.Mastery(0)) != 0)
        {
            rows.Add(new { kind = "exact", qualified_item_id = "(O)GoldenAnimalCracker", quality = 0, quantity_max = 1, branch = "rare_object" });
        }
        if (Game1.stats.DaysPlayed > 2)
        {
            rows.Add(new { kind = "family", family = "native_cosmetic_item", quality = 0, quantity_max = 1, branch = "rare_object" });
            rows.Add(new { kind = "range", qualified_item_id_prefix = "(O)SkillBook_", min_suffix = 0, max_suffix = 4, quality = 0, quantity_max = 1, branch = "rare_object" });
        }
        if (Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS"))
        {
            rows.Add(new { kind = "exact", qualified_item_id = "(O)890", quality = 0, quantity_max = 1, branch = "qi_bean" });
        }
        return rows.ToArray();
    }

    private static int WildTreeProductQuality(string itemId)
    {
        var item = ItemRegistry.Create(itemId);
        return Game1.player.professions.Contains(16) && item.HasContextTag("forage_item") ? 4 : 0;
    }

    private sealed record WildTreeProductProjection(
        string Status,
        string DataContractStatus,
        string Branch,
        string SeedItemId,
        string PrimaryQualifiedItemId,
        int PrimaryQuality,
        int PrimaryQuantity,
        object[] GuaranteedOutputs,
        object[] OptionalOutputDomain,
        string OutputDistributionStatus,
        string ProjectionStatus,
        int? SafeSlotIndex,
        int RestoreSlotIndex);
}
