using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using SObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

internal static class MiningStoneDropResolver
{
    public static MiningStoneDropProjection Resolve(MineShaft mine, SObject stone, Farmer player)
    {
        var guaranteed = new HashSet<string>(StringComparer.Ordinal);
        var conditional = new HashSet<string>(StringComparer.Ordinal);
        var guaranteedOneOf = new List<string[]>();
        var rules = new List<string>();
        var itemId = stone.ItemId;
        var directNode = AddDirectNodeDrops(itemId, guaranteed, conditional, guaranteedOneOf, rules);
        var experience = ResolveExperience(mine, itemId, player, directNode);

        if (!directNode)
        {
            AddGenericMineStoneDrops(mine, conditional, rules);
        }

        AddOnStoneDestroyedDrops(mine, player, conditional, rules);

        foreach (var group in guaranteedOneOf)
        {
            conditional.UnionWith(group);
        }

        var possible = guaranteed.Concat(conditional)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        return new MiningStoneDropProjection
        {
            RuleBranch = directNode ? "game_location_break_stone_direct_node" : "mine_shaft_generic_stone",
            GuaranteedDropQualifiedItemIds = Ordered(guaranteed),
            ConditionalDropQualifiedItemIds = Ordered(conditional),
            GuaranteedOneOfQualifiedItemIdGroups = guaranteedOneOf
                .Select(group => group.OrderBy(id => id, StringComparer.Ordinal).ToArray())
                .ToArray(),
            PossibleDropQualifiedItemIds = possible,
            ItemIdentityCompleteness = "complete_for_current_vanilla_mineshaft_stone",
            ProbabilityStatus = "not_computed_rng_outcomes_enumerated",
            MiningExperienceMinimum = experience.MiningMinimum,
            MiningExperienceMaximum = experience.MiningMaximum,
            MiningExperienceCondition = experience.MiningCondition,
            MiningExperienceProjectionStatus = experience.MiningStatus,
            LuckExperienceMinimum = experience.LuckMinimum,
            LuckExperienceMaximum = experience.LuckMaximum,
            LuckExperienceCondition = experience.LuckCondition,
            LuckExperienceProjectionStatus = experience.LuckStatus,
            AppliedRuleConditions = rules.Distinct(StringComparer.Ordinal).OrderBy(rule => rule, StringComparer.Ordinal).ToArray(),
            Source = "GameLocation.OnStoneDestroyed/breakStone; MineShaft.checkStoneForItems/getOreIdForLevel; Debris.InitializeResource; Farmer.gainExperience"
        };
    }

    private static StoneExperienceProjection ResolveExperience(MineShaft mine, string itemId, Farmer player, bool directNode)
    {
        if (!directNode)
        {
            var area = mine.getMineArea();
            var luckMaximum = mine.mineLevel > 20 ? 60 * area : 20 * area;
            return new StoneExperienceProjection
            {
                MiningMinimum = 0,
                MiningMaximum = 5,
                MiningCondition = "generic_stone_native_ore_roll",
                MiningStatus = "exact_bounded_from_decompiled_rng_branches",
                LuckMinimum = 0,
                LuckMaximum = Math.Max(0, luckMaximum),
                LuckCondition = mine.mineLevel > 20
                    ? "generic_stone_native_area_geode_and_omni_geode_rolls"
                    : "generic_stone_native_area_geode_roll",
                LuckStatus = luckMaximum == 0
                    ? "exact_zero_for_current_mine_area"
                    : "exact_bounded_from_decompiled_rng_branches"
            };
        }

        var (minimum, maximum) = DirectNodeMiningExperience(itemId, player.professions.Contains(19));
        return new StoneExperienceProjection
        {
            MiningMinimum = minimum,
            MiningMaximum = maximum,
            MiningCondition = "native_game_location_break_stone_success",
            MiningStatus = minimum == maximum
                ? "exact_for_current_direct_node"
                : "exact_bounded_from_decompiled_rng_branches",
            LuckMinimum = 0,
            LuckMaximum = 0,
            LuckCondition = "no_native_luck_experience_branch_for_direct_node",
            LuckStatus = "exact_zero_for_current_direct_node"
        };
    }

