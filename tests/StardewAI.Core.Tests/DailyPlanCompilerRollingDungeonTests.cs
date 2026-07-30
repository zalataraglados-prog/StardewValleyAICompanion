using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DailyPlanCompilerRollingDungeonTests
{
    [Theory]
    [InlineData("mining_reach_depth_plan_envelope", "executor.mine_stone", "mine_stone")]
    [InlineData("mining_acquire_golden_scythe_plan_envelope", "executor.move_to_tile", "move_to_tile")]
    [InlineData("mining_obtain_skull_key_plan_envelope", "executor.interact", "interact")]
    [InlineData("volcano_reach_caldera_plan_envelope", "executor.cool_volcano_lava", "cool_volcano_lava")]
    [InlineData("volcano_reach_caldera_plan_envelope", "executor.break_volcano_stone", "break_volcano_stone")]
    [InlineData("volcano_reach_caldera_plan_envelope", "executor.combat_volcano_monster", "combat_volcano_monster")]
    public void RollingDungeonCandidateCompilesCurrentFloorPrimitive(
        string candidateKind,
        string executionOptionId,
        string expectedStepKind)
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "rolling:test",
            OptionId = candidateKind.StartsWith("volcano", StringComparison.Ordinal)
                ? "volcano.reach_caldera"
                : "mining.reach_depth",
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

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter { Name = name, Value = value };
    }
}
