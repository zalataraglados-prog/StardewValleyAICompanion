using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class MiningFloorStepPlannerTests
{
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
            debris: "[{\"debris_index\":6,\"qualified_item_id\":\"(O)390\",\"chunks\":[{\"tile_x\":3,\"tile_y\":2}]}]",
            playerInventory: "[{\"slot_index\":0,\"qualified_item_id\":\"(O)390\",\"stack\":21},{\"slot_index\":1,\"qualified_item_id\":\"(O)390\",\"stack\":10}]");

        Assert.Equal(MiningFloorStepKinds.PickupDebris, plan.StepKind);
        Assert.Equal("(O)390", plan.TargetQualifiedItemId);
        Assert.Equal(6, plan.DebrisIndex);
        Assert.Equal(3, plan.TargetTileX);
        Assert.Equal(31, plan.InventoryItemTotalBefore);
        Assert.Contains(
            MiningFloorStepCompiler.BuildExecutionParameters(plan),
            parameter =>
                parameter.Name == "max_movement_tiles" &&
                parameter.Value == "34");
        Assert.Contains(
            MiningFloorStepCompiler.BuildExecutionParameters(plan),
            parameter =>
                parameter.Name == "inventory_item_total_before" &&
                parameter.Value == "31");
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
            monsters: "[{\"runtime_identity\":\"threat\",\"runtime_type\":\"StardewValley.Monsters.GreenSlime\",\"name\":\"Green Slime\",\"tile_x\":2,\"tile_y\":2,\"health\":20,\"damage_to_farmer\":2,\"combat_experience_on_defeat\":3,\"combat_experience_condition\":\"native_defeat\",\"selected_drop_qualified_item_ids\":[],\"melee_attack_projections\":[{\"slot_index\":1,\"expected_attacks_to_defeat\":3.0,\"expected_active_damage_duration_ms\":900.0,\"duration_status\":\"exact_active_melee_phase_excluding_movement\",\"terminal_effect\":\"defeat\"}]}]",
            resources: "{\"health\":100,\"max_health\":100,\"selected_slot_index\":4,\"food_slots\":[],\"cardinal_movement\":{\"tile_duration_ms\":100.0}}");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("unsafe_tool_window_combat_interrupt", plan.Reason);
        Assert.Equal("blocked_by_immediate_monster_threat", plan.SafetyWindowStatus);
        Assert.Equal(4, plan.RestoreSlotIndex);
        Assert.Equal(3d, plan.ExpectedCombatAttacks);
        Assert.Equal(900d, plan.ExpectedCombatDurationMs);
        Assert.Equal(1, plan.CombatWeaponSlotIndex);
        Assert.Equal(3, plan.ExpectedSkillExperience);
        Assert.Equal(
            TrainingCombatIntents.TransitSelfDefense,
            plan.CombatIntent);
        var parameters =
            MiningFloorStepCompiler.BuildExecutionParameters(plan);
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "max_movement_tiles" &&
                parameter.Value == "16");
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "combat_intent" &&
                parameter.Value ==
                    TrainingCombatIntents.TransitSelfDefense);
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "max_attacks" &&
                parameter.Value == "10");
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
            monsters: "[{\"runtime_identity\":\"target\",\"tile_x\":6,\"tile_y\":2,\"health\":20,\"damage_to_farmer\":8,\"selected_drop_qualified_item_ids\":[\"(O)768\"]}]",
            resources: "{\"health\":10,\"max_health\":100,\"selected_slot_index\":4,\"food_slots\":[{\"slot_index\":7,\"qualified_item_id\":\"(O)194\",\"health_recovery\":25,\"sell_price\":120},{\"slot_index\":8,\"qualified_item_id\":\"(O)247\",\"health_recovery\":5,\"sell_price\":100}]}");

        Assert.Equal(MiningFloorStepKinds.ConsumeFood, plan.StepKind);
        Assert.Equal(7, plan.FoodSlotIndex);
        Assert.Equal(4, plan.RestoreSlotIndex);
        Assert.Equal("health_below_two_hit_or_configured_reserve", plan.Reason);
    }

    [Fact]
    public void ImmediateThreatPreemptsLowHealthRecoveryUntilCombatWindowClears()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" },
                MinimumReserveHealth = 20
            },
            monsters: "[{\"runtime_identity\":\"target\",\"tile_x\":2,\"tile_y\":2,\"health\":20,\"damage_to_farmer\":8,\"selected_drop_qualified_item_ids\":[\"(O)768\"]}]",
            resources: "{\"health\":10,\"max_health\":100,\"selected_slot_index\":4,\"melee_weapons\":[{\"slot_index\":1,\"qualified_item_id\":\"(W)4\",\"can_damage\":true,\"minimum_damage\":10,\"maximum_damage\":20,\"attack_speed_ms\":400}],\"food_slots\":[{\"slot_index\":7,\"qualified_item_id\":\"(O)194\",\"health_recovery\":25,\"sell_price\":120}]}");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal(
            "immediate_monster_threat_preempts_recovery",
            plan.Reason);
        Assert.Equal(
            "blocked_by_immediate_monster_threat",
            plan.SafetyWindowStatus);
        Assert.Equal(
            TrainingCombatIntents.TransitSelfDefense,
            plan.CombatIntent);
    }

    [Fact]
    public void OneHitLethalImmediateThreatUsesNativeFoodBeforeCombat()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = new[] { "(O)768" },
                MinimumReserveHealth = 20
            },
            monsters: "[{\"runtime_identity\":\"target\",\"tile_x\":2,\"tile_y\":2,\"health\":166,\"damage_to_farmer\":18,\"selected_drop_qualified_item_ids\":[\"(O)768\"]}]",
            resources: "{\"health\":15,\"max_health\":140,\"selected_slot_index\":4,\"melee_weapons\":[{\"slot_index\":1,\"qualified_item_id\":\"(W)4\",\"can_damage\":true,\"minimum_damage\":10,\"maximum_damage\":20,\"attack_speed_ms\":400}],\"food_slots\":[{\"slot_index\":7,\"qualified_item_id\":\"(O)773\",\"health_recovery\":200,\"sell_price\":1000}]}");

        Assert.Equal(MiningFloorStepKinds.ConsumeFood, plan.StepKind);
        Assert.Equal(7, plan.FoodSlotIndex);
        Assert.Equal(
            "critical_one_hit_recovery_preempts_immediate_threat",
            plan.SafetyWindowStatus);
    }

    [Fact]
    public void ImmediateThreatPrefersFewerContactExposureWindowsBeforeRawSpeed()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectResourceOrArtifact,
                TargetSourceQualifiedItemIds = new[] { "(O)751" }
            },
            objects: "[{\"tile_x\":6,\"tile_y\":2,\"qualified_item_id\":\"(O)751\",\"best_pickaxe_hits_remaining\":2}]",
            monsters: """
            [
              {
                "runtime_identity":"threat",
                "tile_x":2,
                "tile_y":2,
                "health":405,
                "damage_to_farmer":24,
                "melee_attack_projections":[
                  {"slot_index":1,"expected_attacks_to_defeat":14.1,"expected_active_damage_duration_ms":2900.0,"duration_status":"exact_active_melee_phase_excluding_movement"},
                  {"slot_index":12,"expected_attacks_to_defeat":3.9,"expected_active_damage_duration_ms":4100.0,"duration_status":"exact_active_melee_phase_excluding_movement"}
                ]
              }
            ]
            """,
            resources: "{\"health\":100,\"max_health\":140,\"selected_slot_index\":4,\"food_slots\":[],\"cardinal_movement\":{\"tile_duration_ms\":100.0}}");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal(12, plan.CombatWeaponSlotIndex);
        Assert.Equal(3.9d, plan.ExpectedCombatAttacks);
        Assert.Equal(4100d, plan.ExpectedCombatDurationMs);
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

}
