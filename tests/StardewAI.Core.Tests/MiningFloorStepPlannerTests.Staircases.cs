using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class MiningFloorStepPlannerTests
{
    private const string AvailableStaircasePlacement =
        """
        {
          "status":"available",
          "qualified_item_id":"(BC)71",
          "native_floor_rule_allows":true,
          "projection_status":"exact_native_direct_tile_subset_no_recursive_relocation",
          "candidates":[
            {
              "target_tile_x":3,
              "target_tile_y":2,
              "expected_ladder_tile_x":3,
              "expected_ladder_tile_y":2,
              "native_search_iteration":1,
              "target_rule_status":"exact_first_recursive_candidate_direct_tile"
            }
          ]
        }
        """;

    private const string StaircaseResources =
        """
        {
          "health":100,
          "max_health":100,
          "energy":220,
          "current_time":1200,
          "selected_slot_index":4,
          "inventory_capacity":{"empty_slots":12},
          "food_slots":[],
          "staircase_count":2,
          "staircase_slots":[
            {
              "slot_index":6,
              "qualified_item_id":"(BC)71",
              "stack":2
            }
          ]
        }
        """;

    [Fact]
    public void ExplicitConsumptionPlacesStaircaseBeforeMining()
    {
        var plan = Plan(
            staircasePlacement: AvailableStaircasePlacement,
            objects:
                "[{\"tile_x\":4,\"tile_y\":2,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1}]",
            resources: StaircaseResources,
            objective: new MiningFloorObjective
            {
                ResourcePreservationPolicy =
                    MiningResourcePreservationPolicies
                        .AllowStaircaseConsumption
            });

        Assert.Equal("ready", plan.Status);
        Assert.Equal(MiningFloorStepKinds.PlaceStaircase, plan.StepKind);
        Assert.Equal(
            "explicit_staircase_consumption_no_natural_descent",
            plan.Reason);
        Assert.Equal(3, plan.TargetTileX);
        Assert.Equal(2, plan.TargetTileY);
        Assert.Equal(2, plan.StandTileX);
        Assert.Equal(2, plan.StandTileY);
        Assert.Equal(6, plan.StaircaseSlotIndex);
        Assert.Equal("(BC)71", plan.StaircaseQualifiedItemId);
        Assert.Equal(2, plan.StaircaseCountBefore);
        Assert.Equal(1, plan.StaircaseCountAfter);
        Assert.Equal(
            "executor.place_staircase",
            MiningFloorStepCompiler.ExecutionOptionId(plan));

        var parameters =
            MiningFloorStepCompiler.BuildExecutionParameters(plan);
        Assert.Contains(
            parameters,
            row => row.Name == "qualified_item_id" &&
                row.Value == "(BC)71");
        Assert.Contains(
            parameters,
            row => row.Name == "inventory_item_total_before" &&
                row.Value == "2");
        Assert.Contains(
            parameters,
            row => row.Name == "inventory_item_total_after" &&
                row.Value == "1");
    }

    [Fact]
    public void DefaultPolicyPreservesStaircaseAndContinuesMining()
    {
        var plan = Plan(
            staircasePlacement: AvailableStaircasePlacement,
            objects:
                "[{\"tile_x\":3,\"tile_y\":2,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1}]",
            resources: StaircaseResources);

        Assert.Equal(MiningFloorStepKinds.MineStone, plan.StepKind);
        Assert.Equal("executor.mine_stone",
            MiningFloorStepCompiler.ExecutionOptionId(plan));
    }

    [Fact]
    public void ExistingNaturalLadderPrecedesExplicitStaircaseConsumption()
    {
        var plan = Plan(
            ladders: "[{\"tile_x\":4,\"tile_y\":2}]",
            staircasePlacement: AvailableStaircasePlacement,
            resources: StaircaseResources,
            objective: new MiningFloorObjective
            {
                ResourcePreservationPolicy =
                    MiningResourcePreservationPolicies
                        .AllowStaircaseConsumption
            });

        Assert.Equal(MiningFloorStepKinds.DescendLadder, plan.StepKind);
        Assert.Equal("reachable_ladder_available", plan.Reason);
    }

    [Fact]
    public void ImmediateThreatPreemptsStaircasePlacement()
    {
        var plan = Plan(
            staircasePlacement: AvailableStaircasePlacement,
            monsters:
                "[{\"tile_x\":2,\"tile_y\":2,\"runtime_identity\":\"monster:1\",\"runtime_type\":\"StardewValley.Monsters.GreenSlime\",\"name\":\"Green Slime\",\"expected_melee_attacks\":1,\"selected_melee_weapon_slot_index\":0}]",
            resources: StaircaseResources,
            objective: new MiningFloorObjective
            {
                ResourcePreservationPolicy =
                    MiningResourcePreservationPolicies
                        .AllowStaircaseConsumption
            });

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal(
            "staircase_placement_interrupted_by_immediate_monster_threat",
            plan.Reason);
    }
}
