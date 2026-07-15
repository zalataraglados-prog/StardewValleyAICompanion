using StardewValley;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace StardewAI.TransparentBridge.Adapters;

internal static class MiningMonsterDropResolver
{
    public static MiningMonsterDropProjection Resolve(MineShaft mine, Monster monster, Farmer player, int deathTileX, int deathTileY)
    {
        var selected = monster.objectsToDrop.Select(QualifyDropId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var guaranteed = new HashSet<string>(StringComparer.Ordinal);
        var conditional = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new List<string>();
        var primaryStatus = "base_selected_drops";

        if (monster.hasSpecialItem.Value)
        {
            var special = PreviewSpecialItem(mine, deathTileX, deathTileY);
            conditional.UnionWith(PossibleSpecialItems(mine));
            if (string.IsNullOrWhiteSpace(special))
            {
                primaryStatus = "special_item_replaces_base_drops_unresolved_global_rng_treasure_branch";
                unresolved.Add("MineShaft.getTreasureRoomItem_consumes_global_rng");
            }
            else
            {
                conditional.Add(special);
                primaryStatus = "special_item_replaces_base_drops_current_death_tile_preview";
            }

            var projection = Build(
                selected,
                guaranteed,
                conditional,
                primaryStatus,
                Game1.mine is null || Game1.mine.GetAdditionalDifficulty() <= 0
                    ? "complete_possible_identity_set_for_mineshaft_special_item"
                    : "partial_hard_mine_special_item_treasure_branch",
                unresolved);
            projection.CurrentDeathTilePreviewQualifiedItemId = special ?? string.Empty;
            return projection;
        }

        var pendantOverrideEligible = mine.mineLevel > 121 &&
            player.getFriendshipHeartLevelForNPC("Krobus") >= 10 &&
            player.houseUpgradeLevel.Value >= 1 &&
            !player.isMarriedOrRoommates() &&
            !player.isEngaged();
        if (pendantOverrideEligible)
        {
            conditional.UnionWith(selected);
            conditional.Add("(O)808");
            primaryStatus = "base_drops_or_rare_krobus_pendant_override";
        }
        else
        {
            guaranteed.UnionWith(selected);
        }

        AddBaseMonsterDropPossibilities(monster, player, conditional, unresolved);
        return Build(
            selected,
            guaranteed,
            conditional,
            primaryStatus,
            "partial_death_time_dynamic_sources",
            unresolved);
    }

    private static MiningMonsterDropProjection Build(
        string[] selected,
        HashSet<string> guaranteed,
        HashSet<string> conditional,
        string primaryStatus,
        string completeness,
        List<string> unresolved)
    {
        return new MiningMonsterDropProjection
        {
            SelectedBaseDropQualifiedItemIds = selected,
            GuaranteedDropQualifiedItemIds = Ordered(guaranteed),
            ConditionalDropQualifiedItemIds = Ordered(conditional),
            PossibleDropQualifiedItemIds = Ordered(guaranteed.Concat(conditional)),
            PrimaryDropStatus = primaryStatus,
            ItemIdentityCompleteness = completeness,
            UnresolvedDynamicRules = unresolved.Distinct(StringComparer.Ordinal).OrderBy(rule => rule, StringComparer.Ordinal).ToArray(),
            Source = "MineShaft.monsterDrop; GameLocation.monsterDrop; Monster.objectsToDrop/getExtraDropItems; MineShaft.getSpecialItemForThisMineLevel"
        };
    }

    private static void AddBaseMonsterDropPossibilities(
        Monster monster,
        Farmer player,
        HashSet<string> conditional,
        List<string> unresolved)
    {
        if (player.isWearingRing("526") && DataLoader.Monsters(Game1.content).TryGetValue(monster.Name, out var data))
        {
            var fields = data.Split('/');
            if (fields.Length > 6)
            {
                var dropTokens = ArgUtility.SplitBySpace(fields[6]);
                for (var i = 0; i + 1 < dropTokens.Length; i += 2)
                {
                    conditional.Add(QualifyDropId(dropTokens[i]));
                }
            }
        }

        if (HasUnseenSecretNote(player))
        {
            conditional.Add("(O)79");
        }
        if (Game1.MasterPlayer.mailReceived.Contains("sawQiPlane"))
        {
            conditional.Add(player.stats.Get(StatKeys.Mastery(2)) != 0 ? "(O)GoldenMysteryBox" : "(O)MysteryBox");
        }
        if (player.stats.MonstersKilled > 10)
        {
            conditional.Add("(O)Book_Void");
        }
        if (Game1.netWorldState.Value.GoldenWalnutsFound >= 100 && monster.isHardModeMonster.Value)
        {
            conditional.Add("(O)896");
            conditional.Add("(O)858");
        }
        if (player.stats.Get(StatKeys.Mastery(0)) != 0)
        {
            conditional.Add("(O)GoldenAnimalCracker");
        }
        if (Game1.stats.DaysPlayed > 2)
        {
            for (var i = 0; i < 5; i++)
            {
                conditional.Add("(O)SkillBook_" + i);
            }
            unresolved.Add("Utility.getRandomCosmeticItem_identity_set_not_yet_projected");
        }

        unresolved.Add("Monster.getExtraDropItems_runtime_type_rules_not_yet_projected");
        unresolved.Add("Trinket.TrySpawnTrinket_data_driven_identity_set_not_yet_projected");
    }

    private static string? PreviewSpecialItem(MineShaft mine, int x, int y)
    {
        var level = mine.mineLevel;
        var random = Utility.CreateRandom(level, Game1.stats.DaysPlayed, x, (double)y * 9999.0);
        if (Game1.mine is null)
        {
            return "(O)388";
        }
        if (Game1.mine.GetAdditionalDifficulty() > 0)
        {
            if (random.NextDouble() < 0.02)
            {
                return "(BC)272";
            }
            return random.Next(7) switch
            {
                0 => "(W)61",
                1 => "(O)910",
                2 => "(O)913",
                3 => "(O)915",
                4 => "(O)527",
                5 => "(O)858",
                _ => null
            };
        }
        if (level < 20)
        {
            return new[] { "(W)16", "(W)24", "(B)504", "(B)505", "(O)516", "(O)518" }[random.Next(6)];
        }
        if (level < 40)
        {
            return new[] { "(W)22", "(W)24", "(B)504", "(B)505", "(O)516", "(O)518", "(W)15" }[random.Next(7)];
        }
        if (level < 60)
        {
            return new[] { "(W)6", "(W)26", "(W)15", "(B)510", "(O)517", "(O)519", "(W)27" }[random.Next(7)];
        }
        if (level < 80)
        {
            return new[] { "(W)26", "(W)27", "(B)508", "(B)510", "(O)517", "(O)519", "(W)19" }[random.Next(7)];
        }
        if (level < 100)
        {
            return new[] { "(W)48", "(W)48", "(B)511", "(B)513", "(W)18", "(W)28", "(W)52", "(W)3" }[random.Next(8)];
        }
        if (level < 120)
        {
            return new[] { "(W)19", "(W)50", "(B)511", "(B)513", "(W)18", "(W)46", "(O)887", "(W)3" }[random.Next(8)];
        }
        return new[] { "(W)45", "(W)50", "(B)511", "(B)513", "(W)18", "(W)28", "(W)52", "(O)787", "(B)878", "(O)856", "(O)859", "(O)887" }[random.Next(12)];
    }

    private static string[] PossibleSpecialItems(MineShaft mine)
    {
        if (Game1.mine is null)
        {
            return new[] { "(O)388" };
        }
        if (Game1.mine.GetAdditionalDifficulty() > 0)
        {
            return new[] { "(BC)272", "(W)61", "(O)910", "(O)913", "(O)915", "(O)527", "(O)858" };
        }
        var level = mine.mineLevel;
        if (level < 20)
        {
            return new[] { "(W)16", "(W)24", "(B)504", "(B)505", "(O)516", "(O)518" };
        }
        if (level < 40)
        {
            return new[] { "(W)22", "(W)24", "(B)504", "(B)505", "(O)516", "(O)518", "(W)15" };
        }
        if (level < 60)
        {
            return new[] { "(W)6", "(W)26", "(W)15", "(B)510", "(O)517", "(O)519", "(W)27" };
        }
        if (level < 80)
        {
            return new[] { "(W)26", "(W)27", "(B)508", "(B)510", "(O)517", "(O)519", "(W)19" };
        }
        if (level < 100)
        {
            return new[] { "(W)48", "(B)511", "(B)513", "(W)18", "(W)28", "(W)52", "(W)3" };
        }
        if (level < 120)
        {
            return new[] { "(W)19", "(W)50", "(B)511", "(B)513", "(W)18", "(W)46", "(O)887", "(W)3" };
        }
        return new[] { "(W)45", "(W)50", "(B)511", "(B)513", "(W)18", "(W)28", "(W)52", "(O)787", "(B)878", "(O)856", "(O)859", "(O)887" };
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

    private static string QualifyDropId(string itemId)
    {
        if (itemId.StartsWith("-", StringComparison.Ordinal) && int.TryParse(itemId, out var resourceType))
        {
            return Math.Abs(resourceType) switch
            {
                0 => "(O)378",
                2 => "(O)380",
                4 => "(O)382",
                6 => "(O)384",
                10 => "(O)386",
                12 => "(O)388",
                14 => "(O)390",
                var id => "(O)" + id
            };
        }
        return ItemRegistry.QualifyItemId(itemId) ?? (itemId.StartsWith("(", StringComparison.Ordinal) ? itemId : "(O)" + itemId);
    }

    private static string[] Ordered(IEnumerable<string> ids)
    {
        return ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }
}

internal sealed class MiningMonsterDropProjection
{
    public string[] SelectedBaseDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string[] GuaranteedDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string[] ConditionalDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string[] PossibleDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string CurrentDeathTilePreviewQualifiedItemId { get; set; } = string.Empty;

    public string PrimaryDropStatus { get; set; } = string.Empty;

    public string ItemIdentityCompleteness { get; set; } = string.Empty;

    public string[] UnresolvedDynamicRules { get; set; } = Array.Empty<string>();

    public string Source { get; set; } = string.Empty;
}
