using StardewValley;
using System.Globalization;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace StardewAI.TransparentBridge.Adapters;

internal static partial class MiningMonsterDropResolver
{
    private static void AddBaseMonsterDropPossibilities(
        Monster monster,
        Farmer player,
        HashSet<string> conditional,
        HashSet<string> conditionalCatalogKeys)
    {
        if (player.isWearingRing("526") && DataLoader.Monsters(Game1.content).TryGetValue(monster.Name, out var data))
        {
            var fields = data.Split('/');
            if (fields.Length > 6)
            {
                var dropTokens = ArgUtility.SplitBySpace(fields[6]);
                for (var i = 0; i + 1 < dropTokens.Length; i += 2)
                {
                    if (TryReadDataChance(dropTokens[i + 1], out var chance) && EffectiveChance(chance) > 0d)
                    {
                        conditional.Add(QualifyDropId(dropTokens[i]));
                    }
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
            conditionalCatalogKeys.Add(RandomCosmeticCatalogKey);
        }

        if (player.stats.Get("trinketSlots") != 0)
        {
            conditionalCatalogKeys.Add(NaturalTrinketCatalogKey);
        }
    }

    private static MiningMonsterDropProbabilityRule[] SpecialItemProbabilityRules(string? special)
    {
        return string.IsNullOrWhiteSpace(special)
            ? new[]
            {
                ProbabilityRule(
                    "mine_special_item_treasure_catalog",
                    Array.Empty<string>(),
                    HardMineTreasureCatalogKey,
                    1d,
                    1d,
                    null,
                    "current_death_tile_global_rng_catalog_selection_not_consumed",
                    "MineShaft.monsterDrop/getSpecialItemForThisMineLevel")
            }
            : new[]
            {
                ProbabilityRule(
                    "mine_special_item_current_death_tile",
                    new[] { special! },
                    string.Empty,
                    1d,
                    1d,
                    1d,
                    "fixed_current_death_tile",
                    "MineShaft.monsterDrop/getSpecialItemForThisMineLevel")
            };
    }

    private static MiningMonsterDropProbabilityRule[] ReadBaseProbabilityRules(
        Monster monster,
        Farmer player,
        string[] selected,
        bool pendantOverrideEligible,
        double baseBranchChance)
    {
        var rules = new List<MiningMonsterDropProbabilityRule>();
        if (selected.Length > 0)
        {
            var selectedRule = ProbabilityRule(
                "base_selected_drops",
                selected,
                string.Empty,
                1d,
                baseBranchChance,
                baseBranchChance,
                "all_selected_identities_emitted_when_branch_runs",
                "MineShaft.monsterDrop/GameLocation.monsterDrop");
            selectedRule.ExpectedQuantityPerKill = null;
            selectedRule.QuantityStatus = "selected_identity_multiplicity_not_projected";
            selectedRule.BookVoidDuplicationEligible = true;
            rules.Add(selectedRule);
        }
        if (pendantOverrideEligible)
        {
            rules.Add(ProbabilityRule(
                "rare_krobus_pendant_override",
                new[] { "(O)808" },
                string.Empty,
                0.001d,
                0.001d,
                0.001d,
                "fixed_identity_replaces_base_branch",
                "MineShaft.monsterDrop"));
        }

        AddRuntimeTypeProbabilityRules(monster, player, baseBranchChance, rules);
        AddMonsterDataRingProbabilityRules(monster, player, baseBranchChance, rules);
        ApplyBookVoidLootDuplication(player, rules);

        if (HasUnseenSecretNote(player))
        {
            AddFixedProbabilityRule(rules, "unseen_secret_note", "(O)79", 0.033d, baseBranchChance, "GameLocation.monsterDrop");
        }
        if (Game1.MasterPlayer.mailReceived.Contains("sawQiPlane"))
        {
            var baseChance = 0.01d + player.team.AverageDailyLuck() / 10d + player.LuckLevel * 0.008d;
            var mysteryChance = EffectiveChance(baseChance * (player.stats.Get("Book_Mystery") == 0 ? 0.66d : 0.88d));
            AddFixedProbabilityRule(
                rules,
                "mystery_box",
                player.stats.Get(StatKeys.Mastery(2)) != 0 ? "(O)GoldenMysteryBox" : "(O)MysteryBox",
                mysteryChance,
                baseBranchChance,
                "GameLocation.monsterDrop/Utility.tryRollMysteryBox");
        }
        if (player.stats.MonstersKilled + 1 > 10)
        {
            var voidChance = 0.0001d + (!player.mailReceived.Contains("voidBookDropped")
                ? (player.stats.MonstersKilled + 1) * 0.000015d
                : 0.0004d);
            AddFixedProbabilityRule(rules, "book_void", "(O)Book_Void", EffectiveChance(voidChance), baseBranchChance, "GameLocation.monsterDrop");
        }
        if (Game1.netWorldState.Value.GoldenWalnutsFound >= 100 && monster.isHardModeMonster.Value)
        {
            var hardModeKillsAtDrop = Game1.stats.Get("hardModeMonstersKilled") + 1;
            var radioactiveChance = EffectiveChance(0.008d + player.LuckLevel * 0.002d);
            if (hardModeKillsAtDrop > 50)
            {
                var soulChance = EffectiveChance(0.001d + player.LuckLevel * 0.0002d);
                AddFixedProbabilityRule(rules, "galaxy_soul", "(O)896", soulChance, baseBranchChance, "GameLocation.monsterDrop");
                AddFixedProbabilityRule(rules, "radioactive_ore_else_if", "(O)858", (1d - soulChance) * radioactiveChance, baseBranchChance, "GameLocation.monsterDrop");
            }
            else
            {
                AddFixedProbabilityRule(rules, "radioactive_ore", "(O)858", radioactiveChance, baseBranchChance, "GameLocation.monsterDrop");
            }
        }

        var rareObjectLuckMultiplier = 1d + player.team.AverageDailyLuck();
        if (player.stats.Get(StatKeys.Mastery(0)) != 0)
        {
            AddFixedProbabilityRule(rules, "rare_golden_animal_cracker", "(O)GoldenAnimalCracker", EffectiveChance(0.0015d * rareObjectLuckMultiplier), baseBranchChance, "Utility.trySpawnRareObject(chanceModifier=1.5)");
        }
        if (Game1.stats.DaysPlayed > 2)
        {
            var cosmeticEventChance = 0.003d;
            rules.Add(ProbabilityRule(
                "rare_random_cosmetic",
                Array.Empty<string>(),
                RandomCosmeticCatalogKey,
                cosmeticEventChance,
                baseBranchChance * cosmeticEventChance,
                null,
                "weighted_catalog_selection_with_runtime_error_item_fallback",
                "Utility.trySpawnRareObject/getRandomCosmeticItem"));

            var skillBookIds = Enumerable.Range(0, 5).Select(index => "(O)SkillBook_" + index).ToArray();
            var skillBookEventChance = 0.0009d;
            rules.Add(ProbabilityRule(
                "rare_skill_book",
                skillBookIds,
                string.Empty,
                skillBookEventChance,
                baseBranchChance * skillBookEventChance,
                baseBranchChance * skillBookEventChance / skillBookIds.Length,
                "uniform_identity_selection",
                "Utility.trySpawnRareObject"));
        }

        if (player.stats.Get("trinketSlots") != 0)
        {
            var trinketChance = 0.004d + monster.MaxHealth * 0.00001d;
            if (monster.isGlider.Value && monster.MaxHealth >= 150)
            {
                trinketChance += 0.002d;
            }
            if (monster is Leaper)
            {
                trinketChance -= 0.005d;
            }
            trinketChance = Math.Min(0.025d, trinketChance);
            trinketChance += player.DailyLuck / 25d;
            trinketChance += player.LuckLevel * 0.00133d;
            trinketChance = EffectiveChance(trinketChance);
            var trinketCount = NaturalTrinketQualifiedItemIds().Length;
            rules.Add(ProbabilityRule(
                "natural_trinket",
                Array.Empty<string>(),
                NaturalTrinketCatalogKey,
                trinketChance,
                baseBranchChance * trinketChance,
                trinketCount > 0 ? baseBranchChance * trinketChance / trinketCount : null,
                trinketCount > 0 ? "uniform_active_catalog_identity_selection" : "active_catalog_empty",
                "GameLocation.monsterDrop/Trinket.TrySpawnTrinket/GetRandomTrinket"));
        }

        return rules.ToArray();
    }

    private static void AddRuntimeTypeProbabilityRules(
        Monster monster,
        Farmer player,
        double baseBranchChance,
        List<MiningMonsterDropProbabilityRule> rules)
    {
        var calls = player.isWearingRing("526") ? 2 : 1;
        switch (monster)
        {
            case Bat bat:
                AddBatProbabilityRules(bat, baseBranchChance, calls, rules);
                break;
            case BigSlime bigSlime when bigSlime.heldItem.Value is not null:
                AddRepeatedIdentityRule(rules, "big_slime_held_item", bigSlime.heldItem.Value.QualifiedItemId, 1d, baseBranchChance, calls, Math.Max(1, bigSlime.heldItem.Value.Stack), "deterministic_held_item_per_call", "BigSlime.getExtraDropItems");
                break;
            case Bug bug when bug.isArmoredBug.Value:
                AddRepeatedIdentityRule(rules, "armored_bug_bug_meat", "(O)874", 0.1d, baseBranchChance, calls, 1d, "independent_roll_per_call", "Bug.getExtraDropItems");
                break;
            case DinoMonster:
                AddDinoProbabilityRules(baseBranchChance, calls, rules);
                break;
            case Ghost when player.team.SpecialOrderActive("Wizard") && !Game1.MasterPlayer.hasOrWillReceiveMail("ectoplasmDrop"):
                AddRepeatedIdentityRule(rules, "ghost_ectoplasm", "(O)875", 0.095d, baseBranchChance, calls, 1d, "independent_roll_per_call", "Ghost.getExtraDropItems");
                break;
            case GreenSlime slime:
                AddGreenSlimeProbabilityRules(slime, baseBranchChance, calls, rules);
                break;
            case MetalHead metalHead when (Game1.stats.getMonstersKilled(metalHead.Name) + 1 + (int)Game1.uniqueIDForThisGame) % 100 == 0:
                AddRepeatedIdentityRule(rules, "metal_head_living_hat_counter", "(H)51", 1d, baseBranchChance, calls, 1d, "deterministic_projected_post_kill_counter", "MetalHead.getExtraDropItems");
                break;
            case Mummy:
                AddRepeatedIdentityRule(rules, "mummy_cloth", "(O)485", 0.002d, baseBranchChance, calls, 1d, "independent_roll_per_call", "Mummy.getExtraDropItems");
                break;
            case Serpent:
                AddRepeatedIdentityRule(rules, "serpent_cloth", "(O)485", 0.002d, baseBranchChance, calls, 1d, "independent_roll_per_call", "Serpent.getExtraDropItems");
                break;
            case RockGolem golem:
                AddRockGolemProbabilityRules(golem, baseBranchChance, calls, rules);
                break;
            case Skeleton:
                AddRepeatedIdentityRule(rules, "skeleton_bone_sword", "(W)5", 0.04d, baseBranchChance, calls, 1d, "independent_roll_per_call", "Skeleton.getExtraDropItems");
                break;
        }
    }

    private static void AddBatProbabilityRules(
        Bat bat,
        double baseBranchChance,
        int calls,
        List<MiningMonsterDropProbabilityRule> rules)
    {
        if (!bat.hauntedSkull.Value)
        {
            return;
        }

        var cursedBranchChance = bat.cursedDoll.Value ? 0.1429d : 0d;
        if (cursedBranchChance > 0d)
        {
            var clothingChance = cursedBranchChance / 11d / 6d;
            foreach (var id in new[] { "(P)10", "(S)1004", "(S)1014", "(S)1263", "(S)1262", "(P)12" })
            {
                AddRepeatedIdentityRule(rules, "haunted_skull_cursed_clothing_" + id, id, clothingChance, baseBranchChance, calls, 1d, "mutually_exclusive_cursed_branch_selection", "Bat.getExtraDropItems");
            }
            var caseChance = cursedBranchChance / 11d;
            foreach (var id in new[] { "(W)2", "(O)288", "(O)534", "(O)531", "(O)581", "(O)582", "(O)725", "(O)86" })
            {
                AddRepeatedIdentityRule(rules, "haunted_skull_cursed_item_" + id, id, caseChance, baseBranchChance, calls, 1d, "mutually_exclusive_cursed_branch_selection", "Bat.getExtraDropItems");
            }
            AddRepeatedIdentityRule(rules, "haunted_skull_cursed_solar_essence", "(O)768", caseChance, baseBranchChance, calls, 1d / 0.67d, "cursed_branch_geometric_quantity_continue_0.33", "Bat.getExtraDropItems");
            AddRepeatedIdentityRule(rules, "haunted_skull_cursed_void_essence", "(O)769", caseChance, baseBranchChance, calls, 1d / 0.67d, "cursed_branch_geometric_quantity_continue_0.33", "Bat.getExtraDropItems");
            AddRepeatedIdentityRule(
                rules,
                "haunted_skull_cursed_vault_reward",
                Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccVault") ? "(O)275" : "(O)749",
                caseChance,
                baseBranchChance,
                calls,
                1d,
                "mutually_exclusive_cursed_branch_current_mail_gate",
                "Bat.getExtraDropItems");
        }

        var ordinaryBranchChance = 1d - cursedBranchChance;
        if (Game1.IsWinter)
        {
            AddRepeatedIdentityRule(rules, "haunted_skull_winter_root", "(O)273", ordinaryBranchChance * 0.25d, baseBranchChance, calls, 1d / 0.6d, "after_cursed_branch_geometric_quantity_continue_0.4", "Bat.getExtraDropItems");
        }
        AddRepeatedIdentityRule(rules, "haunted_skull_mannequin_male", "(M)CursedMannequinMale", ordinaryBranchChance * 0.005d, baseBranchChance, calls, 1d, "after_cursed_branch_uniform_gender", "Bat.getExtraDropItems");
        AddRepeatedIdentityRule(rules, "haunted_skull_mannequin_female", "(M)CursedMannequinFemale", ordinaryBranchChance * 0.005d, baseBranchChance, calls, 1d, "after_cursed_branch_uniform_gender", "Bat.getExtraDropItems");
        AddRepeatedIdentityRule(rules, "haunted_skull_strange_doll", "(O)279", ordinaryBranchChance * 0.001502d, baseBranchChance, calls, 1d, "after_cursed_branch_independent_roll", "Bat.getExtraDropItems");
    }

    private static void AddDinoProbabilityRules(double baseBranchChance, int calls, List<MiningMonsterDropProbabilityRule> rules)
    {
        const double eggChance = 0.10000000149011612d;
        AddRepeatedIdentityRule(rules, "dino_egg", "(O)107", eggChance, baseBranchChance, calls, 1d, "mutually_exclusive_get_extra_result", "DinoMonster.getExtraDropItems");
        var alternativeChance = (1d - eggChance) / 3d;
        foreach (var id in new[] { "(O)580", "(O)583", "(O)584" })
        {
            AddRepeatedIdentityRule(rules, "dino_alternative_" + id, id, alternativeChance, baseBranchChance, calls, 1d, "mutually_exclusive_uniform_else_selection", "DinoMonster.getExtraDropItems");
        }
    }

    private static void AddGreenSlimeProbabilityRules(
        GreenSlime slime,
        double baseBranchChance,
        int calls,
        List<MiningMonsterDropProbabilityRule> rules)
    {
        if (slime.prismatic.Value)
        {
            AddRepeatedIdentityRule(rules, "prismatic_slime_jelly", "(O)876", 1d, baseBranchChance, calls, 1d, "return_replaces_all_preceding_extra_items", "GreenSlime.getExtraDropItems");
            return;
        }

        var name = slime.Name;
        var color = slime.color.Value;
        if (!string.Equals(name, "Tiger Slime", StringComparison.Ordinal))
        {
            if (color.R >= 50 && color.R <= 100 && color.G >= 25 && color.G <= 50 && color.B <= 25)
            {
                AddRepeatedIdentityRule(rules, "slime_brown_wood", "(O)388", 1d, baseBranchChance, calls, 4.5d, "uniform_stack_3_to_6", "GreenSlime.getExtraDropItems");
                AddRepeatedIdentityRule(rules, "slime_brown_hardwood", "(O)709", 0.1d, baseBranchChance, calls, 1d, "independent_roll_per_call", "GreenSlime.getExtraDropItems");
            }
            else if (color.R < 80 && color.G < 80 && color.B < 80)
            {
                AddRepeatedIdentityRule(rules, "slime_black_coal", "(O)382", 1d, baseBranchChance, calls, 1d, "deterministic_color_branch", "GreenSlime.getExtraDropItems");
                var seeded = Utility.CreateRandom((double)slime.Position.X * 777d, (double)slime.Position.Y * 77d, Game1.stats.DaysPlayed);
                var neptuniteAtCurrentPosition = seeded.NextDouble() < 0.05d;
                var bixiteAtCurrentPosition = seeded.NextDouble() < 0.05d;
                AddRepeatedIdentityRule(rules, "slime_black_neptunite", "(O)553", neptuniteAtCurrentPosition ? 1d : 0d, baseBranchChance, calls, 1d, "fixed_current_position_seed_preview_recomputed_each_call", "GreenSlime.getExtraDropItems");
                AddRepeatedIdentityRule(rules, "slime_black_bixite", "(O)539", bixiteAtCurrentPosition ? 1d : 0d, baseBranchChance, calls, 1d, "fixed_current_position_seed_preview_recomputed_each_call", "GreenSlime.getExtraDropItems");
            }
            else if (color.R > 200 && color.G > 180 && color.B < 50)
            {
                AddRepeatedIdentityRule(rules, "slime_yellow_gold_ore", "(O)384", 1d, baseBranchChance, calls, 2d, "deterministic_color_branch", "GreenSlime.getExtraDropItems");
            }
            else if (color.R > 220 && color.G > 90 && color.G < 150 && color.B < 50)
            {
                AddRepeatedIdentityRule(rules, "slime_red_copper_ore", "(O)378", 1d, baseBranchChance, calls, 2d, "deterministic_color_branch", "GreenSlime.getExtraDropItems");
            }
            else if (color.R > 230 && color.G > 230 && color.B > 230)
            {
                if (color.R % 2 == 1)
                {
                    AddRepeatedIdentityRule(rules, "slime_white_refined_quartz", "(O)338", 1d, baseBranchChance, calls, color.G % 2 == 1 ? 2d : 1d, "deterministic_color_parity_quantity", "GreenSlime.getExtraDropItems");
                }
                else
                {
                    AddRepeatedIdentityRule(rules, "slime_white_iron_ore", "(O)380", 1d, baseBranchChance, calls, 1d, "deterministic_color_parity", "GreenSlime.getExtraDropItems");
                }
                if (color.R % 2 == 0 && color.G % 2 == 0 && color.B % 2 == 0 || color.Equals(Microsoft.Xna.Framework.Color.White))
                {
                    AddRepeatedIdentityRule(rules, "slime_white_diamond", "(O)72", 1d, baseBranchChance, calls, 1d, "deterministic_color_parity", "GreenSlime.getExtraDropItems");
                }
            }
            else if (color.R > 150 && color.G > 150 && color.B > 150)
            {
                AddRepeatedIdentityRule(rules, "slime_gray_stone", "(O)390", 1d, baseBranchChance, calls, 2d, "deterministic_color_branch", "GreenSlime.getExtraDropItems");
            }
            else if (color.R > 150 && color.B > 180 && color.G < 50 && slime.specialNumber.Value % (slime.firstGeneration.Value ? 4 : 2) == 0)
            {
                AddRepeatedIdentityRule(rules, "slime_purple_iridium_ore", "(O)386", 1d, baseBranchChance, calls, 2d, "deterministic_color_and_special_number_branch", "GreenSlime.getExtraDropItems");
                if (slime.firstGeneration.Value)
                {
                    AddRepeatedIdentityRule(rules, "slime_purple_cloth", "(O)485", 0.005d, baseBranchChance, calls, 1d, "independent_roll_per_call", "GreenSlime.getExtraDropItems");
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
                AddRepeatedIdentityRule(rules, "slime_hutch_special_drop", specialDrop, 1d, baseBranchChance, calls, 1d, "deterministic_name_and_special_number", "GreenSlime.getExtraDropItems");
            }
        }

        if (string.Equals(name, "Tiger Slime", StringComparison.Ordinal))
        {
            AddRepeatedIdentityRule(rules, "tiger_slime_hat", "(H)91", 0.001d, baseBranchChance, calls, 1d, "independent_roll_before_else_if_chain", "GreenSlime.getExtraDropItems");
            AddRepeatedIdentityRule(rules, "tiger_slime_pineapple_seeds", "(O)831", 0.1d, baseBranchChance, calls, 2d, "else_if_chain_first_geometric_quantity_continue_0.5", "GreenSlime.getExtraDropItems");
            AddRepeatedIdentityRule(rules, "tiger_slime_mango_sapling", "(O)829", 0.9d * 0.1d, baseBranchChance, calls, 1d, "else_if_chain_second", "GreenSlime.getExtraDropItems");
            AddRepeatedIdentityRule(rules, "tiger_slime_taro_tuber", "(O)833", 0.9d * 0.9d * 0.02d, baseBranchChance, calls, 2d, "else_if_chain_third_geometric_quantity_continue_0.5", "GreenSlime.getExtraDropItems");
            AddRepeatedIdentityRule(rules, "tiger_slime_ginger", "(O)835", 0.9d * 0.9d * 0.98d * 0.006d, baseBranchChance, calls, 1d, "else_if_chain_fourth", "GreenSlime.getExtraDropItems");
        }
    }

    private static void AddRockGolemProbabilityRules(
        RockGolem golem,
        double baseBranchChance,
        int calls,
        List<MiningMonsterDropProbabilityRule> rules)
    {
        if (string.Equals(golem.Name, "Wilderness Golem", StringComparison.Ordinal))
        {
            const double hatChance = 0.0001d;
            AddRepeatedIdentityRule(rules, "wilderness_golem_hat", "(H)40", hatChance, baseBranchChance, calls, 1d, "early_return_first_roll", "RockGolem.getExtraDropItems");
            if (Game1.IsSpring)
            {
                AddRepeatedIdentityRule(rules, "wilderness_golem_rice_shoot", "(O)273", (1d - hatChance) * 0.0825d, baseBranchChance, calls, 3.5d, "after_hat_failure_uniform_quantity_2_to_5", "RockGolem.getExtraDropItems");
            }
            return;
        }
        if (!string.Equals(golem.Name, "Iridium Golem", StringComparison.Ordinal))
        {
            return;
        }

        AddRepeatedIdentityRule(rules, "iridium_golem_raccoon_seed", CurrentRaccoonSeedQualifiedItemId(), 0.5d, baseBranchChance, calls, 2d, "geometric_zero_or_more_continue_0.5", "RockGolem.getExtraDropItems");
        AddRepeatedIdentityRule(rules, "iridium_golem_iridium_ore", "(O)386", 0.2d, baseBranchChance, calls, 1.25d, "geometric_zero_or_more_continue_0.2", "RockGolem.getExtraDropItems");
        foreach (var index in Enumerable.Range(0, 5))
        {
            AddRepeatedIdentityRule(rules, "iridium_golem_skill_book_" + index, "(O)SkillBook_" + index, 0.002d, baseBranchChance, calls, 1d, "event_0.01_uniform_identity_selection", "RockGolem.getExtraDropItems");
        }
        AddRepeatedIdentityRule(rules, "iridium_golem_ring", "(O)527", 0.001d, baseBranchChance, calls, 1d, "independent_roll_per_call", "RockGolem.getExtraDropItems");
        AddRepeatedIdentityRule(rules, "iridium_golem_hat", "(H)40", 0.0002d, baseBranchChance, calls, 1d, "independent_roll_per_call", "RockGolem.getExtraDropItems");
    }

    private static void AddRepeatedIdentityRule(
        List<MiningMonsterDropProbabilityRule> rules,
        string key,
        string qualifiedItemId,
        double identityChancePerCall,
        double baseBranchChance,
        int calls,
        double meanQuantityWhenIdentityOccursPerCall,
        string itemSelectionStatus,
        string source)
    {
        identityChancePerCall = EffectiveChance(identityChancePerCall);
        var atLeastOncePerKill = baseBranchChance * (1d - Math.Pow(1d - identityChancePerCall, calls));
        var rule = ProbabilityRule(
            key,
            new[] { qualifiedItemId },
            string.Empty,
            identityChancePerCall,
            atLeastOncePerKill,
            atLeastOncePerKill,
            itemSelectionStatus,
            source);
        rule.CallsPerBaseBranch = calls;
        rule.ExpectedEventsPerKill = baseBranchChance * calls * identityChancePerCall;
        rule.ExpectedQuantityPerKill = baseBranchChance * calls * identityChancePerCall * meanQuantityWhenIdentityOccursPerCall;
        rule.QuantityStatus = "exact_mean_from_decompiled_branch";
        rule.BookVoidDuplicationEligible = true;
        rules.Add(rule);
    }

    private static void ApplyBookVoidLootDuplication(Farmer player, List<MiningMonsterDropProbabilityRule> rules)
    {
        if (player.stats.Get("Book_Void") == 0)
        {
            return;
        }
        foreach (var rule in rules.Where(rule => rule.BookVoidDuplicationEligible))
        {
            if (rule.ExpectedQuantityPerKill.HasValue)
            {
                rule.ExpectedQuantityPerKill *= 1.03d;
            }
            rule.QuantityStatus += ";book_void_expected_duplication_factor_1.03";
        }
    }

    private static void AddMonsterDataRingProbabilityRules(
        Monster monster,
        Farmer player,
        double baseBranchChance,
        List<MiningMonsterDropProbabilityRule> rules)
    {
        if (!player.isWearingRing("526") || !DataLoader.Monsters(Game1.content).TryGetValue(monster.Name, out var data))
        {
            return;
        }
        var fields = data.Split('/');
        if (fields.Length <= 6)
        {
            return;
        }
        var tokens = ArgUtility.SplitBySpace(fields[6]);
        for (var i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (!TryReadDataChance(tokens[i + 1], out var chance))
            {
                continue;
            }
            chance = EffectiveChance(chance);
            var rule = ProbabilityRule(
                "burglar_ring_monster_data_" + (i / 2),
                new[] { QualifyDropId(tokens[i]) },
                string.Empty,
                chance,
                baseBranchChance * chance,
                baseBranchChance * chance,
                "fixed_identity_independent_data_roll",
                "GameLocation.monsterDrop/Data/Monsters");
            rule.BookVoidDuplicationEligible = true;
            rules.Add(rule);
        }
    }

    private static void AddFixedProbabilityRule(
        List<MiningMonsterDropProbabilityRule> rules,
        string key,
        string qualifiedItemId,
        double eventChance,
        double baseBranchChance,
        string source)
    {
        rules.Add(ProbabilityRule(
            key,
            new[] { qualifiedItemId },
            string.Empty,
            eventChance,
            baseBranchChance * eventChance,
            baseBranchChance * eventChance,
            "fixed_identity",
            source));
    }

    private static MiningMonsterDropProbabilityRule ProbabilityRule(
        string key,
        string[] qualifiedItemIds,
        string catalogKey,
        double eventChance,
        double effectivePerKillChance,
        double? perIdentityChance,
        string itemSelectionStatus,
        string source)
    {
        return new MiningMonsterDropProbabilityRule
        {
            Key = key,
            QualifiedItemIds = Ordered(qualifiedItemIds),
            CatalogKey = catalogKey,
            EventChance = eventChance,
            EffectivePerKillChance = effectivePerKillChance,
            PerIdentityChance = perIdentityChance,
            CallsPerBaseBranch = 1,
            ExpectedEventsPerKill = effectivePerKillChance,
            ExpectedQuantityPerKill = perIdentityChance,
            QuantityStatus = perIdentityChance.HasValue ? "single_item_per_identity_event" : "not_projected",
            ProbabilityStatus = "exact_current_state_formula",
            ItemSelectionStatus = itemSelectionStatus,
            Source = source
        };
    }

    private static double EffectiveChance(double chance)
    {
        return Math.Clamp(chance, 0d, 1d);
    }

    private static bool TryReadDataChance(string value, out double chance)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out chance) ||
            double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out chance);
    }

}
