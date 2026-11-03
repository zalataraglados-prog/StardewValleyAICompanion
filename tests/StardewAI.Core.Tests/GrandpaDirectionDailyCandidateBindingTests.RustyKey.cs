using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    private const string RustyKeyStoryContract =
        "live_Event_Update_tryEventCommand_and_DialogueBox_native_input_until_event_end_or_fresh_decision_minigame_or_player_control_boundary_without_skipEvent_or_direct_event_state_mutation";

    [Fact]
    public void RustyKeyRemoteDonationBindsThroughExistingConnectorCompiler()
    {
        var donation = MuseumDonationMainlineTests.RemoteSnapshot(
            MuseumDonationMainlineTests.Snapshot(59, 95));
        var root = JsonSerializer.SerializeToNode(GrandpaSnapshot().State, JsonOptions)!.AsObject();
        foreach (var section in donation.State)
        {
            if (root[section.Key] is not JsonObject target)
                root[section.Key] = target = new JsonObject();
            foreach (var field in JsonNode.Parse(section.Value.GetRawText())!.AsObject())
                target[field.Key] = field.Value?.DeepClone();
        }
        var snapshot = RustyKeySnapshotFromRoot(root);
        var candidates = RankRustyKey(snapshot, "museum.donate_items");
        var candidate = Assert.Single(candidates);
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "expected_donated_count_before" && value.Value == "59");

        var binding = BindRustyKey(snapshot, candidates);
        Assert.Equal("ready", binding.BindingStatus);
        var plan = new DailyPlanCompiler().Compile(binding.BoundCandidates, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.traverse_connector", item.OptionId);

        candidate.Parameters = candidate.Parameters.Select(value =>
            value.Name == "continuation.qualified_item_id"
                ? Parameter(value.Name, "(O)100")
                : value).ToArray();
        Assert.Equal("blocked", BindRustyKey(snapshot, candidates).BindingStatus);
    }

    [Fact]
    public void RustyKeyNativeRewardEventBindsAndUsesExistingStoryCompiler()
    {
        var snapshot = RustyKeyEventSnapshot();
        var binding = BindRustyKey(snapshot, RankRustyKey(snapshot, "story.advance_event"));

        Assert.Equal("ready", binding.BindingStatus);
        var candidate = Assert.Single(binding.BoundCandidates);
        Assert.Equal("story.advance_event", candidate.OptionId);
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "grandpa_direction_id" && value.Value == "obtain_rusty_key");
        var plan = new DailyPlanCompiler().Compile(binding.BoundCandidates, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.advance_story_event", item.OptionId);
        Assert.Equal("advance_story_event", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Theory]
    [InlineData("wrong_event")]
    [InlineData("wrong_asset")]
    [InlineData("threshold_not_reached")]
    [InlineData("reward_unclaimed")]
    [InlineData("prerequisite_unseen")]
    [InlineData("key_event_seen")]
    [InlineData("missing_museum_key")]
    [InlineData("invalid_museum_key_type")]
    [InlineData("missing_key_event_seen")]
    [InlineData("missing_festival_flag")]
    [InlineData("museum_unavailable")]
    [InlineData("story_unavailable")]
    public void RustyKeyRejectsUnattributableEvent(string defect)
    {
        var snapshot = RustyKeyEventSnapshot(root =>
        {
            var museum = root["world_progress"]!["museum"]!["value"]!.AsObject();
            var story = root["player"]!["story_event"]!["value"]!;
            switch (defect)
            {
                case "wrong_event": story["event_id"] = "63"; break;
                case "wrong_asset": story["from_asset_name"] = "OtherMod/Events/Farm"; break;
                case "threshold_not_reached": museum["donated_count"] = 59; break;
                case "reward_unclaimed": museum["rusty_key_reward_claimed"] = false; break;
                case "prerequisite_unseen": museum["rusty_key_prerequisite_event_seen"] = false; break;
                case "key_event_seen": museum["rusty_key_event_seen"] = true; break;
                case "missing_museum_key": museum.Remove("has_rusty_key"); break;
                case "invalid_museum_key_type": museum["has_rusty_key"] = "false"; break;
                case "missing_key_event_seen": museum.Remove("rusty_key_event_seen"); break;
                case "missing_festival_flag": story.AsObject().Remove("is_festival"); break;
                case "museum_unavailable": root["world_progress"]!["museum"]!["status"] = "unavailable"; break;
                case "story_unavailable": root["player"]!["story_event"]!["status"] = "unavailable"; break;
            }
        });

        var candidates = RankRustyKey(snapshot, "story.advance_event");
        if (defect == "story_unavailable")
            Assert.Empty(candidates);
        else
            Assert.Single(candidates);
        var result = BindRustyKey(snapshot, candidates);
        Assert.Equal("blocked", result.BindingStatus);
        Assert.Empty(result.BoundCandidates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RustyKeyRejectsStaleOrDuplicateEventFingerprint(bool duplicate)
    {
        var snapshot = RustyKeyEventSnapshot();
        var candidates = RankRustyKey(snapshot, "story.advance_event");
        var candidate = Assert.Single(candidates);
        candidate.Parameters = duplicate
            ? candidate.Parameters.Append(Parameter("story_event_projection_fingerprint", "fixture-fingerprint")).ToArray()
            : candidate.Parameters.Select(value =>
                value.Name == "story_event_projection_fingerprint"
                    ? Parameter(value.Name, "stale-fingerprint")
                    : value).ToArray();

        Assert.Equal("blocked", BindRustyKey(snapshot, candidates).BindingStatus);
    }

    private static PolicyEventCandidatePrediction[] RankRustyKey(
        SnapshotEnvelope snapshot, string optionId) => new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { optionId }, true));

    private static GrandpaDirectionBindingResult BindRustyKey(
        SnapshotEnvelope snapshot, PolicyEventCandidatePrediction[] candidates) =>
        new GrandpaDirectionDailyCandidateBinding().Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "obtain_rusty_key",
            RankedCandidates = candidates
        }, snapshot);

    private static SnapshotEnvelope RustyKeyEventSnapshot(Action<JsonObject>? mutate = null)
    {
        var root = JsonSerializer.SerializeToNode(GrandpaSnapshot().State, JsonOptions)!.AsObject();
        root["world_progress"]!.AsObject()["museum"] = RustyKeyField(new
        {
            donated_count = 60,
            rusty_key_donation_threshold = 60,
            rusty_key_reward_id = "museum60",
            rusty_key_reward_action = "MarkEventSeen Host 295672",
            rusty_key_reward_claimed = true,
            rusty_key_prerequisite_event_seen = true,
            rusty_key_event_seen = false,
            has_rusty_key = false
        });
        root["player"]!.AsObject()["story_event"] = RustyKeyField(new
        {
            projection_status = "complete_locked_base_1.6.15",
            native_contract = RustyKeyStoryContract,
            projection_fingerprint = "fixture-fingerprint",
            active = true, event_up = true, event_id = "66",
            from_asset_name = "Data\\Events\\Farm", location_id = "Farm",
            is_festival = false, skipped = false, player_control_sequence = false,
            active_minigame_type = "", boundary_kind = "automatic_progress",
            current_command_index = 3, current_command_raw = "rustyKey"
        });
        mutate?.Invoke(root);
        return RustyKeySnapshotFromRoot(root);
    }

    private static JsonNode RustyKeyField(object value) => JsonSerializer.SerializeToNode(new
    {
        value, status = "available", source = new { kind = "game_object", path = "test" },
        adapter = "test", read_at_tick = 1, confidence = 1
    }, JsonOptions)!;

    private static SnapshotEnvelope RustyKeySnapshotFromRoot(JsonObject root)
    {
        var state = root.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1", State = state,
            StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-11-03T00:00:00Z", Completeness = "complete"
        };
    }
}
