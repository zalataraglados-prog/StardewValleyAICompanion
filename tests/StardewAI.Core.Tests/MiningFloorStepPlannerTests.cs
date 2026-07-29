using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class MiningFloorStepPlannerTests
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
    public void GoldenScytheObjectiveReplansAfterFourTileApproachHorizon()
    {
        var plan = Plan(
            goldenScytheAltars:
                "[{\"tile_x\":18,\"tile_y\":2,\"action\":\"GoldenScythe\"}]",
            rows: new[]
            {
                "11111111111111111111",
                "10000000000000000001",
                "10000000000000000001",
                "10000000000000000001",
                "11111111111111111111"
            },
            mineKind: "quarry_mine",
            goldenScytheApplicable: true,
            objective: new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.AcquireGoldenScythe
            });

        Assert.Equal(
            MiningFloorStepKinds.MoveToGoldenScytheAltar,
            plan.StepKind);
        Assert.Equal(5, plan.TargetTileX);
        Assert.Equal(2, plan.TargetTileY);
        Assert.Equal(4, plan.EstimatedMovementTiles);
        Assert.Equal(5, plan.Path.Length);
    }

    [Fact]
    public void GoldenScytheObjectiveApproachesDistantRouteClumpBeforeBreaking()
    {
        var plan = Plan(
            goldenScytheAltars:
                "[{\"tile_x\":18,\"tile_y\":2,\"action\":\"GoldenScythe\"}]",
            resourceClumps:
                "[{\"tile_x\":15,\"tile_y\":1,\"width\":2,\"height\":2,\"parent_sheet_index\":754,\"health\":8,\"expected_hits_remaining\":3,\"selected_tool_slot_index\":3,\"required_tool\":\"pickaxe\",\"minimum_upgrade_level\":0,\"selected_tool_qualified_item_id\":\"(T)GoldPickaxe\",\"selected_tool_upgrade_level\":3,\"selected_tool_additional_power\":0,\"selected_tool_effective_upgrade_level\":3,\"damage_per_hit\":3,\"native_executor_supported\":true,\"tool_gate_satisfied\":true,\"executor_status\":\"native_executor_available\",\"expected_core_output_items_json\":\"[]\",\"runtime_type\":\"StardewValley.TerrainFeatures.ResourceClump\"}]",
            rows: new[]
            {
                "11111111111111111111",
                "10000000000000011001",
                "10000000000000011001",
                "11111111111111111111"
            },
            mineKind: "quarry_mine",
            goldenScytheApplicable: true,
            objective: new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.AcquireGoldenScythe
            });

        Assert.Equal(
            MiningFloorStepKinds.MoveToGoldenScytheAltar,
            plan.StepKind);
        Assert.Equal(
            "approach_golden_scythe_route_clearance",
            plan.Reason);
        Assert.Equal(5, plan.TargetTileX);
        Assert.Equal(2, plan.TargetTileY);
        Assert.Equal(4, plan.EstimatedMovementTiles);
        Assert.Equal(5, plan.Path.Length);
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
    public void GoldenScytheObjectiveClearsRouteWhenClaimedExitIsNotYetReachable()
    {
        var plan = Plan(
            goldenScytheAltars: "[{\"tile_x\":2,\"tile_y\":2,\"action\":\"GoldenScythe\"}]",
            exits: "[{\"tile_x\":5,\"tile_y\":2,\"expected_destination\":{\"location_id\":\"Mine\",\"tile_x\":67,\"tile_y\":10}}]",
            objects: "[{\"tile_x\":3,\"tile_y\":2,\"qualified_item_id\":\"(O)32\",\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1}]",
            rows: new[] { "1111111", "1001001", "1001001", "1001001", "1111111" },
            mineKind: "quarry_mine",
            goldenScytheApplicable: true,
            goldenScytheClaimed: true,
            objective: new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.AcquireGoldenScythe
            });

        Assert.Equal(MiningFloorStepKinds.MineStone, plan.StepKind);
        Assert.Equal(3, plan.TargetTileX);
        Assert.Equal(2, plan.TargetTileY);
    }

    [Fact]
    public void GoldenScytheClaimedExitApproachUsesExitRouteSemantics()
    {
        var plan = Plan(
            exits: "[{\"tile_x\":18,\"tile_y\":2,\"expected_destination\":{\"location_id\":\"Mine\",\"tile_x\":67,\"tile_y\":10}}]",
            objects: "[{\"tile_x\":8,\"tile_y\":2,\"qualified_item_id\":\"(O)32\",\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1}]",
            rows: new[]
            {
                "11111111111111111111",
                "10000000010000000001",
                "10000000010000000001",
                "10000000010000000001",
                "11111111111111111111"
            },
            mineKind: "quarry_mine",
            goldenScytheApplicable: true,
            goldenScytheClaimed: true,
            objective: new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.AcquireGoldenScythe
            });

        Assert.Equal(
            MiningFloorStepKinds.MoveToMineExitRoute,
            plan.StepKind);
        Assert.Equal(
            "approach_golden_scythe_exit_route_clearance",
            plan.Reason);
        Assert.Equal(
            "executor.move_to_tile",
            MiningFloorStepCompiler.ExecutionOptionId(plan));
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

}
