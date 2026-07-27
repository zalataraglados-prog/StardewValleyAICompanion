using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class MachineCraftingMainlineTests
{
    [Fact]
    public void EarnMoneyGoalAddsOnlyBoundedPositiveMachineSupport()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 2,
            processMinutes: 4000);
        var availability =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions: true);
        var broad = Assert.Single(
            new EventCandidateRanker()
                .Rank(
                    new BaselineTrainingReport(),
                    availability,
                    "grandpa_max_score_year3")
                .Where(row =>
                    row.Kind == "craft_machine_item"));
        var supported = Assert.Single(
            new EventCandidateRanker()
                .Rank(
                    new BaselineTrainingReport(),
                    availability,
                    "goal.economy.earn_money")
                .Where(row =>
                    row.Kind == "craft_machine_item"));

        Assert.True(supported.Score > broad.Score);
        Assert.Equal(
            "neutral",
            Parameter(
                broad.Parameters,
                "goal_support_status"));
        Assert.Equal(
            "bounded_current_backlog_positive",
            Parameter(
                supported.Parameters,
                "machine_economic_value_status"));
        Assert.Equal(
            "400",
            Parameter(
                supported.Parameters,
                "goal_support_gross_benefit"));
        Assert.Equal(
            "60",
            Parameter(
                supported.Parameters,
                "goal_support_opportunity_cost"));
        Assert.Equal(
            "340",
            Parameter(
                supported.Parameters,
                "goal_support_net_benefit"));
        Assert.Equal(
            "supported_bounded_positive_net_benefit",
            Parameter(
                supported.Parameters,
                "goal_support_status"));

        var plan = new DailyPlanCompiler().Compile(
            new[] { supported },
            snapshot.StateHash,
            "goal.economy.earn_money");
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "goal_support_net_benefit" &&
                parameter.Value == "340");
    }

    [Fact]
    public void MachineSupportTamperIsRejectedAgainstFreshProjection()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 2,
            processMinutes: 4000);
        var availability =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions: true);
        var supported = Assert.Single(
            new EventCandidateRanker()
                .Rank(
                    new BaselineTrainingReport(),
                    availability,
                    "goal.economy.earn_money")
                .Where(row =>
                    row.Kind == "craft_machine_item"));
        var plan = new DailyPlanCompiler().Compile(
            new[] { supported },
            snapshot.StateHash,
            "goal.economy.earn_money");
        var step = Assert.Single(plan.Steps);
        step.Parameters.Single(parameter =>
            parameter.Name == "goal_support_net_benefit").Value =
            "999999";

        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "craft_machine_item_goal_support_projection_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Theory]
    [InlineData(
        "incomplete_output_sale_value_or_ambiguous_context",
        400,
        60,
        "blocked_incomplete_economic_evidence")]
    [InlineData(
        "bounded_current_backlog_positive",
        40,
        60,
        "neutral_nonpositive_bounded_net_benefit")]
    public void MachineSupportDoesNotAddValueWithoutPositiveCompleteEvidence(
        string economicStatus,
        int grossBenefit,
        int materialCost,
        string expectedStatus)
    {
        var availability = new OptionAvailabilityEnvelope
        {
            CurrentTime = 600,
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "farm.process_machines",
                    EventCandidates = new[]
                    {
                        new EventCandidate
                        {
                            CandidateId = "machine-craft:test",
                            Kind = "craft_machine_item",
                            Available = true,
                            AllowedNow = true,
                            AllowedToday = true,
                            ExpectedEffect =
                                "machine_demand_priority=200;" +
                                "machine_demand_class=production_capacity_requirement;" +
                                "machine_build_window_open=true;" +
                                "required_additional_machine_count=1;" +
                                "machine_economic_value_status=" +
                                economicStatus + ";" +
                                "machine_capacity_deficit_processing_net_value=" +
                                grossBenefit + ";" +
                                "machine_craft_material_opportunity_cost_status=" +
                                "complete_exact_native_consumption_sale_value;" +
                                "machine_craft_material_opportunity_cost=" +
                                materialCost
                        }
                    }
                }
            }
        };
        var broad = Assert.Single(
            new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability,
                "grandpa_max_score_year3"));
        var economy = Assert.Single(
            new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability,
                "goal.economy.earn_money"));

        Assert.Equal(broad.Score, economy.Score);
        Assert.Equal(
            expectedStatus,
            Parameter(
                economy.Parameters,
                "goal_support_status"));
        Assert.Equal(
            "0",
            Parameter(
                economy.Parameters,
                "goal_support_score"));
    }

    [Fact]
    public void MissingOneNativeOutputValueBlocksMachineSupport()
    {
        var original = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 2,
            processMinutes: 4000);
        var root = JsonNode.Parse(
            JsonSerializer.Serialize(original.State))!.AsObject();
        root["player"]!["machine_crafting"]!["value"]![
            "rows"]![0]!["potential_loadable_inputs"]![0]![
            "accepting_contexts"]![0]!["predicted_output"]!
            .AsObject()
            .Remove("sale_price");
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            root.ToJsonString(),
            JsonOptions)!;
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = original.SchemaVersion,
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = original.GameTick,
            RealTimestamp = original.RealTimestamp,
            Completeness = original.Completeness,
            State = state
        };
        var availability =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions: true);
        var broad = Assert.Single(
            new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability,
                "grandpa_max_score_year3"));
        var economy = Assert.Single(
            new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability,
                "goal.economy.earn_money"));

        Assert.Equal(broad.Score, economy.Score);
        Assert.Equal(
            "incomplete_output_sale_value_or_ambiguous_context",
            Parameter(
                economy.Parameters,
                "machine_economic_value_status"));
        Assert.Equal(
            "blocked_incomplete_economic_evidence",
            Parameter(
                economy.Parameters,
                "goal_support_status"));
    }
}
