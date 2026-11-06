using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void FriendshipPolicyProjectsTheNativeCountWithoutChangingOtherGoals(int count)
    {
        var files = FriendshipTeacherFiles(qualifying: count);
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        var projector = new PolicyStateFeatureProjector();
        var world = new WorldModelProjector().Project(snapshot, FriendshipPolicyInputProjector.GoalId, "training_singleplayer");
        var features = projector.Project(world, snapshot);
        Assert.Equal(count, features.Numeric.Single(row => row.Name == "npcs.friendships.qualifying_count").Value);
        Assert.Equal(Math.Max(0, 5 - count), features.Numeric.Single(row => row.Name == "npcs.friendships.missing_for_5").Value);
        Assert.Equal(10 - count, features.Numeric.Single(row => row.Name == "npcs.friendships.missing_for_10").Value);
        world.UserGoal = "goal.collection.master_angler";
        Assert.Equal(JsonSerializer.Serialize(projector.Project(world)), JsonSerializer.Serialize(projector.Project(world, snapshot)));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("world")]
    [InlineData("unreadable")]
    public void FriendshipPolicyCannotEncodeUnknownOrForeignCountsAsZero(string mutation)
    {
        var files = FriendshipTeacherFiles();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        if (mutation == "unreadable")
        {
            var npcs = JsonNode.Parse(snapshot.State["npcs"].GetRawText())!;
            npcs["grandpa_friendship_progress"]!["status"] = "unavailable";
            snapshot.State["npcs"] = JsonSerializer.SerializeToElement(npcs);
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        }
        if (mutation == "hash") snapshot.StateHash = "invalid";
        var world = new WorldModelProjector().Project(snapshot, FriendshipPolicyInputProjector.GoalId, "training_singleplayer");
        if (mutation == "world") world.StateHash = "foreign";
        Assert.ThrowsAny<Exception>(() => new PolicyStateFeatureProjector().Project(world, snapshot));
    }

    [Fact]
    public void FriendshipModelInputsReuseTheCertifiedContactWithoutChangingActionParameters()
    {
        var files = FriendshipTeacherFiles(qualifying: 4);
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        var preference = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        var original = JsonSerializer.Serialize(preference.DecisionCandidates);
        var projector = new FriendshipPolicyInputProjector(snapshot, File.ReadAllText(files.Calibration));
        var projected = projector.ProjectCandidates(preference.DecisionCandidates);
        Assert.Equal(original, JsonSerializer.Serialize(preference.DecisionCandidates));
        foreach (var candidate in projected)
        {
            var source = preference.DecisionCandidates.Single(row => row.CandidateId == candidate.CandidateId);
            Assert.Equal(JsonSerializer.Serialize(source.Parameters), JsonSerializer.Serialize(candidate.Parameters));
        }
        var selected = projected.Single(row => row.CandidateId == preference.SelectedCandidate!.CandidateId);
        Assert.True(selected.PolicyFeatures!.Boolean.Single(row => row.Name == "social.contact.budget_verified").Value);
        Assert.True(selected.PolicyFeatures.Numeric.Single(row => row.Name == "social.contact.arrival_budget_minutes").Value > 0);
        Assert.Equal(0, selected.PolicyFeatures.Numeric.Single(row => row.Name == "social.contact.connector_count").Value);
        var certified = projector.CertifyCurrentCandidates(preference.DecisionCandidates);
        Assert.Equal(original, JsonSerializer.Serialize(certified));
    }

    [Fact]
    public void FriendshipModelBudgetIncludesTheEntireRemoteTailAndMissingProofStaysUnknown()
    {
        var files = FriendshipRemoteFiles();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        var candidate = RemoteAbigail(snapshot);
        var projector = new FriendshipPolicyInputProjector(snapshot, File.ReadAllText(files.Calibration));
        var verified = Assert.Single(projector.ProjectCandidates(new[] { candidate })).PolicyFeatures!;
        Assert.True(verified.Boolean.Single().Value);
        Assert.Equal(1, verified.Numeric.Single(row => row.Name == "social.contact.connector_count").Value);
        candidate.TileX = 5;
        candidate.Parameters.Single(row => row.Name == "target_tile_x").Value = "5";
        candidate.Parameters.Single(row => row.Name == "expected_target_location").Value = "Detour";
        candidate.Parameters.Single(row => row.Name == "route.target_location_id").Value = "Detour";
        var detour = Assert.Single(projector.ProjectCandidates(new[] { candidate })).PolicyFeatures!;
        Assert.Equal(2, detour.Numeric.Single(row => row.Name == "social.contact.connector_count").Value);
        Assert.True(detour.Numeric.Single(row => row.Name == "social.contact.arrival_budget_minutes").Value >
            verified.Numeric.Single(row => row.Name == "social.contact.arrival_budget_minutes").Value);
        candidate.Parameters.Single(row => row.Name == "continuation.npc_name").Value = "unknown";
        var unknown = Assert.Single(projector.ProjectCandidates(new[] { candidate })).PolicyFeatures!;
        Assert.False(unknown.Boolean.Single().Value);
        Assert.Empty(unknown.Numeric);
        var absent = Assert.Single(new FriendshipPolicyInputProjector(snapshot, null).ProjectCandidates(new[] { candidate })).PolicyFeatures!;
        Assert.False(absent.Boolean.Single().Value);
        Assert.Empty(absent.Numeric);
        Assert.Contains(absent.Categorical, row => row.Value == "calibration_missing");
        Assert.Throws<InvalidOperationException>(() => new FriendshipPolicyInputProjector(snapshot, "{}"));
    }

    [Fact]
    public void FriendshipProjectionOwnsItsSnapshotAndDoesNotMutateItsCandidates()
    {
        var files = FriendshipTeacherFiles(qualifying: 4);
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        var preference = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        var projector = new FriendshipPolicyInputProjector(snapshot, File.ReadAllText(files.Calibration));
        var original = JsonSerializer.Serialize(projector.ProjectCandidates(preference.DecisionCandidates));
        snapshot.State.Clear(); snapshot.StateHash = "edited_after_construction";
        Assert.Equal(original, JsonSerializer.Serialize(projector.ProjectCandidates(preference.DecisionCandidates)));
        Assert.Equal(JsonSerializer.Serialize(preference.DecisionCandidates),
            JsonSerializer.Serialize(projector.CertifyCurrentCandidates(preference.DecisionCandidates)));
        Assert.All(preference.DecisionCandidates, candidate => Assert.Null(candidate.PolicyFeatures));
    }
}
