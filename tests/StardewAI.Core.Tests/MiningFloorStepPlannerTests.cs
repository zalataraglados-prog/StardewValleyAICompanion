using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class MiningFloorStepPlannerTests
{
    [Fact]
    public void ReachableLadderAlwaysPrecedesMiningAndCombat()
    {
        var plan = Plan(
            ladders: "[{\"tile_x\":4,\"tile_y\":2}]",
            objects: "[{\"tile_x\":2,\"tile_y\":2,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1,\"ladder_preview\":{\"creates_ladder\":true}}]",
            monsters: "[{\"tile_x\":3,\"tile_y\":3}]");

        Assert.Equal("ready", plan.Status);
        Assert.Equal(MiningFloorStepKinds.DescendLadder, plan.StepKind);
        Assert.Equal(4, plan.TargetTileX);
        Assert.Equal("reachable_ladder_available", plan.Reason);
        Assert.Equal("executor.descend_ladder", MiningFloorStepCompiler.ExecutionOptionId(plan));
    }

    [Fact]
    public void SafeShaftPrecedesLadderAndCarriesExactPreview()
    {
        var plan = Plan(
            ladders: "[{\"tile_x\":5,\"tile_y\":2}]",
            shafts: "[{\"tile_x\":4,\"tile_y\":2,\"expected_level_delta\":7,\"expected_mine_level_after\":128,\"expected_health_cost\":21,\"expected_health_after\":79}]",
            mineKind: "skull_cavern");

        Assert.Equal(MiningFloorStepKinds.DescendShaft, plan.StepKind);
        Assert.Equal("executor.descend_shaft", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Equal(7, plan.ExpectedMineLevelDelta);
        Assert.Equal(128, plan.ExpectedMineLevelAfter);
        Assert.Equal(21, plan.ExpectedHealthCost);
        Assert.Equal(79, plan.ExpectedHealthAfter);
        Assert.Equal("shaft_health_reserve_verified", plan.SafetyWindowStatus);
        Assert.Contains(MiningFloorStepCompiler.BuildExecutionParameters(plan), parameter =>
            parameter.Name == "expected_mine_level_delta" && parameter.Value == "7");
    }

    [Fact]
    public void OrdinaryMineRejectsMalformedShaftCandidateAndUsesLadder()
    {
        var plan = Plan(
            ladders: "[{\"tile_x\":5,\"tile_y\":2}]",
            shafts: "[{\"tile_x\":4,\"tile_y\":2,\"expected_level_delta\":7,\"expected_mine_level_after\":47,\"expected_health_cost\":21,\"expected_health_after\":79}]",
            mineKind: "ordinary_mines");

        Assert.Equal(MiningFloorStepKinds.DescendLadder, plan.StepKind);
        Assert.Equal("reachable_ladder_available", plan.Reason);
    }

    [Fact]
    public void ReachedLatestExitTimeSelectsNativeMineExitBeforeOtherWork()
    {
        var plan = Plan(
            exits: "[{\"tile_x\":4,\"tile_y\":2,\"expected_destination\":{\"location_id\":\"Mine\",\"tile_x\":23,\"tile_y\":8}}]",
            objects: "[{\"tile_x\":2,\"tile_y\":2,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1}]",
            resources: "{\"health\":100,\"max_health\":100,\"energy\":100,\"current_time\":1800,\"selected_slot_index\":4,\"food_slots\":[]}",
            objective: new MiningFloorObjective { LatestExitTime = 1800 });

        Assert.Equal(MiningFloorStepKinds.ExitMine, plan.StepKind);
        Assert.Equal("executor.exit_mine", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Contains("latest_exit_time_reached", plan.Reason, StringComparison.Ordinal);
        Assert.Equal("Mine", plan.ExpectedTargetLocation);
        Assert.Equal(23, plan.ExpectedArrivalTileX);
        Assert.Equal(8, plan.ExpectedArrivalTileY);
    }

    [Fact]
    public void GoldenScytheObjectiveMovesToReachableAltarStandTile()
    {
        var plan = Plan(
            goldenScytheAltars: "[{\"tile_x\":4,\"tile_y\":2,\"action\":\"GoldenScythe\"}]",
            mineKind: "quarry_mine",
            goldenScytheApplicable: true,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireGoldenScythe });

        Assert.Equal(MiningFloorStepKinds.MoveToGoldenScytheAltar, plan.StepKind);
        Assert.Equal("executor.move_to_tile", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Equal(3, plan.TargetTileX);
        Assert.Equal(2, plan.TargetTileY);
        Assert.Equal("(W)53", plan.TargetQualifiedItemId);
    }

    [Fact]
    public void GoldenScytheObjectiveClaimsAdjacentUnclaimedAltar()
    {
        var plan = Plan(
            goldenScytheAltars: "[{\"tile_x\":2,\"tile_y\":2,\"action\":\"GoldenScythe\"}]",
            mineKind: "quarry_mine",
            goldenScytheApplicable: true,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireGoldenScythe });

        Assert.Equal(MiningFloorStepKinds.ClaimGoldenScythe, plan.StepKind);
        Assert.Equal("executor.interact", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Contains(MiningFloorStepCompiler.BuildExecutionParameters(plan), parameter =>
            parameter.Name == "expected_action_type" && parameter.Value == "GoldenScythe");
    }

    [Fact]
    public void GoldenScytheObjectiveUsesNativeMineExitAfterClaim()
    {
        var plan = Plan(
            goldenScytheAltars: "[{\"tile_x\":2,\"tile_y\":2,\"action\":\"GoldenScythe\"}]",
            exits: "[{\"tile_x\":4,\"tile_y\":2,\"expected_destination\":{\"location_id\":\"Mine\",\"tile_x\":67,\"tile_y\":10}}]",
            mineKind: "quarry_mine",
            goldenScytheApplicable: true,
            goldenScytheClaimed: true,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireGoldenScythe });

        Assert.Equal(MiningFloorStepKinds.ExitMine, plan.StepKind);
        Assert.Equal("executor.exit_mine", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Equal("Mine", plan.ExpectedTargetLocation);
        Assert.Equal(67, plan.ExpectedArrivalTileX);
        Assert.Equal(10, plan.ExpectedArrivalTileY);
    }

    [Fact]
    public void GoldenScytheObjectiveBlocksBeforeAltarWhenInventoryIsFull()
    {
        var plan = Plan(
            goldenScytheAltars: "[{\"tile_x\":2,\"tile_y\":2,\"action\":\"GoldenScythe\"}]",
            resources: "{\"health\":100,\"max_health\":100,\"energy\":220,\"current_time\":1200,\"selected_slot_index\":4,\"inventory_capacity\":{\"empty_slots\":0},\"food_slots\":[]}",
            mineKind: "quarry_mine",
            goldenScytheApplicable: true,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireGoldenScythe });

        Assert.Equal(MiningFloorStepKinds.Blocked, plan.StepKind);
        Assert.Equal("golden_scythe_inventory_full", plan.Reason);
    }

    [Fact]
    public void SkullKeyObjectiveContinuesDownOrdinaryMineBeforeFloor120()
    {
        var plan = Plan(
            ladders: "[{\"tile_x\":4,\"tile_y\":2}]",
            mineLevel: 119,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireSkullKey });

        Assert.Equal(MiningFloorStepKinds.DescendLadder, plan.StepKind);
        Assert.Equal("executor.descend_ladder", MiningFloorStepCompiler.ExecutionOptionId(plan));
    }

    [Fact]
    public void SkullKeyObjectiveMovesToFloor120RewardChest()
    {
        var plan = Plan(
            skullKeyRewardChests: "[{\"tile_x\":4,\"tile_y\":2,\"contains_skull_key\":true,\"special_item_which\":4,\"interaction_kind\":\"overlay_object\",\"expected_action_type\":\"SkullKeyChest\"}]",
            skullKeyApplicable: true,
            mineLevel: 120,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireSkullKey });

        Assert.Equal(MiningFloorStepKinds.MoveToSkullKeyChest, plan.StepKind);
        Assert.Equal("executor.move_to_tile", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Equal(3, plan.TargetTileX);
        Assert.Equal(2, plan.TargetTileY);
    }

    [Fact]
    public void SkullKeyObjectiveClaimsAdjacentFloor120RewardChest()
    {
        var plan = Plan(
            skullKeyRewardChests: "[{\"tile_x\":2,\"tile_y\":2,\"contains_skull_key\":true,\"special_item_which\":4,\"interaction_kind\":\"overlay_object\",\"expected_action_type\":\"SkullKeyChest\"}]",
            skullKeyApplicable: true,
            mineLevel: 120,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireSkullKey });

        Assert.Equal(MiningFloorStepKinds.ClaimSkullKey, plan.StepKind);
        Assert.Equal("executor.interact", MiningFloorStepCompiler.ExecutionOptionId(plan));
        var parameters = MiningFloorStepCompiler.BuildExecutionParameters(plan);
        Assert.Contains(parameters, parameter => parameter.Name == "interaction_kind" && parameter.Value == "overlay_object");
        Assert.Contains(parameters, parameter => parameter.Name == "required_postcondition" && parameter.Value == "player.has_skull_key=true");
    }

    [Fact]
    public void SkullKeyObjectiveDoesNotTreatFloor120WithoutRewardEvidenceAsComplete()
    {
        var plan = Plan(
            skullKeyApplicable: true,
            mineLevel: 120,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireSkullKey });

        Assert.Equal(MiningFloorStepKinds.Blocked, plan.StepKind);
        Assert.Equal("skull_key_reward_chest_unavailable", plan.Reason);
    }

    [Fact]
    public void SkullKeyObjectiveExitsOnlyAfterTransparentPostcondition()
    {
        var plan = Plan(
            exits: "[{\"tile_x\":4,\"tile_y\":2,\"expected_destination\":{\"location_id\":\"Mine\",\"tile_x\":23,\"tile_y\":8}}]",
            hasSkullKey: true,
            mineLevel: 120,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireSkullKey });

        Assert.Equal(MiningFloorStepKinds.ExitMine, plan.StepKind);
        Assert.Equal("skull_key_acquired_exit_ordinary_mines", plan.Reason);
    }

    [Fact]
    public void SkullKeyObjectiveSupportsFloor120TwoTileExitInteractionStand()
    {
        var plan = Plan(
            rows: new[] { "1111111", "1111111", "1001111", "1000111", "1111111" },
            exits: "[{\"tile_x\":3,\"tile_y\":1,\"expected_destination\":{\"location_id\":\"Mine\",\"tile_x\":23,\"tile_y\":8}}]",
            hasSkullKey: true,
            mineLevel: 120,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireSkullKey });

        Assert.Equal(MiningFloorStepKinds.ExitMine, plan.StepKind);
        Assert.Equal(2, Math.Abs(plan.StandTileX!.Value - plan.TargetTileX!.Value) + Math.Abs(plan.StandTileY!.Value - plan.TargetTileY!.Value));
    }

    [Theory]
    [InlineData("skull_cavern", 121)]
    [InlineData("quarry_mine", 77377)]
    public void SkullKeyObjectiveRejectsOtherMineFamilies(string mineKind, int mineLevel)
    {
        var plan = Plan(
            mineKind: mineKind,
            mineLevel: mineLevel,
            objective: new MiningFloorObjective { Kind = MiningObjectiveKinds.AcquireSkullKey });

        Assert.Equal(MiningFloorStepKinds.Blocked, plan.StepKind);
        Assert.Equal("skull_key_requires_ordinary_mines_1_120", plan.Reason);
    }

    [Fact]
    public void KillAllFloorSelectsReachableMonsterBeforeStone()
    {
        var plan = Plan(
            objects: "[{\"tile_x\":2,\"tile_y\":2,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1,\"ladder_preview\":{\"creates_ladder\":true}}]",
            monsters: "[{\"tile_x\":4,\"tile_y\":2}]",
            mustKillAll: true);

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal(4, plan.TargetTileX);
        Assert.Equal("kill_all_floor_requires_combat", plan.Reason);
    }

    [Fact]
    public void DeterministicLadderStonePrecedesCloserOrdinaryStone()
    {
        var plan = Plan(objects: """
        [
          {"tile_x":2,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":1,"ladder_preview":{"creates_ladder":false}},
          {"tile_x":5,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":3,"ladder_preview":{"creates_ladder":true}}
        ]
        """);

        Assert.Equal(MiningFloorStepKinds.MineStone, plan.StepKind);
        Assert.Equal(5, plan.TargetTileX);
        Assert.True(plan.DeterministicLadderAfterBreak);
        Assert.Equal(3, plan.EstimatedToolSwings);
    }

    [Fact]
    public void OrdinaryStoneUsesMovementPlusActualSwingCost()
    {
        var plan = Plan(objects: """
        [
          {"tile_x":2,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":8,"ladder_preview":{"creates_ladder":false}},
          {"tile_x":5,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":1,"ladder_preview":{"creates_ladder":false}}
        ]
        """);

        Assert.Equal(5, plan.TargetTileX);
        Assert.Equal(1, plan.EstimatedToolSwings);
        Assert.Equal("lowest_reachable_movement_and_swing_cost", plan.Reason);
        Assert.Equal(plan.EstimatedMovementTiles + 1, plan.Path.Length);
    }

    [Fact]
    public void NoReachableStoneFallsBackToCombatWithoutDangerPenalty()
    {
        var plan = Plan(
            rows: new[] { "111111", "100111", "100011", "100101", "111111" },
            objects: "[{\"tile_x\":4,\"tile_y\":1,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1,\"ladder_preview\":{\"creates_ladder\":false}}]",
            monsters: "[{\"tile_x\":3,\"tile_y\":3}]");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("no_reachable_stone_clear_dynamic_monster", plan.Reason);
    }

    [Fact]
    public void MonsterDropObjectiveBfsTargetsMonsterWithSelectedDrop()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: """
            [
              {"runtime_identity":"near","tile_x":2,"tile_y":2,"health":20,"damage_to_farmer":2,"selected_drop_qualified_item_ids":["(O)766"]},
              {"runtime_identity":"target","runtime_type":"StardewValley.Monsters.GreenSlime","name":"Green Slime","tile_x":5,"tile_y":2,"health":20,"damage_to_farmer":2,"selected_drop_qualified_item_ids":["(O)768"]}
            ]
            """);

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("target", plan.TargetRuntimeIdentity);
        Assert.Equal("StardewValley.Monsters.GreenSlime", plan.TargetRuntimeType);
        Assert.Equal("Green Slime", plan.TargetName);
        Assert.Equal("(O)768", plan.TargetQualifiedItemId);
        Assert.Equal(5, plan.TargetTileX);
    }

    [Fact]
    public void MonsterDropObjectiveRespectsExplicitlyEmptyEffectiveDropList()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: "[{\"runtime_identity\":\"special\",\"tile_x\":3,\"tile_y\":2,\"selected_drop_qualified_item_ids\":[\"(O)768\"],\"guaranteed_drop_qualified_item_ids\":[],\"possible_drop_qualified_item_ids\":[],\"has_special_item\":true}]");

        Assert.Equal(MiningFloorStepKinds.Blocked, plan.StepKind);
        Assert.Equal("no_reachable_monster_with_possible_target_drop", plan.Reason);
    }

    [Fact]
    public void MonsterDropObjectiveRejectsTargetThatAvailableMeleeCannotDefeat()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: """
            [{"runtime_identity":"immune","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":["(O)768"],"melee_damage_semantics":{"can_defeat_with_available_melee_weapon":false}}]
            """);

        Assert.Equal(MiningFloorStepKinds.Blocked, plan.StepKind);
        Assert.Equal("no_reachable_monster_with_possible_target_drop", plan.Reason);
    }

    [Fact]
    public void MonsterDropObjectiveCarriesRequiredWeaponEnchantmentToExecutionParameters()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: """
            [{"runtime_identity":"armored","runtime_type":"StardewValley.Monsters.Bug","name":"Armored Bug","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":["(O)768"],"melee_damage_semantics":{"can_defeat_with_available_melee_weapon":true,"required_weapon_enchantment_runtime_type":"BugKillerEnchantment"}}]
            """);

        Assert.Equal("BugKillerEnchantment", plan.RequiredWeaponEnchantmentRuntimeType);
        var parameters = MiningFloorStepCompiler.BuildExecutionParameters(plan);
        Assert.Contains(parameters, parameter => parameter.Name == "required_weapon_enchantment_runtime_type" && parameter.Value == "BugKillerEnchantment");
    }

    [Fact]
    public void MonsterDropObjectiveUsesExactSpecialItemPreview()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(W)24" }
            },
            monsters: "[{\"runtime_identity\":\"special\",\"tile_x\":3,\"tile_y\":2,\"selected_drop_qualified_item_ids\":[\"(O)768\"],\"guaranteed_drop_qualified_item_ids\":[],\"possible_drop_qualified_item_ids\":[\"(W)24\"],\"current_death_tile_preview_qualified_item_id\":\"(W)24\",\"has_special_item\":true}]");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("(W)24", plan.TargetQualifiedItemId);
        Assert.Equal("conditional_monster_drop", plan.SourceMatchStatus);
    }

    [Fact]
    public void MonsterDropObjectiveExpandsActiveSharedCatalogOnce()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(S)1200" }
            },
            monsters: "[{\"runtime_identity\":\"catalog-source\",\"tile_x\":3,\"tile_y\":2,\"possible_drop_qualified_item_ids\":[],\"conditional_drop_catalog_keys\":[\"utility_random_cosmetic_item\"]}]",
            dropCatalogs: "[{\"key\":\"utility_random_cosmetic_item\",\"active\":true,\"item_identity_completeness\":\"complete\",\"possible_qualified_item_ids\":[\"(S)1200\"]}]");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("(S)1200", plan.TargetQualifiedItemId);
        Assert.Equal("conditional_monster_drop", plan.SourceMatchStatus);
    }

    [Fact]
    public void MonsterDropObjectiveMultipliesEventAndCatalogSelectionChance()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(S)1200" }
            },
            monsters: """
            [{"runtime_identity":"weighted-catalog-source","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":[],"conditional_drop_catalog_keys":["utility_random_cosmetic_item"],"drop_probability_rules":[{"catalog_key":"utility_random_cosmetic_item","effective_per_kill_chance":0.003,"probability_status":"exact_current_state_formula","item_selection_status":"weighted_catalog"}]}]
            """,
            dropCatalogs: """
            [{"key":"utility_random_cosmetic_item","active":true,"item_identity_completeness":"complete","possible_qualified_item_ids":["(S)1200","(S)1201"],"selection_probability_completeness":"complete","selection_probability_entries":[{"qualified_item_id":"(S)1200","conditional_selection_chance":0.25,"probability_status":"exact_decompiled_weight_with_loaded_furniture_fallback"},{"qualified_item_id":"(S)1201","conditional_selection_chance":0.75,"probability_status":"exact_decompiled_weight_with_loaded_furniture_fallback"}]}]
            """);

        Assert.Equal("weighted-catalog-source", plan.TargetRuntimeIdentity);
        Assert.Equal(0.00075d, plan.TargetDropChancePreview);
        Assert.Equal(0.00075d, plan.TargetExpectedQuantityPerKill);
        Assert.Equal("exact_current_snapshot", plan.TargetDropProbabilityStatus);
    }

    [Fact]
    public void MonsterDropObjectiveRejectsIncompleteCatalogProbabilityMassOnly()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(S)1200" }
            },
            monsters: """
            [{"runtime_identity":"bad-weight-source","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":[],"conditional_drop_catalog_keys":["utility_random_cosmetic_item"],"drop_probability_rules":[{"catalog_key":"utility_random_cosmetic_item","effective_per_kill_chance":0.003,"probability_status":"exact_current_state_formula","item_selection_status":"weighted_catalog"}]}]
            """,
            dropCatalogs: """
            [{"key":"utility_random_cosmetic_item","active":true,"item_identity_completeness":"complete","possible_qualified_item_ids":["(S)1200","(S)1201"],"selection_probability_completeness":"complete","selection_probability_entries":[{"qualified_item_id":"(S)1200","conditional_selection_chance":0.25,"probability_status":"exact_decompiled_weight_with_loaded_furniture_fallback"},{"qualified_item_id":"(S)1201","conditional_selection_chance":0.25,"probability_status":"exact_decompiled_weight_with_loaded_furniture_fallback"}]}]
            """);

        Assert.Equal("bad-weight-source", plan.TargetRuntimeIdentity);
        Assert.Null(plan.TargetDropChancePreview);
        Assert.Equal("unavailable_no_stable_per_identity_probability", plan.TargetDropProbabilityStatus);
    }

    [Fact]
    public void MonsterDropObjectiveAcceptsExactHardMineTreasureCatalogStatus()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)288" }
            },
            monsters: """
            [{"runtime_identity":"hard-treasure-source","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":[],"conditional_drop_catalog_keys":["mine_hard_special_treasure_room"],"drop_probability_rules":[{"catalog_key":"mine_hard_special_treasure_room","effective_per_kill_chance":1.0,"probability_status":"exact_current_state_formula","item_selection_status":"global_rng_catalog_selection_not_consumed"}]}]
            """,
            dropCatalogs: """
            [{"key":"mine_hard_special_treasure_room","active":true,"item_identity_completeness":"complete","possible_qualified_item_ids":["(O)288","(O)287"],"selection_probability_completeness":"complete","selection_probability_entries":[{"qualified_item_id":"(O)288","conditional_selection_chance":0.25,"conditional_expected_quantity":5.0,"probability_status":"exact_decompiled_hard_mine_treasure_tree"},{"qualified_item_id":"(O)287","conditional_selection_chance":0.75,"conditional_expected_quantity":10.0,"probability_status":"exact_decompiled_hard_mine_treasure_tree"}]}]
            """);

        Assert.Equal("hard-treasure-source", plan.TargetRuntimeIdentity);
        Assert.Equal(0.25d, plan.TargetDropChancePreview);
        Assert.Equal(1.25d, plan.TargetExpectedQuantityPerKill);
        Assert.Equal("exact_current_snapshot", plan.TargetDropProbabilityStatus);
    }

    [Fact]
    public void MonsterDropObjectiveDoesNotRankCurrentDeathTileTreasureCatalogAsStableProbability()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)288" }
            },
            monsters: """
            [{"runtime_identity":"moving-hard-treasure-source","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":[],"conditional_drop_catalog_keys":["mine_hard_special_treasure_room"],"drop_probability_rules":[{"catalog_key":"mine_hard_special_treasure_room","effective_per_kill_chance":1.0,"probability_status":"exact_current_state_formula","item_selection_status":"current_death_tile_global_rng_catalog_selection_not_consumed"}]}]
            """,
            dropCatalogs: """
            [{"key":"mine_hard_special_treasure_room","active":true,"item_identity_completeness":"complete","possible_qualified_item_ids":["(O)288","(O)287"],"selection_probability_completeness":"complete","selection_probability_entries":[{"qualified_item_id":"(O)288","conditional_selection_chance":0.25,"probability_status":"exact_decompiled_hard_mine_treasure_tree"},{"qualified_item_id":"(O)287","conditional_selection_chance":0.75,"probability_status":"exact_decompiled_hard_mine_treasure_tree"}]}]
            """);

        Assert.Equal("moving-hard-treasure-source", plan.TargetRuntimeIdentity);
        Assert.Null(plan.TargetDropChancePreview);
        Assert.Equal("unavailable_no_stable_per_identity_probability", plan.TargetDropProbabilityStatus);
    }

    [Fact]
    public void MonsterDropObjectiveRejectsIncompleteSharedCatalog()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(S)1200" }
            },
            monsters: "[{\"runtime_identity\":\"catalog-source\",\"tile_x\":3,\"tile_y\":2,\"possible_drop_qualified_item_ids\":[],\"conditional_drop_catalog_keys\":[\"utility_random_cosmetic_item\"]}]",
            dropCatalogs: "[{\"key\":\"utility_random_cosmetic_item\",\"active\":true,\"item_identity_completeness\":\"partial\",\"possible_qualified_item_ids\":[\"(S)1200\"]}]");

        Assert.Equal(MiningFloorStepKinds.Blocked, plan.StepKind);
        Assert.Equal("no_reachable_monster_with_possible_target_drop", plan.Reason);
    }

    [Fact]
    public void MonsterDropObjectiveRanksExactChancePerBfsDistance()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: """
            [
              {"runtime_identity":"near-low","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":["(O)768"],"drop_probability_rules":[{"qualified_item_ids":["(O)768"],"per_identity_chance":0.1,"expected_quantity_per_kill":0.1,"probability_status":"exact_current_state_formula","item_selection_status":"independent_roll_per_call"}]},
              {"runtime_identity":"far-high","tile_x":6,"tile_y":2,"possible_drop_qualified_item_ids":["(O)768"],"drop_probability_rules":[{"qualified_item_ids":["(O)768"],"per_identity_chance":0.8,"expected_quantity_per_kill":1.6,"probability_status":"exact_current_state_formula","item_selection_status":"independent_roll_per_call"}]}
            ]
            """);

        Assert.Equal("far-high", plan.TargetRuntimeIdentity);
        Assert.Equal(0.8d, plan.TargetDropChancePreview);
        Assert.Equal(1.6d, plan.TargetExpectedQuantityPerKill);
        Assert.Equal("exact_current_snapshot", plan.TargetDropProbabilityStatus);
        Assert.True(plan.TargetDropEfficiencyScore > 0d);
        var parameters = MiningFloorStepCompiler.BuildExecutionParameters(plan);
        Assert.Contains(parameters, parameter => parameter.Name == "target_drop_chance_preview" && parameter.Value == "0.8");
        Assert.Contains(parameters, parameter => parameter.Name == "target_expected_quantity_per_kill" && parameter.Value == "1.6");
    }

    [Fact]
    public void MonsterDropObjectiveRanksByMovementAndExactCombatDuration()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: """
            [
              {"runtime_identity":"near-slow","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":["(O)768"],"drop_probability_rules":[{"qualified_item_ids":["(O)768"],"per_identity_chance":0.8,"probability_status":"exact_current_state_formula","item_selection_status":"independent"}],"melee_attack_projections":[{"slot_index":1,"expected_attacks_to_defeat":10.0,"expected_active_damage_duration_ms":5000.0,"duration_status":"exact_active_melee_phase_excluding_movement"}]},
              {"runtime_identity":"far-fast","tile_x":6,"tile_y":2,"possible_drop_qualified_item_ids":["(O)768"],"drop_probability_rules":[{"qualified_item_ids":["(O)768"],"per_identity_chance":0.8,"probability_status":"exact_current_state_formula","item_selection_status":"independent"}],"melee_attack_projections":[{"slot_index":2,"expected_attacks_to_defeat":2.0,"expected_active_damage_duration_ms":500.0,"duration_status":"exact_active_melee_phase_excluding_movement"}]}
            ]
            """,
            resources: """{"health":100,"max_health":100,"selected_slot_index":4,"food_slots":[],"cardinal_movement":{"tile_duration_ms":100.0}}""");

        Assert.Equal("far-fast", plan.TargetRuntimeIdentity);
        Assert.Equal(2, plan.CombatWeaponSlotIndex);
        Assert.Equal(2d, plan.ExpectedCombatAttacks);
        Assert.Equal(500d, plan.ExpectedCombatDurationMs);
        Assert.Equal(900d, plan.EstimatedTargetCostMs);
        Assert.Equal("exact_active_melee_plus_unobstructed_bfs_movement", plan.CombatDurationStatus);
    }

    [Fact]
    public void DistantMonsterUsesLoadedSlingshotWhenClearAndFaster()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: """
            [
              {
                "runtime_identity":"ranged-target",
                "runtime_type":"StardewValley.Monsters.GreenSlime",
                "name":"Green Slime",
                "tile_x":6,
                "tile_y":2,
                "possible_drop_qualified_item_ids":["(O)768"],
                "drop_probability_rules":[{"qualified_item_ids":["(O)768"],"per_identity_chance":1.0,"probability_status":"exact_current_state_formula","item_selection_status":"independent"}],
                "melee_attack_projections":[{"slot_index":2,"can_defeat_with_this_weapon":true,"expected_attacks_to_defeat":8.0,"expected_active_damage_duration_ms":4000.0,"duration_status":"exact_active_melee_phase_excluding_movement"}],
                "slingshot_attack_projections":[{"slot_index":5,"ammo_qualified_item_id":"(O)390","ammo_stack":20,"can_defeat_with_this_weapon":true,"expected_shots_to_defeat":2.0,"expected_active_damage_duration_ms":600.0,"duration_status":"exact_charge_phase_excluding_projectile_travel_and_reposition"}]
              }
            ]
            """,
            resources: """{"health":100,"max_health":100,"selected_slot_index":4,"food_slots":[],"cardinal_movement":{"tile_duration_ms":100.0}}""");

        Assert.Equal(MiningFloorStepKinds.ShootMonster, plan.StepKind);
        Assert.Equal("executor.shoot_monster", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Equal("slingshot", plan.CombatMethod);
        Assert.Equal(5, plan.SlingshotSlotIndex);
        Assert.Equal("(O)390", plan.SlingshotAmmoQualifiedItemId);
        Assert.Equal(0, plan.EstimatedMovementTiles);
    }

    [Fact]
    public void SafeExplosiveAmmoWithAreaValueCanBeatMelee()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: """
            [
              {
                "runtime_identity":"explosive-target",
                "runtime_type":"StardewValley.Monsters.GreenSlime",
                "name":"Green Slime",
                "tile_x":6,
                "tile_y":2,
                "possible_drop_qualified_item_ids":["(O)768"],
                "drop_probability_rules":[{"qualified_item_ids":["(O)768"],"per_identity_chance":1.0,"probability_status":"exact_current_state_formula","item_selection_status":"independent"}],
                "melee_attack_projections":[{"slot_index":2,"can_defeat_with_this_weapon":true,"expected_attacks_to_defeat":8.0,"expected_active_damage_duration_ms":4000.0,"duration_status":"exact_active_melee_phase_excluding_movement"}],
                "slingshot_attack_projections":[
                  {
                    "slot_index":5,
                    "ammo_qualified_item_id":"(O)441",
                    "ammo_stack":20,
                    "can_defeat_with_this_weapon":true,
                    "expected_shots_to_defeat":4.0,
                    "expected_active_damage_duration_ms":1200.0,
                    "duration_status":"exact_charge_phase_excluding_projectile_travel_and_reposition",
                    "explosive_area_safe":true,
                    "explosive_area_has_additional_value":true,
                    "explosive_area_useful_object_hits":2,
                    "explosive_area_additional_monster_hits":1
                  }
                ]
              }
            ]
            """,
            resources: """{"health":100,"max_health":100,"selected_slot_index":4,"food_slots":[],"cardinal_movement":{"tile_duration_ms":100.0}}""");

        Assert.Equal(MiningFloorStepKinds.ShootMonster, plan.StepKind);
        Assert.Equal("(O)441", plan.SlingshotAmmoQualifiedItemId);
        Assert.Equal("slingshot", plan.CombatMethod);
    }

    [Fact]
    public void UnsafeOrWastefulExplosiveAmmoFallsBackToMelee()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: """
            [
              {
                "runtime_identity":"unsafe-explosive-target",
                "runtime_type":"StardewValley.Monsters.GreenSlime",
                "name":"Green Slime",
                "tile_x":6,
                "tile_y":2,
                "possible_drop_qualified_item_ids":["(O)768"],
                "drop_probability_rules":[{"qualified_item_ids":["(O)768"],"per_identity_chance":1.0,"probability_status":"exact_current_state_formula","item_selection_status":"independent"}],
                "melee_attack_projections":[{"slot_index":2,"can_defeat_with_this_weapon":true,"expected_attacks_to_defeat":8.0,"expected_active_damage_duration_ms":4000.0,"duration_status":"exact_active_melee_phase_excluding_movement"}],
                "slingshot_attack_projections":[
                  {
                    "slot_index":5,
                    "ammo_qualified_item_id":"(O)441",
                    "ammo_stack":20,
                    "can_defeat_with_this_weapon":true,
                    "expected_shots_to_defeat":1.0,
                    "expected_active_damage_duration_ms":300.0,
                    "duration_status":"exact_charge_phase_excluding_projectile_travel_and_reposition",
                    "explosive_area_safe":false,
                    "explosive_area_has_additional_value":true,
                    "explosive_area_useful_object_hits":4,
                    "explosive_area_additional_monster_hits":2
                  }
                ]
              }
            ]
            """,
            resources: """{"health":100,"max_health":100,"selected_slot_index":4,"food_slots":[],"cardinal_movement":{"tile_duration_ms":100.0}}""");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("melee", plan.CombatMethod);
        Assert.Equal(2, plan.CombatWeaponSlotIndex);
    }

    [Fact]
    public void StandingMummyCompilesMeleeKnockdownBeforeBombFinish()
    {
        var plan = Plan(
            mustKillAll: true,
            monsters: """
            [
              {
                "runtime_identity":"mummy-standing",
                "runtime_type":"StardewValley.Monsters.Mummy",
                "name":"Mummy",
                "tile_x":4,
                "tile_y":2,
                "bomb_damage_semantics":{"special_effect":"standing_mummy_must_be_knocked_down_then_bombed"},
                "melee_attack_projections":[
                  {
                    "slot_index":2,
                    "can_defeat_with_this_weapon":false,
                    "terminal_effect":"knockdown_requires_bomb_finish",
                    "expected_attacks_to_defeat":3.0,
                    "expected_active_damage_duration_ms":900.0,
                    "duration_status":"exact_active_melee_phase_to_mummy_knockdown_excluding_movement"
                  }
                ]
              }
            ]
            """,
            resources: """
            {
              "health":100,
              "max_health":100,
              "energy":220,
              "current_time":1200,
              "selected_slot_index":4,
              "food_slots":[],
              "cardinal_movement":{"tile_duration_ms":100.0},
              "bomb_slots":[{"slot_index":7,"qualified_item_id":"(O)286","stack":2,"radius_tiles":3}]
            }
            """);

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("mummy-standing", plan.TargetRuntimeIdentity);
        Assert.Equal("melee", plan.CombatMethod);
        Assert.Equal("knockdown_requires_bomb_finish", plan.CombatTerminalState);
        Assert.Equal(2, plan.CombatWeaponSlotIndex);
        var parameters = MiningFloorStepCompiler.BuildExecutionParameters(plan);
        Assert.Contains(parameters, parameter =>
            parameter.Name == "combat_terminal_state" &&
            parameter.Value == "knockdown_requires_bomb_finish");
    }

    [Fact]
    public void RevivingMummyCompilesTargetedBombFinishWithFuseEscape()
    {
        var plan = Plan(
            mustKillAll: true,
            rows: new[]
            {
                "11111111111",
                "10000000001",
                "10000000001",
                "10000000001",
                "10000000001",
                "10000000001",
                "10000000001",
                "10000000001",
                "10000000001",
                "10000000001",
                "11111111111"
            },
            monsters: """
            [
              {
                "runtime_identity":"mummy-reviving",
                "runtime_type":"StardewValley.Monsters.Mummy",
                "name":"Mummy",
                "tile_x":5,
                "tile_y":5,
                "bomb_damage_semantics":{"special_effect":"bomb_finalizes_reviving_mummy"}
              }
            ]
            """,
            resources: """
            {
              "health":100,
              "max_health":100,
              "energy":220,
              "current_time":1200,
              "selected_slot_index":4,
              "food_slots":[],
              "cardinal_movement":{"tile_duration_ms":100.0},
              "bomb_slots":[{"slot_index":7,"qualified_item_id":"(O)286","stack":2,"radius_tiles":3}]
            }
            """);

        Assert.Equal(MiningFloorStepKinds.PlaceBomb, plan.StepKind);
        Assert.Equal("mummy-reviving", plan.TargetRuntimeIdentity);
        Assert.Equal("StardewValley.Monsters.Mummy", plan.TargetRuntimeType);
        Assert.Equal("bomb", plan.CombatMethod);
        Assert.Equal("mummy_finalized", plan.CombatTerminalState);
        Assert.Equal(7, plan.BombSlotIndex);
        Assert.False(plan.TargetTileX == 5 && plan.TargetTileY == 5);
        Assert.NotNull(plan.EscapeTileX);
        Assert.NotNull(plan.EscapeTileY);
        Assert.True(Math.Abs(plan.EscapeTileX!.Value - plan.TargetTileX!.Value) > plan.BombRadiusTiles ||
            Math.Abs(plan.EscapeTileY!.Value - plan.TargetTileY!.Value) > plan.BombRadiusTiles);
    }

    [Fact]
    public void DistantIrrelevantRevivingMummyDoesNotConsumeBombBeforeMining()
    {
        var plan = Plan(
            rows: new[]
            {
                "111111111",
                "100000001",
                "100000001",
                "100000001",
                "100000001",
                "100000001",
                "100000001",
                "100000001",
                "111111111"
            },
            objects: """[{"tile_x":2,"tile_y":2,"qualified_item_id":"(O)32","is_breakable_stone":true,"best_pickaxe_hits_remaining":1}]""",
            monsters: """
            [
              {
                "runtime_identity":"mummy-irrelevant",
                "runtime_type":"StardewValley.Monsters.Mummy",
                "name":"Mummy",
                "tile_x":7,
                "tile_y":7,
                "bomb_damage_semantics":{"special_effect":"bomb_finalizes_reviving_mummy"}
              }
            ]
            """,
            resources: """
            {
              "health":100,
              "max_health":100,
              "energy":220,
              "current_time":1200,
              "selected_slot_index":4,
              "food_slots":[],
              "cardinal_movement":{"tile_duration_ms":100.0},
              "bomb_slots":[{"slot_index":7,"qualified_item_id":"(O)286","stack":2,"radius_tiles":3}]
            }
            """);

        Assert.Equal(MiningFloorStepKinds.MineStone, plan.StepKind);
        Assert.NotEqual("mummy_finalized", plan.CombatTerminalState);
    }

    [Fact]
    public void ReachDepthUsesBombOnlyForDenseClusterWithFuseEscape()
    {
        var rows = new[]
        {
            "111111111",
            "100000001",
            "100000001",
            "100000001",
            "100000001",
            "100000001",
            "100000001",
            "100000001",
            "111111111"
        };
        var plan = Plan(
            rows: rows,
            objects: """
            [
              {"tile_x":3,"tile_y":3,"qualified_item_id":"(O)32","is_breakable_stone":true},
              {"tile_x":4,"tile_y":3,"qualified_item_id":"(O)32","is_breakable_stone":true},
              {"tile_x":3,"tile_y":4,"qualified_item_id":"(O)32","is_breakable_stone":true},
              {"tile_x":4,"tile_y":4,"qualified_item_id":"(O)32","is_breakable_stone":true}
            ]
            """,
            resources: """
            {
              "health":100,
              "max_health":100,
              "energy":220,
              "current_time":1200,
              "selected_slot_index":4,
              "food_slots":[],
              "cardinal_movement":{"tile_duration_ms":150.0},
              "bomb_slots":[{"slot_index":7,"qualified_item_id":"(O)286","stack":3,"radius_tiles":3}]
            }
            """);

        Assert.Equal(MiningFloorStepKinds.PlaceBomb, plan.StepKind);
        Assert.Equal("executor.place_bomb", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Equal(7, plan.BombSlotIndex);
        Assert.Equal("(O)286", plan.BombQualifiedItemId);
        Assert.Equal(3, plan.BombRadiusTiles);
        Assert.True(plan.ExpectedBombObjectHits >= 4);
        Assert.NotNull(plan.EscapeTileX);
        Assert.NotNull(plan.EscapeTileY);
    }

    [Fact]
    public void MonsterDropObjectiveDoesNotRankCurrentPositionSeedAsStableProbability()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)553" }
            },
            monsters: """
            [
              {"runtime_identity":"near-position-seeded","tile_x":3,"tile_y":2,"possible_drop_qualified_item_ids":["(O)553"],"drop_probability_rules":[{"qualified_item_ids":["(O)553"],"per_identity_chance":1.0,"probability_status":"exact_current_state_formula","item_selection_status":"fixed_current_position_seed_preview_recomputed_each_call"}]},
              {"runtime_identity":"far-stable","tile_x":6,"tile_y":2,"possible_drop_qualified_item_ids":["(O)553"],"drop_probability_rules":[{"qualified_item_ids":["(O)553"],"per_identity_chance":0.05,"expected_quantity_per_kill":0.05,"probability_status":"exact_current_state_formula","item_selection_status":"independent_roll_per_call"}]}
            ]
            """);

        Assert.Equal("far-stable", plan.TargetRuntimeIdentity);
        Assert.Equal(0.05d, plan.TargetDropChancePreview);
        Assert.Equal("exact_current_snapshot", plan.TargetDropProbabilityStatus);
    }

    [Fact]
    public void ResourceObjectivePicksExistingTargetDebrisBeforeSourceNode()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectResourceOrArtifact,
                TargetQualifiedItemIds = new[] { "(O)390" },
                TargetSourceQualifiedItemIds = new[] { "(O)751" }
            },
            objects: "[{\"tile_x\":6,\"tile_y\":2,\"qualified_item_id\":\"(O)751\",\"best_pickaxe_hits_remaining\":2}]",
            debris: "[{\"debris_index\":6,\"qualified_item_id\":\"(O)390\",\"chunks\":[{\"tile_x\":3,\"tile_y\":2}]}]");

        Assert.Equal(MiningFloorStepKinds.PickupDebris, plan.StepKind);
        Assert.Equal("(O)390", plan.TargetQualifiedItemId);
        Assert.Equal(6, plan.DebrisIndex);
        Assert.Equal(3, plan.TargetTileX);
    }

    [Fact]
    public void MonsterDropObjectivePicksExistingDropBeforeAnotherFight()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" }
            },
            monsters: "[{\"runtime_identity\":\"target\",\"tile_x\":6,\"tile_y\":2,\"health\":20,\"selected_drop_qualified_item_ids\":[\"(O)768\"]}]",
            debris: "[{\"debris_index\":2,\"qualified_item_id\":\"(O)768\",\"chunks\":[{\"tile_x\":3,\"tile_y\":2}]}]");

        Assert.Equal(MiningFloorStepKinds.PickupDebris, plan.StepKind);
        Assert.Equal("target_monster_drop_already_on_floor", plan.Reason);
    }

    [Fact]
    public void ReachDepthOpportunisticallyPicksOnlyNearbyDebris()
    {
        var near = ObjectivePlan(
            new MiningFloorObjective { Kind = MiningObjectiveKinds.ReachDepth },
            debris: "[{\"debris_index\":3,\"qualified_item_id\":\"(O)390\",\"chunks\":[{\"tile_x\":3,\"tile_y\":2}]}]");
        var far = ObjectivePlan(
            new MiningFloorObjective { Kind = MiningObjectiveKinds.ReachDepth },
            debris: "[{\"debris_index\":4,\"qualified_item_id\":\"(O)390\",\"chunks\":[{\"tile_x\":6,\"tile_y\":2}]}]");

        Assert.Equal(MiningFloorStepKinds.PickupDebris, near.StepKind);
        Assert.Equal("opportunistic_debris_within_three_tiles", near.Reason);
        Assert.NotEqual(MiningFloorStepKinds.PickupDebris, far.StepKind);
    }

    [Fact]
    public void ReachDepthCompilesNearbyBreakableContainerSeparatelyFromStone()
    {
        var plan = Plan(objects: "[{\"tile_x\":3,\"tile_y\":2,\"qualified_item_id\":\"(BC)118\",\"is_container\":true,\"health_or_hits_remaining\":3}]");

        Assert.Equal(MiningFloorStepKinds.BreakContainer, plan.StepKind);
        Assert.Equal("executor.break_container", MiningFloorStepCompiler.ExecutionOptionId(plan));
        Assert.Equal(3, plan.EstimatedToolSwings);
        Assert.Equal("(BC)118", plan.TargetQualifiedItemId);
    }

    [Fact]
    public void ResourceObjectiveInfersSourceFromTransparentGuaranteedDrops()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectResourceOrArtifact,
                TargetQualifiedItemIds = new[] { "(O)378" }
            },
            objects: "[{\"tile_x\":5,\"tile_y\":2,\"qualified_item_id\":\"(O)751\",\"best_pickaxe_hits_remaining\":2,\"guaranteed_drop_qualified_item_ids\":[\"(O)378\"],\"possible_drop_qualified_item_ids\":[\"(O)378\"]}]");

        Assert.Equal(MiningFloorStepKinds.MineStone, plan.StepKind);
        Assert.Equal("(O)751", plan.TargetQualifiedItemId);
        Assert.Equal(new[] { "(O)378" }, plan.ExpectedDropQualifiedItemIds);
        Assert.Equal("guaranteed_drop", plan.SourceMatchStatus);
    }

    [Fact]
    public void ResourceObjectivePrefersGuaranteedNodeOverNearerConditionalStone()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectResourceOrArtifact,
                TargetQualifiedItemIds = new[] { "(O)378" }
            },
            objects: "[{\"tile_x\":2,\"tile_y\":2,\"qualified_item_id\":\"(O)40\",\"best_pickaxe_hits_remaining\":1,\"guaranteed_drop_qualified_item_ids\":[],\"possible_drop_qualified_item_ids\":[\"(O)378\"]},{\"tile_x\":6,\"tile_y\":2,\"qualified_item_id\":\"(O)751\",\"best_pickaxe_hits_remaining\":2,\"guaranteed_drop_qualified_item_ids\":[\"(O)378\"],\"possible_drop_qualified_item_ids\":[\"(O)378\"]}]");

        Assert.Equal(MiningFloorStepKinds.MineStone, plan.StepKind);
        Assert.Equal(6, plan.TargetTileX);
        Assert.Equal("guaranteed_drop", plan.SourceMatchStatus);
    }

    [Fact]
    public void ResourceToolActionIsInterruptedByImmediateMonsterThreat()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectResourceOrArtifact,
                TargetSourceQualifiedItemIds = new[] { "(O)751" }
            },
            objects: "[{\"tile_x\":6,\"tile_y\":2,\"qualified_item_id\":\"(O)751\",\"best_pickaxe_hits_remaining\":2}]",
            monsters: "[{\"runtime_identity\":\"threat\",\"tile_x\":2,\"tile_y\":2,\"health\":20,\"damage_to_farmer\":2,\"selected_drop_qualified_item_ids\":[]}]");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("unsafe_tool_window_combat_interrupt", plan.Reason);
        Assert.Equal("blocked_by_immediate_monster_threat", plan.SafetyWindowStatus);
        Assert.Equal(4, plan.RestoreSlotIndex);
    }

    [Fact]
    public void LowHealthSelectsCheapestAdequateFoodBeforeCombat()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" },
                MinimumReserveHealth = 20
            },
            monsters: "[{\"runtime_identity\":\"target\",\"tile_x\":3,\"tile_y\":2,\"health\":20,\"damage_to_farmer\":8,\"selected_drop_qualified_item_ids\":[\"(O)768\"]}]",
            resources: "{\"health\":10,\"max_health\":100,\"selected_slot_index\":4,\"food_slots\":[{\"slot_index\":7,\"qualified_item_id\":\"(O)194\",\"health_recovery\":25,\"sell_price\":120}]}");

        Assert.Equal(MiningFloorStepKinds.ConsumeFood, plan.StepKind);
        Assert.Equal(7, plan.FoodSlotIndex);
        Assert.Equal(4, plan.RestoreSlotIndex);
        Assert.Equal("health_below_two_hit_or_configured_reserve", plan.Reason);
    }

    [Fact]
    public void MissingCollisionFailsClosed()
    {
        var snapshot = Snapshot("""
        {"mining":{"tiles":{"status":"unavailable","value":null},"objects":{"status":"available","value":[]},"monsters":{"status":"available","value":[]},"floor_objectives":{"status":"available","value":{}}}}
        """);

        var plan = new MiningFloorStepPlanner().Plan(snapshot);

        Assert.Equal("blocked", plan.Status);
        Assert.Equal("mining_required_group_unavailable", plan.Reason);
    }

    [Fact]
    public void RuntimeMineStoneUsesNativeToolLifecycleWithoutDirectToolFunction()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        var start = source.IndexOf("private void StartMineStone", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartSetupMiningFloor", start, StringComparison.Ordinal);
        var mineStoneSource = source[start..end];

        Assert.Contains("executor.mine_stone", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.BeginUsingTool()", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.EndUsingTool()", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("RecordMineStoneCompletedSwing(active, 0);", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("native_pickaxe_lifecycle_removed_breakable_stone", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("ImmediateMiningThreat(mine)", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("active.CombatInterrupted = true", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("active.ElapsedTicks - active.CombatInterruptedTicks", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("activeMineStone?.CombatInterrupted == true", source, StringComparison.Ordinal);
        Assert.Contains("RestoreManualAutoCombatTool()", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".DoFunction(", mineStoneSource, StringComparison.Ordinal);
        Assert.DoesNotContain("objects.Remove", mineStoneSource, StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        Assert.Contains("[switch] $MineOneStone", smoke, StringComparison.Ordinal);
        Assert.Contains("option_id = \"executor.mine_stone\"", smoke, StringComparison.Ordinal);
        Assert.Contains("mine_stone_native_swing_count", smoke, StringComparison.Ordinal);
        Assert.Contains("terminal zero-health observation", smoke, StringComparison.Ordinal);
        Assert.Contains("mine_stone_removed", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeMineStoneUsesCompilerStandTileAndReplansDynamicObstacles()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        var start = source.IndexOf("private void StartMineStone", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartSetupMiningFloor", start, StringComparison.Ordinal);
        var mineStoneSource = source[start..end];

        Assert.Contains("request.StandTileX", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("BuildCompilerAdjacentPath", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("avoidSoftObstacles: true", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: false", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("TryReplanMineStone", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("mine_stone_dynamic_path_unavailable", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("path ?? new List<Point>()", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("if (!active.CombatInterrupted)", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("StopAllMovement();", mineStoneSource, StringComparison.Ordinal);
        Assert.DoesNotContain("mine_stone_path_changed", mineStoneSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeMineTransitionsUseCompilerStandTilesWithoutImplicitClearance()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        foreach (var method in new[] { "StartDescendLadder", "StartDescendShaft", "StartExitMine" })
        {
            var start = source.IndexOf("private void " + method, StringComparison.Ordinal);
            var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
            var methodSource = source[start..end];
            Assert.Contains("request.StandTileX", methodSource, StringComparison.Ordinal);
            Assert.Contains(method == "StartExitMine" ? "BuildCompilerMineExitPath" : "BuildCompilerAdjacentPath", methodSource, StringComparison.Ordinal);
        }

        var helperStart = source.IndexOf("private static List<Point>? BuildCompilerAdjacentPath", StringComparison.Ordinal);
        var helperEnd = source.IndexOf("\n    private void TickMineStone", helperStart, StringComparison.Ordinal);
        var helperSource = source[helperStart..helperEnd];
        Assert.Contains("avoidSoftObstacles: true", helperSource, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: false", helperSource, StringComparison.Ordinal);
        Assert.Contains("TryReplanDescendLadder", source, StringComparison.Ordinal);
        Assert.Contains("TryReplanDescendShaft", source, StringComparison.Ordinal);
        Assert.Contains("TryReplanExitMine", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeMiningCalibrationLoadoutIsSandboxScopedAndRuntimeDataDriven()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        var loop = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMiningReachDepthLoop.ps1"));
        var start = source.IndexOf("private static MiningCalibrationLoadoutFacts EnsureMiningCalibrationLoadout", StringComparison.Ordinal);
        var end = source.IndexOf("private static MineFishingFixtureFacts EnsureMineFishingFixtureEquipment", start, StringComparison.Ordinal);
        var loadoutSource = source[start..end];

        Assert.Contains("STARDEWAI_MINING_CALIBRATION_LOADOUT", source, StringComparison.Ordinal);
        Assert.Contains("Game1.objectData.Keys", loadoutSource, StringComparison.Ordinal);
        Assert.Contains("new MeleeWeapon(itemId.ToString())", loadoutSource, StringComparison.Ordinal);
        Assert.Contains("healthRecoveredOnConsumption()", loadoutSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.health =", loadoutSource, StringComparison.Ordinal);
        Assert.Contains("[switch] $MiningCalibrationLoadout", smoke, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_MINING_CALIBRATION_LOADOUT", smoke, StringComparison.Ordinal);
        Assert.Contains("-MiningCalibrationLoadout", loop, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeBreakContainerUsesNativeHeavyHitterInputAndVerifiesRemoval()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var driverSource = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "NativeHeavyHitterAction.cs"));
        var start = source.IndexOf("private void StartBreakContainer", StringComparison.Ordinal);
        var end = source.IndexOf("private static bool ImmediateMiningThreat", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var containerSource = source[start..end];

        Assert.Contains("executor.break_container", source, StringComparison.Ordinal);
        Assert.Contains("obj is not BreakableContainer", containerSource, StringComparison.Ordinal);
        Assert.Contains("tool.isHeavyHitter()", containerSource, StringComparison.Ordinal);
        Assert.Contains("TryTickNativeHeavyHitterAction", containerSource, StringComparison.Ordinal);
        Assert.Contains("tool is MeleeWeapon ? SButton.MouseLeft : SButton.C", driverSource, StringComparison.Ordinal);
        Assert.Contains("native_heavy_hitter_input_removed_container", containerSource, StringComparison.Ordinal);
        Assert.Contains("released_contents_left_as_game_debris", containerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(", containerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("objects.Remove", containerSource, StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        Assert.Contains("[switch] $BreakOneContainer", smoke, StringComparison.Ordinal);
        Assert.Contains("[switch] $ForceBreakableContainerFixture", smoke, StringComparison.Ordinal);
        Assert.Contains("option_id = \"executor.break_container\"", smoke, StringComparison.Ordinal);
        Assert.Contains("break_container_removed", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCombatUsesFarmerInputAndPreservesTypedFeedback()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = source.IndexOf("private void StartCombatMonster", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartSetupMiningFloor", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var combatSource = source[start..end];

        Assert.Contains("executor.combat_monster", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.C, pressed: true", combatSource, StringComparison.Ordinal);
        Assert.Contains("PriorityQueue<Point, int>", source, StringComparison.Ordinal);
        Assert.Contains("MovementTraversalCost(location, next)", source, StringComparison.Ordinal);
        Assert.Contains("pickaxe.UpgradeLevel + 1", source, StringComparison.Ordinal);

        var bridgeSource = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "MiningReadAdapter.cs"));
        Assert.Contains("[\"resource_clumps\"]", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("ResourceClumpRequirement", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("minimum_upgrade_level", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("stone_damage_per_hit", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.C, pressed: false", combatSource, StringComparison.Ordinal);
        Assert.Contains("RuntimeHelpers.GetHashCode(monster)", combatSource, StringComparison.Ordinal);
        Assert.Contains("CombatTargetHealthSequence", combatSource, StringComparison.Ordinal);
        Assert.Contains("CombatPlayerHealthSequence", combatSource, StringComparison.Ordinal);
        Assert.Contains("executorCombatInterrupt && !manualAutoCombatEnabled", combatSource, StringComparison.Ordinal);
        Assert.Contains("MoveTowardCombatTarget(mine, target)", combatSource, StringComparison.Ordinal);
        Assert.Contains("BuildAdjacentToolPath(mine, target.TilePoint", combatSource, StringComparison.Ordinal);
        Assert.Contains("ResolveCombatWeapon(target, request.CombatWeaponSlotIndex", combatSource, StringComparison.Ordinal);
        Assert.Contains("weapon.enchantments.Any", combatSource, StringComparison.Ordinal);
        Assert.Contains("AreAdjacent(Game1.player.TilePoint, target.TilePoint)", combatSource, StringComparison.Ordinal);
        Assert.Contains("TrackCombatProgress(active) > 600", combatSource, StringComparison.Ordinal);
        Assert.Contains("combat_no_movement_or_damage_progress", combatSource, StringComparison.Ordinal);
        Assert.Contains("out var pathReason, avoidSoftObstacles: true", combatSource, StringComparison.Ordinal);
        Assert.Contains("ApplyExecutorMovementInput", source, StringComparison.Ordinal);
        Assert.Contains("SButton.W, SButton.D, SButton.S, SButton.A", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.MovePosition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position +=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("beforePosition", source, StringComparison.Ordinal);
        Assert.Contains("movedSinceLastTick", source, StringComparison.Ordinal);
        Assert.Contains("ObserveCombatMovement(active)", combatSource, StringComparison.Ordinal);
        Assert.Contains("BeginCombatClearance(active, mine, next)", combatSource, StringComparison.Ordinal);
        Assert.Contains("TickCombatClearance(active, mine)", combatSource, StringComparison.Ordinal);
        Assert.Contains("obj is BreakableContainer", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.C, pressed: true", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("damageMonster(", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("takeDamage(", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("characters.Remove", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Target.Health =", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.FireTool()", combatSource, StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        Assert.Contains("[switch] $CombatOneMonster", smoke, StringComparison.Ordinal);
        Assert.Contains("target_runtime_identity", smoke, StringComparison.Ordinal);
        Assert.Contains("combat_target_health_sequence", smoke, StringComparison.Ordinal);
        Assert.Contains("combat_target_removed", smoke, StringComparison.Ordinal);
        Assert.Contains("-TimeoutSeconds 150", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSlingshotAndBombUseNativeInputWithoutDirectDamageOrExplosionCalls()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var shootStart = source.IndexOf("private void StartShootMonster", StringComparison.Ordinal);
        var bombStart = source.IndexOf("private void StartPlaceBomb", shootStart, StringComparison.Ordinal);
        var meleeStart = source.IndexOf("private void StartCombatMonster", bombStart, StringComparison.Ordinal);
        Assert.True(shootStart >= 0 && bombStart > shootStart && meleeStart > bombStart);
        var shootSource = source[shootStart..bombStart];
        var bombSource = source[bombStart..meleeStart];

        Assert.Contains("executor.shoot_monster", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.BeginUsingTool()", shootSource, StringComparison.Ordinal);
        Assert.Contains("active.Slingshot.onRelease", shootSource, StringComparison.Ordinal);
        Assert.Contains("HoldTicks < 20", shootSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.setMousePosition", shootSource, StringComparison.Ordinal);
        Assert.Contains("AimPrepared", shootSource, StringComparison.Ordinal);
        Assert.Contains("SlingshotAimPatch.AimWorldPixel", shootSource, StringComparison.Ordinal);
        Assert.Contains("HasClearProjectilePath", shootSource, StringComparison.Ordinal);
        Assert.Contains("ExplosiveAmmoAreaIsSafe", shootSource, StringComparison.Ordinal);
        Assert.Contains("explosive_ammo_player_inside_target_motion_envelope", shootSource, StringComparison.Ordinal);
        Assert.Contains("explosive_ammo_other_farmer_inside_target_motion_envelope", shootSource, StringComparison.Ordinal);
        Assert.Contains("explosive_ammo_protected_object_inside_target_motion_envelope", shootSource, StringComparison.Ordinal);
        Assert.Contains("explosive_ammo_terrain_feature_inside_target_motion_envelope", shootSource, StringComparison.Ordinal);
        Assert.DoesNotContain("damageMonster(", shootSource, StringComparison.Ordinal);
        Assert.DoesNotContain("takeDamage(", shootSource, StringComparison.Ordinal);

        Assert.Contains("executor.place_bomb", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiRightButtonOverride(pressed: true", bombSource, StringComparison.Ordinal);
        Assert.Contains("PlaceBombStage.AimPlacement", bombSource, StringComparison.Ordinal);
        Assert.Contains("PrepareNativeBombPlacement", bombSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.TilePoint != active.Stand", bombSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.GetGrabTile()", bombSource, StringComparison.Ordinal);
        Assert.Contains("BombPlacementCursorPatch.ScreenPixel", bombSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.setMousePosition", bombSource, StringComparison.Ordinal);
        Assert.Contains("TickBombPathMovement", bombSource, StringComparison.Ordinal);
        Assert.Contains("bomb_escape_finished_inside_damage_square", bombSource, StringComparison.Ordinal);
        Assert.Contains("bomb_target_terminal_state_not_ready", bombSource, StringComparison.Ordinal);
        Assert.Contains("natural_explosion_finalized_target_monster", bombSource, StringComparison.Ordinal);
        Assert.Contains("bomb_target_outside_damage_square", bombSource, StringComparison.Ordinal);
        Assert.Contains("CombatTerminalState = active.TerminalState", bombSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".explode(", bombSource, StringComparison.Ordinal);
        Assert.DoesNotContain("placementAction(", bombSource, StringComparison.Ordinal);

        var meleeSource = source[meleeStart..];
        Assert.Contains("knockdown_requires_bomb_finish", meleeSource, StringComparison.Ordinal);
        Assert.Contains("native_melee_knocked_down_mummy_for_bomb_finish", meleeSource, StringComparison.Ordinal);
        Assert.Contains("mummy.reviveTimer.Value > 0", meleeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentSlingshotProjectionPublishesExplosiveAreaSafetyAndUtility()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "StardewAI.TransparentBridge", "Adapters", "MiningReadAdapter.cs"));
        var start = source.IndexOf("private static object ReadSlingshotAttackProjection", StringComparison.Ordinal);
        var end = source.IndexOf("private static MeleeDamageDistribution BuildSlingshotDamageDistribution", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var projectionSource = source[start..end];

        Assert.Contains("ReadExplosiveAmmoAreaProjection", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_safe", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_useful_object_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_additional_monster_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_protected_object_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_protected_terrain_feature_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_other_farmer_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("safe_across_current_target_plus_one_tile_motion_envelope", projectionSource, StringComparison.Ordinal);
        Assert.Contains("complete_direct_projectile_damage_with_exact_area_safety_and_utility_but_uncomposed_area_damage", projectionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeRecoveryUsesNativeEatLifecycleWithoutDirectHealthOrInventoryMutation()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = source.IndexOf("private void StartConsumeFood", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartCombatMonster", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var recoverySource = source[start..end];

        Assert.Contains("executor.consume_food", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImmediateMiningThreat(mine)", recoverySource, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiRightButtonOverride(pressed: true", recoverySource, StringComparison.Ordinal);
        Assert.Contains("answerDialogueAction(\"Eat_Yes\"", recoverySource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.isEating", recoverySource, StringComparison.Ordinal);
        Assert.Contains("consume_food_health_not_recovered", recoverySource, StringComparison.Ordinal);
        Assert.Contains("RestoreConsumeFoodSlot(active)", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("eatHeldObject(", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("eatObject(", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.health =", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.health +=", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("reduceActiveItemByOne", recoverySource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePickupWalksIntoNaturalCollectionAndCombatInterruptsWithoutDirectCollect()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = source.IndexOf("private void StartPickupDebris", StringComparison.Ordinal);
        var end = source.IndexOf("private static Debris? DebrisAt", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var pickupSource = source[start..end];

        Assert.Contains("TryBuildTilePath", pickupSource, StringComparison.Ordinal);
        Assert.Contains("MovePlayerForTick()", pickupSource, StringComparison.Ordinal);
        Assert.Contains("ImmediateMiningThreat(mine)", pickupSource, StringComparison.Ordinal);
        Assert.Contains("active.CombatInterrupted = true", pickupSource, StringComparison.Ordinal);
        Assert.Contains("activePickupDebris?.CombatInterrupted == true", source, StringComparison.Ordinal);
        Assert.Contains("game_update_naturally_collected_chunk", pickupSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".collect(", pickupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Chunks.Remove", pickupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("debris.Remove", pickupSource, StringComparison.Ordinal);
    }

    [Fact]
    public void OpportunisticDebrisSkipsNonItemVisualsAndRuntimeRebindsStableIdentity()
    {
        var root = FindRepositoryRoot();
        var bridge = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "MiningReadAdapter.cs"));
        var planner = File.ReadAllText(Path.Combine(root, "src", "StardewAI.Core", "Execution", "MiningFloorStepPlanner.cs"));
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));

        Assert.Contains("is_collectible_item_debris", bridge, StringComparison.Ordinal);
        Assert.Contains("non_item_visual_or_numeric_debris", bridge, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(qualifiedItemId)", planner, StringComparison.Ordinal);
        Assert.Contains("pickup_debris_item_identity_required", runtime, StringComparison.Ordinal);
        Assert.Contains("DebrisAt(location, target, request.DebrisIndex, request.QualifiedItemId)", runtime, StringComparison.Ordinal);
        Assert.Contains("Concat(indexes.Where(index => index != debrisIndex.Value))", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrySeparatesMiningMechanicalPrimitivesFromSmallModelGoal()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();

        foreach (var optionId in new[] { "executor.mine_stone", "executor.break_container", "executor.combat_monster", "executor.shoot_monster", "executor.place_bomb", "executor.consume_food", "executor.descend_ladder", "executor.descend_shaft", "executor.exit_mine" })
        {
            var option = registry.GetRequired(optionId);
            Assert.Equal(CompilerResponsibilities.FullActionExpansion, option.CompilerResponsibility);
            Assert.Equal(TrainingRoles.ExecutorCalibration, option.TrainingRole);
        }

        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.mine_stone").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.combat_monster").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.shoot_monster").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.place_bomb").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.break_container").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Recovery, registry.GetRequired("executor.consume_food").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.descend_ladder").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.descend_shaft").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Recovery, registry.GetRequired("executor.exit_mine").BehaviorCategory);
    }

    [Fact]
    public void RuntimeLadderUsesBfsAndNativeCheckActionWithoutDirectMineTransition()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = source.IndexOf("private void StartDescendLadder", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartConsumeFood", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var ladderSource = source[start..end];

        Assert.Contains("BuildCompilerAdjacentPath", ladderSource, StringComparison.Ordinal);
        Assert.Contains("MovePlayerForTick()", ladderSource, StringComparison.Ordinal);
        Assert.Contains("getTileIndexAt(active.Target.X, active.Target.Y, \"Buildings\", \"mine\") != 173", ladderSource, StringComparison.Ordinal);
        Assert.Contains("active.MineBefore.checkAction", ladderSource, StringComparison.Ordinal);
        Assert.Contains("afterMine.mineLevel == active.MineLevelBefore + 1", ladderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine(", ladderSource, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\.mineLevel\s*=(?!=)", ladderSource);
    }

    [Fact]
    public void RuntimeShaftUsesNativePromptAndVerifiesPreviewWithoutDirectTransition()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = source.IndexOf("private void StartDescendShaft", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartConsumeFood", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var shaftSource = source[start..end];

        Assert.Contains("BuildCompilerAdjacentPath", shaftSource, StringComparison.Ordinal);
        Assert.Contains("getTileIndexAt(target.X, target.Y, \"Buildings\", \"mine\") != 174", shaftSource, StringComparison.Ordinal);
        Assert.Contains("active.MineBefore.checkAction", shaftSource, StringComparison.Ordinal);
        Assert.Contains("answerDialogueAction(\"Shaft_Jump\"", shaftSource, StringComparison.Ordinal);
        Assert.Contains("afterMine.mineLevel == active.ExpectedMineLevelAfter", shaftSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.health != active.ExpectedHealthAfter", shaftSource, StringComparison.Ordinal);
        Assert.Contains("mine.getMineArea() != MineShaft.desertArea", shaftSource, StringComparison.Ordinal);
        Assert.Contains("mine.mineLevel <= MineShaft.bottomOfMineLevel", shaftSource, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiLeftButtonOverride(pressed: true", shaftSource, StringComparison.Ordinal);
        Assert.Contains("native_fall_dialogue_advanced_by_input", shaftSource, StringComparison.Ordinal);
        Assert.DoesNotContain("enterMineShaft(", shaftSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine(", shaftSource, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\.mineLevel\s*=(?!=)", shaftSource);
        Assert.DoesNotMatch(@"(?m)^\s*(?:Game1\.)?player\.health\s*=(?!=)", shaftSource);
    }

    [Fact]
    public void RuntimeMineExitUsesNativePromptWithoutDirectWarp()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = source.IndexOf("private void StartExitMine", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartConsumeFood", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var exitSource = source[start..end];

        Assert.Contains("getTileIndexAt(target.X, target.Y, \"Buildings\", \"mine\") != 115", exitSource, StringComparison.Ordinal);
        Assert.Contains("active.MineBefore.checkAction", exitSource, StringComparison.Ordinal);
        Assert.Contains("answerDialogueAction(\"ExitMine_Leave\"", exitSource, StringComparison.Ordinal);
        Assert.Contains("ExpectedMineExitDestination", exitSource, StringComparison.Ordinal);
        Assert.Contains("BuildCompilerMineExitPath", exitSource, StringComparison.Ordinal);
        Assert.Contains("is < 1 or > 2", exitSource, StringComparison.Ordinal);
        Assert.DoesNotContain("warpFarmer(", exitSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine(", exitSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSkullKeyClaimUsesNativeTwoStageChestActionWithoutDirectProgressMutation()
    {
        var root = FindRepositoryRoot();
        var runtimeSource = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = runtimeSource.IndexOf("private void StartSkullKeyChestInteraction", StringComparison.Ordinal);
        var end = runtimeSource.IndexOf("private TrainingExecutionResult ExecuteInteract", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var claimSource = runtimeSource[start..end];

        Assert.Contains("SkullKeyChestStage.OpenChest", claimSource, StringComparison.Ordinal);
        Assert.Contains("SkullKeyChestStage.ClaimItem", claimSource, StringComparison.Ordinal);
        Assert.True(claimSource.Split("mine.checkAction(", StringSplitOptions.None).Length >= 3);
        Assert.Contains("Game1.player.hasSkullKey", claimSource, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"Game1\.player\.hasSkullKey\s*=", claimSource);

        var adapterSource = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "MiningReadAdapter.cs"));
        Assert.Contains("mine.overlayObjects", adapterSource, StringComparison.Ordinal);
        Assert.Contains("item.which.Value == 4", adapterSource, StringComparison.Ordinal);

        var loopSource = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMiningReachDepthLoop.ps1"));
        Assert.Contains("[switch] $AcquireSkullKey", loopSource, StringComparison.Ordinal);
        Assert.Contains("mining.obtain_skull_key", loopSource, StringComparison.Ordinal);
        Assert.Contains("$skullKeyTransitionObserved", loopSource, StringComparison.Ordinal);
        Assert.Contains("before player.has_skull_key became true", loopSource, StringComparison.Ordinal);
    }

    private static MiningFloorStepPlan Plan(
        string ladders = "[]",
        string shafts = "[]",
        string exits = "[]",
        string goldenScytheAltars = "[]",
        string objects = "[]",
        string monsters = "[]",
        bool mustKillAll = false,
        bool goldenScytheApplicable = false,
        bool goldenScytheClaimed = false,
        string skullKeyRewardChests = "[]",
        bool skullKeyApplicable = false,
        bool hasSkullKey = false,
        int? mineLevel = null,
        string[]? rows = null,
        string resources = "{\"health\":100,\"max_health\":100,\"energy\":220,\"current_time\":1200,\"selected_slot_index\":4,\"inventory_capacity\":{\"empty_slots\":12},\"food_slots\":[]}",
        string mineKind = "ordinary_mines",
        MiningFloorObjective? objective = null)
    {
        rows ??= new[] { "111111", "100001", "100001", "100001", "111111" };
        var rowsJson = JsonSerializer.Serialize(rows);
        var width = rows[0].Length;
        var height = rows.Length;
        var json = """
        {
          "player": {
            "has_skull_key": {"status":"available","value":HAS_SKULL_KEY}
          },
          "mining": {
            "current_mine": {"status":"available","value":{"mine_level":MINE_LEVEL,"mine_kind":"MINE_KIND"}},
            "tiles": {"status":"available","value":{"player_tile":{"tile_x":1,"tile_y":2},"ladders":LADDERS,"shafts":SHAFTS,"exits":EXITS,"golden_scythe_altars":GOLDEN_SCYTHE_ALTARS,"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":WIDTH,"height":HEIGHT,"blocked_rows":ROWS}}},
            "objects": {"status":"available","value":OBJECTS},
            "resource_clumps": {"status":"available","value":[]},
            "monsters": {"status":"available","value":MONSTERS},
            "floor_objectives": {"status":"available","value":{"must_kill_all_monsters_to_advance":MUST_KILL_ALL,"golden_scythe_applicable":GOLDEN_SCYTHE_APPLICABLE,"golden_scythe_claimed":GOLDEN_SCYTHE_CLAIMED,"skull_key_applicable":SKULL_KEY_APPLICABLE,"skull_key_acquired":HAS_SKULL_KEY,"skull_key_reward_chests":SKULL_KEY_REWARD_CHESTS}},
            "player_resources": {"status":"available","value":RESOURCES}
          }
        }
        """
            .Replace("LADDERS", ladders, StringComparison.Ordinal)
            .Replace("SHAFTS", shafts, StringComparison.Ordinal)
            .Replace("EXITS", exits, StringComparison.Ordinal)
            .Replace("GOLDEN_SCYTHE_ALTARS", goldenScytheAltars, StringComparison.Ordinal)
            .Replace("SKULL_KEY_REWARD_CHESTS", skullKeyRewardChests, StringComparison.Ordinal)
            .Replace("WIDTH", width.ToString(), StringComparison.Ordinal)
            .Replace("HEIGHT", height.ToString(), StringComparison.Ordinal)
            .Replace("ROWS", rowsJson, StringComparison.Ordinal)
            .Replace("OBJECTS", objects, StringComparison.Ordinal)
            .Replace("MONSTERS", monsters, StringComparison.Ordinal)
            .Replace("RESOURCES", resources, StringComparison.Ordinal)
            .Replace("MINE_LEVEL", (mineLevel ?? (mineKind == "skull_cavern" ? 121 : mineKind == "quarry_mine" ? 77377 : 40)).ToString(), StringComparison.Ordinal)
            .Replace("MINE_KIND", mineKind, StringComparison.Ordinal)
            .Replace("MUST_KILL_ALL", mustKillAll.ToString().ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("GOLDEN_SCYTHE_APPLICABLE", goldenScytheApplicable.ToString().ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("GOLDEN_SCYTHE_CLAIMED", goldenScytheClaimed.ToString().ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("SKULL_KEY_APPLICABLE", skullKeyApplicable.ToString().ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("HAS_SKULL_KEY", hasSkullKey.ToString().ToLowerInvariant(), StringComparison.Ordinal);
        return new MiningFloorStepPlanner().Plan(Snapshot(json), objective ?? new MiningFloorObjective());
    }

    private static MiningFloorStepPlan ObjectivePlan(
        MiningFloorObjective objective,
        string objects = "[]",
        string monsters = "[]",
        string debris = "[]",
        string resources = "{\"health\":100,\"max_health\":100,\"selected_slot_index\":4,\"food_slots\":[]}",
        string dropCatalogs = "[]")
    {
        var json = """
        {
          "mining": {
            "tiles": {"status":"available","value":{"player_tile":{"tile_x":1,"tile_y":2},"ladders":[],"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":8,"height":5,"blocked_rows":["11111111","10000001","10000001","10000001","11111111"]}}},
            "objects": {"status":"available","value":OBJECTS},
            "resource_clumps": {"status":"available","value":[]},
            "monsters": {"status":"available","value":MONSTERS},
            "monster_drop_catalogs": {"status":"available","value":DROP_CATALOGS},
            "debris": {"status":"available","value":DEBRIS},
            "floor_objectives": {"status":"available","value":{"must_kill_all_monsters_to_advance":false}},
            "player_resources": {"status":"available","value":RESOURCES}
          }
        }
        """
            .Replace("OBJECTS", objects, StringComparison.Ordinal)
            .Replace("MONSTERS", monsters, StringComparison.Ordinal)
            .Replace("DROP_CATALOGS", dropCatalogs, StringComparison.Ordinal)
            .Replace("DEBRIS", debris, StringComparison.Ordinal)
            .Replace("RESOURCES", resources, StringComparison.Ordinal);
        return new MiningFloorStepPlanner().Plan(Snapshot(json), objective);
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = "test",
            GameTick = 1,
            State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
