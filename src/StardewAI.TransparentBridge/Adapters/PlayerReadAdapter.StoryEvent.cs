using System.Text.Json;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string StoryEventNativeContract =
        "live_Event_Update_tryEventCommand_and_DialogueBox_native_input_until_event_end_or_fresh_decision_minigame_or_player_control_boundary_without_skipEvent_or_direct_event_state_mutation";

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
        var questionOpen = dialogue?.isQuestion == true && dialogue.responses is { Length: > 0 };
        var boundaryKind = !active
            ? "none"
            : minigame is not null
                ? "event_minigame"
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
            service_status = active && currentEvent?.isFestival != true && minigame is null && currentEvent?.playerControlSequence != true
                ? "ordinary_event_ready"
                : boundaryKind,
            blocked_diagnostics = diagnostics.ToArray()
        };
    }

    private static string ReadEventCommandName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var args = ArgUtility.SplitBySpaceQuoteAware(raw);
        return args.Length == 0 ? string.Empty : args[0];
    }
}
