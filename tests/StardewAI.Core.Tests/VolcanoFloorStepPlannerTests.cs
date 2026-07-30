using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class VolcanoFloorStepPlannerTests
{
    [Fact]
    public void PlannerClearsTheForwardRouteInsteadOfNearestDeadEnd()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":9}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":5},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "1111111",
                  "1000011",
                  "1111011",
                  "1001011",
                  "1111011",
                  "1100011",
                  "1111111"
                ],
                "blocked_rows":[
                  "1111111",
                  "1000011",
                  "1111011",
                  "1001011",
                  "1111011",
                  "1110011",
                  "1111111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":4,
                "tile_y":1,
                "target_location":"Caldera",
                "target_tile_x":21,
                "target_tile_y":39
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[
              {
                "tile_x":1,
                "tile_y":4,
                "is_breakable_stone":true,
                "is_breakable_container":false,
                "health_or_hits_remaining":1,
                "runtime_type":"StardewValley.Object",
                "qualified_item_id":"(O)390"
              },
              {
                "tile_x":2,
                "tile_y":5,
                "is_breakable_stone":true,
                "is_breakable_container":false,
                "health_or_hits_remaining":1,
                "runtime_type":"StardewValley.Object",
                "qualified_item_id":"(O)390"
              }
            ]},
            "monsters": {"status":"available","value":[]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[{
                "slot_index":1,
                "damage_per_hit":4
              }],
              "heavy_hitter_slots":[],
              "weapon_slots":[]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(VolcanoFloorStepKinds.BreakStone, step.StepKind);
        Assert.Equal(2, step.TargetTileX);
        Assert.Equal(5, step.TargetTileY);
        Assert.Equal("forward_route_native_pickaxe_target", step.Reason);
    }

    [Fact]
    public void CompilerKeepsDynamicMonsterPursuitBudgetOpen()
    {
        var parameters = VolcanoFloorStepCompiler.BuildExecutionParameters(
            new VolcanoFloorStepPlan
            {
                Status = "ready",
                StepKind = VolcanoFloorStepKinds.CombatMonster,
                EstimatedMovementTiles = 3
            });

        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "max_movement_tiles" &&
                parameter.Value == "512");
    }

    [Fact]
    public void PlannerWaitsForPressedDwarfGateNativeOpening()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":9}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "111",
                  "101",
                  "111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[]
            }},
            "gates": {"status":"available","value":[{
              "gate_index":3,
              "blocking_tile_x":1,
              "blocking_tile_y":0,
              "opened":false,
              "all_switches_pressed":true,
              "switches":[{
                "tile_x":1,
                "tile_y":1,
                "pressed":true
              }]
            }]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);
        var parameters =
            VolcanoFloorStepCompiler.BuildExecutionParameters(step);

        Assert.Equal(
            VolcanoFloorStepKinds.WaitForDwarfGate,
            step.StepKind);
        Assert.Equal(
            "executor.wait_ticks",
            VolcanoFloorStepCompiler.ExecutionOptionId(step));
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "wait_ticks" &&
                parameter.Value == "120");
    }

    [Fact]
    public void CompilerTypesForwardConnectorAsNativeWarp()
    {
        var parameters = VolcanoFloorStepCompiler.BuildExecutionParameters(
            new VolcanoFloorStepPlan
            {
                Status = "ready",
                StepKind =
                    VolcanoFloorStepKinds.TraverseForwardConnector
            });

        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "connector_kind" &&
                parameter.Value == "warp");
    }

    [Fact]
    public void GeneratedFloorWarpSentinelDoesNotClaimExactArrival()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":0}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "blocked_rows":[
                  "11111",
                  "10001",
                  "11111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":3,
                "tile_y":1,
                "target_location":"VolcanoDungeon1",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(
            VolcanoFloorStepKinds.TraverseForwardConnector,
            step.StepKind);
        Assert.Null(step.ExpectedArrivalTileX);
        Assert.Null(step.ExpectedArrivalTileY);
    }

    [Fact]
    public void PlannerInterceptsGliderOnlyInsideRuntimeDangerWindow()
    {
        var snapshotJson = """
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":3}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "11111111111111",
                  "10000000000001",
                  "11111111111111"
                ],
                "blocked_rows":[
                  "11111111111111",
                  "10000000000001",
                  "11111111111111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":3,
                "tile_y":1,
                "target_location":"VolcanoDungeon4",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[{
              "runtime_identity":"A1",
              "runtime_type":"StardewValley.Monsters.Bat",
              "name":"Magma Sprite",
              "tile_x":4,
              "tile_y":1,
              "is_glider":true,
              "melee_executor_supported":true
            }]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[{
                "slot_index":2,
                "maximum_damage":120,
                "is_scythe":false
              }]
            }}
          }
        }
        """;
        var snapshot = Snapshot(snapshotJson);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(
            VolcanoFloorStepKinds.TraverseForwardConnector,
            step.StepKind);

        var distantStep = new VolcanoFloorStepPlanner().Plan(
            Snapshot(
                snapshotJson.Replace(
                    "\"tile_x\":4",
                    "\"tile_x\":5",
                    StringComparison.Ordinal)));

        Assert.Equal(
            VolcanoFloorStepKinds.TraverseForwardConnector,
            distantStep.StepKind);
    }

    [Fact]
    public void PlannerInterceptsNearbyGliderOnPlayerSideOfConnector()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":3}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":["1111111","1000001","1111111"],
                "blocked_rows":["1111111","1000001","1111111"]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":5,
                "tile_y":1,
                "target_location":"VolcanoDungeon4",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[{
              "runtime_identity":"A2",
              "runtime_type":"StardewValley.Monsters.Bat",
              "name":"Magma Sprite",
              "tile_x":3,
              "tile_y":1,
              "is_glider":true,
              "melee_executor_supported":true
            }]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[{
                "slot_index":2,
                "maximum_damage":120,
                "is_scythe":false
              }]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(
            VolcanoFloorStepKinds.CombatMonster,
            step.StepKind);
        Assert.Equal("A2", step.TargetRuntimeIdentity);
        Assert.Equal(
            TrainingCombatIntents.TransitSelfDefense,
            step.CombatIntent);
        var parameters =
            VolcanoFloorStepCompiler.BuildExecutionParameters(step);
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "max_movement_tiles" &&
                parameter.Value == "16");
    }

    [Fact]
    public void PlannerInterceptsNearbyGliderWithoutLandApproach()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":7}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "111111",
                  "101111",
                  "111111",
                  "101111",
                  "111111"
                ],
                "blocked_rows":[
                  "111111",
                  "101111",
                  "111111",
                  "101111",
                  "111111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":1,
                "tile_y":3,
                "target_location":"VolcanoDungeon8",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[{
              "runtime_identity":"FLOATING",
              "runtime_type":"StardewValley.Monsters.Bat",
              "name":"Magma Sprite",
              "tile_x":3,
              "tile_y":2,
              "is_glider":true,
              "melee_executor_supported":true
            }]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[{
                "slot_index":2,
                "maximum_damage":120,
                "is_scythe":false
              }]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(
            VolcanoFloorStepKinds.CombatMonster,
            step.StepKind);
        Assert.Equal("FLOATING", step.TargetRuntimeIdentity);
    }

    [Fact]
    public void PlannerDoesNotFightGroundMonsterAcrossDynamicBarrier()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":9}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "1111111",
                  "1000111",
                  "1011111",
                  "1111111"
                ],
                "blocked_rows":[
                  "1111111",
                  "1011111",
                  "1011111",
                  "1111111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":1,
                "tile_y":2,
                "target_location":"Caldera",
                "target_tile_x":21,
                "target_tile_y":39
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[{
              "runtime_identity":"S1",
              "runtime_type":"StardewValley.Monsters.GreenSlime",
              "name":"Tiger Slime",
              "tile_x":3,
              "tile_y":1,
              "is_glider":false,
              "melee_executor_supported":true
            }]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[{
                "slot_index":2,
                "maximum_damage":120,
                "is_scythe":false
              }]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(
            VolcanoFloorStepKinds.TraverseForwardConnector,
            step.StepKind);
    }

    [Fact]
    public void PlannerClearsReachableMonsterBlockingDynamicObstacleRoute()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":7}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "1111111111",
                  "1000000001",
                  "1111111111"
                ],
                "blocked_rows":[
                  "1111111111",
                  "1000010001",
                  "1111111111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":8,
                "tile_y":1,
                "target_location":"VolcanoDungeon8",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[{
              "tile_x":7,
              "tile_y":1,
              "is_breakable_stone":true,
              "is_breakable_container":false,
              "health_or_hits_remaining":1,
              "runtime_type":"StardewValley.Object",
              "qualified_item_id":"(O)847"
            }]},
            "monsters": {"status":"available","value":[{
              "runtime_identity":"BLOCKER",
              "runtime_type":"StardewValley.Monsters.GreenSlime",
              "name":"Tiger Slime",
              "tile_x":5,
              "tile_y":1,
              "is_glider":false,
              "melee_executor_supported":true
            }]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[{
                "slot_index":1,
                "damage_per_hit":4
              }],
              "heavy_hitter_slots":[],
              "weapon_slots":[{
                "slot_index":2,
                "maximum_damage":120,
                "is_scythe":false
              }]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(
            VolcanoFloorStepKinds.CombatMonster,
            step.StepKind);
        Assert.Equal("BLOCKER", step.TargetRuntimeIdentity);
        Assert.Equal(4, step.StandTileX);
        Assert.Equal(1, step.StandTileY);
        Assert.Equal(
            TrainingCombatIntents.TransitRouteClearance,
            step.CombatIntent);

        var parameters =
            VolcanoFloorStepCompiler.BuildExecutionParameters(step);
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "combat_intent" &&
                parameter.Value ==
                    TrainingCombatIntents.TransitRouteClearance);
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "max_movement_tiles" &&
                int.Parse(parameter.Value) < 512);
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "route_objective_id" &&
                parameter.Value == "volcano_forward_connector");
        Assert.Contains(
            parameters,
            parameter =>
                parameter.Name == "blocked_route_cell_x" &&
                parameter.Value == "5");
    }

    [Fact]
    public void PlannerChoosesRouteBlockerOverCloserSideMonster()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":7}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":2},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "1111111111",
                  "1000011111",
                  "1000000001",
                  "1111111111"
                ],
                "blocked_rows":[
                  "1111111111",
                  "1000111111",
                  "1000001001",
                  "1111111111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":8,
                "tile_y":2,
                "target_location":"VolcanoDungeon8",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[
              {
                "runtime_identity":"SIDE",
                "runtime_type":"StardewValley.Monsters.GreenSlime",
                "name":"Side Slime",
                "tile_x":4,
                "tile_y":1,
                "is_glider":false,
                "melee_executor_supported":true
              },
              {
                "runtime_identity":"BLOCKER",
                "runtime_type":"StardewValley.Monsters.GreenSlime",
                "name":"Route Slime",
                "tile_x":6,
                "tile_y":2,
                "is_glider":false,
                "melee_executor_supported":true
              }
            ]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[{
                "slot_index":2,
                "maximum_damage":120,
                "is_scythe":false
              }]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(VolcanoFloorStepKinds.CombatMonster, step.StepKind);
        Assert.Equal("BLOCKER", step.TargetRuntimeIdentity);
        Assert.Equal(6, step.BlockedRouteCellX);
        Assert.Equal(
            "exact_weighted_route_cell_identity",
            step.BlockerAttributionStatus);
    }

    [Fact]
    public void PlannerFailsClosedForUnattributedDynamicRouteBlock()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":7}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "1111111111",
                  "1000000001",
                  "1111111111"
                ],
                "blocked_rows":[
                  "1111111111",
                  "1000010001",
                  "1111111111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":8,
                "tile_y":1,
                "target_location":"VolcanoDungeon8",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(VolcanoFloorStepKinds.Blocked, step.StepKind);
        Assert.Equal("volcano_route_blocker_unattributed", step.Reason);
    }

    [Fact]
    public void PlannerTreatsVerifiedCooledLavaAsDynamicallyPassable()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":0}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "111111",
                  "100001",
                  "111111"
                ],
                "blocked_rows":[
                  "111111",
                  "101001",
                  "111111"
                ]
              },
              "cooled_lava_tiles":[{"tile_x":2,"tile_y":1}],
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":4,
                "tile_y":1,
                "target_location":"VolcanoDungeon1",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(
            VolcanoFloorStepKinds.TraverseForwardConnector,
            step.StepKind);
        Assert.Equal(4, step.TargetTileX);
    }

    [Fact]
    public void PlannerUsesDynamicAlternateRouteInsteadOfClearingMonster()
    {
        var snapshot = Snapshot("""
        {
          "volcano": {
            "current_level": {"status":"available","value":{"level":7}},
            "tiles": {"status":"available","value":{
              "player_tile":{"tile_x":1,"tile_y":1},
              "collision_context":{
                "status":"available",
                "static_blocked_rows":[
                  "11111111",
                  "10000001",
                  "10000001",
                  "11111111"
                ],
                "blocked_rows":[
                  "11111111",
                  "10000101",
                  "10000001",
                  "11111111"
                ]
              },
              "coolable_uncooled_tiles":[]
            }},
            "connectors": {"status":"available","value":{
              "forward_warps":[{
                "tile_x":6,
                "tile_y":1,
                "target_location":"VolcanoDungeon8",
                "target_tile_x":0,
                "target_tile_y":1
              }]
            }},
            "gates": {"status":"available","value":[]},
            "objects": {"status":"available","value":[]},
            "monsters": {"status":"available","value":[{
              "runtime_identity":"SIDE-ROUTE",
              "runtime_type":"StardewValley.Monsters.GreenSlime",
              "name":"Tiger Slime",
              "tile_x":5,
              "tile_y":1,
              "is_glider":false,
              "melee_executor_supported":true
            }]},
            "player_resources": {"status":"available","value":{
              "pickaxe_slots":[],
              "heavy_hitter_slots":[],
              "weapon_slots":[{
                "slot_index":2,
                "maximum_damage":120,
                "is_scythe":false
              }]
            }}
          }
        }
        """);

        var step = new VolcanoFloorStepPlanner().Plan(snapshot);

        Assert.Equal(
            VolcanoFloorStepKinds.TraverseForwardConnector,
            step.StepKind);
        Assert.NotEqual("SIDE-ROUTE", step.TargetRuntimeIdentity);
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
