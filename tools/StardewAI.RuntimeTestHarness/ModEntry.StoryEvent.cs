using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartStoryEvent(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        var nativeEvent = Game1.CurrentEvent;
        if (nativeEvent is null || !Game1.eventUp)
            reasons.Add("story_event_not_active");
        else
        {
            if (!string.Equals(nativeEvent.id, pending.Request.StoryEventId, StringComparison.Ordinal))
                reasons.Add("story_event_id_mismatch");
            if (!string.Equals(Game1.currentLocation?.NameOrUniqueName, pending.Request.StoryEventLocationId, StringComparison.Ordinal))
                reasons.Add("story_event_location_mismatch");
            if (nativeEvent.CurrentCommand != pending.Request.StoryEventCommandIndex)
                reasons.Add("story_event_command_index_mismatch");
            if (!string.Equals(CurrentStoryEventCommand(nativeEvent), pending.Request.StoryEventCommandRaw, StringComparison.Ordinal))
                reasons.Add("story_event_command_raw_mismatch");
            if (nativeEvent.isFestival)
                reasons.Add("story_event_festival_owned_by_festival_executor");
            if (nativeEvent.skipped)
                reasons.Add("story_event_already_skipped");
            if (nativeEvent.playerControlSequence)
                reasons.Add("story_event_player_control_requires_exact_sequence_handler");
        }
        if (Game1.currentMinigame is not null)
            reasons.Add("story_event_minigame_requires_separate_action");

        if (string.Equals(pending.Request.StoryEventBoundaryKind, "dialogue_decision", StringComparison.Ordinal))
        {
            if (Game1.activeClickableMenu is not DialogueBox dialogue || !dialogue.isQuestion)
                reasons.Add("story_event_dialogue_decision_not_open");
            else if (!StoryEventResponseMatches(dialogue, pending.Request))
                reasons.Add("story_event_dialogue_response_mismatch");
        }
        else if (pending.Request.StoryEventResponseIndex.HasValue ||
                 !string.IsNullOrWhiteSpace(pending.Request.StoryEventResponseKey))
        {
            reasons.Add("story_event_response_supplied_without_decision_boundary");
        }

        if (reasons.Count > 0 || nativeEvent is null)
        {
            pending.Completion.SetResult(StoryEventBlocked(pending.Request, reasons.ToArray()));
            return;
        }
        if (activeStoryEvent is not null)
        {
            pending.Completion.SetResult(StoryEventBlocked(pending.Request, "story_event_executor_busy"));
            return;
        }

        activeStoryEvent = new ActiveStoryEvent(pending, nativeEvent);
    }

    private void TickStoryEventSafely()
    {
        if (activeStoryEvent is null)
            return;
        var active = activeStoryEvent;
        try
        {
            TickStoryEvent(active);
        }
        catch (Exception ex)
        {
            activeStoryEvent = null;
            active.Pending.Completion.SetResult(StoryEventBlocked(
                active.Pending.Request,
                "story_event_executor_exception:" + ex.GetType().Name));
        }
    }

    private void TickStoryEvent(ActiveStoryEvent active)
    {
        active.ElapsedTicks++;
        if (active.ElapsedTicks > 7200)
        {
            CompleteStoryEventBlocked(active, "story_event_runtime_timeout");
            return;
        }

        var currentEvent = Game1.CurrentEvent;
        if (currentEvent is null || !Game1.eventUp)
        {
            CompleteStoryEventApplied(active, "event_completed");
            return;
        }
        if (!ReferenceEquals(currentEvent, active.NativeEvent))
        {
            CompleteStoryEventBlocked(active, "story_event_instance_changed_without_fresh_snapshot");
            return;
        }
        if (currentEvent.isFestival)
        {
            CompleteStoryEventBlocked(active, "story_event_changed_to_festival");
            return;
        }
        if (Game1.currentMinigame is not null)
        {
            active.ProgressObserved |= currentEvent.CurrentCommand != active.InitialCommandIndex;
            CompleteStoryEventApplied(active, "event_minigame_boundary");
            return;
        }
        if (currentEvent.playerControlSequence)
        {
            active.ProgressObserved |= currentEvent.CurrentCommand != active.InitialCommandIndex;
            CompleteStoryEventApplied(active, "event_player_control_boundary");
            return;
        }

        ObserveStoryEventProgress(active, currentEvent);
        var menu = Game1.activeClickableMenu;
        if (menu is DialogueBox dialogue)
        {
            TickStoryEventDialogue(active, dialogue);
            return;
        }
        if (menu is NamingMenu namingMenu)
        {
            if (!string.Equals(ReadStoryEventCommandName(currentEvent), "animalNaming", StringComparison.Ordinal))
            {
                CompleteStoryEventBlocked(active, "story_event_unbound_naming_menu");
                return;
            }
            if (string.IsNullOrWhiteSpace(namingMenu.textBox.Text))
            {
                CompleteStoryEventBlocked(active, "story_event_native_default_name_missing");
                return;
            }
            var center = namingMenu.doneNamingButton.bounds.Center;
            namingMenu.performHoverAction(center.X, center.Y);
            namingMenu.receiveLeftClick(center.X, center.Y);
            active.MenuActions++;
            active.ProgressObserved = true;
            return;
        }
        if (menu is TutorialMenu or ItemListMenu)
        {
            if (menu.readyToClose())
            {
                menu.exitThisMenu();
                active.MenuActions++;
                active.ProgressObserved = true;
            }
            return;
        }
        if (menu is ReadyCheckDialog)
        {
            return;
        }
        if (menu is not null)
        {
            CompleteStoryEventBlocked(active, "story_event_unsupported_native_menu:" + menu.GetType().FullName);
        }
    }

    private void TickStoryEventDialogue(ActiveStoryEvent active, DialogueBox dialogue)
    {
        var request = active.Pending.Request;
        var questionKey = Game1.currentLocation?.lastQuestionKey ?? string.Empty;
        if (dialogue.isQuestion)
        {
            if (!string.Equals(request.StoryEventBoundaryKind, "dialogue_decision", StringComparison.Ordinal))
            {
                CompleteStoryEventApplied(active, "fresh_dialogue_decision_boundary");
                return;
            }
            if (active.BoundResponseConsumed)
            {
                if (active.NativeEvent.CurrentCommand != active.InitialCommandIndex ||
                    !string.Equals(questionKey, request.StoryEventQuestionKey, StringComparison.Ordinal))
                {
                    CompleteStoryEventApplied(active, "fresh_dialogue_decision_boundary");
                }
                return;
            }
            if (!string.Equals(questionKey, request.StoryEventQuestionKey, StringComparison.Ordinal) ||
                !StoryEventResponseMatches(dialogue, request))
            {
                CompleteStoryEventBlocked(active, "story_event_dialogue_response_changed_before_input");
                return;
            }
            if (dialogue.transitioning)
                return;
            if (dialogue.characterIndexInDialogue < dialogue.getCurrentString().Length - 1)
            {
                dialogue.receiveLeftClick(0, 0);
                active.DialogueClicks++;
                active.ProgressObserved = true;
                return;
            }
            if (dialogue.safetyTimer > 0)
                return;
            var index = request.StoryEventResponseIndex!.Value;
            var center = StoryEventResponseCenter(dialogue, index);
            dialogue.performHoverAction(center.X, center.Y);
            if (dialogue.selectedResponse != index)
            {
                CompleteStoryEventBlocked(active, "story_event_dialogue_hover_did_not_bind_response");
                return;
            }
            dialogue.receiveLeftClick(center.X, center.Y);
            active.DialogueClicks++;
            active.BoundResponseConsumed = true;
            active.ProgressObserved = true;
            return;
        }

        if (string.Equals(request.StoryEventBoundaryKind, "dialogue_decision", StringComparison.Ordinal) &&
            !active.BoundResponseConsumed)
        {
            CompleteStoryEventBlocked(active, "story_event_bound_decision_disappeared_before_input");
            return;
        }
        if (dialogue.transitioning || dialogue.safetyTimer > 0)
            return;
        if (active.ElapsedTicks % 12 != 0)
            return;
        dialogue.receiveLeftClick(0, 0);
        active.DialogueClicks++;
        active.ProgressObserved = true;
    }

    private static Point StoryEventResponseCenter(DialogueBox dialogue, int responseIndex)
    {
        if (dialogue.responseCC is not null && responseIndex < dialogue.responseCC.Count)
            return dialogue.responseCC[responseIndex].bounds.Center;

        var responseY = dialogue.y - (dialogue.heightForQuestions - dialogue.height) +
            SpriteText.getHeightOfString(dialogue.getCurrentString(), dialogue.width - 16) + 48;
        for (var index = 0; index < responseIndex; index++)
        {
            responseY += SpriteText.getHeightOfString(dialogue.responses[index].responseText, dialogue.width - 16) + 16;
        }
        var responseHeight = SpriteText.getHeightOfString(
            dialogue.responses[responseIndex].responseText,
            dialogue.width - 16);
        return new Point(dialogue.x + dialogue.width / 2, responseY + responseHeight / 2);
    }

    private static bool StoryEventResponseMatches(DialogueBox dialogue, TrainingExecutionRequest request)
    {
        if (request.StoryEventResponseIndex is not { } index || index < 0 ||
            dialogue.responses is null || index >= dialogue.responses.Length)
            return false;
        var response = dialogue.responses[index];
        return string.Equals(response.responseKey, request.StoryEventResponseKey, StringComparison.Ordinal) &&
            string.Equals(Game1.currentLocation?.lastQuestionKey, request.StoryEventQuestionKey, StringComparison.Ordinal);
    }

    private static void ObserveStoryEventProgress(ActiveStoryEvent active, Event currentEvent)
    {
        var menuType = Game1.activeClickableMenu?.GetType().FullName ?? string.Empty;
        var questionKey = Game1.currentLocation?.lastQuestionKey ?? string.Empty;
        var changed = currentEvent.CurrentCommand != active.LastCommandIndex ||
            !string.Equals(menuType, active.LastMenuType, StringComparison.Ordinal) ||
            !string.Equals(questionKey, active.LastQuestionKey, StringComparison.Ordinal);
        if (changed)
        {
            active.ProgressObserved = true;
            active.StalledTicks = 0;
            active.LastCommandIndex = currentEvent.CurrentCommand;
            active.LastMenuType = menuType;
            active.LastQuestionKey = questionKey;
        }
        else
        {
            active.StalledTicks++;
        }
    }

    private void CompleteStoryEventApplied(ActiveStoryEvent active, string boundary)
    {
        activeStoryEvent = null;
        active.Pending.Completion.SetResult(StoryEventResult(active, "applied", "verified", boundary));
    }

    private void CompleteStoryEventBlocked(ActiveStoryEvent active, string reason)
    {
        activeStoryEvent = null;
        active.Pending.Completion.SetResult(StoryEventResult(active, "blocked", "blocked", reason));
    }

    private static TrainingExecutionResult StoryEventResult(
        ActiveStoryEvent active,
        string status,
        string verification,
        string boundary)
    {
        var currentEvent = Game1.CurrentEvent;
        var afterEventId = currentEvent?.id ?? string.Empty;
        var afterCommand = currentEvent?.CurrentCommand ?? -1;
        var afterSeen = Game1.player.eventsSeen?.Contains(active.NativeEvent.id) == true;
        var reasons = status == "applied"
            ? new[] { "native_event_input_only", "boundary=" + boundary, "skipEvent_not_used" }
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
            PrimitiveKind = "advance_story_event",
            PrimitiveVerificationStatus = verification,
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "native_event_progressed_to_end_or_next_fresh_boundary=true",
            ObservedEffect = "boundary=" + boundary + ";event_id=" + (string.IsNullOrWhiteSpace(afterEventId) ? "none" : afterEventId) +
                ";command_index=" + afterCommand + ";dialogue_clicks=" + active.DialogueClicks +
                ";menu_actions=" + active.MenuActions + ";progress_observed=" + active.ProgressObserved.ToString().ToLowerInvariant(),
            BlockReasons = status == "blocked" ? reasons : Array.Empty<string>(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.story_event.event_id", Before = active.NativeEvent.id, After = afterEventId },
                new SimulatedFactChange { Path = "player.story_event.current_command_index", Before = active.InitialCommandIndex.ToString(), After = afterCommand.ToString() },
                new SimulatedFactChange { Path = "player.story_event.event_seen", Before = active.InitialEventSeen.ToString().ToLowerInvariant(), After = afterSeen.ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "player.location_id", Before = active.InitialLocationId, After = Game1.currentLocation?.NameOrUniqueName ?? string.Empty }
            }
        };
    }

    private static TrainingExecutionResult StoryEventBlocked(TrainingExecutionRequest request, params string[] reasons) =>
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
            PrimitiveKind = "advance_story_event",
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "native_event_progressed_to_end_or_next_fresh_boundary=true",
            ObservedEffect = "event_id=" + (Game1.CurrentEvent?.id ?? "none") + ";command_index=" + (Game1.CurrentEvent?.CurrentCommand ?? -1),
            BlockReasons = reasons
        };

    private static string CurrentStoryEventCommand(Event nativeEvent) =>
        nativeEvent.CurrentCommand >= 0 && nativeEvent.CurrentCommand < nativeEvent.eventCommands.Length
            ? nativeEvent.eventCommands[nativeEvent.CurrentCommand] ?? string.Empty
            : string.Empty;

    private static string ReadStoryEventCommandName(Event nativeEvent)
    {
        var raw = CurrentStoryEventCommand(nativeEvent);
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var args = ArgUtility.SplitBySpaceQuoteAware(raw);
        return args.Length == 0 ? string.Empty : args[0];
    }
}
