using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifySequentialNonReboundQueueReceipt(
        SnapshotEnvelope before,
        ActionQueueEnvelope sourceQueue,
        string selectedCandidateId)
    {
        var receiptBefore = CloneSnapshot(before);
        receiptBefore.SaveId.Value = "sequential-receipt-save";
        receiptBefore.PlayerId.Value = "sequential-receipt-player";
        var first = CloneQueueItem(sourceQueue.Items.Single());
        var second = CloneQueueItem(first);
        second.QueueItemId += ".second";
        second.SourceActionId += ".second";
        var queue = CloneQueue(sourceQueue);
        queue.QueueId += ".sequential";
        queue.Items = new[] { first, second };

        const string intermediateStateHash =
            "sequential-non-rebound-intermediate-state";
        const string finalStateHash = "sequential-non-rebound-final-state";
        var after = CloneSnapshot(receiptBefore);
        after.StateHash = finalStateHash;
        after.GameTick = before.GameTick + 2;
        var changedFacts = JsonSerializer.SerializeToElement(
            new[] { new { fact = "fixture", transition = "changed" } },
            JsonDefaults.Options);
        var receipt = new QueueExecutionReceiptEnvelope
        {
            RunId = "run.sequential-non-rebound",
            QueueId = queue.QueueId,
            SourceStateHash = receiptBefore.StateHash,
            AfterStateHash = finalStateHash,
            BeforeGameTick = receiptBefore.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = 2,
            ExecutedItemCount = 2,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = 2,
            SelectedCandidateId = selectedCandidateId,
            SelectedCandidateCompleted = true,
            StepResults = new[]
            {
                SequentialReceiptStep(
                    queue,
                    first,
                    0,
                    receiptBefore.StateHash,
                    intermediateStateHash,
                    receiptBefore.GameTick,
                    false,
                    changedFacts),
                SequentialReceiptStep(
                    queue,
                    second,
                    1,
                    intermediateStateHash,
                    finalStateHash,
                    receiptBefore.GameTick + 1,
                    true,
                    changedFacts)
            }
        };
        receipt.StepResults[0].AfterGameTick = receiptBefore.GameTick;
        receipt.StepResults[1].BeforeGameTick = receiptBefore.GameTick;

        var reasons = QueueExecutionReceiptValidator.Validate(
            queue,
            receiptBefore,
            receipt,
            after,
            receipt.RunId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            selectedCandidateId,
            receiptBefore.StateHash,
            requireTeacherPreferenceStateRebound: false);
        Require(reasons.Length == 0,
            "A valid same-tick native sub-step receipt was rejected: " +
            string.Join(",", reasons));

        receipt.StepResults[0].AfterGameTick = receiptBefore.GameTick - 1;
        receipt.StepResults[1].BeforeGameTick = receiptBefore.GameTick - 1;
        reasons = QueueExecutionReceiptValidator.Validate(
            queue,
            receiptBefore,
            receipt,
            after,
            receipt.RunId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            selectedCandidateId,
            receiptBefore.StateHash,
            requireTeacherPreferenceStateRebound: false);
        Require(reasons.Contains(
                "queue_execution_receipt_step_transition_mismatch:0",
                StringComparer.Ordinal),
            "A native sub-step with a reversed game-tick boundary was admitted.");
        receipt.StepResults[0].AfterGameTick = receiptBefore.GameTick;
        receipt.StepResults[1].BeforeGameTick = receiptBefore.GameTick;

        var incorrectlyRebound = CloneQueueItem(second);
        incorrectlyRebound.NormalizedCommand.StateHash =
            intermediateStateHash;
        receipt.StepResults[1].EffectiveQueueItem =
            JsonSerializer.SerializeToElement(
                incorrectlyRebound,
                JsonDefaults.Options);
        reasons = QueueExecutionReceiptValidator.Validate(
            queue,
            receiptBefore,
            receipt,
            after,
            receipt.RunId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            selectedCandidateId,
            receiptBefore.StateHash,
            requireTeacherPreferenceStateRebound: false);
        Require(reasons.Contains(
                "execution_receipt_effective_queue_item_mismatch:1",
                StringComparer.Ordinal),
            "An undeclared queue-item state rebind was admitted.");

        var continuationRebind = CloneQueueItem(second);
        continuationRebind.QueueItemId += ".continuation";
        continuationRebind.NormalizedCommand.StateHash =
            intermediateStateHash;
        continuationRebind.NormalizedCommand.Steps[0].StepId +=
            ".continuation";
        var continuationElement = JsonSerializer.SerializeToElement(
            continuationRebind,
            JsonDefaults.Options);
        Require(ExecutionReceiptValidationSupport
                .EffectiveQueueItemSemanticallyMatches(
                    continuationElement,
                    second),
            "A semantic continuation queue-item rebind was rejected.");
        continuationRebind.NormalizedCommand.Steps[0].Target += ".drifted";
        continuationElement = JsonSerializer.SerializeToElement(
            continuationRebind,
            JsonDefaults.Options);
        Require(!ExecutionReceiptValidationSupport
                .EffectiveQueueItemSemanticallyMatches(
                    continuationElement,
                    second),
            "A continuation queue item with semantic target drift was admitted.");
    }

    private static QueueExecutionStepReceipt SequentialReceiptStep(
        ActionQueueEnvelope queue,
        ActionQueueItem item,
        int index,
        string beforeStateHash,
        string afterStateHash,
        long beforeTick,
        bool completed,
        JsonElement changedFacts) => new()
        {
            QueueItemIndex = index,
            QueueItemCount = queue.Items.Length,
            OriginalPlannedItemCount = queue.Items.Length,
            QueueId = queue.QueueId,
            QueueItemId = item.QueueItemId,
            OptionId = item.OptionId,
            SourceStateHash = beforeStateHash,
            CompiledCommandStateHash = queue.StateHash,
            TeacherPreferenceStateRebound = false,
            SelectedQueueCandidateCompleted = completed,
            AfterStateHash = afterStateHash,
            StateHashChanged = true,
            BeforeGameTick = beforeTick,
            AfterGameTick = beforeTick + 1,
            AfterSnapshotFresh = true,
            Status = "applied",
            PrimitiveKind = item.NormalizedCommand.Steps.Single().StepType,
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons =
                new[] { "fixture_native_transition_verified" },
            EffectiveQueueItem = JsonSerializer.SerializeToElement(
                item,
                JsonDefaults.Options),
            ChangedFacts = changedFacts
        };

    private static ActionQueueItem CloneQueueItem(ActionQueueItem source) =>
        JsonSerializer.Deserialize<ActionQueueItem>(
            JsonSerializer.Serialize(source, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
                "Queue receipt self-test item clone failed.");

    private static ActionQueueEnvelope CloneQueue(ActionQueueEnvelope source) =>
        JsonSerializer.Deserialize<ActionQueueEnvelope>(
            JsonSerializer.Serialize(source, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
                "Queue receipt self-test queue clone failed.");

    private static SnapshotEnvelope CloneSnapshot(SnapshotEnvelope source) =>
        JsonSerializer.Deserialize<SnapshotEnvelope>(
            JsonSerializer.Serialize(source, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
                "Queue receipt self-test snapshot clone failed.");
}
