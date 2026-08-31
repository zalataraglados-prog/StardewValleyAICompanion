using System.Reflection;
using System.Text.Json;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string StoryEventNativeContract =
        "live_Event_Update_tryEventCommand_and_DialogueBox_native_input_until_event_end_or_fresh_decision_minigame_or_player_control_boundary_without_skipEvent_or_direct_event_state_mutation";

    private const string StoryEventMinigameNativeContract =
        "live_Game1_currentMinigame_native_tick_and_event_Update_with_exact_DialogueBox_input_until_minigame_end_or_fresh_decision_without_forceQuit_manual_tick_or_direct_event_minigame_state_mutation";

    private static readonly HashSet<string> SupportedEventMinigameTypes = new(StringComparer.Ordinal)
    {
        "StardewValley.Minigames.FantasyBoardGame",
        "StardewValley.Minigames.HaleyCowPictures",
        "StardewValley.Minigames.MaruComet",
        "StardewValley.Minigames.PlaneFlyBy",
        "StardewValley.Minigames.RobotBlastoff"
    };

    private const string BoatJourneyType = "StardewValley.Minigames.BoatJourney";
    private const string GrandpaStoryType = "StardewValley.Minigames.GrandpaStory";
    private const string IntroType = "StardewValley.Minigames.Intro";
    private const string TelescopeSceneType = "StardewValley.Minigames.TelescopeScene";

    private static object ReadStoryEventContext(Farmer? player)
    {
        if (player is null)
            return new { schema_version = "story_event.v1", projection_status = "unavailable_world_or_player" };

        var currentEvent = Game1.CurrentEvent;
        var location = Game1.currentLocation;
        var dialogue = Game1.activeClickableMenu as DialogueBox;
        var active = Game1.eventUp && currentEvent is not null;
        var commands = currentEvent?.eventCommands ?? Array.Empty<string>();
        var commandRows = commands.Select((raw, index) =>
        {
            var commandName = ReadEventCommandName(raw);
            var resolved = string.Empty;
            if (index >= 3 && !string.IsNullOrWhiteSpace(commandName) &&
                Event.TryResolveCommandName(commandName, out var resolvedName))
                resolved = resolvedName;
            return new
            {
                index,
                raw,
                command_name = commandName,
                resolved_command_name = resolved,
                is_initialization_field = index < 3,
                is_current = currentEvent is not null && index == currentEvent.CurrentCommand
            };
        }).ToArray();
        var responseRows = dialogue?.responses?
            .Select((response, index) => new
            {
                index,
                response_key = response.responseKey ?? string.Empty,
                response_text = response.responseText ?? string.Empty,
                hotkey = response.hotkey.ToString()
            })
            .ToArray() ?? Array.Empty<object>();
        var currentCommandIndex = currentEvent?.CurrentCommand ?? -1;
        var currentCommandRaw = currentEvent is null || currentCommandIndex < 0 || currentCommandIndex >= commands.Length
            ? string.Empty
            : commands[currentCommandIndex] ?? string.Empty;
        var currentCommandName = ReadEventCommandName(currentCommandRaw);
        var minigame = Game1.currentMinigame;
        var minigameType = minigame?.GetType().FullName ?? string.Empty;
        var minigameSupport = ClassifyStoryEventMinigame(minigameType, active);
        var minigameRuntimeState = ReadStoryEventMinigameRuntimeState(minigame, minigameType);
        var questionOpen = dialogue?.isQuestion == true && dialogue.responses is { Length: > 0 };
        var boundaryKind = minigame is not null
            ? active ? "event_minigame" : "world_minigame"
            : !active
                ? "none"
                : currentEvent!.playerControlSequence
                    ? "player_control"
                    : questionOpen
                        ? "dialogue_decision"
                        : "automatic_progress";
        var diagnostics = new List<string>();
        if (!active) diagnostics.Add("no_active_native_event");
        if (currentEvent?.isFestival == true) diagnostics.Add("festival_event_owned_by_festival_actions");
        if (minigame is not null) diagnostics.Add("active_minigame_requires_story_advance_event_minigame");
        if (currentEvent?.playerControlSequence == true) diagnostics.Add("player_control_sequence_requires_exact_sequence_handler");

        var fingerprintBody = new
        {
            schema = "story_event.v1",
            active,
            event_up = Game1.eventUp,
            location_id = location?.NameOrUniqueName,
            event_id = currentEvent?.id,
            from_asset_name = currentEvent?.fromAssetName,
            current_command_index = currentCommandIndex,
            current_command_raw = currentCommandRaw,
            command_count = commands.Length,
            commands,
            currentEvent?.isFestival,
            currentEvent?.isWedding,
            currentEvent?.isMemory,
            currentEvent?.skippable,
            currentEvent?.skipped,
            currentEvent?.forked,
            currentEvent?.eventSwitched,
            currentEvent?.simultaneousCommand,
            currentEvent?.playerControlSequence,
            currentEvent?.playerControlSequenceID,
            player_control_target_x = currentEvent?.playerControlTargetTile.X,
            player_control_target_y = currentEvent?.playerControlTargetTile.Y,
            currentEvent?.markEventSeen,
            active_minigame = minigameType,
            minigame_id = minigame?.minigameId(),
            minigame_support = new
            {
                minigameSupport.supported,
                minigameSupport.owner_kind,
                minigameSupport.execution_mode,
                minigameSupport.support_status,
                minigameSupport.block_reason
            },
            active_menu_type = Game1.activeClickableMenu?.GetType().FullName,
            question_key = location?.lastQuestionKey,
            responses = responseRows
        };

        return new
        {
            schema_version = "story_event.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = Sha256(JsonSerializer.Serialize(fingerprintBody)),
            projection_tick = unchecked((long)Game1.ticks),
            invocation_policy = "autonomous_story_progress_with_model_selected_live_dialogue_decisions",
            native_contract = StoryEventNativeContract,
            active,
            event_up = Game1.eventUp,
            event_id = currentEvent?.id ?? string.Empty,
            from_asset_name = currentEvent?.fromAssetName ?? string.Empty,
            location_id = location?.NameOrUniqueName ?? string.Empty,
            is_festival = currentEvent?.isFestival ?? false,
            is_wedding = currentEvent?.isWedding ?? false,
            is_memory = currentEvent?.isMemory ?? false,
            skippable = currentEvent?.skippable ?? false,
            skipped = currentEvent?.skipped ?? false,
            forked = currentEvent?.forked ?? false,
            event_switched = currentEvent?.eventSwitched ?? false,
            simultaneous_command = currentEvent?.simultaneousCommand ?? false,
            mark_event_seen = currentEvent?.markEventSeen ?? false,
            event_already_seen = currentEvent is not null && player.eventsSeen?.Contains(currentEvent.id) == true,
            current_command_index = currentCommandIndex,
            command_count = commands.Length,
            current_command_raw = currentCommandRaw,
            current_command_name = currentCommandName,
            current_command_resolved_name = Event.TryResolveCommandName(currentCommandName, out var resolvedCurrent)
                ? resolvedCurrent
                : string.Empty,
            commands = commandRows,
            remaining_commands = commandRows.Where(row => row.index >= Math.Max(0, currentCommandIndex)).ToArray(),
            actors = currentEvent?.actors.Select(actor => new
            {
                name = actor.Name,
                display_name = actor.displayName,
                runtime_type = actor.GetType().FullName,
                tile_x = actor.TilePoint.X,
                tile_y = actor.TilePoint.Y,
                facing_direction = actor.FacingDirection
            }).ToArray() ?? Array.Empty<object>(),
            farmer_actor_count = currentEvent?.farmerActors.Count ?? 0,
            player_control_sequence = currentEvent?.playerControlSequence ?? false,
            player_control_sequence_id = currentEvent?.playerControlSequenceID ?? string.Empty,
            player_control_target_tile_x = currentEvent?.playerControlTargetTile.X,
            player_control_target_tile_y = currentEvent?.playerControlTargetTile.Y,
            active_custom_event_script_type = currentEvent?.currentCustomEventScript?.GetType().FullName ?? string.Empty,
            active_minigame_type = minigameType,
            active_minigame_id = minigame?.minigameId() ?? string.Empty,
            active_minigame_native_contract = StoryEventMinigameNativeContract,
            active_minigame_owner_kind = minigameSupport.owner_kind,
            active_minigame_execution_mode = minigameSupport.execution_mode,
            active_minigame_support_status = minigameSupport.support_status,
            active_minigame_supported = minigameSupport.supported,
            active_minigame_requires_model_response = minigameSupport.supported && questionOpen,
            active_minigame_block_reason = minigameSupport.block_reason,
            active_minigame_runtime_state = minigameRuntimeState,
            dialogue_menu_open = dialogue is not null,
            dialogue_is_question = dialogue?.isQuestion ?? false,
            dialogue_question_key = location?.lastQuestionKey ?? string.Empty,
            dialogue_speaker_name = dialogue?.characterDialogue?.speaker?.Name ?? string.Empty,
            dialogue_transitioning = dialogue?.transitioning,
            dialogue_safety_timer = dialogue?.safetyTimer,
            dialogue_typing = dialogue?.showTyping,
            dialogue_finished = dialogue?.dialogueFinished,
            dialogue_selected_response = dialogue?.selectedResponse,
            dialogue_responses = responseRows,
            boundary_kind = boundaryKind,
            exit_location_name = currentEvent?.exitLocation?.Name ?? string.Empty,
            exit_location_is_structure = currentEvent?.exitLocation?.IsStructure,
            service_status = minigameSupport.supported
                ? "story_minigame_ready"
                : active && currentEvent?.isFestival != true && minigame is null && currentEvent?.playerControlSequence != true
                    ? "ordinary_event_ready"
                    : boundaryKind,
            blocked_diagnostics = diagnostics.ToArray()
        };
    }

    private static object ReadStoryEventMinigameRuntimeState(object? minigame, string type)
    {
        if (minigame is null)
            return new { state_kind = "none" };
        return type switch
        {
            "StardewValley.Minigames.FantasyBoardGame" => new
            {
                state_kind = "fantasy_board_game",
                which_slide = ReadMinigameMember<int>(minigame, "whichSlide"),
                shake_timer_ms = ReadMinigameMember<int>(minigame, "shakeTimer"),
                end_timer_ms = ReadMinigameMember<int>(minigame, "endTimer")
            },
            "StardewValley.Minigames.MaruComet" => new
            {
                state_kind = "maru_comet",
                total_timer_ms = ReadMinigameMember<int>(minigame, "totalTimer"),
                flyby_timer_ms = ReadMinigameMember<int>(minigame, "flybyTimer"),
                fade = ReadMinigameMember<float>(minigame, "fade")
            },
            "StardewValley.Minigames.HaleyCowPictures" => new
            {
                state_kind = "haley_cow_pictures",
                between_photo_timer_ms = ReadMinigameMember<int>(minigame, "betweenPhotoTimer"),
                photos_taken = ReadMinigameMember<int>(minigame, "numberOfPhotosSoFar"),
                fade_alpha = ReadMinigameMember<float>(minigame, "fadeAlpha")
            },
            "StardewValley.Minigames.PlaneFlyBy" or "StardewValley.Minigames.RobotBlastoff" => new
            {
                state_kind = type.EndsWith("PlaneFlyBy", StringComparison.Ordinal) ? "plane_fly_by" : "robot_blastoff",
                elapsed_ms = ReadMinigameMember<int>(minigame, "millisecondsSinceStart"),
                smoke_timer_ms = ReadMinigameMember<int>(minigame, "smokeTimer")
            },
            BoatJourneyType => new
            {
                state_kind = "boat_journey",
                fade_complete = ReadMinigameMember<bool>(minigame, "_fadeComplete"),
                total_path_distance = ReadMinigameMember<float>(minigame, "_totalPathDistance"),
                traveled_distance = ReadMinigameMember<float>(minigame, "traveledBoatDistance"),
                departure_delay_seconds = ReadMinigameMember<float>(minigame, "departureDelay")
            },
            GrandpaStoryType => new
            {
                state_kind = "grandpa_story_player_setup",
                scene = ReadMinigameMember<int>(minigame, "scene"),
                total_ms = ReadMinigameMember<int>(minigame, "totalMilliseconds"),
                speech_timer_ms = ReadMinigameMember<int>(minigame, "grandpaSpeechTimer"),
                mouse_active = ReadMinigameMember<bool>(minigame, "mouseActive"),
                letter_clicked = ReadMinigameMember<bool>(minigame, "clickedLetter"),
                letter_open = ReadMinigameMember<object>(minigame, "letterView") is not null,
                fading_to_quit = ReadMinigameMember<bool>(minigame, "fadingToQuit")
            },
            IntroType => new
            {
                state_kind = "intro_player_setup",
                current_state = ReadMinigameMember<int>(minigame, "currentState"),
                character_creation_open = ReadMinigameMember<object>(minigame, "characterCreateMenu") is not null,
                quit_requested = ReadMinigameMember<bool>(minigame, "quit"),
                quit_completed = ReadMinigameMember<bool>(minigame, "hasQuit")
            },
            TelescopeSceneType => new { state_kind = "deprecated_telescope_scene_placeholder" },
            _ => new { state_kind = "other_minigame" }
        };
    }

    private static (bool supported, string owner_kind, string execution_mode, string support_status, string block_reason)
        ClassifyStoryEventMinigame(string type, bool eventActive)
    {
        if (SupportedEventMinigameTypes.Contains(type))
        {
            return eventActive
                ? (true, "event_script", type.EndsWith("FantasyBoardGame", StringComparison.Ordinal)
                    ? "native_event_with_dialogue" : "native_passive", "supported", string.Empty)
                : (false, "orphaned_event_minigame", "blocked", "blocked", "event_minigame_without_active_event");
        }
        if (type == BoatJourneyType)
            return (true, "world_cinematic", "native_passive", "supported", string.Empty);
        if (type is GrandpaStoryType or IntroType)
            return (false, "new_game_player_setup", "player_owned", "excluded_player_setup", "player_identity_and_character_creation_boundary");
        if (type == TelescopeSceneType)
            return (false, "deprecated_base_placeholder", "unreachable", "deprecated_placeholder", "base_1_6_15_has_no_instantiation_and_no_completion_path");
        return string.IsNullOrWhiteSpace(type)
            ? (false, "none", "none", "inactive", string.Empty)
            : (false, "other_minigame_owner", "separate_action", "owned_elsewhere", "minigame_owned_by_another_registered_action");
    }

    private static T? ReadMinigameMember<T>(object source, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = source.GetType();
        while (type is not null)
        {
            var field = type.GetField(name, flags | BindingFlags.DeclaredOnly);
            if (field is not null)
                return field.GetValue(source) is T value ? value : default;
            var property = type.GetProperty(name, flags | BindingFlags.DeclaredOnly);
            if (property is not null)
                return property.GetValue(source) is T value ? value : default;
            type = type.BaseType;
        }
        return default;
    }

    private static string ReadEventCommandName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var args = ArgUtility.SplitBySpaceQuoteAware(raw);
        return args.Length == 0 ? string.Empty : args[0];
    }
}
