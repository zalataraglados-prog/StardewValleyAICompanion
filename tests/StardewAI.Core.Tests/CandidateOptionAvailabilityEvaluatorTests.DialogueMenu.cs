using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void CloseMenuSafeOrdinaryDialogueAvailableWhenAllProofsPresent()
    {
        var snapshot = DialogueMenuRecoverySnapshot(
            eventUp: false, isQuestion: false, responseCount: 0, characterPresent: true,
            speakerName: "Lewis", lastQuestionKey: null, isSleepPrompt: false, transitioning: false);

        var candidate = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates
            .Single(c => c.Kind == "recovery_close_menu");

        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenEventUpTrue()
    {
        var snapshot = DialogueMenuRecoverySnapshot(eventUp: true);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_event_up_true", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenIsQuestionTrue()
    {
        var snapshot = DialogueMenuRecoverySnapshot(isQuestion: true);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_is_question_true", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenResponseCountGreaterThanZero()
    {
        var snapshot = DialogueMenuRecoverySnapshot(responseCount: 3);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_responses_present:3", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenCharacterPresentFalse()
    {
        var snapshot = DialogueMenuRecoverySnapshot(characterPresent: false);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_character_present_false", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenSpeakerNameEmpty()
    {
        var snapshot = DialogueMenuRecoverySnapshot(speakerName: "");
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_speaker_name_empty", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenSpeakerNameNull()
    {
        var snapshot = DialogueMenuRecoverySnapshot(speakerName: null);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_speaker_name_field_missing", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenLastQuestionKeyPresent()
    {
        var snapshot = DialogueMenuRecoverySnapshot(lastQuestionKey: "Sleep");
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains(candidate.BlockReasons, reason => reason.Contains("dialogue_close_last_question_key_present"));
    }

    [Fact]
    public void CloseMenuBlocksWhenSleepPromptTrue()
    {
        var snapshot = DialogueMenuRecoverySnapshot(isSleepPrompt: true);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_is_sleep_prompt", candidate.BlockReasons);
    }

    [Fact]
    public void LevelUpRecoveryCarriesExplicitPreferredProfessionChoice()
    {
        var snapshot = LevelUpMenuRecoverySnapshot(professionChooser: true, professionChoices: "[{\"profession_id\":0,\"title\":\"Rancher\"},{\"profession_id\":1,\"title\":\"Tiller\"}]");

        var candidate = GetCloseMenuCandidate(snapshot);

        Assert.True(candidate.Available);
        Assert.Contains(candidate.Parameters, row => row.Name == "profession_choice_id" && row.Value == "1");
        Assert.Contains(candidate.Parameters, row => row.Name == "profession_choice_source" && row.Value == "baseline_grandpa_perfection_policy_v1");
    }

    [Fact]
    public void LevelUpRecoveryBlocksUntilNativeMenuInputIsReady()
    {
        var snapshot = LevelUpMenuRecoverySnapshot(
            professionChooser: false,
            professionChoices: "[]",
            canReceiveInput: false);

        var candidate = GetCloseMenuCandidate(snapshot);

        Assert.False(candidate.Available);
        Assert.Contains("level_up_menu_input_not_ready", candidate.BlockReasons);
    }

    [Fact]
    public void ShippingSummaryRecoveryIsAvailableBeforeAnimationFinishes()
    {
        var candidate = GetCloseMenuCandidate(ShippingSummaryRecoverySnapshot());

        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "execution_option_id" && row.Value == "executor.close_menu");
    }

    [Fact]
    public void ShippingSummaryRecoveryBlocksWhenOkButtonProofIsMissing()
    {
        var candidate = GetCloseMenuCandidate(ShippingSummaryRecoverySnapshot(okButtonPresent: false));

        Assert.False(candidate.Available);
        Assert.Contains("shipping_summary_ok_button_missing", candidate.BlockReasons);
    }

    private static EventCandidate GetCloseMenuCandidate(SnapshotEnvelope snapshot)
    {
        return new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates
            .Single(c => c.Kind == "recovery_close_menu");
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
        return Snapshot(json);
    }

    private static SnapshotEnvelope ShippingSummaryRecoverySnapshot(bool okButtonPresent = true)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "current_item_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"FarmHouse","current_location_is_home":true,"bed_tile_x":3,"bed_tile_y":8,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"ShippingMenu","full_type":"StardewValley.Menus.ShippingMenu","is_sleep_prompt":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "menu_specific_state": {"value":{"kind":"shipping_summary","can_receive_input":false,"current_page":0,"ok_button_present":OK_BUTTON_PRESENT,"ready_for_native_ok":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"FarmHouse","width":12,"height":12,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("OK_BUTTON_PRESENT", okButtonPresent ? "true" : "false"));
    }

    private static SnapshotEnvelope LevelUpMenuRecoverySnapshot(
        bool professionChooser,
        string professionChoices,
        bool canReceiveInput = true)
    {
        var json = """
        {
          "time": {
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"FarmHouse","current_location_is_home":true,"bed_tile_x":3,"bed_tile_y":8,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"LevelUpMenu","full_type":"StardewValley.Menus.LevelUpMenu","is_sleep_prompt":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "menu_specific_state": {"value":{"kind":"level_up","is_active":true,"is_profession_chooser":PROFESSION_CHOOSER,"can_receive_input":CAN_INPUT,"reflection_fields_complete":true,"current_skill":0,"current_level":5,"profession_choices":PROFESSION_CHOICES},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"FarmHouse","width":12,"height":12,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
            .Replace("PROFESSION_CHOOSER", professionChooser ? "true" : "false")
            .Replace("CAN_INPUT", canReceiveInput ? "true" : "false")
            .Replace("PROFESSION_CHOICES", professionChoices);
        return Snapshot(json);
    }
}
