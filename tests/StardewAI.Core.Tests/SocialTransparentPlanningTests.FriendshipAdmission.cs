using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void FriendshipPartialPairsRequireExplicitScopeAndRetainTheWholeDecisionContext()
    {
        var files = FriendshipPartialPairFiles();
        var before = File.ReadAllText(files.Snapshot);
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.IndependentPairwiseLabelEligible, string.Join(",", result.BlockingReasons));
        Assert.False(result.TeacherPreferenceLabelEligible);
        Assert.True(result.CandidateDenominatorComplete);
        Assert.Equal(3, result.DecisionCandidates.Length);
        var unknown = Assert.Single(result.UncomparedCandidateIds);
        var pair = Assert.Single(result.PairwisePreferences);
        Assert.Equal(result.OriginalTeacher.SelectedCandidateId, pair.PreferredCandidateId);
        Assert.NotEqual(unknown, pair.AlternativeCandidateId);
        var path = Path.Combine(files.Directory, "partial-pairs.json");
        File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOptions));
        Assert.Throws<InvalidDataException>(() => FriendshipTeacherDispatchBuilder.Compile(path));
        var dispatch = FriendshipTeacherDispatchBuilder.Compile(path, FriendshipTeacherAdmissionMode.IndependentPairs);
        var item = Assert.Single(dispatch.Queue.Items);
        Assert.Equal("pending", item.Status);
        Assert.Equal("executor.traverse_connector", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");
        Assert.Equal(before, File.ReadAllText(files.Snapshot));
        var replayed = CurrentFriendshipTeacherPreferenceBuilder.Verify(path);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(replayed));
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FullCriterionVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("pairwise_flag")]
    [InlineData("full_order_flag")]
    [InlineData("invented_pair")]
    [InlineData("missing_uncompared")]
    [InlineData("missing_decision_candidate")]
    public void FriendshipExplicitPairModeStillRequiresReplayOfEveryClaim(string change)
    {
        var files = FriendshipPartialPairFiles();
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.IndependentPairwiseLabelEligible);
        var raw = JsonSerializer.SerializeToNode(result, JsonOptions)!;
        switch (change)
        {
            case "pairwise_flag": raw["independentPairwiseLabelEligible"] = false; break;
            case "full_order_flag": raw["teacherPreferenceLabelEligible"] = true; break;
            case "invented_pair":
                var pair = raw["pairwisePreferences"]![0]!.DeepClone();
                pair["alternative_candidate_id"] = result.UncomparedCandidateIds[0];
                raw["pairwisePreferences"]!.AsArray().Add(pair);
                break;
            case "missing_uncompared": raw["uncomparedCandidateIds"] = new JsonArray(); break;
            case "missing_decision_candidate": raw["decisionCandidates"]!.AsArray().RemoveAt(2); break;
        }
        var path = Path.Combine(files.Directory, "edited-partial-pairs.json");
        File.WriteAllText(path, raw.ToJsonString());
        Assert.Throws<InvalidDataException>(() =>
            FriendshipTeacherDispatchBuilder.Compile(path, FriendshipTeacherAdmissionMode.IndependentPairs));
    }

    [Fact]
    public void AStoredPairwiseFlagCannotPromoteOneUncomparedControlAction()
    {
        var files = FriendshipTeacherFiles(targets: 1);
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.False(result.IndependentPairwiseLabelEligible);
        var raw = JsonSerializer.SerializeToNode(result, JsonOptions)!;
        raw["independentPairwiseLabelEligible"] = true;
        var path = Path.Combine(files.Directory, "forged-pairwise-flag.json");
        File.WriteAllText(path, raw.ToJsonString());
        Assert.Throws<InvalidDataException>(() =>
            FriendshipTeacherDispatchBuilder.Compile(path, FriendshipTeacherAdmissionMode.IndependentPairs));
    }

    [Fact]
    public void InvalidAdmissionModeIsRejectedBeforeReadingEvidence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FriendshipTeacherDispatchBuilder.Compile("does-not-exist.json", (FriendshipTeacherAdmissionMode)333));
    }

    private static (string Snapshot, string Calibration, string Directory) FriendshipPartialPairFiles()
    {
        var files = FriendshipRemoteFiles(targets: 3);
        var raw = JsonNode.Parse(File.ReadAllText(files.Snapshot))!;
        var npcs = raw["state"]!["npcs"]!;
        npcs["social_interaction"]!["value"]![2]!["friendship_points"] = 50;
        npcs["social_interaction"]!["value"]![2]!["expected_talk_friendship_points_after"] = 70;
        npcs["friendships"]!["value"]![2]!["points"] = 50;
        npcs["grandpa_friendship_progress"]!["value"]!["eligible_villager_rows"]![2]!["friendship_points"] = 50;
        var snapshot = raw.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        File.WriteAllText(files.Snapshot, JsonSerializer.Serialize(snapshot, JsonOptions));
        return files;
    }
}