    private static (int Minimum, int Maximum) DirectNodeMiningExperience(string itemId, bool hasGeologist)
    {
        var baseExperience = itemId switch
        {
            "95" => 18,
            "843" or "844" => 12,
            "25" => 5,
            "75" => 8,
            "76" => 16,
            "77" => 32,
            "816" or "817" or "818" => 6,
            "819" => 64,
            "8" or "10" => 16,
            "12" or "4" => 80,
            "14" or "6" => 40,
            "2" or "46" => 150,
            "849" or "751" => 5,
            "850" or "290" => 12,
            "BasicCoalNode0" or "BasicCoalNode1" or "VolcanoCoalNode0" or "VolcanoCoalNode1" => 10,
            "764" or "VolcanoGoldNode" => 18,
            "765" or "CalicoEggStone_0" or "CalicoEggStone_1" or "CalicoEggStone_2" => 50,
            _ => 0
        };

        if (itemId is "846" or "847" or "668" or "845" or "670")
        {
            return (3, 4);
        }
        if (itemId == "44")
        {
            return hasGeologist ? (8, 150) : (16, 150);
        }
        if (!hasGeologist)
        {
            return (baseExperience, baseExperience);
        }

        var geologistExperience = itemId switch
        {
            "8" or "10" => 8,
            "12" or "4" => 50,
            "14" or "6" => 20,
            "2" => 100,
            _ => baseExperience
        };
        return (Math.Min(baseExperience, geologistExperience), Math.Max(baseExperience, geologistExperience));
    }

    private static bool AddDirectNodeDrops(
        string itemId,
        HashSet<string> guaranteed,
        HashSet<string> conditional,
        List<string[]> guaranteedOneOf,
        List<string> rules)
    {
        switch (itemId)
        {
            case "95":
                AddGuaranteed(guaranteed, rules, "(O)909");
                return true;
            case "843":
            case "844":
                AddGuaranteed(guaranteed, rules, "(O)848");
                return true;
            case "25":
                AddGuaranteed(guaranteed, rules, "(O)719");
                return true;
            case "75":
                AddGuaranteed(guaranteed, rules, "(O)535");
                return true;
            case "76":
                AddGuaranteed(guaranteed, rules, "(O)536");
                return true;
            case "77":
                AddGuaranteed(guaranteed, rules, "(O)537");
                return true;
            case "816":
            case "817":
                AddGuaranteed(guaranteed, rules, "(O)881");
                AddConditional(conditional, rules, "fossilized_node_secondary_rolls", "(O)823", "(O)824");
                for (var id = 579; id <= 589; id++)
                {
                    conditional.Add("(O)" + id);
                }
                return true;
            case "818":
                AddGuaranteed(guaranteed, rules, "(O)330");
                return true;
            case "819":
                AddGuaranteed(guaranteed, rules, "(O)749");
                return true;
            case "8":
                AddGuaranteed(guaranteed, rules, "(O)66");
                return true;
            case "10":
                AddGuaranteed(guaranteed, rules, "(O)68");
                return true;
            case "12":
                AddGuaranteed(guaranteed, rules, "(O)60");
                return true;
            case "14":
                AddGuaranteed(guaranteed, rules, "(O)62");
                return true;
            case "6":
                AddGuaranteed(guaranteed, rules, "(O)70");
                return true;
            case "4":
                AddGuaranteed(guaranteed, rules, "(O)64");
                return true;
            case "2":
                AddGuaranteed(guaranteed, rules, "(O)72");
                return true;
            case "44":
                guaranteedOneOf.Add(new[] { "(O)60", "(O)62", "(O)64", "(O)66", "(O)68", "(O)70", "(O)72" });
                rules.Add("gem_node_guarantees_one_seed_selected_gem");
                return true;
            case "846":
            case "847":
            case "668":
            case "845":
            case "670":
                AddGuaranteed(guaranteed, rules, "(O)390");
                AddConditional(conditional, rules, "stone_node_coal_roll", "(O)382");
                return true;
            case "849":
            case "751":
                AddGuaranteed(guaranteed, rules, "(O)378");
                return true;
            case "850":
            case "290":
                AddGuaranteed(guaranteed, rules, "(O)380");
                return true;
            case "BasicCoalNode0":
            case "BasicCoalNode1":
            case "VolcanoCoalNode0":
            case "VolcanoCoalNode1":
                AddGuaranteed(guaranteed, rules, "(O)382");
                return true;
            case "764":
            case "VolcanoGoldNode":
                AddGuaranteed(guaranteed, rules, "(O)384");
                return true;
            case "765":
                AddGuaranteed(guaranteed, rules, "(O)386");
                AddConditional(conditional, rules, "iridium_node_prismatic_shard_roll", "(O)74");
                return true;
            case "CalicoEggStone_0":
            case "CalicoEggStone_1":
            case "CalicoEggStone_2":
                AddGuaranteed(guaranteed, rules, "(O)CalicoEgg");
                return true;
            case "46":
                // Debris resource types 10 and 6 resolve to iridium and gold ore respectively.
                AddGuaranteed(guaranteed, rules, "(O)386", "(O)384");
                AddConditional(conditional, rules, "mystic_stone_prismatic_shard_roll", "(O)74");
                return true;
            default:
                return false;
        }
    }

