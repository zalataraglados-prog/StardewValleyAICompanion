using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class DailyPlanBudgetReaderTests
{
    [Fact]
    public void RecoveryBudgetDoesNotDependOnPolicyTrainingAdmission()
    {
        var snapshot = SnapshotAt(2120);
        var recovery = new PolicyEventCandidatePrediction
        {
            OptionId = "recovery.stabilize_day",
            Kind = "recovery_return_home",
            Available = false,
            BlockReasons = new[] { "policy_training_option_not_admitted" }
        };

        var available = DailyPlanBudgetReader.AvailablePlanMinutes(
            snapshot,
            new[] { recovery });

        Assert.Equal(220, available);
    }

    [Fact]
    public void OrdinaryWorkHasNoBudgetAfterRecoveryWindowStarts()
    {
        var snapshot = SnapshotAt(2120);
        var ordinary = new PolicyEventCandidatePrediction
        {
            OptionId = "economy.buy_supplies",
            Kind = "buy_shop_item",
            Available = true
        };

        var available = DailyPlanBudgetReader.AvailablePlanMinutes(
            snapshot,
            new[] { ordinary });

        Assert.Equal(0, available);
    }

    private static SnapshotEnvelope SnapshotAt(int time)
    {
        var state = new Dictionary<string, JsonElement>
        {
            ["time"] = JsonSerializer.SerializeToElement(new
            {
                time = new { value = time }
            })
        };
        return new SnapshotEnvelope { State = state };
    }
}
