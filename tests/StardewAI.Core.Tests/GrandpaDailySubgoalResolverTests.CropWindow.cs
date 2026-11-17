using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    [Theory]
    [InlineData(28, 4, "spring", false)]
    [InlineData(24, 4, "Spring", true)]
    [InlineData(25, 4, "spring", false)]
    [InlineData(1, 4, "summer", false)]
    [InlineData(1, 0, "spring", false)]
    public void InitialCropHandoffRespectsKnownTeacherHarvestWindow(
        int day, int growDays, string cropSeason, bool expectedHandoff)
    {
        var snapshot = CropWindowSnapshot(day, new
        {
            seed_id = "472", seasons = new[] { cropSeason }, grow_days = growDays
        });

        AssertCropWindowResolution(snapshot, expectedHandoff);
    }

    [Fact]
    public void InitialCropHandoffConsidersPurchasableCatalogCropsNotOnlyOwnedSeeds()
    {
        var snapshot = CropWindowSnapshot(24,
            new { seed_id = "472", seasons = new[] { "spring" }, grow_days = 5 },
            new { seed_id = "another_seed", seasons = new[] { "spring" }, grow_days = 4 });

        AssertCropWindowResolution(snapshot, expectedHandoff: true);
    }

    [Theory]
    [InlineData("catalog_missing")]
    [InlineData("catalog_unavailable")]
    [InlineData("catalog_empty")]
    [InlineData("growth_unknown")]
    [InlineData("seasons_unknown")]
    [InlineData("clock_missing")]
    [InlineData("clock_unavailable")]
    [InlineData("invalid_day")]
    public void InitialCropHandoffDoesNotTreatUnknownCalendarAsProvenInfeasible(
        string missing)
    {
        var snapshot = CropWindowSnapshot(28, new
        {
            seed_id = "472", seasons = new[] { "spring" }, grow_days = 4
        });
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!.AsObject();
        var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!.AsObject();
        switch (missing)
        {
            case "catalog_missing": farm.Remove("crop_catalog"); break;
            case "catalog_unavailable": farm["crop_catalog"]!["status"] = "unavailable"; break;
            case "catalog_empty": farm["crop_catalog"]!["value"] = new JsonArray(); break;
            case "growth_unknown": farm["crop_catalog"]!["value"]![0]!.AsObject().Remove("grow_days"); break;
            case "seasons_unknown": farm["crop_catalog"]!["value"]![0]!.AsObject().Remove("seasons"); break;
            case "clock_missing": time.Remove("total_days"); break;
            case "clock_unavailable": time["day"]!["status"] = "unavailable"; break;
            case "invalid_day": time["day"]!["value"] = 29; break;
        }
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        snapshot.State["time"] = JsonSerializer.SerializeToElement(time);

        AssertCropWindowResolution(SnapshotFromState(snapshot.State), expectedHandoff: true);
    }

    private static SnapshotEnvelope CropWindowSnapshot(int day, params object[] crops)
    {
        var snapshot = GrandpaSnapshot(withUnplantedSeedInventory: true);
        var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!.AsObject();
        time["day"]!["value"] = day;
        time["total_days"] = time["day"]!.DeepClone();
        time["total_days"]!["value"] = day - 1;
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!.AsObject();
        farm["crop_catalog"] = farm["crops"]!.DeepClone();
        farm["crop_catalog"]!["value"] = JsonSerializer.SerializeToNode(crops);
        snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        return SnapshotFromState(snapshot.State);
    }

    private static void AssertCropWindowResolution(
        SnapshotEnvelope snapshot, bool expectedHandoff)
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "social_talk:Abigail:Farm",
            OptionId = "social.talk_npc",
            Kind = "social_talk_current",
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now",
            Parameters = new[]
            {
                Parameter("npc_name", "Abigail"),
                Parameter("npc_location", "Farm"),
                Parameter("friendship_row_exists_before", "false"),
                Parameter("friendship_points_before", "0"),
                Parameter("expected_friendship_delta", "20"),
                Parameter("expected_friendship_points_after", "20")
            }
        };
        var result = new GrandpaDailySubgoalResolver().Resolve(
            snapshot, GrandpaEvaluationGoalDefinition.StrategicGoal, new[] { candidate });

        Assert.Equal(expectedHandoff ? "planning_handoff_required" : "resolved", result.Status);
        Assert.Equal(expectedHandoff ? "earn_money" : "raise_friendships", result.DirectionId);
        Assert.Equal(expectedHandoff, result.PlanningHandoff is not null);
        Assert.Equal(expectedHandoff ? Array.Empty<string>() : new[] { candidate.CandidateId },
            result.BoundCandidateIds);
    }
}
