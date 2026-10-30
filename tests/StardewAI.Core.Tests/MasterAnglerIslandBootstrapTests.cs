using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MasterAnglerMainlineTests
{
    [Fact]
    public void IslandBootstrapControlReachesExistingPrerequisiteEvaluatorWithoutTerminalRoute()
    {
        using var fixture = CreateFixture("IslandSouthEastCave:5",
            targetLocation: "IslandSouthEastCave");
        var snapshot = IslandBootstrapSnapshot(fixture);
        var parameters = fixture.Parameters.Select(parameter => Parameter(parameter.Name,
            parameter.Name == "master_angler_target_total_day" ? "0" : parameter.Value)).ToArray();

        var terminal = EvaluateGingerIslandCandidate(snapshot, parameters);
        Assert.False(terminal.Available);
        Assert.Contains(
            "master_angler_full_route_timing_unavailable:future_route_target_map_inaccessible_on_date",
            terminal.BlockReasons);

        var controlParameters = parameters
            .Select(parameter => parameter.Name switch
            {
                "master_angler_runtime_terminal_validation_required" =>
                    Parameter(parameter.Name, "false"),
                MasterAnglerRouteTimingValidator.ValidationRequiredParameter =>
                    Parameter(parameter.Name, "false"),
                _ => parameter
            })
            .Append(Parameter(
                "master_angler_ginger_island_prerequisite_control_only",
                "true"))
            .ToArray();
        Assert.True(
            MasterAnglerWindowIntentValidator
                .TryValidateGingerIslandPrerequisiteControl(
                    snapshot,
                    controlParameters,
                    out var controlIntent,
                    out var controlReason),
            controlReason);
        Assert.Equal("IslandSouthEastCave:5", controlIntent.SourceKey);

        var candidate = EvaluateGingerIslandCandidate(snapshot, controlParameters);
        Assert.False(candidate.Available);
        Assert.Contains(
            "master_angler_ginger_island_access_requires_community_center_or_joja_completion",
            candidate.BlockReasons);
        Assert.DoesNotContain(
            candidate.BlockReasons,
            reason => reason.Contains("route_timing", StringComparison.Ordinal));

        // Change only the map's capture-date accessibility. This confirms that
        // artifact identity, species, current date, weather and timing inputs
        // were otherwise accepted by the real validator (not a mocked planner).
        var accessibleMap = RewriteGingerIslandState(snapshot, state =>
            state["locations"]!["social_route_date_evidence"]!["value"]!["locations"]![1]!["build_conditions_met"] = true);
        Assert.True(MasterAnglerWindowIntentValidator.TryValidate(
            accessibleMap, parameters, out var intent, out var reason), reason);
        Assert.Equal("IslandSouthEastCave:5", intent.SourceKey);
        var prerequisite = EvaluateGingerIslandCandidate(accessibleMap, parameters);
        Assert.Equal("ginger_island_access_wait", prerequisite.Kind);
        Assert.Contains("master_angler_ginger_island_access_requires_community_center_or_joja_completion",
            prerequisite.BlockReasons);
    }

    [Fact]
    public void IslandBootstrapFreshMainlandEmitsControlWithoutClaimingTerminalRoute()
    {
        using var fixture = CreateFixture("IslandSouthEastCave:5",
            targetLocation: "IslandSouthEastCave");
        var snapshot = IslandBootstrapSnapshot(fixture);
        var snapshotPath = Path.Combine(Path.GetTempPath(), "opencode", "island-bootstrap-" + Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        try
        {
            File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            var result = MasterAnglerTargetDateIntentBuilder.Build(
                fixture.WindowIndexPath, snapshotPath, fixture.TimingCalibrationPath);

            Assert.Empty(result.Candidates);
            Assert.False(result.TrainingLabelEligible);
            Assert.Equal(72, result.MissingSpeciesCount);
            Assert.Equal(1, result.PrerequisiteControlCandidateCount);
            Assert.Equal(
                "validated_current_date_ginger_island_prerequisite_control",
                result.Status);
            Assert.Contains(
                "terminal_route_unavailable_ginger_island_prerequisite_control_ready",
                result.BlockingReasons);
            var control = Assert.Single(result.PrerequisiteControlCandidates);
            Assert.Equal("IslandSouthEastCave:5", control.SourceKey);
            Assert.Equal("IslandSouthEastCave", control.TargetLocation);
            Assert.False(control.RuntimeTerminalValidationRequired);
            Assert.Equal(0, control.RouteEdgeCount);
            Assert.Equal(0, control.RouteGuaranteedArrivalTime);
            Assert.Empty(control.RouteTimingEvidenceId);
            Assert.Equal(
                "ginger_island_prerequisite_control_only",
                control.RouteTimingStatus);
            Assert.Contains(control.Parameters, parameter =>
                parameter.Name ==
                    "master_angler_ginger_island_prerequisite_control_only" &&
                parameter.Value == "true");

            var accessibleMap = RewriteGingerIslandState(snapshot, state =>
                state["locations"]!["social_route_date_evidence"]!["value"]!["locations"]![1]!["build_conditions_met"] = true);
            File.WriteAllText(snapshotPath, JsonSerializer.Serialize(accessibleMap, JsonOptions));
            var accessible = MasterAnglerTargetDateIntentBuilder.Build(
                fixture.WindowIndexPath, snapshotPath, fixture.TimingCalibrationPath);
            var intent = Assert.Single(accessible.Candidates);
            Assert.Empty(accessible.PrerequisiteControlCandidates);
            Assert.Equal(0, accessible.PrerequisiteControlCandidateCount);
            Assert.True(intent.RuntimeTerminalValidationRequired);
            Assert.Equal("IslandSouthEastCave:5", intent.SourceKey);
            Assert.Equal("IslandSouthEastCave", intent.TargetLocation);
        }
        finally
        {
            File.Delete(snapshotPath);
        }
    }

    private static SnapshotEnvelope IslandBootstrapSnapshot(Fixture fixture) =>
        RewriteGingerIslandState(GingerIslandSnapshot(fixture.Before, "Farm", 1, 5,
            GingerIslandAccess("requires_community_center_or_joja_completion")), state =>
        {
            state["farm"] = new JsonObject { ["farm_type_key"] = Field(JsonValue.Create("Standard")!) };
            state["time"]!["total_days"] = Field(0);
            var progress = state["world_progress"]!["fish_collection_progress"]!["value"]!;
            foreach (var item in progress["items"]!.AsArray())
                item!["caught"] = false;
            progress["caught_eligible_species_count"] = 0;
            progress["missing_species_count"] = 72;
            progress["missing_item_ids"] = new JsonArray(progress["items"]!.AsArray()
                .Select(item => (JsonNode?)JsonValue.Create(item!["item_id"]!.GetValue<string>())).ToArray());
            var access = state["locations"]!["ginger_island_access"]!["value"]!;
            foreach (var field in access["target_access"]!.AsObject().ToArray())
                access["target_access"]![field.Key] = false;
            foreach (var field in new[] { "first_parrot", "western_turtle_parrot", "island_house_parrot", "resort_parrot" })
                access[field]!["complete_or_pending"] = false;
            access["walnuts"]!["current"] = 0;
            access["walnuts"]!["found"] = 0;
            var evidence = state["locations"]!["social_route_date_evidence"]!["value"]!;
            evidence["capture_total_days"] = 0;
            evidence["locations"]![1]!["build_conditions"] = "PLAYER_HAS_MAIL Current Visited_Island";
            evidence["locations"]![1]!["build_conditions_met"] = false;
        });
}
