using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string EndOfNightUnknownMenuTransitionTimeoutProfile =
        "unknown_menu_transition_timeout";
    private const string EndOfNightOverallTimeoutProfile = "overall_timeout";
    private const int EndOfNightNegativeFixtureTimeoutTicks = 120;

    private readonly object endOfNightTransactionGate = new();
    private EndOfNightTransaction? endOfNightTransaction;
    private string pendingEndOfNightNegativeProfile = string.Empty;
    private string pendingEndOfNightNegativeProfileRunId = string.Empty;

    private sealed class EndOfNightTransaction
    {
        public string TransactionId { get; init; } = string.Empty;
        public string RunId { get; init; } = string.Empty;
        public string QueueId { get; init; } = string.Empty;
        public string SleepQueueItemId { get; init; } = string.Empty;
        public string StartedAt { get; init; } = string.Empty;
        public int StartTotalDays { get; init; }
        public int ExpectedTotalDays { get; init; }
        public int StartYear { get; init; }
        public string StartSeason { get; init; } = string.Empty;
        public int StartDay { get; init; }
        public int StartTime { get; init; }
        public string Status { get; set; } = "sleep_pending";
        public string EvidencePath { get; set; } = string.Empty;
        public string NativeSaveCommittedAt { get; set; } = string.Empty;
        public string CompletedAt { get; set; } = string.Empty;
        public int? CompletedTotalDays { get; set; }
        public int? CompletedYear { get; set; }
        public string CompletedSeason { get; set; } = string.Empty;
        public int? CompletedDay { get; set; }
        public int? CompletedTime { get; set; }
        public string LastTransitionKey { get; set; } = string.Empty;
        public int TransitionTicks { get; set; }
        public int OverallTicks { get; set; }
        public bool SleepConfirmed;
        public bool YieldedForPlannedProfession;
        public bool SawFarmEvent;
        public bool SawStoryEvent;
        public bool SawShippingSummary;
        public bool SawSaveMenu;
        public bool SawNativeSaveCommit;
        public bool SawExpectedNewDay;
        public bool SawSafeNewDayReadyState;
        public bool TerminalResultAttached;
        public string DebugNegativeProfile { get; init; } = string.Empty;
        public bool DebugNegativeFixtureInjected;
        public string SafeNextDayPlanningOptionId { get; set; } = string.Empty;
        public string SafeNextDayOptionId { get; set; } = string.Empty;
        public string SafeNextDayQueueId { get; set; } = string.Empty;
        public string SafeNextDayQueueItemId { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public List<EndOfNightTransition> Transitions { get; } = new();
    }

    private sealed class EndOfNightTransition
    {
        public int Ordinal { get; init; }
        public string ObservedAt { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public int TotalDays { get; init; }
        public int TimeOfDay { get; init; }
        public string MenuType { get; init; } = string.Empty;
        public bool IsProfessionChooser { get; init; }
        public string FarmEventType { get; init; } = string.Empty;
        public string StoryEventType { get; init; } = string.Empty;
        public bool SaveInProgress { get; init; }
    }

    private void BeginEndOfNightTransaction(ActiveSleep sleep)
    {
        lock (endOfNightTransactionGate)
        {
            if (endOfNightTransaction is { Status: not "complete" and not "blocked" })
            {
                throw new InvalidOperationException(
                    "end_of_night_transaction_already_active:" +
                    endOfNightTransaction.TransactionId + ":" +
                    endOfNightTransaction.Status);
            }

            var request = sleep.Pending.Request;
            var debugNegativeProfile = string.Equals(
                pendingEndOfNightNegativeProfileRunId,
                request.RunId,
                StringComparison.Ordinal)
                ? pendingEndOfNightNegativeProfile
                : string.Empty;
            pendingEndOfNightNegativeProfile = string.Empty;
            pendingEndOfNightNegativeProfileRunId = string.Empty;
            endOfNightTransaction = new EndOfNightTransaction
            {
                TransactionId = request.RunId + ".night." +
                    Guid.NewGuid().ToString("N"),
                RunId = request.RunId,
                QueueId = request.QueueId,
                SleepQueueItemId = request.QueueItemId,
                StartedAt = DateTimeOffset.UtcNow.ToString("O"),
                StartTotalDays = sleep.StartTotalDays,
                ExpectedTotalDays = sleep.StartTotalDays + 1,
                StartYear = sleep.StartYear,
                StartSeason = sleep.StartSeason,
                StartDay = sleep.StartDay,
                StartTime = sleep.StartTime,
                DebugNegativeProfile = debugNegativeProfile
            };
            RecordEndOfNightTransitionLocked(
                endOfNightTransaction,
                CurrentEndOfNightTransitionKey(sleep));
            PersistEndOfNightTransactionLocked(endOfNightTransaction);
        }
    }

    private bool HasIncompleteEndOfNightTransaction(out string reason)
    {
        lock (endOfNightTransactionGate)
        {
            if (endOfNightTransaction is not { Status: not "complete" and not "blocked" } transaction)
            {
                reason = string.Empty;
                return false;
            }

            reason = "end_of_night_transaction_incomplete:" +
                transaction.TransactionId + ":" + transaction.Status;
            return true;
        }
    }

    private void MarkEndOfNightSleepConfirmed()
    {
        lock (endOfNightTransactionGate)
        {
            if (endOfNightTransaction is not { } transaction)
            {
                return;
            }

            transaction.SleepConfirmed = true;
            transaction.OverallTicks = 0;
            transaction.TransitionTicks = 0;
            transaction.Status = "native_outcomes_pending";
            PersistEndOfNightTransactionLocked(transaction);
        }
    }

    private void YieldEndOfNightForPlannedProfession()
    {
        lock (endOfNightTransactionGate)
        {
            if (endOfNightTransaction is not { } transaction)
            {
                return;
            }

            transaction.YieldedForPlannedProfession = true;
            transaction.Status = "awaiting_planned_profession_selection";
            PersistEndOfNightTransactionLocked(transaction);
        }
    }

    private void ObserveNativeSaveCommitForEndOfNight()
    {
        lock (endOfNightTransactionGate)
        {
            if (endOfNightTransaction is not { } transaction ||
                Game1.Date.TotalDays != transaction.ExpectedTotalDays)
            {
                return;
            }

            if (!transaction.SawNativeSaveCommit)
            {
                transaction.SawNativeSaveCommit = true;
                transaction.NativeSaveCommittedAt =
                    DateTimeOffset.UtcNow.ToString("O");
            }
            PersistEndOfNightTransactionLocked(transaction);
        }
    }

    private void MarkEndOfNightBlocked(string reason)
    {
        lock (endOfNightTransactionGate)
        {
            if (endOfNightTransaction is not { } transaction)
            {
                return;
            }

            BlockEndOfNightTransactionLocked(transaction, reason);
        }
    }

    private TrainingExecutionResult AttachEndOfNightTransaction(
        TrainingExecutionResult result)
    {
        lock (endOfNightTransactionGate)
        {
            if (endOfNightTransaction is not { } transaction)
            {
                return result;
            }
            var isTerminal = transaction.Status is "complete" or "blocked";
            if (isTerminal && transaction.TerminalResultAttached)
            {
                return result;
            }

            result.EndOfNightTransactionId = transaction.TransactionId;
            result.EndOfNightTransactionStatus = transaction.Status;
            result.EndOfNightTransactionEvidencePath = transaction.EvidencePath;
            if (isTerminal)
            {
                transaction.TerminalResultAttached = true;
            }
            return result;
        }
    }

    private void ObserveCompletedExecutionForEndOfNight(
        TrainingExecutionResult result)
    {
        lock (endOfNightTransactionGate)
        {
            var transaction = endOfNightTransaction;
            if (transaction is not { Status: "ready_for_safe_next_day_action" } ||
                !string.Equals(
                    result.RunId,
                    transaction.RunId,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(result.QueueId) ||
                string.IsNullOrWhiteSpace(result.QueueItemId) ||
                !string.Equals(result.Status, "applied", StringComparison.Ordinal) ||
                !string.Equals(
                    result.PrimitiveVerificationStatus,
                    "verified",
                    StringComparison.Ordinal) ||
                !IsRegisteredSafeNextDayWorkResult(result))
            {
                return;
            }

            transaction.SafeNextDayPlanningOptionId =
                result.SourcePlanningOptionId;
            transaction.SafeNextDayOptionId = result.OptionId;
            transaction.SafeNextDayQueueId = result.QueueId;
            transaction.SafeNextDayQueueItemId = result.QueueItemId;
            transaction.Status = "complete";
            transaction.CompletedAt = DateTimeOffset.UtcNow.ToString("O");
            RecordEndOfNightSafeActionLocked(
                transaction,
                result.SourcePlanningOptionId,
                result.OptionId);
            PersistEndOfNightTransactionLocked(transaction);
        }
    }

    private static bool IsRegisteredSafeNextDayWorkResult(
        TrainingExecutionResult result)
    {
        if (!EndOfNightTransactionPolicy.IsSafeNextDayPlanningOption(
                result.SourcePlanningOptionId) ||
            !OptionCapabilityRegistrySource.TryGet(
                result.SourcePlanningOptionId,
                out var planningCapability) ||
            planningCapability.RegistrationStatus !=
                CapabilityRegistrationStatus.Registered ||
            (planningCapability.CandidateStatus !=
                 CapabilityCandidateStatus.Declared &&
             planningCapability.CandidateStatus !=
                 CapabilityCandidateStatus.PartiallyBlocked) ||
            planningCapability.PlayerConfirmationRequired ||
            planningCapability.InvocationPolicy !=
                OptionInvocationPolicy.PolicyOrAutonomous)
        {
            return false;
        }

        return EndOfNightTransactionPolicy.IsSafeNextDayExecutionOption(
                result.OptionId) &&
            ProductExecutorCapabilityCatalog.IsSupported(result.OptionId);
    }

    private object ReadEndOfNightTransactionSnapshot()
    {
        lock (endOfNightTransactionGate)
        {
            if (endOfNightTransaction is null)
            {
                return new
                {
                    schema_version = "end_of_night_transaction.v1",
                    status = "none"
                };
            }

            var transaction = endOfNightTransaction;
            return new
            {
                schema_version = "end_of_night_transaction.v1",
                transaction_id = transaction.TransactionId,
                run_id = transaction.RunId,
                queue_id = transaction.QueueId,
                sleep_queue_item_id = transaction.SleepQueueItemId,
                started_at = transaction.StartedAt,
                start_total_days = transaction.StartTotalDays,
                expected_total_days = transaction.ExpectedTotalDays,
                start_year = transaction.StartYear,
                start_season = transaction.StartSeason,
                start_day = transaction.StartDay,
                start_time = transaction.StartTime,
                status = transaction.Status,
                evidence_path = transaction.EvidencePath,
                native_save_committed_at =
                    transaction.NativeSaveCommittedAt,
                completed_at = transaction.CompletedAt,
                completed_total_days = transaction.CompletedTotalDays,
                completed_year = transaction.CompletedYear,
                completed_season = transaction.CompletedSeason,
                completed_day = transaction.CompletedDay,
                completed_time = transaction.CompletedTime,
                sleep_confirmed = transaction.SleepConfirmed,
                yielded_for_planned_profession =
                    transaction.YieldedForPlannedProfession,
                saw_farm_event = transaction.SawFarmEvent,
                saw_story_event = transaction.SawStoryEvent,
                saw_shipping_summary = transaction.SawShippingSummary,
                saw_save_menu = transaction.SawSaveMenu,
                saw_native_save_commit = transaction.SawNativeSaveCommit,
                saw_expected_new_day = transaction.SawExpectedNewDay,
                saw_safe_new_day_ready_state =
                    transaction.SawSafeNewDayReadyState,
                debug_negative_profile = transaction.DebugNegativeProfile,
                debug_negative_fixture_injected =
                    transaction.DebugNegativeFixtureInjected,
                safe_next_day_planning_option_id =
                    transaction.SafeNextDayPlanningOptionId,
                safe_next_day_option_id = transaction.SafeNextDayOptionId,
                safe_next_day_queue_id = transaction.SafeNextDayQueueId,
                safe_next_day_queue_item_id =
                    transaction.SafeNextDayQueueItemId,
                failure_reason = transaction.FailureReason,
                last_transition_key = transaction.LastTransitionKey,
                transition_ticks = transaction.TransitionTicks,
                overall_ticks = transaction.OverallTicks,
                transitions = transaction.Transitions.ToArray()
            };
        }
    }

    private static bool SetObserved(ref bool field, bool observed)
    {
        if (!observed || field)
        {
            return false;
        }

        field = true;
        return true;
    }

    private void BlockEndOfNightTransactionLocked(
        EndOfNightTransaction transaction,
        string reason)
    {
        transaction.Status = "blocked";
        transaction.FailureReason = reason;
        CaptureEndOfNightTerminalStateLocked(transaction);
        PersistEndOfNightTransactionLocked(transaction);
        Monitor.Log(
            "End-of-night transaction blocked: " + reason,
            LogLevel.Error);
    }

    private void PersistEndOfNightTransactionLocked(
        EndOfNightTransaction transaction)
    {
        var trainingDir =
            Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(trainingDir))
        {
            return;
        }

        try
        {
            var directory = Path.Combine(
                Path.GetFullPath(trainingDir),
                "end-of-night-transactions");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(
                directory,
                SanitizeFileName(transaction.TransactionId) + ".json");
            transaction.EvidencePath = path;
            var tempPath = path + ".tmp";
            File.WriteAllText(
                tempPath,
                JsonSerializer.Serialize(
                    ReadEndOfNightTransactionSnapshot(),
                    JsonOptions),
                System.Text.Encoding.UTF8);
            File.Move(tempPath, path, true);
        }
        catch (Exception ex)
        {
            Monitor.Log(
                "Could not persist end-of-night transaction evidence: " +
                ex.Message,
                LogLevel.Error);
        }
    }

    private static void CaptureEndOfNightTerminalStateLocked(
        EndOfNightTransaction transaction)
    {
        transaction.CompletedAt = DateTimeOffset.UtcNow.ToString("O");
        CaptureEndOfNightWorldStateLocked(transaction);
    }

    private static void CaptureEndOfNightWorldStateLocked(
        EndOfNightTransaction transaction)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        transaction.CompletedTotalDays = Game1.Date.TotalDays;
        transaction.CompletedYear = Game1.year;
        transaction.CompletedSeason = Game1.currentSeason;
        transaction.CompletedDay = Game1.dayOfMonth;
        transaction.CompletedTime = Game1.timeOfDay;
    }

    private static void RecordEndOfNightSafeActionLocked(
        EndOfNightTransaction transaction,
        string sourcePlanningOptionId,
        string optionId)
    {
        var key = "safe_next_day_action:" + sourcePlanningOptionId + ":" +
            optionId;
        transaction.LastTransitionKey = key;
        transaction.Transitions.Add(new EndOfNightTransition
        {
            Ordinal = transaction.Transitions.Count + 1,
            ObservedAt = DateTimeOffset.UtcNow.ToString("O"),
            Key = key,
            TotalDays = transaction.CompletedTotalDays ?? -1,
            TimeOfDay = transaction.CompletedTime ?? -1
        });
    }
}
