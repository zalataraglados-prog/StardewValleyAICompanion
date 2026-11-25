using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NativeNpcQuestionRecoveryTests
{
    [Fact]
    public void NativeNpcQuestionCompilesThroughMandatoryRecoveryInsteadOfClosingIt()
    {
        var snapshot = NpcQuestionSnapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, ["recovery.stabilize_day"], includeExecutorCalibrationOptions: true);
        var ranking = StageOneMandatoryMenuRecovery.BuildRanking(availability, "grandpa_evaluation_max_score");
        var selected = StageOneMandatoryMenuRecovery.TrySelect(ranking);
        Assert.True(selected is not null, JsonSerializer.Serialize(ranking));
        Assert.Equal(NativeNpcQuestionRecovery.CandidateKind, selected.Kind);
        Assert.Contains(selected.Parameters, row => row.Name == "dialogue_response_key" && row.Value == "positive");
        var plan = new DailyPlanCompiler().Compile([selected], snapshot.StateHash, maxCandidates: 1);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        Assert.Single(queue.Items);
        Assert.Equal("executor.choose_dialogue_response", queue.Items[0].OptionId);
        Assert.All(queue.Items, item => Assert.Equal("pending", item.Status));
        Assert.False(availability.Options.Single().EventCandidates.Single(row => row.Kind == "recovery_close_menu").Available);
    }

    [Theory]
    [InlineData("event")]
    [InlineData("speaker")]
    [InlineData("source")]
    [InlineData("unreadable")]
    [InlineData("callback")]
    [InlineData("extra_action")]
    [InlineData("quick_response")]
    [InlineData("transition")]
    [InlineData("location_question")]
    public void NativeNpcQuestionDoesNotWidenOtherDialogueContracts(string mutation)
    {
        var snapshot = NpcQuestionSnapshot(mutation);
        Assert.Null(NativeNpcQuestionRecovery.Candidate(snapshot));
    }

    [Fact]
    public void NpcQuestionIdentityBindsEffectsAndRejectsAChangedOrInferiorAnswer()
    {
        var original = NpcQuestionSnapshot();
        var evidence = NativeNpcQuestionRecovery.Read(original)!;
        Assert.True(NativeNpcQuestionRecovery.Matches(original, evidence.Identity(), "positive"));
        Assert.False(NativeNpcQuestionRecovery.Matches(original, evidence.Identity(), "negative"));
        var changed = NpcQuestionSnapshot("effect");
        Assert.False(NativeNpcQuestionRecovery.Matches(changed, evidence.Identity(), "positive"));
    }

    [Theory]
    [InlineData("Yes!$h", true)]
    [InlineData("No.$a#$eAnother page", false)]
    [InlineData("%item object 72 1", false)]
    [InlineData("[72]", false)]
    [InlineData("$action AddMoney 100", false)]
    [InlineData("$q 1 next", false)]
    public void NpcReplyRequiresOnlyTextAndPortraitEffects(string reply, bool allowed) =>
        Assert.Equal(allowed, NativeNpcQuestionEvidence.IsDisplayOnlyReply(reply));

    private static SnapshotEnvelope NpcQuestionSnapshot(string mutation = "")
    {
        var snapshot = DialogueMenuRecoverySnapshot(isQuestion: true, responseCount: 3, speakerName: "Alex");
        var menus = JsonNode.Parse(snapshot.State["menus"].GetRawText())!.AsObject();
        var active = menus["active_menu"]!["value"]!.AsObject();
        var evidence = new NativeNpcQuestionEvidence
        {
            Speaker = "Alex", OrdinaryQuestion = true,
            Responses =
            [
                new() { Key = "negative", SeenId = "qid", FriendshipChange = -20, Reply = "No.$a" },
                new() { Key = "neutral", SeenId = "qid", FriendshipChange = 0, Reply = "Maybe." },
                new() { Key = "positive", SeenId = "qid", FriendshipChange = 20, Reply = "Thank you!$h" }
            ]
        };
        if (mutation == "event") active["event_up"] = true;
        if (mutation == "speaker") evidence.Speaker = "Lewis";
        if (mutation == "source") evidence.Source = "guessed_effects";
        if (mutation == "callback") evidence.OrdinaryQuestion = false;
        if (mutation == "extra_action") evidence.Responses[2].Reply = "$action AddMoney 100";
        if (mutation == "quick_response") evidence.Responses[2].SeenId = "";
        if (mutation == "transition") active["dialogue_transitioning"] = true;
        if (mutation == "location_question") active["last_question_key"] = "Sleep";
        if (mutation == "effect") evidence.Responses[0].FriendshipChange = 25;
        menus["menu_specific_state"]!["value"] = JsonSerializer.SerializeToNode(new { npc_question = evidence });
        if (mutation == "unreadable") menus["menu_specific_state"]!["status"] = "unavailable";
        snapshot.State["menus"] = JsonSerializer.SerializeToElement(menus);
        return snapshot;
    }
    private static SnapshotEnvelope DialogueMenuRecoverySnapshot(
        bool eventUp = false,
        bool isQuestion = false,
        int responseCount = 0,
        bool characterPresent = true,
        string? speakerName = "Lewis",
        string? lastQuestionKey = null,
        bool isSleepPrompt = false,
        bool transitioning = false)
    {
        var speakerField = speakerName is null
            ? ""
            : ",\"dialogue_speaker_name\":\"" + speakerName.Replace("\"", "\\\"") + "\"";
        var lastQkField = string.IsNullOrWhiteSpace(lastQuestionKey)
            ? ""
            : ",\"last_question_key\":\"" + lastQuestionKey.Replace("\"", "\\\"") + "\"";

        var json = """
        {
          "time": {
            "time": {"value":2300,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":3,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "current_item_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"Town","current_location_is_home":false,"entry_tile_x":3,"entry_tile_y":9,"bed_tile_x":3,"bed_tile_y":8,"bed_tile_has_bed":true,"sleep_executor_enabled":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{
              "is_open":true,
              "type":"DialogueBox",
              "full_type":"StardewValley.Menus.DialogueBox",
              "is_sleep_prompt":IS_SLEEP,
              "event_up":EVENT_UP,
              "dialogue_is_question":IS_QUESTION,
              "dialogue_response_count":RESPONSE_COUNT,
              "dialogue_transitioning":TRANSITIONING,
              "dialogue_safety_timer":0,
              "dialogue_character_present":CHAR_PRESENT,
              "dialogue_typing":true,
              "dialogue_finished":false
              SPEAKER
              LAST_QK
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "menu_specific_state": {"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Town","width":12,"height":12,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
            .Replace("EVENT_UP", eventUp ? "true" : "false")
            .Replace("IS_QUESTION", isQuestion ? "true" : "false")
            .Replace("RESPONSE_COUNT", responseCount.ToString())
            .Replace("CHAR_PRESENT", characterPresent ? "true" : "false")
            .Replace("SPEAKER", speakerField)
            .Replace("LAST_QK", lastQkField)
            .Replace("IS_SLEEP", isSleepPrompt ? "true" : "false")
            .Replace("TRANSITIONING", transitioning ? "true" : "false");
        return new SnapshotEnvelope { StateHash = new string('a', 64), State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)! };
    }

}
