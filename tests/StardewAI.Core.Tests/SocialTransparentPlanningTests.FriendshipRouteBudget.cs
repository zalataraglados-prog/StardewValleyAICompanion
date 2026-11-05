using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void FriendshipRemoteComparisonKeepsOldEmptyStandAndCompilesOnlyOneConnector()
    {
        var files = FriendshipRemoteFiles();
        var before = File.ReadAllText(files.Snapshot);
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.TeacherPreferenceLabelEligible, string.Join(",", result.BlockingReasons));
        var row = result.Candidates.Single(candidate => candidate.CandidateId == result.SelectedCandidate!.CandidateId);
        Assert.Equal("route_connector_tile", row.Kind);
        Assert.True(row.RouteContactBudget!.FullContactBudgetVerified);
        Assert.Equal(result.Deficit.SourceStateHash, row.RouteContactBudget.SourceStateHash);
        Assert.Equal(row.ContactBudgetArrivalTime, row.RouteContactBudget.ContactWindow!.EarliestInteractionTime);
        Assert.True(row.ContactBudgetArrivalTime > result.OriginalTeacher.Itinerary.PlanningStartTime);
        Assert.DoesNotContain(result.SelectedCandidate!.Parameters,
            parameter => parameter.Name == "stand_tile_x" && !string.IsNullOrWhiteSpace(parameter.Value));
        Assert.Single(result.PairwisePreferences);
        var path = Path.Combine(files.Directory, "remote-preference.json");
        File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOptions));
        var replay = CurrentFriendshipTeacherPreferenceBuilder.Verify(path);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(replay));
        var dispatch = FriendshipTeacherDispatchBuilder.Compile(path);
        var item = Assert.Single(dispatch.Queue.Items);
        Assert.Equal("pending", item.Status);
        Assert.Equal("executor.traverse_connector", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "continuation.npc_name" && parameter.Value == row.NpcName);
        Assert.Equal(before, File.ReadAllText(files.Snapshot));
        Assert.False(result.NativeOutcomeVerified);
    }

    [Fact]
    public void ContactBudgetRequiresTheActualFirstConnectorEvenWhenItsApproachIsShorter()
    {
        var files = FriendshipRemoteFiles();
        using var document = JsonDocument.Parse(File.ReadAllText(files.Snapshot));
        var snapshot = document.RootElement.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        var builder = new CurrentSocialRouteContactBudgetBuilder(document.RootElement, File.ReadAllText(files.Calibration));
        var opportunity = builder.Frontier.Opportunities.First(row => row.NpcName == "Abigail" && row.InteractionKind == "talk");
        var candidate = RemoteAbigail(snapshot);
        document.Dispose();
        var direct = builder.Build(candidate, opportunity);
        Assert.True(direct.FullContactBudgetVerified, string.Join(",", direct.BlockingReasons));
        Assert.Same(direct, builder.Build(candidate, opportunity));
        Assert.Equal(1, builder.RouteProductionBuildCount);
        Assert.Equal(8, direct.RequiredFirstEdge!.FromX);

        candidate.TileX = 5;
        Set("target_tile_x", "5");
        Set("expected_target_location", "Detour");
        Set("route.target_location_id", "Detour");
        var detour = builder.Build(candidate, opportunity);
        Assert.True(detour.FullContactBudgetVerified, string.Join(",", detour.BlockingReasons));
        Assert.Equal(2, builder.RouteProductionBuildCount);
        var path = Assert.Single(detour.RouteScenario!.ProducedPaths!);
        Assert.Equal(2, path.Length);
        Assert.True(path[0].Matches(detour.RequiredFirstEdge!));
        Assert.Equal("Detour", path[0].TargetLocation);
        Assert.True(detour.ContactWindow!.EarliestInteractionTime > direct.ContactWindow!.EarliestInteractionTime);
        Assert.True(detour.RouteScenario.SegmentEvidence![0].ApproachTravelGameMinutes <
            direct.RouteScenario!.SegmentEvidence![0].ApproachTravelGameMinutes);

        void Set(string name, string value) => candidate.Parameters.Single(parameter => parameter.Name == name).Value = value;
    }

    [Theory]
    [InlineData("first_edge")]
    [InlineData("continuation")]
    [InlineData("duplicate_parameter")]
    [InlineData("foreign_window")]
    [InlineData("edited_window")]
    [InlineData("time_permission")]
    public void ContactBudgetRejectsUnboundRouteOrFrontierInputs(string change)
    {
        var files = FriendshipRemoteFiles();
        using var document = JsonDocument.Parse(File.ReadAllText(files.Snapshot));
        var snapshot = document.RootElement.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        var builder = new CurrentSocialRouteContactBudgetBuilder(document.RootElement, File.ReadAllText(files.Calibration));
        var opportunity = builder.Frontier.Opportunities.First(row => row.NpcName == "Abigail" && row.InteractionKind == "talk");
        var candidate = RemoteAbigail(snapshot);
        switch (change)
        {
            case "first_edge": candidate.TileX = 19; break;
            case "continuation": candidate.Parameters.Single(parameter => parameter.Name == "continuation.npc_name").Value = "George"; break;
            case "duplicate_parameter": candidate.Parameters = candidate.Parameters.Append(candidate.Parameters[0]).ToArray(); break;
            case "foreign_window": opportunity = JsonSerializer.Deserialize<CurrentSocialContactOpportunity>(JsonSerializer.Serialize(opportunity))!; break;
            case "edited_window": opportunity.NpcPresentUntilTimeExclusive = 601; break;
            case "time_permission": candidate.AllowedToday = false; break;
        }
        var result = builder.Build(candidate, opportunity);
        Assert.False(result.FullContactBudgetVerified);
        Assert.NotEmpty(result.BlockingReasons);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("missing_tick")]
    [InlineData("string_tick")]
    [InlineData("hash")]
    public void ContactBudgetDoesNotBorrowDefaultIdentityFromLegacyJsonAdapter(string change)
    {
        var files = FriendshipRemoteFiles();
        using var original = JsonDocument.Parse(File.ReadAllText(files.Snapshot));
        var good = new CurrentSocialRouteContactBudgetBuilder(original.RootElement, File.ReadAllText(files.Calibration));
        var opportunity = good.Frontier.Opportunities.First(row => row.NpcName == "Abigail");
        var snapshot = original.RootElement.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        var raw = JsonNode.Parse(File.ReadAllText(files.Snapshot))!;
        if (change == "schema") raw["schema_version"] = "snapshot.unsupported";
        if (change == "missing_tick") raw.AsObject().Remove("game_tick");
        if (change == "string_tick") raw["game_tick"] = "1";
        if (change == "hash") raw["state_hash"] = "wrong";
        using var invalid = JsonDocument.Parse(raw.ToJsonString());
        var builder = new CurrentSocialRouteContactBudgetBuilder(invalid.RootElement, File.ReadAllText(files.Calibration));
        var result = builder.Build(RemoteAbigail(snapshot), opportunity);
        Assert.False(result.FullContactBudgetVerified);
        Assert.Contains("current_social_route_snapshot_identity_invalid", result.BlockingReasons);
    }

    [Fact]
    public void CurrentConnectorAvailabilityCannotStandInForAnUnreachableRemoteTail()
    {
        var files = FriendshipRemoteFiles();
        var raw = JsonNode.Parse(File.ReadAllText(files.Snapshot))!;
        var town = raw["state"]!["locations"]!["social_route_date_evidence"]!["value"]!["locations"]![1]!;
        town["static_walkable_tile_count"] = 0;
        town["static_walkable_tile_ranges"] = new JsonArray();
        var snapshot = raw.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        File.WriteAllText(files.Snapshot, JsonSerializer.Serialize(snapshot, JsonOptions));
        Assert.True(RemoteAbigail(snapshot).Available);
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.False(result.TeacherPreferenceLabelEligible);
        Assert.Empty(result.PairwisePreferences);
        Assert.DoesNotContain(result.Candidates, candidate => candidate.RouteContactBudget?.FullContactBudgetVerified == true);
    }

    [Theory]
    [InlineData("arrival")]
    [InlineData("first_edge")]
    [InlineData("tail")]
    public void FriendshipRemoteReplayRejectsEditedFullRouteTiming(string change)
    {
        var files = FriendshipRemoteFiles();
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.TeacherPreferenceLabelEligible, string.Join(",", result.BlockingReasons));
        var raw = JsonSerializer.SerializeToNode(result, JsonOptions)!;
        var row = raw["candidates"]!.AsArray().Single(candidate => candidate!["candidateId"]!.GetValue<string>() == result.SelectedCandidate!.CandidateId)!;
        var budget = row["routeContactBudget"]!;
        if (change == "arrival") budget["contactWindow"]!["earliestInteractionTime"] = 901;
        if (change == "first_edge") budget["requiredFirstEdge"]!["fromX"] = 5;
        if (change == "tail") budget["routeScenario"]!["approachEvidence"]![0]!["travelGameMinutes"] = 0;
        var path = Path.Combine(files.Directory, "edited-route.json");
        File.WriteAllText(path, raw.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CurrentFriendshipTeacherPreferenceBuilder.Verify(path));
    }

    private static PolicyEventCandidatePrediction RemoteAbigail(SnapshotEnvelope snapshot)
    {
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options);
        return ToPrediction(option.SocialCandidates.Single(candidate =>
            candidate.Parameters.Any(parameter => parameter.Name == "npc_name" && parameter.Value == "Abigail")), "social.talk_npc");
    }

    private static (string Snapshot, string Calibration, string Directory) FriendshipRemoteFiles()
    {
        var files = FriendshipTeacherFiles(qualifying: 4);
        var raw = JsonNode.Parse(File.ReadAllText(files.Snapshot))!;
        var state = raw["state"]!;
        state["player"]!["location_id"]!["value"] = "Farm";
        state["player"]!["tile_x"]!["value"] = 4;
        state["player"]!["tile_y"]!["value"] = 10;
        state["current_location"]!["map"]!["value"]!["location_id"] = "Farm";
        var locations = state["locations"]!;
        locations["collision_grid"]!["value"]!["location_id"] = "Farm";
        var direct = JsonNode.Parse("{\"kind\":\"warp\",\"from_location\":\"Farm\",\"from_x\":8,\"from_y\":10,\"target_location\":\"Town\",\"target_x\":2,\"target_y\":10,\"resolved\":true}")!;
        var detour = direct.DeepClone(); detour["from_x"] = 5; detour["target_location"] = "Detour";
        var tail = direct.DeepClone(); tail["from_location"] = "Detour"; tail["from_x"] = 18;
        locations["route_graph"]!["value"] = new JsonObject { ["edges"] = new JsonArray(direct.DeepClone(), detour.DeepClone(), tail) };
        var connectors = new[] { direct, detour }.Select(edge => new JsonObject
        {
            ["kind"] = "warp", ["tile_x"] = edge["from_x"]!.DeepClone(), ["tile_y"] = 10,
            ["target_location"] = edge["target_location"]!.DeepClone(), ["target_x"] = 2, ["target_y"] = 10, ["resolved"] = true
        }).ToArray();
        locations["route_connectors"]!["value"] = new JsonObject { ["location_id"] = "Farm", ["connectors"] = new JsonArray(connectors) };
        locations["route_gate_context"]!["value"] = new JsonObject { ["location_id"] = "Farm", ["action_gates"] = new JsonArray() };
        var evidence = locations["social_route_date_evidence"]!["value"]!;
        var town = evidence["locations"]![0]!.DeepClone();
        var farm = town.DeepClone(); farm["location_id"] = "Farm";
        var detourMap = town.DeepClone(); detourMap["location_id"] = "Detour";
        evidence["locations"] = new JsonArray(farm, town, detourMap);
        evidence["location_count"] = 3;
        var snapshot = raw.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        File.WriteAllText(files.Snapshot, JsonSerializer.Serialize(snapshot, JsonOptions));
        return files;
    }
}
