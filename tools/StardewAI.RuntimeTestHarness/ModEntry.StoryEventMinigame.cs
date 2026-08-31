using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly HashSet<string> ExecutableStoryMinigameTypes = new(StringComparer.Ordinal)
    {
        "StardewValley.Minigames.BoatJourney",
        "StardewValley.Minigames.FantasyBoardGame",
        "StardewValley.Minigames.HaleyCowPictures",
        "StardewValley.Minigames.MaruComet",
        "StardewValley.Minigames.PlaneFlyBy",
        "StardewValley.Minigames.RobotBlastoff"
    };

    private const string RuntimeStoryEventMinigameNativeContract =
        "live_Game1_currentMinigame_native_tick_and_event_Update_with_exact_DialogueBox_input_until_minigame_end_or_fresh_decision_without_forceQuit_manual_tick_or_direct_event_minigame_state_mutation";

    private void StartStoryEventMinigame(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        var minigame = Game1.currentMinigame;
        var nativeEvent = Game1.CurrentEvent;
        var type = minigame?.GetType().FullName ?? string.Empty;
        if (minigame is null)
            reasons.Add("story_event_minigame_not_active");
        if (!ExecutableStoryMinigameTypes.Contains(type))
            reasons.Add("story_event_minigame_type_not_executable");
        if (!string.Equals(type, request.StoryEventMinigameType, StringComparison.Ordinal))
            reasons.Add("story_event_minigame_type_mismatch");
        if (!string.Equals(minigame?.minigameId() ?? string.Empty, request.StoryEventMinigameId, StringComparison.Ordinal))
            reasons.Add("story_event_minigame_id_mismatch");
        if (!string.Equals(request.StoryEventMinigameNativeContract, RuntimeStoryEventMinigameNativeContract, StringComparison.Ordinal))
            reasons.Add("story_event_minigame_native_contract_mismatch");
        if (!string.Equals(Game1.currentLocation?.NameOrUniqueName, request.StoryEventLocationId, StringComparison.Ordinal))
            reasons.Add("story_event_minigame_location_mismatch");

        if (string.Equals(request.StoryEventMinigameOwnerKind, "event_script", StringComparison.Ordinal))
        {
            if (nativeEvent is null || !Game1.eventUp)
                reasons.Add("story_event_minigame_event_not_active");
            else
            {
                if (!string.Equals(nativeEvent.id, request.StoryEventId, StringComparison.Ordinal))
                    reasons.Add("story_event_minigame_event_id_mismatch");
                if (nativeEvent.CurrentCommand != request.StoryEventCommandIndex)
                    reasons.Add("story_event_minigame_command_index_mismatch");
                if (!string.Equals(CurrentStoryEventCommand(nativeEvent), request.StoryEventCommandRaw, StringComparison.Ordinal))
                    reasons.Add("story_event_minigame_command_raw_mismatch");
                if (nativeEvent.isFestival)
                    reasons.Add("story_event_minigame_festival_owned_elsewhere");
            }
        }
        else if (!string.Equals(request.StoryEventMinigameOwnerKind, "world_cinematic", StringComparison.Ordinal) ||
                 !string.Equals(type, "StardewValley.Minigames.BoatJourney", StringComparison.Ordinal))
        {
            reasons.Add("story_event_minigame_owner_mismatch");
        }

        var hasResponse = request.StoryEventResponseIndex.HasValue ||
            !string.IsNullOrWhiteSpace(request.StoryEventResponseKey);
        if (hasResponse)
        {
            if (minigame is not FantasyBoardGame ||
                Game1.activeClickableMenu is not DialogueBox dialogue || !dialogue.isQuestion ||
                !StoryEventResponseMatches(dialogue, request))
                reasons.Add("story_event_minigame_dialogue_response_mismatch");
        }
        if (reasons.Count > 0 || minigame is null)
        {
            pending.Completion.SetResult(StoryEventMinigameBlocked(request, reasons.ToArray()));
            return;
        }
        if (activeStoryEventMinigame is not null)
        {
            pending.Completion.SetResult(StoryEventMinigameBlocked(request, "story_event_minigame_executor_busy"));
            return;
        }
        activeStoryEventMinigame = new ActiveStoryEventMinigame(pending, minigame, nativeEvent);
        Monitor.Log(
            "Story minigame executor bound " + type + " at command " + (nativeEvent?.CurrentCommand ?? -1) + ".",
            StardewModdingAPI.LogLevel.Trace);
    }

    private void TickStoryEventMinigameSafely()
    {
        if (activeStoryEventMinigame is null)
            return;
        var active = activeStoryEventMinigame;
        try
        {
            TickStoryEventMinigame(active);
        }
        catch (Exception ex)
        {
            activeStoryEventMinigame = null;
            active.Pending.Completion.SetResult(StoryEventMinigameBlocked(
                active.Pending.Request,
                "story_event_minigame_executor_exception:" + ex.GetType().Name));
        }
    }

    private void TickStoryEventMinigame(ActiveStoryEventMinigame active)
    {
        active.ElapsedTicks++;
        if (active.ElapsedTicks > 10800)
        {
            CompleteStoryEventMinigameBlocked(active, "story_event_minigame_runtime_timeout");
            return;
        }

        var currentMinigame = Game1.currentMinigame;
        if (currentMinigame is null)
        {
            if (string.Equals(active.Pending.Request.StoryEventMinigameOwnerKind, "world_cinematic", StringComparison.Ordinal) &&
                !string.Equals(Game1.currentLocation?.NameOrUniqueName, "IslandSouth", StringComparison.Ordinal))
            {
                CompleteStoryEventMinigameBlocked(active, "boat_journey_ended_without_island_warp");
                return;
            }
            var currentEvent = Game1.CurrentEvent;
            active.ProgressObserved |= active.NativeEvent is null || currentEvent is null || !Game1.eventUp ||
                !ReferenceEquals(currentEvent, active.NativeEvent) ||
                currentEvent.CurrentCommand != active.InitialCommandIndex ||
                !string.Equals(Game1.currentLocation?.NameOrUniqueName, active.InitialLocationId, StringComparison.Ordinal);
            if (!active.ProgressObserved)
            {
                CompleteStoryEventMinigameBlocked(active, "story_event_minigame_ended_without_progress_receipt");
                return;
            }
            CompleteStoryEventMinigameApplied(active, "minigame_completed");
            return;
        }
        if (!ReferenceEquals(currentMinigame, active.NativeMinigame))
        {
            CompleteStoryEventMinigameBlocked(active, "story_event_minigame_instance_changed_without_fresh_snapshot");
            return;
        }
        if (active.NativeEvent is not null)
        {
            var currentEvent = Game1.CurrentEvent;
            if (currentEvent is null || !Game1.eventUp || !ReferenceEquals(currentEvent, active.NativeEvent))
            {
                CompleteStoryEventMinigameBlocked(active, "story_event_minigame_event_instance_changed");
                return;
            }
            if (currentEvent.CurrentCommand != active.LastCommandIndex)
            {
                active.LastCommandIndex = currentEvent.CurrentCommand;
                active.ProgressObserved = true;
            }
        }

        var menu = Game1.activeClickableMenu;
        if (menu is DialogueBox dialogue)
        {
            if (active.NativeMinigame is not FantasyBoardGame)
            {
                CompleteStoryEventMinigameBlocked(
                    active,
                    "story_event_minigame_dialogue_owned_only_by_fantasy_board_game");
                return;
            }
            TickStoryEventMinigameDialogue(active, dialogue);
            return;
        }
        if (menu is not null)
            CompleteStoryEventMinigameBlocked(active, "story_event_minigame_unsupported_native_menu:" + menu.GetType().FullName);
    }

    private void TickStoryEventMinigameDialogue(ActiveStoryEventMinigame active, DialogueBox dialogue)
    {
        var request = active.Pending.Request;
        var hasBoundResponse = request.StoryEventResponseIndex.HasValue &&
            !string.IsNullOrWhiteSpace(request.StoryEventResponseKey);
        if (dialogue.isQuestion)
        {
            if (!hasBoundResponse)
            {
                CompleteStoryEventMinigameApplied(active, "fresh_dialogue_decision_boundary");
                return;
            }
            if (active.BoundResponseConsumed)
            {
                if (!ReferenceEquals(dialogue, active.BoundDialogue))
                    CompleteStoryEventMinigameApplied(active, "fresh_dialogue_decision_boundary");
                return;
            }
            if (!StoryEventResponseMatches(dialogue, request))
            {
                CompleteStoryEventMinigameBlocked(active, "story_event_minigame_dialogue_response_changed_before_input");
                return;
            }
            if (dialogue.transitioning)
                return;
            if (dialogue.characterIndexInDialogue < dialogue.getCurrentString().Length - 1)
            {
                active.NativeMinigame.receiveLeftClick(0, 0);
                active.DialogueClicks++;
                return;
            }
            if (dialogue.safetyTimer > 0)
                return;
            var index = request.StoryEventResponseIndex!.Value;
            var center = StoryEventResponseCenter(dialogue, index);
            dialogue.performHoverAction(center.X, center.Y);
            if (dialogue.selectedResponse != index)
            {
                CompleteStoryEventMinigameBlocked(active, "story_event_minigame_dialogue_hover_did_not_bind_response");
                return;
            }
            active.NativeMinigame.receiveLeftClick(center.X, center.Y);
            active.DialogueClicks++;
            active.BoundResponseConsumed = true;
            active.BoundDialogue = dialogue;
            active.ProgressObserved = true;
            return;
        }

        if (hasBoundResponse && !active.BoundResponseConsumed)
        {
            CompleteStoryEventMinigameBlocked(active, "story_event_minigame_bound_decision_disappeared_before_input");
            return;
        }
        if (dialogue.transitioning || dialogue.safetyTimer > 0 || active.ElapsedTicks % 12 != 0)
            return;
        if (!active.MessageClickLogged)
        {
            active.MessageClickLogged = true;
            Monitor.Log(
                "Story minigame executor is advancing a native non-question DialogueBox.",
                StardewModdingAPI.LogLevel.Trace);
        }
        active.NativeMinigame.receiveLeftClick(0, 0);
        active.DialogueClicks++;
        active.ProgressObserved = true;
    }

    private void CompleteStoryEventMinigameApplied(ActiveStoryEventMinigame active, string boundary)
    {
        activeStoryEventMinigame = null;
        active.Pending.Completion.SetResult(StoryEventMinigameResult(active, "applied", "verified", boundary));
    }

    private void CompleteStoryEventMinigameBlocked(ActiveStoryEventMinigame active, string reason)
    {
        activeStoryEventMinigame = null;
        active.Pending.Completion.SetResult(StoryEventMinigameResult(active, "blocked", "blocked", reason));
    }

    private static TrainingExecutionResult StoryEventMinigameResult(
        ActiveStoryEventMinigame active,
        string status,
        string verification,
        string boundary)
    {
        var afterMinigame = Game1.currentMinigame?.GetType().FullName ?? string.Empty;
        var afterCommand = Game1.CurrentEvent?.CurrentCommand ?? -1;
        var reasons = status == "applied"
            ? new[]
            {
                "native_Game1_minigame_tick_ownership_preserved",
                "native_event_and_dialogue_input_only",
                "boundary=" + boundary,
                "manual_tick_forceQuit_and_direct_state_mutation_not_used"
            }
            : new[] { boundary };
        return new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "advance_story_event_minigame",
            PrimitiveVerificationStatus = verification,
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "native_minigame_completed_or_reached_fresh_boundary=true",
            ObservedEffect = "boundary=" + boundary + ";minigame_type=" +
                (string.IsNullOrWhiteSpace(afterMinigame) ? "none" : afterMinigame) +
                ";command_index=" + afterCommand + ";dialogue_clicks=" + active.DialogueClicks +
                ";progress_observed=" + active.ProgressObserved.ToString().ToLowerInvariant(),
            BlockReasons = status == "blocked" ? reasons : Array.Empty<string>(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.story_event.active_minigame_type", Before = active.Pending.Request.StoryEventMinigameType, After = afterMinigame },
                new SimulatedFactChange { Path = "player.story_event.current_command_index", Before = active.InitialCommandIndex.ToString(), After = afterCommand.ToString() },
                new SimulatedFactChange { Path = "player.location_id", Before = active.InitialLocationId, After = Game1.currentLocation?.NameOrUniqueName ?? string.Empty }
            }
        };
    }

    private static TrainingExecutionResult StoryEventMinigameBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        new()
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "advance_story_event_minigame",
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "native_minigame_completed_or_reached_fresh_boundary=true",
            ObservedEffect = "minigame_type=" + (Game1.currentMinigame?.GetType().FullName ?? "none"),
            BlockReasons = reasons
        };
}
