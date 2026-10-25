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

        var nativeMailItem = CloneQueueItem(first);
        nativeMailItem.OptionId = "executor.close_menu";
        nativeMailItem.NormalizedCommand.OptionId = "executor.close_menu";
        nativeMailItem.NormalizedCommand.Parameters = new[]
        {
            new SmallModelActionParameter
            {
                Name = "plan_step_kind",
                Value = "close_menu"
            },
            new SmallModelActionParameter
            {
                Name = "target_runtime_type",
                Value = "LetterViewerMenu"
            },
            new SmallModelActionParameter
            {
                Name = "target_runtime_identity",
                Value = "fixture_mail"
            },
            new SmallModelActionParameter
            {
                Name = "mail_menu_identity_sha256",
                Value = "fixture-menu-identity"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.option_id",
                Value = "mail.process_letter"
            },
            new SmallModelActionParameter
            {
                Name = "continuation.mail_menu_identity_sha256",
                Value = "fixture-menu-identity"
            },
            new SmallModelActionParameter
            {
                Name = "safety_constraint",
                Value = "native_LetterViewerMenu_receiveLeftClick_only"
            },
            new SmallModelActionParameter
            {
                Name = "safety_constraint",
                Value = "reuse_executor.close_menu"
            }
        };
        nativeMailItem.NormalizedCommand.Steps = new[]
        {
            new CompiledActionStep
            {
                StepId = "step.fixture-mail",
                StepType = "close_menu",
                Target = "active_menu:LetterViewerMenu",
                ExpectedEffect = "menus.active_menu.is_open=false",
                EstimatedTicks = 10
            }
        };
        queue.Items[0] = nativeMailItem;
        receipt.StepResults[0] = SequentialReceiptStep(
            queue,
            nativeMailItem,
            0,
            receiptBefore.StateHash,
            intermediateStateHash,
            receiptBefore.GameTick,
            false,
            changedFacts);
        receipt.StepResults[0].AfterGameTick = receiptBefore.GameTick;
        receipt.StepResults[0].PrimitiveKind = "process_mail_letter";
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
        Require(reasons.Length == 0,
            "An exact native mail lifecycle receipt was rejected: " +
            string.Join(",", reasons));

        nativeMailItem.NormalizedCommand.Parameters = nativeMailItem
            .NormalizedCommand.Parameters
            .Where(parameter => !string.Equals(
                parameter.Name,
                "continuation.option_id",
                StringComparison.Ordinal))
            .ToArray();
        receipt.StepResults[0].EffectiveQueueItem =
            JsonSerializer.SerializeToElement(
                nativeMailItem,
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
                "queue_execution_receipt_step_identity_mismatch:0",
                StringComparer.Ordinal),
            "A generic close-menu queue was admitted as native mail processing.");

        queue.Items[0] = first;
        receipt.StepResults[0] = SequentialReceiptStep(
            queue,
            first,
            0,
            receiptBefore.StateHash,
            intermediateStateHash,
            receiptBefore.GameTick,
            false,
            changedFacts);
        receipt.StepResults[0].AfterGameTick = receiptBefore.GameTick;

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
