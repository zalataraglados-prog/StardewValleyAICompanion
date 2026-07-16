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
            shafts: "[{\"tile_x\":4,\"tile_y\":2,\"expected_level_delta\":7,\"expected_mine_level_after\":128,\"expected_health_cost\":21,\"expected_health_after\":79}]");

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
    public void RuntimeBreakContainerUsesNativeHeavyHitterInputAndVerifiesRemoval()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = source.IndexOf("private void StartBreakContainer", StringComparison.Ordinal);
        var end = source.IndexOf("private static bool ImmediateMiningThreat", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var containerSource = source[start..end];

        Assert.Contains("executor.break_container", source, StringComparison.Ordinal);
        Assert.Contains("obj is not BreakableContainer", containerSource, StringComparison.Ordinal);
        Assert.Contains("tool.isHeavyHitter()", containerSource, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.C, pressed: true", containerSource, StringComparison.Ordinal);
        Assert.Contains("native_heavy_hitter_input_removed_container", containerSource, StringComparison.Ordinal);
        Assert.Contains("released_contents_left_as_game_debris", containerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(", containerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("objects.Remove", containerSource, StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        Assert.Contains("[switch] $BreakOneContainer", smoke, StringComparison.Ordinal);
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
    public void RegistrySeparatesMiningMechanicalPrimitivesFromSmallModelGoal()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();

        foreach (var optionId in new[] { "executor.mine_stone", "executor.break_container", "executor.combat_monster", "executor.consume_food", "executor.descend_ladder", "executor.descend_shaft", "executor.exit_mine" })
        {
            var option = registry.GetRequired(optionId);
            Assert.Equal(CompilerResponsibilities.FullActionExpansion, option.CompilerResponsibility);
            Assert.Equal(TrainingRoles.ExecutorCalibration, option.TrainingRole);
        }

        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.mine_stone").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.combat_monster").BehaviorCategory);
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

        Assert.Contains("BuildAdjacentToolPath", ladderSource, StringComparison.Ordinal);
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

        Assert.Contains("BuildAdjacentToolPath", shaftSource, StringComparison.Ordinal);
        Assert.Contains("getTileIndexAt(target.X, target.Y, \"Buildings\", \"mine\") != 174", shaftSource, StringComparison.Ordinal);
        Assert.Contains("active.MineBefore.checkAction", shaftSource, StringComparison.Ordinal);
        Assert.Contains("answerDialogueAction(\"Shaft_Jump\"", shaftSource, StringComparison.Ordinal);
        Assert.Contains("afterMine.mineLevel == active.ExpectedMineLevelAfter", shaftSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.health != active.ExpectedHealthAfter", shaftSource, StringComparison.Ordinal);
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
        Assert.DoesNotContain("warpFarmer(", exitSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine(", exitSource, StringComparison.Ordinal);
    }

    private static MiningFloorStepPlan Plan(
        string ladders = "[]",
        string shafts = "[]",
        string exits = "[]",
        string objects = "[]",
        string monsters = "[]",
        bool mustKillAll = false,
        string[]? rows = null,
        string resources = "{\"health\":100,\"max_health\":100,\"energy\":220,\"current_time\":1200,\"selected_slot_index\":4,\"food_slots\":[]}",
        MiningFloorObjective? objective = null)
    {
        rows ??= new[] { "111111", "100001", "100001", "100001", "111111" };
        var rowsJson = JsonSerializer.Serialize(rows);
        var json = """
        {
          "mining": {
            "tiles": {"status":"available","value":{"player_tile":{"tile_x":1,"tile_y":2},"ladders":LADDERS,"shafts":SHAFTS,"exits":EXITS,"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":6,"height":5,"blocked_rows":ROWS}}},
            "objects": {"status":"available","value":OBJECTS},
            "monsters": {"status":"available","value":MONSTERS},
            "floor_objectives": {"status":"available","value":{"must_kill_all_monsters_to_advance":MUST_KILL_ALL}},
            "player_resources": {"status":"available","value":RESOURCES}
          }
        }
        """
            .Replace("LADDERS", ladders, StringComparison.Ordinal)
            .Replace("SHAFTS", shafts, StringComparison.Ordinal)
            .Replace("EXITS", exits, StringComparison.Ordinal)
            .Replace("ROWS", rowsJson, StringComparison.Ordinal)
            .Replace("OBJECTS", objects, StringComparison.Ordinal)
            .Replace("MONSTERS", monsters, StringComparison.Ordinal)
            .Replace("RESOURCES", resources, StringComparison.Ordinal)
            .Replace("MUST_KILL_ALL", mustKillAll.ToString().ToLowerInvariant(), StringComparison.Ordinal);
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
