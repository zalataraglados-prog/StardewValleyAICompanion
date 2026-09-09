using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.GameData.WildTrees;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string WildTreeChopNativeContract =
        "Axe native tool lifecycle -> Tree.performToolAction -> Tree.performTreeFall(trunk,stump) -> Tree.tickUpdate falling settlement; locked Data/WildTrees DropWoodOnChop, DropHardwoodOnLumberChop, ChopItems, SeedItemId, and SeedOnChopChance; complete stochastic output domain; no direct tree, RNG, debris, inventory, stats, or skill mutation";

    private static string? ValidateWildTreeChopExecutionRequest(
        TrainingExecutionRequest request,
        GameLocation location,
        Point target,
        Tree? tree,
        Tool tool,
        out WildTreeChopExecutionState? state)
    {
        state = null;
        if (tree is null || tree.GetType() != typeof(Tree) || tool is not Axe axe || axe.GetType() != typeof(Axe))
        {
            return "wild_tree_chop_target_or_axe_not_exact_vanilla_runtime";
        }
        var expectedHits = RuntimeWildTreeAxeHits(tree, axe.UpgradeLevel);
        var projection = ProjectRuntimeWildTreeChop(target.ToVector2(), tree, axe, expectedHits);
        if (projection.Status != "ready")
        {
            return "wild_tree_chop_not_ready:" + projection.Status;
        }
        if (request.TargetRuntimeType != typeof(Tree).FullName ||
            request.TreeChopTreeType != tree.treeType.Value ||
            request.TreeChopDataContractStatus != "exact_locked_base_1.6.15_chop" ||
            request.TreeChopProtectionStatus != "unprotected" ||
            request.TreeChopProjectionStatus != "exact_live_tree_and_locked_wild_tree_chop_domain" ||
            request.TreeChopOutputDomainContract != "complete_stochastic_native_branch_domain_no_rng_consumed" ||
            request.TreeChopNativeContract != WildTreeChopNativeContract)
        {
            return "wild_tree_chop_native_or_projection_contract_mismatch";
        }
        if (request.ToolSlotIndex != Game1.player.Items.IndexOf(axe) ||
            request.RequiredToolKind != "axe" ||
            request.MaxCrops != expectedHits)
        {
            return "wild_tree_chop_exact_axe_and_hit_budget_required";
        }
        if (request.ExpectedTreeHasSeedBefore != false ||
            request.ExpectedTreeHasMossBefore != false ||
            request.ExpectedTreeGrowthStageBefore != tree.growthStage.Value ||
            !request.ExpectedTreeHealthBefore.HasValue ||
            Math.Abs(request.ExpectedTreeHealthBefore.Value - tree.health.Value) > 0.000001 ||
            request.ExpectedTreePresentAfter != false)
        {
            return "wild_tree_chop_tree_state_projection_drifted";
        }
        var experienceBefore = Game1.player.experiencePoints[Farmer.foragingSkill];
        var treesChoppedBefore = (long)Game1.player.stats.Get("TreesChopped");
        if (request.ExpectedForagingExperienceBefore != experienceBefore ||
            request.ExpectedForagingExperienceDelta != 16 ||
            request.ExpectedForagingExperienceAfter != checked(experienceBefore + 16) ||
            request.ExpectedTreesChoppedBefore != treesChoppedBefore ||
            request.ExpectedTreesChoppedDelta != 1 ||
            request.ExpectedTreesChoppedAfter != checked(treesChoppedBefore + 1))
        {
            return "wild_tree_chop_stat_or_experience_projection_drifted";
        }
        if (!WildTreeJsonEquivalent(request.TreeChopGuaranteedMinimumOutputsJson, projection.GuaranteedMinimumOutputsJson) ||
            !WildTreeJsonEquivalent(request.TreeChopOutputDomainJson, projection.OptionalOutputDomainJson))
        {
            return "wild_tree_chop_output_domain_drifted";
        }

        state = new WildTreeChopExecutionState(
            tree,
            projection,
            CaptureWildTreeProductOutputs(location),
            treesChoppedBefore,
            Game1.player.mailReceived.Contains("GotWoodcuttingBook"));
        return null;
    }

    private static RuntimeWildTreeChopProjection ProjectRuntimeWildTreeChop(
        Vector2 tile,
        Tree tree,
        Axe axe,
        int expectedHits)
    {
        var data = tree.GetData();
        var dataStatus = ValidateRuntimeWildTreeChopData(tree.treeType.Value, data);
        var protectionStatus = RuntimeWildTreeChopProtectionStatus(tile, tree);
        var minimums = data is null
            ? Array.Empty<WildTreeChopMinimum>()
            : RuntimeWildTreeChopMinimums(tile, tree, data);
        var rules = data is null
            ? Array.Empty<WildTreeChopRule>()
            : RuntimeWildTreeChopRules(tree, data, axe, expectedHits, minimums);
        var status = tree.GetType() != typeof(Tree)
            ? "blocked_custom_tree_runtime_type"
            : dataStatus != "exact_locked_base_1.6.15_chop"
                ? "blocked_wild_tree_chop_data_contract_drift"
                : protectionStatus != "unprotected"
                    ? protectionStatus
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
                                        : tree.falling.Value || tree.maxShake != 0f
                                            ? "blocked_tree_animation_in_progress"
                                            : "ready";
        return new RuntimeWildTreeChopProjection(
            status,
            minimums,
            rules,
            JsonSerializer.Serialize(minimums.Select(row => new
            {
                qualifiedItemId = row.QualifiedItemId,
                quality = row.Quality,
                quantityMin = row.QuantityMin
            })),
            JsonSerializer.Serialize(rules.Select(row => row.ToPayload())));
    }

    private static string RuntimeWildTreeChopProtectionStatus(Vector2 tile, Tree tree)
    {
        var location = tree.Location ?? Game1.currentLocation;
        if (location is not Town || tile.X >= 100f || tree.isTemporaryGreenRainTree.Value)
        {
            return "unprotected";
        }
        return location.getTileIndexAt((int)tile.X, (int)tile.Y, "Paths") is 9 or 10 or 11
            ? "blocked_native_town_tree_protection"
            : "unprotected";
    }

    private static string ValidateRuntimeWildTreeChopData(string treeType, WildTreeData? data)
    {
        if (data is null)
        {
            return "missing";
        }
        var expected = treeType switch
        {
            "1" => new RuntimeWildTreeChopData(true, true, "(O)309", 0.75f, new[] { RuntimeStumpRow("(O)92") }),
            "2" => new RuntimeWildTreeChopData(true, true, "(O)310", 0.75f, new[] { RuntimeStumpRow("(O)92") }),
            "3" => new RuntimeWildTreeChopData(true, true, "(O)311", 0.75f, new[] { RuntimeStumpRow("(O)92") }),
            "6" => new RuntimeWildTreeChopData(true, true, "(O)88", 0.75f, Array.Empty<RuntimeWildTreeChopRow>()),
            "7" => new RuntimeWildTreeChopData(false, false, "(O)891", 0f, new[]
            {
                new RuntimeWildTreeChopRow("(O)420", 3, 3, false, 1f, 1, -1),
                new RuntimeWildTreeChopRow("(O)420", 5, null, false, 1f, 5, -1),
                new RuntimeWildTreeChopRow("(H)42", 5, null, false, 0.01f, 1, -1),
                RuntimeStumpRow("(O)420")
            }),
            "8" => new RuntimeWildTreeChopData(false, true, "(O)292", 0.5625f, new[]
            {
                new RuntimeWildTreeChopRow("(O)709", 5, null, false, 1f, 7, 11),
                RuntimeStumpRow("(O)709")
            }),
            "9" => new RuntimeWildTreeChopData(true, true, "(O)88", 0.75f, Array.Empty<RuntimeWildTreeChopRow>()),
            "10" or "11" => new RuntimeWildTreeChopData(true, true, "MossySeed", 0.05f, new[] { RuntimeStumpRow("(O)92") }),
            "12" => new RuntimeWildTreeChopData(false, false, "MossySeed", 0.05f, new[]
            {
                new RuntimeWildTreeChopRow("(O)259", 5, null, false, 1f, 5, -1),
                RuntimeStumpRow("(O)259")
            }),
            "13" => new RuntimeWildTreeChopData(false, true, "MysticTreeSeed", 0f, new[]
            {
                new RuntimeWildTreeChopRow("(O)709", 5, null, false, 1f, 7, 11),
                RuntimeStumpRow("(O)709")
            }),
            _ => null
        };
        if (expected is null || data.DropWoodOnChop != expected.DropWood ||
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
            if (!HasLockedRuntimeWildTreeChopItemFields(row, contract.ItemId) || row.RandomItemId is not null ||
                (int?)row.MinSize != contract.MinSize || (int?)row.MaxSize != contract.MaxSize ||
                row.ForStump != contract.ForStump || Math.Abs(row.Chance - contract.Chance) > 0.000001f ||
                row.Season is not null || row.Condition is not null ||
                row.MinStack != contract.MinStack || row.MaxStack != contract.MaxStack)
            {
                return "drifted_chop_item_fields";
            }
        }
        return "exact_locked_base_1.6.15_chop";
    }

    private static bool HasLockedRuntimeWildTreeChopItemFields(WildTreeChopItemData row, string itemId) =>
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

    private static WildTreeChopMinimum[] RuntimeWildTreeChopMinimums(Vector2 tile, Tree tree, WildTreeData data)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        if (data.DropWoodOnChop)
        {
            AddRuntimeWildTreeMinimum(totals, "(O)388", RuntimeWildTreeGuaranteedWoodMinimum(tree));
            AddRuntimeWildTreeMinimum(totals, "(O)92", 5);
        }
        foreach (var stump in new[] { false, true })
        {
            foreach (var row in data.ChopItems ?? Enumerable.Empty<WildTreeChopItemData>())
            {
                if (row.Chance < 1f || !row.IsValidForGrowthStage(Tree.treeStage, stump))
                {
                    continue;
                }
                var itemId = stump && row.ItemId == "(O)420" && tile.X % 7f == 0f
                    ? "(O)422"
                    : row.ItemId;
                AddRuntimeWildTreeMinimum(totals, ItemRegistry.Create(itemId).QualifiedItemId, Math.Max(1, row.MinStack));
            }
        }
        return totals.OrderBy(row => row.Key, StringComparer.Ordinal)
            .Select(row => new WildTreeChopMinimum(row.Key, 0, row.Value))
            .ToArray();
    }

    private static WildTreeChopRule[] RuntimeWildTreeChopRules(
        Tree tree,
        WildTreeData data,
        Axe axe,
        int maxSwings,
        IReadOnlyList<WildTreeChopMinimum> minimums)
    {
        var rules = new List<WildTreeChopRule>();
        var exact = new HashSet<string>(StringComparer.Ordinal);
        void AddExact(string itemId, string branch, int? maximum = null)
        {
            var qualified = ItemRegistry.Create(itemId).QualifiedItemId;
            if (exact.Add(qualified + "|" + branch))
            {
                rules.Add(WildTreeChopRule.Exact(qualified, 0, maximum, branch));
            }
        }
        foreach (var minimum in minimums)
        {
            AddExact(minimum.QualifiedItemId, "quantity_above_guaranteed");
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
        if (axe.hasEnchantmentOfType<ShavingEnchantment>())
        {
            AddExact(tree.treeType.Value switch
            {
                "12" => "(O)259",
                "7" => "(O)420",
                "8" => "(O)709",
                _ => "(O)388"
            }, "shaving_enchantment", maxSwings);
        }
        var location = tree.Location ?? Game1.currentLocation;
        if (location.HasUnlockedAreaSecretNotes(Game1.player))
        {
            AddExact(location.InIslandContext() ? "(O)842" : "(O)79", "unseen_secret_note", maxSwings);
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
            rules.Add(WildTreeChopRule.Family("native_cosmetic_item", maxSwings, "rare_object"));
            rules.Add(WildTreeChopRule.Range("(O)SkillBook_", 0, 4, maxSwings, "rare_object"));
        }
        if (Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS"))
        {
            AddExact("(O)890", "qi_bean", 1);
        }
        return rules.ToArray();
    }

    private static int RuntimeWildTreeGuaranteedWoodMinimum(Tree tree)
    {
        var deterministicExtra = tree.treeType.Value == "3" ? 1 : 0;
        var multiplier = Game1.player.professions.Contains(12) ? 1.25d : 1d;
        var trunk = (int)(multiplier * (12 + deterministicExtra));
        var stump = (int)(multiplier * (Game1.IsMultiplayer ? 4 : 5 + deterministicExtra));
        return checked(trunk + stump);
    }

    private static int RuntimeWildTreeAxeHits(Tree tree, int axeUpgradeLevel)
    {
        var damage = axeUpgradeLevel switch
        {
            0 => 1f,
            1 => 1.25f,
            2 => 1.67f,
            3 => 2.5f,
            4 => 5f,
            _ => axeUpgradeLevel + 1f
        };
        return Math.Max(1, (int)Math.Ceiling(Math.Max(0f, tree.health.Value) / damage)) +
            Math.Max(1, (int)Math.Ceiling(5f / damage));
    }

    private static void AddRuntimeWildTreeMinimum(IDictionary<string, int> totals, string itemId, int quantity)
    {
        var qualified = ItemRegistry.Create(itemId).QualifiedItemId;
        totals[qualified] = (totals.TryGetValue(qualified, out var current) ? current : 0) + quantity;
    }

    private static RuntimeWildTreeChopRow RuntimeStumpRow(string itemId) =>
        new(itemId, null, null, true, 1f, 1, -1);

    private sealed record RuntimeWildTreeChopData(
        bool DropWood,
        bool DropHardwood,
        string SeedItemId,
        float SeedOnChopChance,
        RuntimeWildTreeChopRow[] Rows);

    private sealed record RuntimeWildTreeChopRow(
        string ItemId,
        int? MinSize,
        int? MaxSize,
        bool? ForStump,
        float Chance,
        int MinStack,
        int MaxStack);

    private sealed record WildTreeChopMinimum(string QualifiedItemId, int Quality, int QuantityMin)
    {
        public string Key => QualifiedItemId + "|" + Quality;
    }

    private sealed record WildTreeChopRule(
        string Kind,
        string QualifiedItemId,
        int Quality,
        int? QuantityMax,
        string FamilyName,
        int MinSuffix,
        int MaxSuffix,
        string Branch)
    {
        public static WildTreeChopRule Exact(string id, int quality, int? maximum, string branch) =>
            new("exact", id, quality, maximum, string.Empty, 0, 0, branch);
        public static WildTreeChopRule Family(string family, int maximum, string branch) =>
            new("family", string.Empty, 0, maximum, family, 0, 0, branch);
        public static WildTreeChopRule Range(string prefix, int minimum, int maximum, int quantityMaximum, string branch) =>
            new("range", prefix, 0, quantityMaximum, string.Empty, minimum, maximum, branch);
        public bool Matches(string id, int quality) => quality == Quality && Kind switch
        {
            "exact" => id == QualifiedItemId,
            "family" => FamilyName == "native_cosmetic_item" && IsNativeWildTreeCosmetic(id),
            "range" => id.StartsWith(QualifiedItemId, StringComparison.Ordinal) &&
                int.TryParse(id[QualifiedItemId.Length..], out var suffix) &&
                suffix >= MinSuffix && suffix <= MaxSuffix,
            _ => false
        };
        public object ToPayload() => Kind switch
        {
            "exact" => new { kind = Kind, qualified_item_id = QualifiedItemId, quality = Quality, quantity_max = QuantityMax, branch = Branch },
            "family" => new { kind = Kind, family = FamilyName, quality = 0, quantity_max = QuantityMax, branch = Branch },
            _ => new { kind = Kind, qualified_item_id_prefix = QualifiedItemId, min_suffix = MinSuffix, max_suffix = MaxSuffix, quality = 0, quantity_max = QuantityMax, branch = Branch }
        };
    }

    private sealed record RuntimeWildTreeChopProjection(
        string Status,
        IReadOnlyList<WildTreeChopMinimum> Minimums,
        IReadOnlyList<WildTreeChopRule> Rules,
        string GuaranteedMinimumOutputsJson,
        string OptionalOutputDomainJson);

    private sealed record WildTreeChopExecutionState(
        Tree Tree,
        RuntimeWildTreeChopProjection Projection,
        Dictionary<string, int> OutputCountsBefore,
        long TreesChoppedBefore,
        bool WoodcuttingBookMailBefore);

}
