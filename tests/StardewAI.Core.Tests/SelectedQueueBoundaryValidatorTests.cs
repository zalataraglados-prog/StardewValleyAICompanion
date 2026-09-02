using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class SelectedQueueBoundaryValidatorTests
{
    [Fact]
    public void AcceptsStrictNextCandidateWithFreshAggregateBudgets()
    {
        var decision = SelectedQueueBoundaryValidator.Validate(
            Snapshot(time: 900, energy: 100),
            new[]
            {
                QueueItem("candidate.second", 1, minutes: 10, energy: 5),
                QueueItem("candidate.third", 2, minutes: 20, energy: 7)
            },
            previousQueueIndex: 0,
            nextQueueIndex: 1,
            "goal.test",
            "training_singleplayer");

        Assert.True(decision.Allowed);
        Assert.Empty(decision.Reasons);
        Assert.Equal(12, decision.RemainingEnergyCost);
    }

    [Fact]
    public void RejectsSkippedOrRegressedCandidateOrder()
    {
        var skipped = SelectedQueueBoundaryValidator.Validate(
            Snapshot(900, 100),
            new[] { QueueItem("candidate.third", 2, 10, 5) },
            previousQueueIndex: 0,
            nextQueueIndex: 2,
            "goal.test",
            "training_singleplayer");
        var regressed = SelectedQueueBoundaryValidator.Validate(
            Snapshot(900, 100),
            new[]
            {
                QueueItem("candidate.second", 1, 10, 5),
                QueueItem("candidate.first", 0, 10, 5)
            },
            previousQueueIndex: 0,
            nextQueueIndex: 1,
            "goal.test",
            "training_singleplayer");

        Assert.False(skipped.Allowed);
        Assert.Contains("selected_queue_precedence_discontinuity", skipped.Reasons);
        Assert.False(regressed.Allowed);
        Assert.Contains("selected_queue_remaining_order_regressed", regressed.Reasons);
    }

    [Fact]
    public void RejectsFreshRemainingEnergyOverflow()
    {
        var decision = SelectedQueueBoundaryValidator.Validate(
            Snapshot(900, 10),
            new[] { QueueItem("candidate.second", 1, 10, 11) },
            previousQueueIndex: 0,
            nextQueueIndex: 1,
            "goal.test",
            "training_singleplayer");

        Assert.False(decision.Allowed);
        Assert.Contains("selected_queue_remaining_energy_budget_exceeded", decision.Reasons);
    }

    private static JsonObject Snapshot(int time, int energy) => new()
    {
        ["state_hash"] = "hash.boundary",
        ["game_tick"] = 100L,
        ["state"] = new JsonObject
        {
            ["time"] = new JsonObject
            {
                ["time"] = Field(time)
            },
            ["player"] = new JsonObject
            {
                ["energy"] = Field(energy)
            }
        }
    };

    private static JsonObject QueueItem(
        string candidateId,
        int selectedQueueIndex,
        int minutes,
        int energy) => new()
    {
        ["queue_item_id"] = "queue." + candidateId,
        ["source_action_id"] = "action." + candidateId,
        ["option_id"] = "executor.wait",
        ["status"] = "pending",
        ["selected_queue_index"] = selectedQueueIndex,
        ["normalized_command"] = new JsonObject
        {
            ["parameters"] = new JsonArray
            {
                Parameter("precondition", "candidate_id:" + candidateId),
                Parameter("estimated_minutes", minutes.ToString()),
                Parameter("budget.candidate_energy_cost", energy.ToString())
            }
        }
    };

    private static JsonObject Field(int value) => new()
    {
        ["status"] = "readable",
        ["value"] = value
    };

    private static JsonObject Parameter(string name, string value) => new()
    {
        ["name"] = name,
        ["value"] = value
    };
}
