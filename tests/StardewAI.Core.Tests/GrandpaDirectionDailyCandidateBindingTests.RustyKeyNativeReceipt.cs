using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    [Fact]
    public void NativeRustyKeyReceiptReusesOriginalBindingAndCompilerWithoutTeacherAuthority()
    {
        var fixture = CreateNativeKeyReceiptFixture();
        var receipt = RustyKeyNativeKeyReceiptBuilder.Build(fixture.RequestPath);
        Assert.True(receipt.NativeTerminalOutcomeVerified);
        Assert.False(receipt.TeacherComparisonVerified);
        Assert.False(receipt.FullCriterionVerified);
        Assert.False(receipt.FormalTrainingAuthorized);
        Assert.Equal(7, receipt.TotalDay);
        Assert.Equal(fixture.Before.StateHash, receipt.BeforeStateHash);
        Assert.Equal(fixture.After.StateHash, receipt.AfterStateHash);
        Assert.Equal(fixture.Queue.QueueId, receipt.QueueId);
        Assert.Equal(5, receipt.SourceSha256.Count);
        var path = Path.Combine(fixture.Root, "native-receipt.json");
        WriteNativeKeyJson(path, receipt);
        Assert.Equal(JsonSerializer.Serialize(receipt, JsonOptions),
            JsonSerializer.Serialize(RustyKeyNativeKeyReceiptBuilder.Verify(path), JsonOptions));
    }

    [Theory]
    [InlineData("grandpa.maximum_21")]
    [InlineData("goal.grandpa_21")]
    public void NativeRustyKeyReceiptAcceptsCanonicalParentGoalWithoutWeakeningAttribution(string goalId)
    {
        var f = CreateNativeKeyReceiptFixture();
        EditNativeKeyJson(f.Request.ActionQueuePath, node => node["goal_id"] = goalId);
        Assert.True(RustyKeyNativeKeyReceiptBuilder.Build(f.RequestPath).NativeTerminalOutcomeVerified);
    }

    [Theory]
    [InlineData("aggregate_before")]
    [InlineData("wrong_event")]
    [InlineData("wrong_asset")]
    [InlineData("missing_native_key_before")]
    [InlineData("late_day")]
    [InlineData("negative_day")]
    [InlineData("missing_day")]
    [InlineData("boolean_day")]
    [InlineData("mismatched_day")]
    [InlineData("foreign_player")]
    [InlineData("game_version")]
    [InlineData("nonfresh_tick")]
    [InlineData("missing_tick")]
    [InlineData("broken_hash")]
    [InlineData("no_key_after")]
    [InlineData("missing_key_after")]
    [InlineData("missing_museum_key_after")]
    [InlineData("unreleased_event")]
    [InlineData("wrong_location")]
    [InlineData("receipt_queue_mismatch")]
    [InlineData("receipt_run_mismatch")]
    [InlineData("skip_event")]
    [InlineData("missing_event_fact")]
    [InlineData("conflicting_event_fact")]
    [InlineData("null_event_facts")]
    [InlineData("null_fact")]
    [InlineData("null_parameter")]
    [InlineData("null_step")]
    [InlineData("null_audit")]
    [InlineData("extra_queue_item")]
    [InlineData("stale_fingerprint")]
    [InlineData("extra_response")]
    [InlineData("wrong_step")]
    [InlineData("failed_precondition")]
    [InlineData("conflicting_parameter")]
    [InlineData("os_actor")]
    [InlineData("missing_schema")]
    [InlineData("wrong_goal")]
    [InlineData("command_goal")]
    [InlineData("later_after_same_state")]
    [InlineData("later_after_different_state")]
    [InlineData("recorded_before_tick_mismatch")]
    [InlineData("missing_recorded_after_hash")]
    [InlineData("missing_recorded_before_tick")]
    [InlineData("missing_recorded_after_tick")]
    [InlineData("mistyped_recorded_tick")]
    [InlineData("recorded_stale_after")]
    public void NativeRustyKeyReceiptRejectsUnboundSourcesAndIncompleteOutcomes(string mode)
    {
        var f = CreateNativeKeyReceiptFixture();
        void Before(Action<JsonObject> edit) => EditNativeKeySnapshot(f.Request.BeforeSnapshotPath, edit, f);
        void After(Action<JsonObject> edit) => EditNativeKeySnapshot(f.Request.AfterSnapshotPath, edit);
        JsonObject State(JsonObject node) => node["state"]!.AsObject();
        void Command(Action<JsonNode> edit) => EditNativeKeyJson(f.Request.ActionQueuePath,
            node => edit(node["items"]![0]!["normalized_command"]!));
        void Result(Action<JsonNode> edit) => EditNativeKeyJson(f.Request.ExecutionReceiptPath, edit);
        switch (mode)
        {
            case "aggregate_before": Before(node =>
                { State(node)["player"]!["story_event"]!["value"]!["active"] = false; State(node)["player"]!["location_id"]!["value"] = "FarmHouse"; }); break;
            case "wrong_event": Before(node => State(node)["player"]!["story_event"]!["value"]!["event_id"] = "67"); break;
            case "wrong_asset": Before(node => State(node)["player"]!["story_event"]!["value"]!["from_asset_name"] = "Data/Events/Town"); break;
            case "missing_native_key_before": Before(node => State(node)["world_progress"]!["museum"]!["value"]!.AsObject().Remove("has_rusty_key")); break;
            case "late_day": Before(node => State(node)["time"]!["total_days"]!["value"] = 224); After(node => State(node)["time"]!["total_days"]!["value"] = 224); break;
            case "negative_day": Before(node => State(node)["time"]!["total_days"]!["value"] = -1); break;
            case "missing_day": Before(node => State(node)["time"]!.AsObject().Remove("total_days")); break;
            case "boolean_day": Before(node => State(node)["time"]!["total_days"]!["value"] = true); break;
            case "mismatched_day": After(node => State(node)["time"]!["total_days"]!["value"] = 8); break;
            case "foreign_player": After(node => node["player_id"]!["value"] = "other-player"); break;
            case "game_version": After(node => node["game_version"] = "1.6.14"); break;
            case "nonfresh_tick": After(node => node["game_tick"] = 100); break;
            case "missing_tick": EditNativeKeyJson(f.Request.BeforeSnapshotPath, node => node.AsObject().Remove("game_tick")); break;
            case "broken_hash": EditNativeKeyJson(f.Request.BeforeSnapshotPath, node => node["state_hash"] = new string('b', 64)); break;
            case "no_key_after": After(node => State(node)["player"]!["has_rusty_key"]!["value"] = false); break;
            case "missing_key_after": After(node => State(node)["player"]!.AsObject().Remove("has_rusty_key")); break;
            case "missing_museum_key_after": After(node => State(node)["world_progress"]!["museum"]!["value"]!.AsObject().Remove("has_rusty_key")); break;
            case "unreleased_event": After(node => State(node)["player"]!["story_event"]!["value"]!["active"] = true); break;
            case "wrong_location": After(node => State(node)["player"]!["location_id"]!["value"] = new JsonObject()); break;
            case "receipt_queue_mismatch": Result(node => node["queue_item_id"] = "foreign-item"); break;
            case "receipt_run_mismatch": Result(node => node["run_id"] = "foreign-run"); break;
            case "skip_event": Result(node => node["primitive_verification_reasons"] = new JsonArray("native_event_input_only", "boundary=event_completed", "skipEvent_used")); break;
            case "missing_event_fact": Result(node => node["changed_facts"]!.AsArray().RemoveAt(0)); break;
            case "conflicting_event_fact": Result(node => node["changed_facts"]!.AsArray().Add(new JsonObject
                { ["path"] = "player.story_event.event_seen", ["before"] = "false", ["after"] = "false" })); break;
            case "null_event_facts": Result(node => node["changed_facts"] = null); break;
            case "null_fact": Result(node => node["changed_facts"]!.AsArray().Add((JsonNode?)null)); break;
            case "null_parameter": Command(node => node["parameters"]!.AsArray().Add((JsonNode?)null)); break;
            case "null_step": Command(node => node["steps"]!.AsArray().Add((JsonNode?)null)); break;
            case "null_audit": EditNativeKeyJson(f.Request.ActionQueuePath, node => node["candidate_audit"]!.AsArray().Add((JsonNode?)null)); break;
            case "extra_queue_item": EditNativeKeyJson(f.Request.ActionQueuePath, node => node["items"]!.AsArray().Add(node["items"]![0]!.DeepClone())); break;
            case "stale_fingerprint": Command(node => node["parameters"]!.AsArray().Single(p => p!["name"]!.GetValue<string>() == "story_event_projection_fingerprint")!["value"] = new string('b', 64)); break;
            case "extra_response": Command(node => node["parameters"]!.AsArray().Add(new JsonObject { ["name"] = "story_event_response_key", ["value"] = "Yes" })); break;
            case "wrong_step": Command(node => node["steps"]![0]!["target"] = "foreign-event"); break;
            case "failed_precondition": EditNativeKeyJson(f.Request.ActionQueuePath, node => node["items"]![0]!["precondition_results"]![0]!["status"] = "failed"); break;
            case "conflicting_parameter": Command(node => node["parameters"]!.AsArray().Add(new JsonObject { ["name"] = "story_event_id", ["value"] = "67" })); break;
            case "os_actor": Command(node => node["actor"]!["control_surface"] = "os_keyboard"); break;
            case "missing_schema": EditNativeKeyJson(f.RequestPath, node => node.AsObject().Remove("schema_version")); break;
            case "wrong_goal": EditNativeKeyJson(f.Request.ActionQueuePath, node => node["goal_id"] = "goal.other"); break;
            case "command_goal": Command(node => node["goal_id"] = "goal.other"); break;
            case "later_after_same_state": After(node => node["game_tick"] = 201); break;
            case "later_after_different_state": After(node => State(node)["player"]!["fixture_later"] = RustyKeyField(true)); break;
            case "recorded_before_tick_mismatch": Result(node => node["before_game_tick"] = 99); break;
            case "missing_recorded_after_hash": Result(node => node.AsObject().Remove("after_state_hash")); break;
            case "missing_recorded_before_tick": Result(node => node.AsObject().Remove("before_game_tick")); break;
            case "missing_recorded_after_tick": Result(node => node.AsObject().Remove("after_game_tick")); break;
            case "mistyped_recorded_tick": Result(node => node["after_game_tick"] = "200"); break;
            case "recorded_stale_after": Result(node => node["after_snapshot_fresh"] = false); break;
            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }
        Assert.Throws<InvalidDataException>(() => RustyKeyNativeKeyReceiptBuilder.Build(f.RequestPath));
    }

    [Theory]
    [InlineData("teacher_comparison_verified", true)]
    [InlineData("formal_training_authorized", true)]
    [InlineData("full_criterion_verified", true)]
    [InlineData("native_terminal_outcome_verified", false)]
    public void PersistedNativeKeyReceiptCannotForgeReadOnlyClaims(string field, bool value)
    {
        var f = CreateNativeKeyReceiptFixture();
        var receipt = RustyKeyNativeKeyReceiptBuilder.Build(f.RequestPath);
        var path = Path.Combine(f.Root, "forged-receipt.json");
        var node = JsonSerializer.SerializeToNode(receipt, JsonOptions)!;
        node[field] = value;
        WriteNativeKeyJson(path, node);
        Assert.Throws<InvalidDataException>(() => RustyKeyNativeKeyReceiptBuilder.Verify(path));
    }

    [Fact]
    public void PersistedNativeKeyReceiptMustRebuildRetainedSourceFiles()
    {
        var f = CreateNativeKeyReceiptFixture();
        var path = Path.Combine(f.Root, "receipt.json");
        WriteNativeKeyJson(path, RustyKeyNativeKeyReceiptBuilder.Build(f.RequestPath));
        EditNativeKeyJson(f.Request.ExecutionReceiptPath, node => node["queue_id"] = "changed-after-build");
        Assert.Throws<InvalidDataException>(() => RustyKeyNativeKeyReceiptBuilder.Verify(path));
    }

    private sealed record NativeKeyReceiptFixture(string Root, string RequestPath,
        RustyKeyNativeKeyReceiptRequest Request, SnapshotEnvelope Before, SnapshotEnvelope After, ActionQueueEnvelope Queue);

    private static NativeKeyReceiptFixture CreateNativeKeyReceiptFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-rusty-key-native-receipt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var before = RustyKeyEventSnapshot(state =>
        {
            state["time"]!["total_days"] = RustyKeyField(7);
            state["time"]!["year"] = RustyKeyField(1);
            state["time"]!["season"] = RustyKeyField("spring");
            state["time"]!["day"] = RustyKeyField(8);
            state["player"]!["has_rusty_key"] = RustyKeyField(false);
            state["player"]!["location_id"] = RustyKeyField("Farm");
            state["player"]!["story_event"]!["value"]!["projection_fingerprint"] = new string('a', 64);
        });
        before.SchemaVersion = "snapshot.v1";
        before.GameVersion = "1.6.15";
        before.BridgeVersion = "fixture-native-key";
        before.GameTick = 100;
        before.SaveId = new FieldEnvelope<string?> { Status = FieldStatus.Available, Value = "fixture-save" };
        before.PlayerId = new FieldEnvelope<string?> { Status = FieldStatus.Available, Value = "17" };
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
        var afterState = JsonSerializer.SerializeToNode(after.State, JsonOptions)!;
        afterState["player"]!["has_rusty_key"]!["value"] = true;
        afterState["player"]!["story_event"]!["value"]!["active"] = false;
        afterState["player"]!["story_event"]!["value"]!["event_up"] = false;
        afterState["player"]!["story_event"]!["value"]!["event_id"] = "";
        afterState["world_progress"]!["museum"]!["value"]!["has_rusty_key"] = true;
        afterState["world_progress"]!["museum"]!["value"]!["rusty_key_event_seen"] = true;
        after.State = afterState.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        after.GameTick = 200;
        var binding = BindRustyKey(before, RankRustyKey(before, "story.advance_event"));
        Assert.Equal("ready", binding.BindingStatus);
        var goal = GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == "obtain_rusty_key").EffectiveGoalId;
        var queue = new ActionQueueCompiler().Compile(new DailyPlanCompiler().Compile(
            binding.BoundCandidates, before.StateHash, goal, maxCandidates: 1), before);
        Assert.Empty(Assert.Single(queue.Items).BlockingReasons);
        var result = new TrainingExecutionResult
        {
            RunId = "fixture-native-key", QueueId = queue.QueueId, QueueItemId = queue.Items[0].QueueItemId,
            BeforeStateHash = before.StateHash, OptionId = "executor.advance_story_event", Status = "applied",
            FeedbackAvailable = true, PrimitiveKind = "advance_story_event",
            PrimitiveVerificationStatus = "verified", PrimitiveVerificationReasons = new[]
                { "native_event_input_only", "boundary=event_completed", "skipEvent_not_used" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.story_event.event_id", Before = "66", After = "" },
                new SimulatedFactChange { Path = "player.story_event.event_seen", Before = "false", After = "true" }
            }
        };
        var request = new RustyKeyNativeKeyReceiptRequest
        {
            RunId = result.RunId, BeforeSnapshotPath = Path.Combine(root, "before.json"),
            AfterSnapshotPath = Path.Combine(root, "after.json"), ActionQueuePath = Path.Combine(root, "queue.json"),
            ExecutionReceiptPath = Path.Combine(root, "execution.json")
        };
        WriteNativeKeyJson(request.BeforeSnapshotPath, before);
        WriteNativeKeyJson(request.AfterSnapshotPath, after);
        WriteNativeKeyJson(request.ActionQueuePath, queue);
        var captured = JsonSerializer.SerializeToNode(result, JsonOptions)!;
        captured["before_game_tick"] = before.GameTick;
        captured["after_game_tick"] = after.GameTick;
        captured["after_state_hash"] = after.StateHash;
        captured["after_snapshot_fresh"] = true;
        WriteNativeKeyJson(request.ExecutionReceiptPath, captured);
        var requestPath = Path.Combine(root, "request.json");
        WriteNativeKeyJson(requestPath, request);
        return new NativeKeyReceiptFixture(root, requestPath, request, before, after, queue);
    }

    private static void EditNativeKeySnapshot(string path, Action<JsonObject> edit, NativeKeyReceiptFixture? rebind = null)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(node);
        var state = node["state"]!.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!;
        var hash = SnapshotHash.ComputeStateHash(state);
        node["state_hash"] = hash;
        WriteNativeKeyJson(path, node);
        if (rebind is null) return;
        EditNativeKeyJson(rebind.Request.ActionQueuePath, queue =>
        {
            queue["state_hash"] = hash;
            var command = queue["items"]![0]!["normalized_command"]!;
            command["state_hash"] = hash;
            command["parameters"]!.AsArray().Single(p => p!["name"]!.GetValue<string>() == "grandpa_source_state_hash")!["value"] = hash;
        });
        EditNativeKeyJson(rebind.Request.ExecutionReceiptPath, result => result["before_state_hash"] = hash);
    }

    private static void EditNativeKeyJson(string path, Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        edit(node);
        WriteNativeKeyJson(path, node);
    }

    private static void WriteNativeKeyJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
}
