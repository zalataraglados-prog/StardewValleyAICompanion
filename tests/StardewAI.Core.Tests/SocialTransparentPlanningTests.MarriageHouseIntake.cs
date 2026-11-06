using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void MarriageIntakeReplaysOriginalSourcesAndPreservesMoreThan64SocialRows()
    {
        var social = FriendshipTeacherFiles(targets: 65);
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(social.Snapshot), JsonOptions)!;
        MergeMarriageCurrentState(snapshot);
        var files = MarriageIntakeFiles(snapshot);
        var original = File.ReadAllText(files.Snapshot);
        var result = CurrentMarriageHouseIntakeBuilder.Build(files.Snapshot, files.Knowledge, files.Dependencies);
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(5, result.ConsideredOptionIds.Length);
        Assert.Equal(result.ConsideredOptionIds, result.Availability.Options.Select(row => row.OptionId));
        var talk = result.Availability.Options.Single(row => row.OptionId == "social.talk_npc");
        Assert.Equal(65, talk.SocialCandidates.Length);
        Assert.False(result.SocialCandidateLimitApplied);
        Assert.False(result.TrainingLabelEligible);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.Contains("future_acquisition", result.CandidateScope);
        Assert.NotEmpty(result.OriginalDirection.GetProperty("blockers").EnumerateArray());
        File.WriteAllText(files.Intake, JsonSerializer.Serialize(result, JsonOptions));
        var replay = CurrentMarriageHouseIntakeBuilder.Verify(files.Intake);
        Assert.Equal(JsonSerializer.Serialize(result, JsonOptions), JsonSerializer.Serialize(replay, JsonOptions));
        Assert.Equal(original, File.ReadAllText(files.Snapshot));
        var stored = JsonNode.Parse(File.ReadAllText(files.Intake))!;
        stored["trainingLabelEligible"] = true;
        File.WriteAllText(files.Intake, stored.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CurrentMarriageHouseIntakeBuilder.Verify(files.Intake));
    }

    [Theory]
    [InlineData("joint_rule")]
    [InlineData("missing_option")]
    [InlineData("foreign_option")]
    [InlineData("duplicate_direction")]
    [InlineData("scoped_view")]
    public void MarriageIntakeCannotShrinkAuthorityOrTransitionSourceDenominator(string mutation)
    {
        var files = MarriageIntakeFiles(CurrentMarriageHouseStateTests.Snapshot());
        if (mutation == "joint_rule")
        {
            var knowledge = JsonNode.Parse(File.ReadAllText(files.Knowledge))!;
            knowledge["grandpa_goal"]!["criteria"]![0]!["target"] = "farmhouse_upgrade_level>=2";
            File.WriteAllText(files.Knowledge, knowledge.ToJsonString());
        }
        else
        {
            var graph = JsonNode.Parse(File.ReadAllText(files.Dependencies))!;
            var direction = graph["directions"]![0]!;
            if (mutation == "missing_option") direction["nodes"]!.AsArray().RemoveAt(0);
            if (mutation == "foreign_option") direction["nodes"]![0]!["option_id"] = "unknown.option";
            if (mutation == "duplicate_direction") graph["directions"]!.AsArray().Add(direction.DeepClone());
            if (mutation == "scoped_view") graph["friendship_local_method_view"] = new JsonObject();
            File.WriteAllText(files.Dependencies, graph.ToJsonString());
        }
        Assert.Throws<InvalidDataException>(() => CurrentMarriageHouseIntakeBuilder.Build(files.Snapshot, files.Knowledge, files.Dependencies));
        Assert.False(File.Exists(files.Intake));
    }

    [Fact]
    public void InvalidCurrentFactsNeverReachCandidateEvaluationOrAcquireALabel()
    {
        var snapshot = CurrentMarriageHouseStateTests.Snapshot();
        snapshot.StateHash = "wrong";
        var files = MarriageIntakeFiles(snapshot);
        var result = CurrentMarriageHouseIntakeBuilder.Build(files.Snapshot, files.Knowledge, files.Dependencies);
        Assert.Equal("blocked", result.Status);
        Assert.Equal(5, result.ConsideredOptionIds.Length);
        Assert.Empty(result.Availability.Options);
        Assert.False(result.CurrentTransitionSourcesReadable);
        Assert.False(result.TrainingLabelEligible);
    }

    [Fact]
    public void RawQuoteIntakeDoesNotBypassTheOriginalNativePurchaseTupleGate()
    {
        var snapshot = CompleteSocialSnapshot();
        MergeMarriageCurrentState(snapshot);
        CurrentMarriageHouseStateTests.Mutate(snapshot, "world_progress", node =>
            node["marriage_house"]!["value"]!["house_upgrade"]!["price"] = 65001);
        var files = MarriageIntakeFiles(snapshot);
        var result = CurrentMarriageHouseIntakeBuilder.Build(files.Snapshot, files.Knowledge, files.Dependencies);
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(65001, result.CurrentState.HouseUpgradeQuote!.Value.GetProperty("price").GetInt32());
        var purchase = Assert.Single(result.Availability.Options.Single(option => option.OptionId == "housing.advance_farmhouse").EventCandidates);
        Assert.False(purchase.Available);
        Assert.Contains("farmhouse_upgrade_native_tuple_invalid", purchase.BlockReasons);
        Assert.False(result.TrainingLabelEligible);
    }

    private static void MergeMarriageCurrentState(SnapshotEnvelope snapshot)
    {
        var state = CurrentMarriageHouseStateTests.Snapshot();
        snapshot.GameVersion = state.GameVersion;
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
        foreach (var field in JsonNode.Parse(state.State["player"].GetRawText())!.AsObject()) player[field.Key] = field.Value!.DeepClone();
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.State["world_progress"] = state.State["world_progress"];
        var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!.AsObject();
        foreach (var field in JsonNode.Parse(state.State["time"].GetRawText())!.AsObject())
            if (!time.ContainsKey(field.Key)) time[field.Key] = field.Value!.DeepClone();
        snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }

    private static (string Snapshot, string Knowledge, string Dependencies, string Intake) MarriageIntakeFiles(SnapshotEnvelope snapshot)
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-marriage-intake-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var criteria = new[] { new { id = "married_or_roommate_house_2", points = 1,
            statePaths = new[] { "player.married_or_roommate", "player.farmhouse_upgrade_level" }, operation = "and",
            target = "married_or_roommate=true;farmhouse_upgrade_level>=2" } }.Concat(Enumerable.Range(0, 18).Select(index => new
            { id = "fixture" + index, points = index < 2 ? 2 : 1, statePaths = new[] { "fixture" }, operation = "equals", target = "true" }));
        var knowledge = new { schema_version = "fixture", issues = Array.Empty<string>(),
            summary = new { grandpaCriterionCount = 19, grandpaMaximumScore = 21 },
            grandpa_goal = new { goalId = "grandpa.maximum_21", targetScore = 21, criteria } };
        var dependencies = new { schema_version = "goal_method_dependency_expansions.v1", goal_id = "grandpa.maximum_21",
            directions = new[] { new { direction_id = "marriage_and_house_upgrade", blockers = new[] { "full_new_save_obligations_remain" },
                nodes = new[] { "housing.advance_farmhouse", "social.advance_partnership", "social.talk_npc", "social.gift_npc", "recovery.stabilize_day" }
                    .Select((id, index) => new { id = "node" + index, option_id = id }) } } };
        var files = (Snapshot: Path.Combine(root, "snapshot.json"), Knowledge: Path.Combine(root, "knowledge.json"),
            Dependencies: Path.Combine(root, "dependencies.json"), Intake: Path.Combine(root, "intake.json"));
        File.WriteAllText(files.Snapshot, JsonSerializer.Serialize(snapshot, JsonOptions));
        File.WriteAllText(files.Knowledge, JsonSerializer.Serialize(knowledge));
        File.WriteAllText(files.Dependencies, JsonSerializer.Serialize(dependencies));
        return files;
    }
}