    private static void AddGenericMineStoneDrops(
        MineShaft mine,
        HashSet<string> conditional,
        List<string> rules)
    {
        var area = mine.getMineArea();
        var level = mine.mineLevel;

        conditional.Add(area switch
        {
            40 => "(O)536",
            80 => "(O)537",
            121 => "(O)749",
            _ => "(O)535"
        });
        rules.Add("generic_stone_area_geode_roll");

        if (level > 20)
        {
            conditional.Add("(O)749");
            rules.Add("generic_stone_omni_geode_roll_after_level_20");
        }

        conditional.Add("(O)382");
        conditional.Add("(O)390");
        rules.Add("generic_stone_coal_or_stone_resource_roll");

        foreach (var oreId in OreIdsForLevel(mine))
        {
            conditional.Add(oreId);
        }
        rules.Add("generic_stone_floor_ore_roll");
    }

    private static IEnumerable<string> OreIdsForLevel(MineShaft mine)
    {
        var level = mine.mineLevel;
        if (mine.getMineArea(level) == 77377)
        {
            return new[] { "(O)380" };
        }
        if (level < 20)
        {
            return new[] { "(O)378" };
        }
        if (level < 40)
        {
            return new[] { "(O)378", "(O)380" };
        }
        if (level < 60)
        {
            return new[] { "(O)378", "(O)380" };
        }
        if (level < 80)
        {
            return new[] { "(O)378", "(O)380", "(O)384" };
        }
        if (level < 120)
        {
            return new[] { "(O)378", "(O)380", "(O)384" };
        }

        var ids = new List<string> { "(O)378", "(O)380", "(O)384", "(O)386" };
        if (Utility.GetDayOfPassiveFestival("DesertFestival") > 0)
        {
            ids.Add("(O)CalicoEgg");
        }
        return ids;
    }

    private static void AddOnStoneDestroyedDrops(
        MineShaft mine,
        Farmer player,
        HashSet<string> conditional,
        List<string> rules)
    {
        if (mine.mineLevel > 120 && !mine.isSideBranch() && Utility.GetDayOfPassiveFestival("DesertFestival") > 0)
        {
            AddConditional(conditional, rules, "desert_festival_skull_cavern_stone_roll", "(O)CalicoEgg");
        }
        if (player.team.SpecialOrderRuleActive("DROP_QI_BEANS"))
        {
            AddConditional(conditional, rules, "qi_bean_special_order_stone_roll", "(O)890");
        }
        if (HasUnseenSecretNote(player))
        {
            AddConditional(conditional, rules, "unseen_secret_note_stone_roll", "(O)79");
        }
    }

    private static bool HasUnseenSecretNote(Farmer player)
    {
        if (!player.hasMagnifyingGlass)
        {
            return false;
        }

        var unseen = Utility.GetUnseenSecretNotes(player, journal: false, out _);
        return unseen.Length - player.Items.CountId("(O)79") > 0;
    }

    private static void AddGuaranteed(HashSet<string> target, List<string> rules, params string[] ids)
    {
        target.UnionWith(ids);
        rules.Add("direct_node_guaranteed_output");
    }

    private static void AddConditional(HashSet<string> target, List<string> rules, string rule, params string[] ids)
    {
        target.UnionWith(ids);
        rules.Add(rule);
    }

    private static string[] Ordered(IEnumerable<string> ids)
    {
        return ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }
}

internal sealed class MiningStoneDropProjection
{
    public string RuleBranch { get; set; } = string.Empty;

    public string[] GuaranteedDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string[] ConditionalDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string[][] GuaranteedOneOfQualifiedItemIdGroups { get; set; } = Array.Empty<string[]>();

    public string[] PossibleDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string ItemIdentityCompleteness { get; set; } = string.Empty;

    public string ProbabilityStatus { get; set; } = string.Empty;

    public int MiningExperienceMinimum { get; set; }

    public int MiningExperienceMaximum { get; set; }

    public string MiningExperienceCondition { get; set; } = string.Empty;

    public string MiningExperienceProjectionStatus { get; set; } = string.Empty;

    public int LuckExperienceMinimum { get; set; }

    public int LuckExperienceMaximum { get; set; }

    public string LuckExperienceCondition { get; set; } = string.Empty;

    public string LuckExperienceProjectionStatus { get; set; } = string.Empty;

    public string[] AppliedRuleConditions { get; set; } = Array.Empty<string>();

    public string Source { get; set; } = string.Empty;
}

internal sealed class StoneExperienceProjection
{
    public int MiningMinimum { get; set; }

    public int MiningMaximum { get; set; }

    public string MiningCondition { get; set; } = string.Empty;

    public string MiningStatus { get; set; } = string.Empty;

    public int LuckMinimum { get; set; }

    public int LuckMaximum { get; set; }

    public string LuckCondition { get; set; } = string.Empty;

    public string LuckStatus { get; set; } = string.Empty;
}
