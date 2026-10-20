using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class QueueExecutionReceiptValidator
{
    public static string[] ValidateContinuationSlice(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope after,
        string receiptPath,
        string expectedRunId,
        string expectedExecutorVersion,
        string expectedSelectedCandidateId,
        string compiledCommandStateHash,
        out ActionQueueItem[] effectiveItems)
    {
        effectiveItems = ReadEffectiveItems(receipt);
        if (string.Equals(receipt.Status, "applied", StringComparison.Ordinal))
        {
            return Validate(
                queue,
                before,
                receipt,
                after,
                expectedRunId,
                expectedExecutorVersion,
                expectedSelectedCandidateId,
                compiledCommandStateHash,
                requireTeacherPreferenceStateRebound: false);
        }

        var reasons = new List<string>();
        var queueItems = queue.Items ?? Array.Empty<ActionQueueItem>();
        var receiptSteps = receipt.StepResults ??
            Array.Empty<QueueExecutionStepReceipt>();
        if (!string.Equals(queue.SchemaVersion, "action_queue.v1",
                StringComparison.Ordinal) ||
            !string.Equals(queue.Status, "pending", StringComparison.Ordinal) ||
            queueItems.Length == 0 ||
            queueItems.Length > TeacherEvidenceRolloutLimits.MaxQueueItems ||
            string.IsNullOrWhiteSpace(queue.QueueId) ||
            string.IsNullOrWhiteSpace(expectedSelectedCandidateId) ||
            string.IsNullOrWhiteSpace(compiledCommandStateHash))
        {
            reasons.Add("continuation_receipt_compiled_queue_invalid");
        }
        if (!string.Equals(before.SchemaVersion, "snapshot.v1",
                StringComparison.Ordinal) ||
            !string.Equals(after.SchemaVersion, "snapshot.v1",
                StringComparison.Ordinal) ||
            !string.Equals(before.GameVersion, after.GameVersion,
                StringComparison.Ordinal))
        {
            reasons.Add("continuation_receipt_snapshot_schema_or_game_version_mismatch");
        }
        if (string.IsNullOrWhiteSpace(before.SaveId.Value) ||
            !string.Equals(before.SaveId.Value, after.SaveId.Value,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(before.PlayerId.Value) ||
            !string.Equals(before.PlayerId.Value, after.PlayerId.Value,
                StringComparison.Ordinal))
        {
            reasons.Add("continuation_receipt_snapshot_identity_mismatch");
        }
        if (!string.Equals(receipt.SchemaVersion, "queue_execution_receipt.v1",
                StringComparison.Ordinal) ||
            !string.Equals(receipt.QueueExecutionMode, "sequential_queue_items",
                StringComparison.Ordinal))
        {
            reasons.Add("continuation_receipt_schema_or_mode_mismatch");
        }
        if (string.IsNullOrWhiteSpace(expectedRunId) ||
            !string.Equals(receipt.RunId, expectedRunId,
                StringComparison.Ordinal) ||
            !PolicyTrajectoryVersionPins.IsKnownExecutor(
                expectedExecutorVersion))
        {
            reasons.Add("continuation_receipt_run_or_executor_mismatch");
        }
        if (!string.Equals(receipt.QueueId, queue.QueueId,
                StringComparison.Ordinal) ||
            !string.Equals(receipt.SelectedCandidateId,
                expectedSelectedCandidateId,
                StringComparison.Ordinal))
        {
            reasons.Add("continuation_receipt_queue_or_candidate_mismatch");
        }
        if (!string.Equals(queue.StateHash, compiledCommandStateHash,
                StringComparison.Ordinal) ||
            !string.Equals(before.StateHash, compiledCommandStateHash,
                StringComparison.Ordinal) ||
            !string.Equals(receipt.SourceStateHash, before.StateHash,
                StringComparison.Ordinal))
        {
            reasons.Add("continuation_receipt_before_state_hash_mismatch");
        }
        if (string.IsNullOrWhiteSpace(after.StateHash) ||
            string.Equals(after.StateHash, before.StateHash,
                StringComparison.Ordinal) ||
            !string.Equals(receipt.AfterStateHash, after.StateHash,
                StringComparison.Ordinal) ||
            receipt.BeforeGameTick != before.GameTick ||
            receipt.AfterGameTick != after.GameTick ||
            receipt.AfterGameTick <= receipt.BeforeGameTick)
        {
            reasons.Add("continuation_receipt_terminal_boundary_mismatch");
        }
        if ((receipt.BlockReasons?.Length ?? 0) > 0 ||
            receipt.PlannedItemCount != queueItems.Length ||
            receipt.ExecutedItemCount != receiptSteps.Length ||
            receiptSteps.Length == 0 ||
            receiptSteps.Length > TeacherEvidenceRolloutLimits.MaxQueueItems ||
            receipt.FinalPendingItemCount != 0 ||
            receipt.MaxQueueItemAttempts < receiptSteps.Length ||
            receipt.MaxQueueItemAttempts > TeacherEvidenceRolloutLimits.MaxQueueItems)
        {
            reasons.Add("continuation_receipt_item_count_or_block_mismatch");
        }

        var continuation = ReadContinuationState(receiptPath, reasons);
        var verifiedPending = continuation is not null &&
            continuation.PrimitiveVerificationStatus == "verified" &&
            receipt.AfterSnapshotFresh &&
            continuation.RetryNextIteration &&
            continuation.QueueReplanStopReason ==
                "selected_queue_continuation_retry_next_iteration" &&
            !continuation.ObjectiveCompleted &&
            !continuation.DecisionComplete &&
            continuation.CompletedObjectiveCount == 0 &&
            continuation.ResumeIndex >= 0;
        var verifiedCompleted = continuation is not null &&
            continuation.PrimitiveVerificationStatus == "verified" &&
            receipt.AfterSnapshotFresh &&
            !continuation.RetryNextIteration &&
            continuation.ObjectiveCompleted &&
            continuation.DecisionComplete &&
            continuation.CompletedObjectiveCount == 1 &&
            continuation.ResumeIndex == -1;
        if (!string.Equals(receipt.Status, "blocked", StringComparison.Ordinal) ||
            receipt.Success ||
            receipt.SelectedCandidateCompleted ||
            verifiedPending == verifiedCompleted)
        {
            reasons.Add("continuation_receipt_terminal_status_invalid");
        }

        var expectedStateHash = before.StateHash;
        var expectedTick = before.GameTick;
        var queueCount = 0;
        string? activeQueueId = null;
        string? activeCompiledStateHash = null;
        for (var index = 0; index < receiptSteps.Length; index++)
        {
            var step = receiptSteps[index];
            var effectiveItem = index < effectiveItems.Length
                ? effectiveItems[index]
                : null;
            if (step.QueueItemIndex != index ||
                step.OriginalPlannedItemCount != queueItems.Length ||
                step.QueueItemCount < index + 1 ||
                step.QueueItemCount < queueCount ||
                step.QueueItemCount > TeacherEvidenceRolloutLimits.MaxQueueItems)
            {
                reasons.Add("continuation_receipt_step_order_mismatch:" + index);
            }
            queueCount = Math.Max(queueCount, step.QueueItemCount);
            if (effectiveItem is null ||
                string.IsNullOrWhiteSpace(step.QueueId) ||
                !string.Equals(step.QueueItemId, effectiveItem.QueueItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(step.OptionId, effectiveItem.OptionId,
                    StringComparison.Ordinal) ||
                effectiveItem.NormalizedCommand is null ||
                !string.Equals(
                    effectiveItem.NormalizedCommand.StateHash,
                    step.CompiledCommandStateHash,
                    StringComparison.Ordinal) ||
                !ReceiptPrimitiveIdentityMatches(
                    effectiveItem,
                    effectiveItem.NormalizedCommand.Steps ??
                        Array.Empty<CompiledActionStep>(),
                    step.PrimitiveKind))
            {
                reasons.Add("continuation_receipt_step_identity_mismatch:" + index);
            }
            if (!string.Equals(activeQueueId, step.QueueId,
                    StringComparison.Ordinal))
            {
                activeQueueId = step.QueueId;
                activeCompiledStateHash = step.CompiledCommandStateHash;
                if (!string.Equals(activeCompiledStateHash,
                        step.SourceStateHash,
                        StringComparison.Ordinal))
                {
                    reasons.Add("continuation_receipt_queue_rebind_mismatch:" + index);
                }
            }
            else if (!string.Equals(activeCompiledStateHash,
                         step.CompiledCommandStateHash,
                         StringComparison.Ordinal))
            {
                reasons.Add("continuation_receipt_queue_rebind_mismatch:" + index);
            }
            if (index < queueItems.Length &&
                (step.EffectiveQueueItem is not { } effective ||
                 !ExecutionReceiptValidationSupport
                     .EffectiveQueueItemSemanticallyMatches(
                         effective,
                         queueItems[index])))
            {
                reasons.Add("continuation_receipt_original_item_mismatch:" + index);
            }
            if (!string.Equals(step.SourceStateHash, expectedStateHash,
                    StringComparison.Ordinal) ||
                step.BeforeGameTick != expectedTick ||
                string.IsNullOrWhiteSpace(step.AfterStateHash) ||
                !step.StateHashChanged ||
                string.Equals(step.SourceStateHash, step.AfterStateHash,
                    StringComparison.Ordinal) ||
                step.AfterGameTick < step.BeforeGameTick)
            {
                reasons.Add("continuation_receipt_step_boundary_mismatch:" + index);
            }
            if (!string.Equals(step.Status, "applied", StringComparison.Ordinal) ||
                !step.AfterSnapshotFresh ||
                !string.Equals(step.PrimitiveVerificationStatus, "verified",
                    StringComparison.Ordinal) ||
                (step.PrimitiveVerificationReasons?.Length ?? 0) == 0 ||
                (step.BlockReasons?.Length ?? 0) > 0 ||
                !string.IsNullOrWhiteSpace(step.FailureAttribution) ||
                step.ChangedFacts.ValueKind != JsonValueKind.Array ||
                step.ChangedFacts.GetArrayLength() == 0)
            {
                reasons.Add("continuation_receipt_step_not_verified:" + index);
            }
            expectedStateHash = step.AfterStateHash;
            expectedTick = step.AfterGameTick;
        }
        if (!string.Equals(expectedStateHash, after.StateHash,
                StringComparison.Ordinal) ||
            expectedTick != after.GameTick ||
            effectiveItems.Length != receiptSteps.Length ||
            verifiedPending && receiptSteps.Any(value =>
                value.SelectedQueueCandidateCompleted) ||
            verifiedCompleted &&
                (receiptSteps.Length == 0 ||
                 !receiptSteps[^1].SelectedQueueCandidateCompleted))
        {
            reasons.Add("continuation_receipt_final_step_boundary_mismatch");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static ActionQueueItem[] ReadEffectiveItems(
        QueueExecutionReceiptEnvelope receipt)
    {
        var items = new List<ActionQueueItem>();
        foreach (var step in receipt.StepResults ??
                 Array.Empty<QueueExecutionStepReceipt>())
        {
            if (step.EffectiveQueueItem is not { } effective ||
                effective.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            try
            {
                var item = JsonSerializer.Deserialize<ActionQueueItem>(
                    effective.GetRawText(),
                    JsonDefaults.Options);
                if (item is not null)
                    items.Add(item);
            }
            catch (JsonException)
            {
            }
        }
        return items.ToArray();
    }

    private static ContinuationState? ReadContinuationState(
        string receiptPath,
        List<string> reasons)
    {
        try
        {
            using var document = JsonDocument.Parse(
                CurrentTeacherFrontierSupport.ReadArtifactText(receiptPath));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryString(root, "primitive_verification_status", out var status) ||
                !TryBool(root, "objective_continuation_completed",
                    out var objectiveCompleted) ||
                !TryBool(root, "selected_queue_decision_complete",
                    out var decisionComplete) ||
                !TryInt(root, "selected_queue_resume_index", out var resumeIndex) ||
                !root.TryGetProperty("completed_objective_continuations",
                    out var completed) ||
                completed.ValueKind != JsonValueKind.Array)
            {
                reasons.Add("continuation_receipt_state_missing");
                return null;
            }
            var retry = false;
            if (root.TryGetProperty(
                    "selected_queue_continuation_retry_next_iteration",
                    out var retryProperty) &&
                retryProperty.ValueKind is not
                    (JsonValueKind.True or JsonValueKind.False))
            {
                reasons.Add("continuation_receipt_state_missing");
                return null;
            }
            if (retryProperty.ValueKind is
                JsonValueKind.True or JsonValueKind.False)
            {
                retry = retryProperty.GetBoolean();
            }
            var stopReason = string.Empty;
            if (root.TryGetProperty("queue_replan_stop_reason",
                    out var stopReasonProperty))
            {
                if (stopReasonProperty.ValueKind != JsonValueKind.String)
                {
                    reasons.Add("continuation_receipt_state_missing");
                    return null;
                }
                stopReason = stopReasonProperty.GetString() ?? string.Empty;
            }
            return new ContinuationState(
                status,
                retry,
                stopReason,
                objectiveCompleted,
                decisionComplete,
                completed.GetArrayLength(),
                resumeIndex);
        }
        catch (JsonException)
        {
            reasons.Add("continuation_receipt_state_invalid_json");
            return null;
        }
    }

    private static bool TryString(
        JsonElement root,
        string name,
        out string value)
    {
        value = string.Empty;
        return root.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }

    private static bool TryBool(
        JsonElement root,
        string name,
        out bool value)
    {
        value = false;
        if (!root.TryGetProperty(name, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        value = property.GetBoolean();
        return true;
    }

    private static bool TryInt(
        JsonElement root,
        string name,
        out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out var property) &&
            property.TryGetInt32(out value);
    }

    private sealed record ContinuationState(
        string PrimitiveVerificationStatus,
        bool RetryNextIteration,
        string QueueReplanStopReason,
        bool ObjectiveCompleted,
        bool DecisionComplete,
        int CompletedObjectiveCount,
        int ResumeIndex);
}
