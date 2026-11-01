using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class StageOneDailyMaintenancePreemptionPolicyTests
{
    [Fact]
    public void CurrentLocationWateringObligationPreemptsCollectionTeacher()
    {
        var snapshot = Snapshot(
            currentLocationCrops:
            """[{"tile_x":1,"tile_y":2,"needs_watering":true},{"tile_x":3,"tile_y":4,"needs_watering":false}]""",
            farmCrops: "[]");

        var decision =
            StageOneDailyMaintenancePreemptionPolicy.Evaluate(snapshot);

        Assert.True(decision.ShouldPreempt);
        Assert.Equal(
            StageOneDailyMaintenancePreemptionPolicy.PreemptionReason,
            decision.Reason);
        Assert.Equal(1, decision.WateringCandidateCount);
        Assert.Equal(
            new[] { "farm.maintain_crops" },
            StageOneDailyMaintenancePreemptionPolicy.CandidateOptionIds);
        Assert.Equal(
            "water_crop_tile",
            StageOneDailyMaintenancePreemptionPolicy.CandidateKind);
    }

    [Fact]
    public void FarmSingletonDoesNotReplaceCurrentLocationAuthority()
    {
        var snapshot = Snapshot(
            currentLocationCrops:
            """[{"tile_x":1,"tile_y":2,"needs_watering":false}]""",
            farmCrops:
            """[{"tile_x":9,"tile_y":9,"needs_watering":true}]""");

        var decision =
            StageOneDailyMaintenancePreemptionPolicy.Evaluate(snapshot);

        Assert.False(decision.ShouldPreempt);
        Assert.Equal(0, decision.WateringCandidateCount);
        Assert.Equal(string.Empty, decision.Reason);
    }

    [Fact]
    public void HarvestOnlyStateDoesNotUseWateringPreemption()
    {
        var snapshot = Snapshot(
            currentLocationCrops:
            """[{"tile_x":1,"tile_y":2,"needs_watering":false,"ready_for_harvest":true}]""",
            farmCrops: "[]");

        var decision =
            StageOneDailyMaintenancePreemptionPolicy.Evaluate(snapshot);

        Assert.False(decision.ShouldPreempt);
        Assert.Equal(0, decision.WateringCandidateCount);
    }

    private static JsonObject Snapshot(
        string currentLocationCrops,
        string farmCrops) =>
        JsonNode.Parse(
            $$"""
            {
              "state": {
                "current_location": {
                  "crops": {"value": {{currentLocationCrops}}}
                },
                "farm": {
                  "crops": {"value": {{farmCrops}}}
                }
              }
            }
            """)!.AsObject();
}
