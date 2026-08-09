using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DailyPlanCompilerRollingDungeonTests
{
    [Theory]
    [InlineData("mining.reach_depth", "mining_reach_depth_plan_envelope", "executor.mine_stone", "mine_stone")]
    [InlineData("mining.acquire_golden_scythe", "mining_acquire_golden_scythe_plan_envelope", "executor.move_to_tile", "move_to_tile")]
    [InlineData("mining.obtain_skull_key", "mining_obtain_skull_key_plan_envelope", "executor.interact", "interact")]
    [InlineData("volcano.reach_caldera", "volcano_reach_caldera_plan_envelope", "executor.cool_volcano_lava", "cool_volcano_lava")]
    [InlineData("volcano.reach_caldera", "volcano_reach_caldera_plan_envelope", "executor.break_volcano_stone", "break_volcano_stone")]
    [InlineData("volcano.reach_caldera", "volcano_reach_caldera_plan_envelope", "executor.combat_volcano_monster", "combat_volcano_monster")]
    public void RollingDungeonCandidateCompilesCurrentFloorPrimitive(
        string optionId,
        string candidateKind,
        string executionOptionId,
        string expectedStepKind)
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "rolling:test",
            OptionId = optionId,
            Kind = candidateKind,
            Available = true,
            LocationId = "Mine",
            TileX = 3,
            TileY = 4,
            EstimatedTicks = -1,
            EnergyCost = -1,
            Parameters = new[]
            {
                Parameter("execution_option_id", executionOptionId),
                Parameter("target_tile_x", "3"),
                Parameter("target_tile_y", "4")
            }
        };

        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            "state.test",
            availableMinutes: 10,
            energyBudget: 10);

        var step = Assert.Single(plan.Steps);
        Assert.Equal(expectedStepKind, step.Kind);
        Assert.Equal(3, step.TargetTileX);
        Assert.Equal(4, step.TargetTileY);
        var audit = Assert.Single(plan.CandidateAudit);
        Assert.Equal("rolling:test", audit.CandidateId);
        Assert.Equal("accepted", audit.Decision);
        Assert.Equal(10, audit.RemainingEnergyBefore);
        Assert.Equal(10, audit.RemainingEnergyAfter);
    }

    [Theory]
    [InlineData("mine_stone", "executor.mine_stone")]
    [InlineData("break_container", "executor.break_container")]
    [InlineData("break_resource_clump", "executor.break_resource_clump")]
    [InlineData("combat_monster", "executor.combat_monster")]
    [InlineData("shoot_monster", "executor.shoot_monster")]
    [InlineData("place_bomb", "executor.place_bomb")]
    [InlineData("place_staircase", "executor.place_staircase")]
    [InlineData("consume_food", "executor.consume_food")]
    [InlineData("descend_ladder", "executor.descend_ladder")]
    [InlineData("descend_shaft", "executor.descend_shaft")]
    [InlineData("exit_mine", "executor.exit_mine")]
    [InlineData("cool_volcano_lava", "executor.cool_volcano_lava")]
    [InlineData("break_volcano_stone", "executor.break_volcano_stone")]
    [InlineData("break_volcano_container", "executor.break_volcano_container")]
    [InlineData("combat_volcano_monster", "executor.combat_volcano_monster")]
    public void PlanTranslationPreservesRollingPrimitiveIdentity(string stepKind, string expectedOptionId)
    {
        var plan = new SmallModelPlanEnvelope
        {
            StateHash = "state.test",
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "step.test",
                    Kind = stepKind,
                    TargetTileX = 3,
                    TargetTileY = 4
                }
            }
        };
        var snapshot = new SnapshotEnvelope
        {
            StateHash = "state.test",
            State = new Dictionary<string, System.Text.Json.JsonElement>()
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal(expectedOptionId, Assert.Single(queue.Items).OptionId);
    }

    [Theory]
    [InlineData("mining_perfect_executor", "mining.tiles")]
    [InlineData("volcano_perfect_executor", "volcano.tiles")]
    public void PurposeLimitedRollingMoveUsesItsDomainTileEvidence(
        string executorProfile,
        string expectedMapFactor)
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>("""
        {
          "state_hash":"state.test",
          "state":{
            "player":{
              "location_id":{"value":"Dungeon","status":"available"},
              "tile_x":{"value":1,"status":"available"},
              "tile_y":{"value":1,"status":"available"}
            },
            "mining":{"tiles":{"value":{},"status":"available"}},
            "volcano":{"tiles":{"value":{},"status":"available"}}
          }
        }
        """)!;
        var plan = new SmallModelPlanEnvelope
        {
            StateHash = snapshot.StateHash,
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "rolling.move",
                    Kind = "move_to_tile",
                    TargetLocation = "Dungeon",
                    TargetTileX = 2,
                    TargetTileY = 1,
                    Parameters = new[] { Parameter("required_executor_profile", executorProfile) }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(item =>
                item.BlockingReasons.Concat(item.MissingStateFactors))));
        var item = Assert.Single(queue.Items);
        Assert.Contains(expectedMapFactor, item.RequiredStateFactors);
        Assert.DoesNotContain("current_location.map", item.RequiredStateFactors);
        Assert.Empty(item.MissingStateFactors);
    }

    [Fact]
    public void OrdinaryMoveStillRequiresCurrentLocationMapEvidence()
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>("""
        {
          "state_hash":"state.test",
          "state":{
            "player":{
              "location_id":{"value":"Farm","status":"available"},
              "tile_x":{"value":1,"status":"available"},
              "tile_y":{"value":1,"status":"available"}
            },
            "mining":{"tiles":{"value":{},"status":"available"}}
          }
        }
        """)!;
        var plan = new SmallModelPlanEnvelope
        {
            StateHash = snapshot.StateHash,
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "ordinary.move",
                    Kind = "move_to_tile",
                    TargetLocation = "Farm",
                    TargetTileX = 2,
                    TargetTileY = 1
                }
            }
        };

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);

        Assert.Contains("current_location.map", item.RequiredStateFactors);
        Assert.Contains("current_location.map", item.MissingStateFactors);
        Assert.DoesNotContain("mining.tiles", item.RequiredStateFactors);
    }

    [Fact]
    public void GoldenScytheInteractUsesTransparentMiningAltarEvidence()
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>("""
        {
          "state_hash":"state.test",
          "state":{
            "player":{
              "location_id":{"value":"UndergroundMine77377","status":"available"},
              "tile_x":{"value":28,"status":"available"},
              "tile_y":{"value":6,"status":"available"},
              "facing_direction":{"value":1,"status":"available"}
            },
            "menus":{"active_menu":{"value":{"is_open":false},"status":"available"}},
            "mining":{"tiles":{"value":{"golden_scythe_altars":[
              {"tile_x":29,"tile_y":6,"action":"GoldenScythe","present":true}
            ]},"status":"available"}}
          }
        }
        """)!;
        var plan = new SmallModelPlanEnvelope
        {
            StateHash = snapshot.StateHash,
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "golden-scythe.claim",
                    Kind = "interact",
                    TargetLocation = "UndergroundMine77377",
                    TargetTileX = 29,
                    TargetTileY = 6,
                    Parameters = new[]
                    {
                        Parameter("required_executor_profile", "mining_perfect_executor"),
                        Parameter("interaction_kind", "map_action"),
                        Parameter("expected_action_type", "GoldenScythe")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Contains("mining.tiles", item.RequiredStateFactors);
        Assert.DoesNotContain("current_location.route_context", item.RequiredStateFactors);
        Assert.DoesNotContain("locations.route_action_branch_coverage", item.RequiredStateFactors);
        Assert.Empty(item.MissingStateFactors);
        Assert.Empty(item.BlockingReasons);
    }

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter { Name = name, Value = value };
    }
}
