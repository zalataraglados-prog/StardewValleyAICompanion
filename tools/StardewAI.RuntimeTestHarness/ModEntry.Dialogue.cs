using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private void StartDialogueAdvance(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(pending.Request, reasons.ToArray()));
            return;
        }

        var menu = Game1.activeClickableMenu;
        if (menu is ShippingMenu shippingMenu)
        {
            StartShippingSummaryClose(pending, shippingMenu);
            return;
        }

        if (menu is not DialogueBox dialogueBox ||
            !CanAdvanceOrdinaryDialogue(dialogueBox, pending.Request.SocialContinuationDialogueRecovery))
        {
            pending.Completion.SetResult(ExecuteCloseMenu(pending.Request));
            return;
        }

        if (activeDialogueAdvance is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request, "close_menu",
                "menus.active_menu.is_open=false",
                CloseMenuObservedEffect(),
                "dialogue_advance_executor_busy"));
            return;
        }

        activeDialogueAdvance = new ActiveDialogueAdvance(pending, dialogueBox);
        Monitor.Log($"Started native dialogue advance: isQuestion={dialogueBox.isQuestion}, responses={dialogueBox.responses?.Length ?? 0}, transitioning={dialogueBox.transitioning}, safetyTimer={dialogueBox.safetyTimer}, eventUp={Game1.eventUp}, speaker={dialogueBox.characterDialogue?.speaker?.Name ?? "none"}", LogLevel.Info);
    }

    private void TickDialogueAdvance()
    {
        if (activeDialogueAdvance is null)
        {
            return;
        }

        var advance = activeDialogueAdvance;
        advance.ElapsedTicks++;

        try
        {
            TickDialogueAdvanceCore(advance);
        }
        catch (Exception ex)
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance, "blocked", "blocked", "dialogue_advance_exception:" + ex.GetType().Name,
                new[] { "dialogue_advance_exception:" + ex.GetType().Name + ":" + ex.Message }));
        }
    }

    private void TickDialogueAdvanceCore(ActiveDialogueAdvance advance)
    {
        if (advance.ElapsedTicks > advance.MaxTicks)
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance, "blocked", "blocked", "dialogue_advance_timeout",
                new[] { "dialogue_advance_timeout" }));
            return;
        }

        var currentBox = Game1.activeClickableMenu as DialogueBox;

        if (!ReferenceEquals(currentBox, advance.InitialMenu))
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            var verified = Game1.activeClickableMenu is null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance,
                verified ? "applied" : "blocked",
                verified ? "verified" : "observed_mismatch",
                verified ? "dialogue_advanced_and_closed_natively" : "dialogue_menu_instance_changed_during_advance",
                verified
                    ? new[] { "dialogue_advanced_and_closed_natively", "press_attempts=" + advance.PressAttempts, "advance_ticks=" + advance.ElapsedTicks }
                    : new[] { "dialogue_menu_instance_changed_during_advance", "type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") }));
            return;
        }

        var currentSpeakerName = currentBox.characterDialogue?.speaker?.Name ?? string.Empty;
        if (!string.Equals(currentSpeakerName, advance.InitialSpeakerName, StringComparison.Ordinal))
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance, "blocked", "blocked", "dialogue_speaker_changed_during_advance",
                new[] { "dialogue_speaker_changed_during_advance:expected=" + advance.InitialSpeakerName + ";actual=" + currentSpeakerName }));
            return;
        }

        if (!CanAdvanceOrdinaryDialogue(currentBox, advance.Pending.Request.SocialContinuationDialogueRecovery))
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance, "blocked", "blocked", "dialogue_became_unsafe_during_advance",
                new[] { "dialogue_became_unsafe_during_advance:isQuestion=" + currentBox.isQuestion + ";responses=" + (currentBox.responses?.Length ?? 0) + ";lastQuestionKey=" + (Game1.currentLocation?.lastQuestionKey ?? "null") + ";eventUp=" + Game1.eventUp }));
            return;
        }

        switch (advance.Stage)
        {
            case DialogueAdvanceStage.WaitTransition:
                if (currentBox.transitioning || currentBox.safetyTimer > 0)
                {
                    advance.TransitionWaitTicks++;
                    return;
                }

                advance.Stage = DialogueAdvanceStage.Press;
                break;

            case DialogueAdvanceStage.Press:
                if (!TryApplySmapiLeftButtonOverride(pressed: true, out var pressReason))
                {
                    ReleaseSmapiLeftButtonOverride();
                    activeDialogueAdvance = null;
                    advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                        advance, "blocked", "blocked", "dialogue_advance_input_press_failed",
                        new[] { "dialogue_advance_input_press_failed:" + pressReason }));
                    return;
                }

                advance.PressAttempts++;
                advance.SawDialogueFinishedBeforePress = currentBox.dialogueFinished;
                advance.SawShowTypingBeforePress = currentBox.showTyping;
                advance.SawTransitioningBeforePress = currentBox.transitioning;
                advance.Stage = DialogueAdvanceStage.ReleaseAfterAdvance;
                break;

            case DialogueAdvanceStage.ReleaseAfterAdvance:
                if (!TryApplySmapiLeftButtonOverride(pressed: false, out var releaseReason))
                {
                    ReleaseSmapiLeftButtonOverride();
                    activeDialogueAdvance = null;
                    advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                        advance, "blocked", "blocked", "dialogue_advance_input_release_failed",
                        new[] { "dialogue_advance_input_release_failed:" + releaseReason }));
                    return;
                }

                advance.AdvanceWaitTicks = 0;
                advance.Stage = DialogueAdvanceStage.WaitAdvanceEffect;
                break;

            case DialogueAdvanceStage.WaitAdvanceEffect:
                advance.AdvanceWaitTicks++;
                var dialogueChanged = currentBox.dialogueFinished != advance.SawDialogueFinishedBeforePress ||
                    currentBox.showTyping != advance.SawShowTypingBeforePress ||
                    currentBox.transitioning != advance.SawTransitioningBeforePress;
                if (dialogueChanged || advance.AdvanceWaitTicks > 30)
                {
                    advance.Stage = DialogueAdvanceStage.CheckClose;
                }

                break;

            case DialogueAdvanceStage.CheckClose:
                advance.CheckCloseTicks++;
                if (advance.PressAttempts >= advance.MaxPressAttempts)
                {
                    ReleaseSmapiLeftButtonOverride();
                    activeDialogueAdvance = null;
                    advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                        advance, "blocked", "blocked", "dialogue_advance_max_press_exhausted",
                        new[] { "dialogue_advance_max_press_exhausted:" + advance.PressAttempts }));
                    return;
                }

                if (currentBox.transitioning || currentBox.safetyTimer > 0)
                {
                    advance.Stage = DialogueAdvanceStage.WaitTransition;
                    return;
                }

                advance.Stage = DialogueAdvanceStage.Press;
                break;
        }
    }

    private static TrainingExecutionResult DialogueAdvanceResult(
        ActiveDialogueAdvance advance,
        string status,
        string verificationStatus,
        string primaryReason,
        string[] allReasons)
    {
        var observedMenu = Game1.activeClickableMenu;
        var observedType = observedMenu?.GetType().Name ?? "none";
        var observedBox = observedMenu as DialogueBox;
        return new TrainingExecutionResult
        {
            RunId = advance.Pending.Request.RunId,
            QueueId = advance.Pending.Request.QueueId,
            QueueItemId = advance.Pending.Request.QueueItemId,
            BeforeStateHash = advance.Pending.Request.BeforeStateHash,
            OptionId = advance.Pending.Request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = advance.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = allReasons,
            RequestedEffect = "menus.active_menu.is_open=false",
            ObservedEffect = CloseMenuObservedEffect() + ";dialogue_press_attempts=" + advance.PressAttempts + ";advance_ticks=" + advance.ElapsedTicks,
            BlockReasons = status == "blocked" ? allReasons : Array.Empty<string>(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = "true", After = (observedMenu is not null).ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "menus.active_menu.type", Before = advance.BeforeMenuType, After = observedType }
            },
            DialogueNativeHandled = true,
            DialoguePressAttempts = advance.PressAttempts,
            DialogueAdvanceTicks = advance.ElapsedTicks,
            DialogueMenuTypeBefore = advance.BeforeMenuType,
            DialogueMenuTypeAfter = observedType,
            DialogueIsQuestionBefore = advance.BeforeIsQuestion,
            DialogueIsQuestionAfter = observedBox?.isQuestion,
            DialogueResponseCountBefore = advance.BeforeResponseCount,
            DialogueResponseCountAfter = observedBox?.responses?.Length,
            DialogueSpeakerNameBefore = advance.BeforeSpeakerName,
            DialogueSpeakerNameAfter = observedBox?.characterDialogue?.speaker?.Name ?? string.Empty,
            DialogueEventUpBefore = advance.BeforeEventUp,
            DialogueEventUpAfter = Game1.eventUp
        };
    }

    private static TrainingExecutionResult CompletedCloseMenu(TrainingExecutionRequest request, bool beforeOpen, string beforeType, string status, string verificationStatus, string[] verificationReasons)
    {
        var observedOpen = Game1.activeClickableMenu is not null;
        var observedType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = "menus.active_menu.is_open=false",
            ObservedEffect = CloseMenuObservedEffect(),
            BlockReasons = status == "blocked" ? verificationReasons : Array.Empty<string>(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "menus.active_menu.is_open",
                    Before = beforeOpen.ToString().ToLowerInvariant(),
                    After = observedOpen.ToString().ToLowerInvariant()
                },
                new SimulatedFactChange
                {
                    Path = "menus.active_menu.type",
                    Before = beforeType,
                    After = observedType
                }
            }
        };
    }

    private static string CloseMenuObservedEffect()
    {
        return "menus.active_menu.is_open=" + (Game1.activeClickableMenu is not null).ToString().ToLowerInvariant() + ";menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
    }
}
