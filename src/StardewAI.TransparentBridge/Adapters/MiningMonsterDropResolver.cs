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
        var guaranteedOneOf = new List<string[]>();
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
                guaranteedOneOf,
                primaryStatus,
                Game1.mine is null || Game1.mine.GetAdditionalDifficulty() <= 0
                    ? "complete_possible_identity_set_for_mineshaft_special_item"
                    : "partial_hard_mine_special_item_treasure_branch",
                unresolved);
            projection.CurrentDeathTilePreviewQualifiedItemId = special ?? string.Empty;
            projection.RuntimeExtraDropRuleInputs = ReadRuntimeExtraDropRuleInputs(monster, player);
            projection.RuntimeExtraDropRuleCompleteness = "not_executed_special_item_override";
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

        AddRuntimeTypeExtraDrops(monster, guaranteed, conditional, guaranteedOneOf, unresolved);
        if (pendantOverrideEligible)
        {
            conditional.UnionWith(guaranteed);
            guaranteed.Clear();
            foreach (var group in guaranteedOneOf)
            {
                conditional.UnionWith(group);
            }
            guaranteedOneOf.Clear();
        }
        AddBaseMonsterDropPossibilities(monster, player, conditional, unresolved);
        var result = Build(
            selected,
            guaranteed,
            conditional,
            guaranteedOneOf,
            primaryStatus,
            unresolved.Count == 0 ? "complete_possible_identity_set_for_vanilla_mineshaft_monster" : "partial_death_time_dynamic_sources",
            unresolved);
        result.RuntimeExtraDropRuleInputs = ReadRuntimeExtraDropRuleInputs(monster, player);
        result.RuntimeExtraDropRuleCompleteness = monster.GetType().Assembly == typeof(Monster).Assembly
            ? "complete_for_vanilla_runtime_type"
            : "partial_custom_runtime_type";
        return result;
    }

    private static MiningMonsterDropProjection Build(
        string[] selected,
        HashSet<string> guaranteed,
        HashSet<string> conditional,
        List<string[]> guaranteedOneOf,
        string primaryStatus,
        string completeness,
        List<string> unresolved)
    {
        return new MiningMonsterDropProjection
        {
            SelectedBaseDropQualifiedItemIds = selected,
            GuaranteedDropQualifiedItemIds = Ordered(guaranteed),
            ConditionalDropQualifiedItemIds = Ordered(conditional),
            GuaranteedOneOfQualifiedItemIdGroups = guaranteedOneOf.Select(Ordered).ToArray(),
            PossibleDropQualifiedItemIds = Ordered(guaranteed.Concat(conditional).Concat(guaranteedOneOf.SelectMany(group => group))),
            PrimaryDropStatus = primaryStatus,
            ItemIdentityCompleteness = completeness,
            UnresolvedDynamicRules = unresolved.Distinct(StringComparer.Ordinal).OrderBy(rule => rule, StringComparer.Ordinal).ToArray(),
            Source = "MineShaft.monsterDrop; GameLocation.monsterDrop; Monster.objectsToDrop/getExtraDropItems; MineShaft.getSpecialItemForThisMineLevel"
        };
    }

    private static void AddRuntimeTypeExtraDrops(
        Monster monster,
        HashSet<string> guaranteed,
        HashSet<string> conditional,
        List<string[]> guaranteedOneOf,
        List<string> unresolved)
    {
        switch (monster)
        {
            case Bat bat:
                AddBatExtraDrops(bat, conditional);
                break;
            case BigSlime bigSlime when bigSlime.heldItem.Value is not null:
                guaranteed.Add(bigSlime.heldItem.Value.QualifiedItemId);
                break;
            case Bug bug when bug.isArmoredBug.Value:
                conditional.Add("(O)874");
                break;
            case DinoMonster:
                guaranteedOneOf.Add(new[] { "(O)107", "(O)580", "(O)583", "(O)584" });
                break;
            case Ghost when Game1.player.team.SpecialOrderActive("Wizard") && !Game1.MasterPlayer.hasOrWillReceiveMail("ectoplasmDrop"):
                conditional.Add("(O)875");
                break;
            case GreenSlime slime:
                AddGreenSlimeExtraDrops(slime, guaranteed, conditional);
                break;
            case MetalHead metalHead when (Game1.stats.getMonstersKilled(metalHead.Name) + 1 + (int)Game1.uniqueIDForThisGame) % 100 == 0:
                guaranteed.Add("(H)51");
                break;
            case Mummy:
            case Serpent:
                conditional.Add("(O)485");
                break;
            case RockGolem rockGolem:
                AddRockGolemExtraDrops(rockGolem, conditional);
                break;
            case Skeleton:
                conditional.Add("(W)5");
                break;
            default:
                if (monster.GetType().Assembly != typeof(Monster).Assembly)
                {
                    unresolved.Add("custom_monster_getExtraDropItems_override_not_introspected");
                }
                break;
        }
    }

    private static void AddBatExtraDrops(Bat bat, HashSet<string> conditional)
    {
        if (!bat.hauntedSkull.Value)
        {
            return;
        }
        if (bat.cursedDoll.Value)
        {
            conditional.UnionWith(new[]
            {
                "(P)10", "(S)1004", "(S)1014", "(S)1263", "(S)1262", "(P)12", "(W)2", "(O)288",
                "(O)534", "(O)531", "(O)768", "(O)769", "(O)581", "(O)582", "(O)725", "(O)86",
                Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccVault") ? "(O)275" : "(O)749"
            });
        }
        if (Game1.IsWinter)
        {
            conditional.Add("(O)273");
        }
        conditional.Add("(M)CursedMannequinMale");
        conditional.Add("(M)CursedMannequinFemale");
        conditional.Add("(O)279");
    }

    private static void AddGreenSlimeExtraDrops(GreenSlime slime, HashSet<string> guaranteed, HashSet<string> conditional)
    {
        if (slime.prismatic.Value)
        {
            guaranteed.Add("(O)876");
            return;
        }

        var name = slime.Name;
        var color = slime.color.Value;
        if (!string.Equals(name, "Tiger Slime", StringComparison.Ordinal))
        {
            if (color.R >= 50 && color.R <= 100 && color.G >= 25 && color.G <= 50 && color.B <= 25)
            {
                guaranteed.Add("(O)388");
                conditional.Add("(O)709");
            }
            else if (color.R < 80 && color.G < 80 && color.B < 80)
            {
                guaranteed.Add("(O)382");
                conditional.Add("(O)553");
                conditional.Add("(O)539");
            }
            else if (color.R > 200 && color.G > 180 && color.B < 50)
            {
                guaranteed.Add("(O)384");
            }
            else if (color.R > 220 && color.G > 90 && color.G < 150 && color.B < 50)
            {
                guaranteed.Add("(O)378");
            }
            else if (color.R > 230 && color.G > 230 && color.B > 230)
            {
                guaranteed.Add(color.R % 2 == 1 ? "(O)338" : "(O)380");
                if (color.R % 2 == 0 && color.G % 2 == 0 && color.B % 2 == 0 || color.R == 255 && color.G == 255 && color.B == 255)
                {
                    guaranteed.Add("(O)72");
                }
            }
            else if (color.R > 150 && color.G > 150 && color.B > 150)
            {
                guaranteed.Add("(O)390");
            }
            else if (color.R > 150 && color.B > 180 && color.G < 50 && slime.specialNumber.Value % (slime.firstGeneration.Value ? 4 : 2) == 0)
            {
                guaranteed.Add("(O)386");
                if (slime.firstGeneration.Value)
                {
                    conditional.Add("(O)485");
                }
            }
        }

        if (Game1.MasterPlayer.mailReceived.Contains("slimeHutchBuilt") && slime.specialNumber.Value == 1)
        {
            var specialDrop = name switch
            {
                "Green Slime" => "(O)680",
                "Frost Jelly" => "(O)413",
                "Tiger Slime" => "(O)857",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(specialDrop))
            {
                guaranteed.Add(specialDrop);
            }
        }

        if (string.Equals(name, "Tiger Slime", StringComparison.Ordinal))
        {
            conditional.UnionWith(new[] { "(H)91", "(O)831", "(O)829", "(O)833", "(O)835" });
        }
    }

    private static void AddRockGolemExtraDrops(RockGolem golem, HashSet<string> conditional)
    {
        if (string.Equals(golem.Name, "Wilderness Golem", StringComparison.Ordinal))
        {
            conditional.Add("(H)40");
            if (Game1.IsSpring)
            {
                conditional.Add("(O)273");
            }
            return;
        }
        if (!string.Equals(golem.Name, "Iridium Golem", StringComparison.Ordinal))
        {
            return;
        }

        conditional.Add(CurrentRaccoonSeedQualifiedItemId());
        conditional.Add("(O)386");
        for (var i = 0; i < 5; i++)
        {
            conditional.Add("(O)SkillBook_" + i);
        }
        conditional.Add("(O)527");
        conditional.Add("(H)40");
    }

    private static string CurrentRaccoonSeedQualifiedItemId()
    {
        var season = Game1.season;
        if (Game1.dayOfMonth > (season == Season.Spring ? 23 : 20))
        {
            season = (Season)(((int)season + 1) % 4);
        }
        return season switch
        {
            Season.Spring => "(O)CarrotSeeds",
            Season.Summer => "(O)SummerSquashSeeds",
            Season.Fall => "(O)BroccoliSeeds",
            _ => "(O)PowdermelonSeeds"
        };
    }

    private static object ReadRuntimeExtraDropRuleInputs(Monster monster, Farmer player)
    {
        return monster switch
        {
            Bat bat => new { runtime_type = "Bat", haunted_skull = bat.hauntedSkull.Value, cursed_doll = bat.cursedDoll.Value, is_winter = Game1.IsWinter, cc_vault_complete = Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccVault") },
            BigSlime bigSlime => new { runtime_type = "BigSlime", held_item_qualified_id = bigSlime.heldItem.Value?.QualifiedItemId ?? string.Empty },
            Bug bug => new { runtime_type = "Bug", is_armored_bug = bug.isArmoredBug.Value },
            DinoMonster => new { runtime_type = "DinoMonster", guaranteed_one_of = true },
            Ghost => new { runtime_type = "Ghost", wizard_order_active = player.team.SpecialOrderActive("Wizard"), ectoplasm_already_received = Game1.MasterPlayer.hasOrWillReceiveMail("ectoplasmDrop") },
            GreenSlime slime => new { runtime_type = "GreenSlime", name = slime.Name, color_r = slime.color.R, color_g = slime.color.G, color_b = slime.color.B, special_number = slime.specialNumber.Value, first_generation = slime.firstGeneration.Value, prismatic = slime.prismatic.Value, slime_hutch_built = Game1.MasterPlayer.mailReceived.Contains("slimeHutchBuilt") },
            MetalHead metalHead => new { runtime_type = "MetalHead", prior_kills = Game1.stats.getMonstersKilled(metalHead.Name), projected_kills_at_drop = Game1.stats.getMonstersKilled(metalHead.Name) + 1, unique_game_id = Game1.uniqueIDForThisGame },
            RockGolem golem => new { runtime_type = "RockGolem", name = golem.Name, season = Game1.season.ToString(), day_of_month = Game1.dayOfMonth, projected_raccoon_seed = CurrentRaccoonSeedQualifiedItemId() },
            Mummy => new { runtime_type = "Mummy" },
            Serpent => new { runtime_type = "Serpent" },
            Skeleton => new { runtime_type = "Skeleton" },
            _ => new { runtime_type = monster.GetType().FullName ?? monster.GetType().Name, vanilla_base_implementation = monster.GetType().Assembly == typeof(Monster).Assembly }
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
        if (player.stats.MonstersKilled + 1 > 10)
        {
            conditional.Add("(O)Book_Void");
        }
        if (Game1.netWorldState.Value.GoldenWalnutsFound >= 100 && monster.isHardModeMonster.Value)
        {
            conditional.Add("(O)858");
            if (Game1.stats.Get("hardModeMonstersKilled") + 1 > 50)
            {
                conditional.Add("(O)896");
            }
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

        if (player.stats.Get("trinketSlots") != 0)
        {
            conditional.UnionWith(DataLoader.Trinkets(Game1.content)
                .Where(pair => pair.Value.DropsNaturally)
                .Select(pair => "(TR)" + pair.Key));
        }
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

    public string[][] GuaranteedOneOfQualifiedItemIdGroups { get; set; } = Array.Empty<string[]>();

    public string[] PossibleDropQualifiedItemIds { get; set; } = Array.Empty<string>();

    public string CurrentDeathTilePreviewQualifiedItemId { get; set; } = string.Empty;

    public object RuntimeExtraDropRuleInputs { get; set; } = new { };

    public string RuntimeExtraDropRuleCompleteness { get; set; } = string.Empty;

    public string PrimaryDropStatus { get; set; } = string.Empty;

    public string ItemIdentityCompleteness { get; set; } = string.Empty;

    public string[] UnresolvedDynamicRules { get; set; } = Array.Empty<string>();

    public string Source { get; set; } = string.Empty;
}
