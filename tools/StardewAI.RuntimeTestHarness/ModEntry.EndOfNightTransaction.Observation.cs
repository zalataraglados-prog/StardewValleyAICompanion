using System.Runtime.CompilerServices;
using StardewAI.RuntimePrimitives;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void ObserveEndOfNightTransaction(ActiveSleep? sleep = null)
    {
        lock (endOfNightTransactionGate)
        {
            var transaction = endOfNightTransaction;
            if (transaction is null ||
                transaction.Status is "complete" or "blocked")
            {
                return;
            }

            if (transaction.SleepConfirmed)
            {
                transaction.OverallTicks++;
            }
            var transitionKey = CurrentEndOfNightTransitionKey(sleep);
            var evidenceChanged = false;
            if (!string.Equals(
                    transitionKey,
                    transaction.LastTransitionKey,
                    StringComparison.Ordinal))
            {
                transaction.TransitionTicks = 0;
                RecordEndOfNightTransitionLocked(transaction, transitionKey);
                evidenceChanged = true;
            }
            else if (transaction.SleepConfirmed)
            {
                transaction.TransitionTicks++;
            }

            evidenceChanged |= SetObserved(
                ref transaction.SawFarmEvent,
                Game1.farmEvent is not null);
            evidenceChanged |= SetObserved(
                ref transaction.SawStoryEvent,
                Game1.eventUp && Game1.CurrentEvent is not null);
            evidenceChanged |= SetObserved(
                ref transaction.SawShippingSummary,
                Game1.activeClickableMenu is ShippingMenu);
            evidenceChanged |= SetObserved(
                ref transaction.SawSaveMenu,
                Game1.activeClickableMenu is SaveGameMenu);
            evidenceChanged |= SetObserved(
                ref transaction.SawExpectedNewDay,
                Game1.Date.TotalDays == transaction.ExpectedTotalDays);
            if (Game1.Date.TotalDays == transaction.ExpectedTotalDays &&
                Game1.newDaySync.hasInstance() &&
                Game1.newDaySync.hasSaved() &&
                !Game1.game1.IsSaving &&
                !SaveGame.IsProcessing)
            {
                var newlyObservedSave = SetObserved(
                    ref transaction.SawNativeSaveCommit,
                    observed: true);
                evidenceChanged |= newlyObservedSave;
                if (newlyObservedSave)
                {
                    transaction.NativeSaveCommittedAt =
                        DateTimeOffset.UtcNow.ToString("O");
                }
            }

            if (transaction.SleepConfirmed &&
                transaction.OverallTicks >
                EndOfNightOverallTickBudget(transaction))
            {
                BlockEndOfNightTransactionLocked(
                    transaction,
                    "end_of_night_transaction_overall_timeout");
                return;
            }

            var transitionBudget =
                EndOfNightTransitionTickBudget(transaction, transitionKey);
            if (transaction.SleepConfirmed &&
                transaction.TransitionTicks > transitionBudget)
            {
                BlockEndOfNightTransactionLocked(
                    transaction,
                    "end_of_night_transition_timeout:" + transitionKey);
                return;
            }

            if (EndOfNightReachedSafeNewDayControl(transaction))
            {
                evidenceChanged |= SetObserved(
                    ref transaction.SawSafeNewDayReadyState,
                    observed: true);
                if (!string.Equals(
                        transaction.Status,
                        "ready_for_safe_next_day_action",
                        StringComparison.Ordinal))
                {
                    CaptureEndOfNightWorldStateLocked(transaction);
                    transaction.Status = "ready_for_safe_next_day_action";
                    evidenceChanged = true;
                }
            }

            evidenceChanged |= TryInjectEndOfNightNegativeFixture(
                transaction,
                sleep);

            if (evidenceChanged)
            {
                PersistEndOfNightTransactionLocked(transaction);
            }
        }
    }

    private static int EndOfNightOverallTickBudget(
        EndOfNightTransaction transaction) =>
        transaction.DebugNegativeFixtureInjected &&
        string.Equals(
            transaction.DebugNegativeProfile,
            EndOfNightOverallTimeoutProfile,
            StringComparison.Ordinal)
            ? EndOfNightNegativeFixtureTimeoutTicks
            : EndOfNightTransactionPolicy.MaximumOverallTicks;

    private static int EndOfNightTransitionTickBudget(
        EndOfNightTransaction transaction,
        string transitionKey) =>
        transaction.DebugNegativeFixtureInjected &&
        string.Equals(
            transaction.DebugNegativeProfile,
            EndOfNightUnknownMenuTransitionTimeoutProfile,
            StringComparison.Ordinal)
            ? EndOfNightNegativeFixtureTimeoutTicks
            : EndOfNightTransactionPolicy.TransitionTickBudget(transitionKey);

    private static bool TryInjectEndOfNightNegativeFixture(
        EndOfNightTransaction transaction,
        ActiveSleep? sleep)
    {
        if (sleep is not null ||
            transaction.DebugNegativeFixtureInjected ||
            !string.Equals(
                transaction.Status,
                "ready_for_safe_next_day_action",
                StringComparison.Ordinal) ||
            (transaction.DebugNegativeProfile !=
                 EndOfNightUnknownMenuTransitionTimeoutProfile &&
             transaction.DebugNegativeProfile !=
                 EndOfNightOverallTimeoutProfile) ||
            !EndOfNightReachedSafeNewDayControl(transaction))
        {
            return false;
        }

        // Locked 1.6.15 Game1 input handling opens the quest log with this
        // exact native constructor after the same safe-control predicates.
        Game1.activeClickableMenu = new QuestLog();
        transaction.DebugNegativeFixtureInjected = true;
        transaction.OverallTicks = 0;
        transaction.TransitionTicks = 0;
        transaction.Status = "debug_negative_fixture_pending";
        RecordEndOfNightTransitionLocked(
            transaction,
            CurrentEndOfNightTransitionKey(sleep: null));
        return true;
    }

    private static bool TryObserveSleepTransitionBudget(
        ActiveSleep sleep,
        out string reason)
    {
        var transitionKey = CurrentEndOfNightTransitionKey(sleep);
        if (!string.Equals(
                transitionKey,
                sleep.LastTransitionKey,
                StringComparison.Ordinal))
        {
            sleep.LastTransitionKey = transitionKey;
            sleep.TransitionTicks = 0;
        }
        else
        {
            sleep.TransitionTicks++;
        }

        if (sleep.ElapsedTicks > sleep.MaxOverallTicks)
        {
            reason = "sleep_macro_overall_timeout";
            return false;
        }

        if (sleep.TransitionTicks >
            EndOfNightTransactionPolicy.TransitionTickBudget(transitionKey))
        {
            reason = "sleep_transition_timeout:" + transitionKey;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool EndOfNightReachedSafeNewDayControl(
        EndOfNightTransaction transaction)
    {
        return Context.IsWorldReady &&
            Game1.Date.TotalDays == transaction.ExpectedTotalDays &&
            transaction.SawNativeSaveCommit &&
            !Game1.game1.IsSaving &&
            !SaveGame.IsProcessing &&
            Game1.farmEvent is null &&
            !Game1.eventUp &&
            Game1.CurrentEvent is null &&
            Game1.currentMinigame is null &&
            Game1.activeClickableMenu is null &&
            !Game1.dialogueUp &&
            Game1.timeOfDay >= 600 &&
            Game1.timeOfDay < 700 &&
            Game1.player.CanMove;
    }

    private static string CurrentEndOfNightTransitionKey(ActiveSleep? sleep)
    {
        if (Game1.currentMinigame is not null)
        {
            return "minigame:" + Game1.currentMinigame.GetType().Name;
        }
        if (Game1.farmEvent is not null)
        {
            return "farm_event:" + Game1.farmEvent.GetType().Name;
        }
        if (Game1.eventUp && Game1.CurrentEvent is not null)
        {
            return "story_event:" + Game1.CurrentEvent.GetType().Name;
        }
        if (Game1.activeClickableMenu is { } menu)
        {
            var suffix = menu is LevelUpMenu levelUpMenu
                ? levelUpMenu.isProfessionChooser
                    ? ":profession"
                    : ":ordinary"
                : string.Empty;
            return "menu:" + menu.GetType().Name + suffix + ":" +
                RuntimeHelpers.GetHashCode(menu);
        }
        if (Game1.game1.IsSaving || SaveGame.IsProcessing)
        {
            return "native_save_processing";
        }
        return sleep is null
            ? "controller_handoff"
            : "sleep_stage:" + sleep.Stage;
    }

    private static void RecordEndOfNightTransitionLocked(
        EndOfNightTransaction transaction,
        string key)
    {
        transaction.LastTransitionKey = key;
        transaction.Transitions.Add(new EndOfNightTransition
        {
            Ordinal = transaction.Transitions.Count + 1,
            ObservedAt = DateTimeOffset.UtcNow.ToString("O"),
            Key = key,
            TotalDays = Context.IsWorldReady ? Game1.Date.TotalDays : -1,
            TimeOfDay = Context.IsWorldReady ? Game1.timeOfDay : -1,
            MenuType = Game1.activeClickableMenu?.GetType().Name ?? string.Empty,
            IsProfessionChooser =
                Game1.activeClickableMenu is LevelUpMenu levelUpMenu &&
                levelUpMenu.isProfessionChooser,
            FarmEventType = Game1.farmEvent?.GetType().Name ?? string.Empty,
            StoryEventType = Game1.CurrentEvent?.GetType().Name ?? string.Empty,
            SaveInProgress = Game1.game1.IsSaving || SaveGame.IsProcessing
        });
    }
}
