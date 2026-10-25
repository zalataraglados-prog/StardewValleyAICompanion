using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class QueueExecutionReceiptValidator
{
    public static string[] Validate(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope after,
        string expectedRunId,
        string expectedExecutorVersion,
        string expectedSelectedCandidateId,
        string compiledCommandStateHash,
        bool requireTeacherPreferenceStateRebound)
    {
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
            reasons.Add("execution_receipt_compiled_queue_invalid");
        if (!string.Equals(before.SchemaVersion, "snapshot.v1",
                StringComparison.Ordinal) ||
            !string.Equals(after.SchemaVersion, "snapshot.v1",
                StringComparison.Ordinal) ||
            !string.Equals(before.GameVersion, after.GameVersion,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_snapshot_schema_or_game_version_mismatch");
        if (string.IsNullOrWhiteSpace(before.SaveId.Value) ||
            !string.Equals(before.SaveId.Value, after.SaveId.Value,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(before.PlayerId.Value) ||
            !string.Equals(before.PlayerId.Value, after.PlayerId.Value,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_snapshot_identity_mismatch");
        if (!string.Equals(
                receipt.SchemaVersion,
                "queue_execution_receipt.v1",
                StringComparison.Ordinal) ||
            !string.Equals(
                receipt.QueueExecutionMode,
                "sequential_queue_items",
                StringComparison.Ordinal))
            reasons.Add("queue_execution_receipt_schema_or_mode_mismatch");
        if (string.IsNullOrWhiteSpace(expectedRunId) ||
            !string.Equals(receipt.RunId, expectedRunId,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_run_id_mismatch");
        if (!PolicyTrajectoryVersionPins.IsKnownExecutor(
                expectedExecutorVersion))
            reasons.Add("execution_receipt_executor_version_unknown");
        if (!string.Equals(receipt.QueueId, queue.QueueId,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_queue_id_mismatch");
        if (!string.Equals(
                receipt.SelectedCandidateId,
                expectedSelectedCandidateId,
                StringComparison.Ordinal))
            reasons.Add("queue_execution_receipt_candidate_mismatch");
        if (!string.Equals(queue.StateHash, compiledCommandStateHash,
                StringComparison.Ordinal) ||
            !string.Equals(before.StateHash, compiledCommandStateHash,
                StringComparison.Ordinal) ||
            !string.Equals(receipt.SourceStateHash, before.StateHash,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_before_state_hash_mismatch");
        if (string.IsNullOrWhiteSpace(after.StateHash) ||
            string.Equals(after.StateHash, before.StateHash,
                StringComparison.Ordinal) ||
            !string.Equals(receipt.AfterStateHash, after.StateHash,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_after_state_hash_mismatch");
        if (receipt.BeforeGameTick != before.GameTick ||
            receipt.AfterGameTick != after.GameTick ||
            receipt.AfterGameTick <= receipt.BeforeGameTick)
            reasons.Add("execution_receipt_tick_boundary_mismatch");
        if (!string.Equals(receipt.Status, "applied", StringComparison.Ordinal) ||
            !receipt.Success ||
            !receipt.AfterSnapshotFresh ||
            !receipt.SelectedCandidateCompleted)
            reasons.Add("queue_execution_receipt_not_completed_verified_fresh");
        if ((receipt.BlockReasons?.Length ?? 0) > 0)
            reasons.Add("execution_receipt_contains_block_or_failure_reasons");
        if (receipt.PlannedItemCount != queueItems.Length ||
            receipt.ExecutedItemCount != queueItems.Length ||
            receipt.FinalPendingItemCount != 0 ||
            receipt.MaxQueueItemAttempts < queueItems.Length ||
            receipt.MaxQueueItemAttempts >
                TeacherEvidenceRolloutLimits.MaxQueueItems ||
            receiptSteps.Length != queueItems.Length)
            reasons.Add("queue_execution_receipt_item_count_mismatch");

        var expectedStateHash = before.StateHash;
        var expectedTick = before.GameTick;
        for (var index = 0;
             index < Math.Min(receiptSteps.Length, queueItems.Length);
             index++)
        {
            var step = receiptSteps[index];
            var expectedItem = queueItems[index];
            var expectedSteps = expectedItem.NormalizedCommand?.Steps ??
                Array.Empty<CompiledActionStep>();
            if (step.QueueItemIndex != index ||
                step.QueueItemCount != queueItems.Length ||
                step.OriginalPlannedItemCount != queueItems.Length)
                reasons.Add("queue_execution_receipt_step_order_mismatch:" + index);
            if (!string.Equals(step.QueueId, queue.QueueId,
                    StringComparison.Ordinal) ||
                !string.Equals(step.QueueItemId, expectedItem.QueueItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(step.OptionId, expectedItem.OptionId,
                    StringComparison.Ordinal) ||
                !ReceiptPrimitiveIdentityMatches(
                    expectedItem,
                    expectedSteps,
                    step.PrimitiveKind))
                reasons.Add("queue_execution_receipt_step_identity_mismatch:" + index);
            if (!string.Equals(
                    step.CompiledCommandStateHash,
                    compiledCommandStateHash,
                    StringComparison.Ordinal) ||
                requireTeacherPreferenceStateRebound &&
                    !step.TeacherPreferenceStateRebound ||
                !string.Equals(step.SourceStateHash, expectedStateHash,
                    StringComparison.Ordinal))
                reasons.Add("queue_execution_receipt_step_state_rebind_mismatch:" + index);
            if (string.IsNullOrWhiteSpace(step.AfterStateHash) ||
                !step.StateHashChanged ||
                string.Equals(step.SourceStateHash, step.AfterStateHash,
                    StringComparison.Ordinal) ||
                step.BeforeGameTick != expectedTick ||
                step.AfterGameTick < step.BeforeGameTick)
                reasons.Add("queue_execution_receipt_step_transition_mismatch:" + index);
            if (!string.Equals(step.Status, "applied", StringComparison.Ordinal) ||
                !step.AfterSnapshotFresh ||
                step.SelectedQueueCandidateCompleted !=
                    (index == queueItems.Length - 1) ||
                !string.Equals(
                    step.PrimitiveVerificationStatus,
                    "verified",
                    StringComparison.Ordinal) ||
                (step.PrimitiveVerificationReasons?.Length ?? 0) == 0 ||
                (step.BlockReasons?.Length ?? 0) > 0 ||
                !string.IsNullOrWhiteSpace(step.FailureAttribution) ||
                step.ChangedFacts.ValueKind != JsonValueKind.Array ||
                step.ChangedFacts.GetArrayLength() == 0)
                reasons.Add("queue_execution_receipt_step_not_verified:" + index);
            if (step.EffectiveQueueItem is not { } effective ||
                !ExecutionReceiptValidationSupport.EffectiveQueueItemMatches(
                    effective,
                    expectedItem,
                    step.TeacherPreferenceStateRebound
                        ? expectedStateHash
                        : step.CompiledCommandStateHash))
                reasons.Add("execution_receipt_effective_queue_item_mismatch:" + index);
            expectedStateHash = step.AfterStateHash;
            expectedTick = step.AfterGameTick;
        }

        if (!string.Equals(expectedStateHash, after.StateHash,
                StringComparison.Ordinal) ||
            expectedTick != after.GameTick)
            reasons.Add("queue_execution_receipt_final_step_boundary_mismatch");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool ReceiptPrimitiveIdentityMatches(
        ActionQueueItem item,
        CompiledActionStep[] steps,
        string primitiveKind)
    {
        if (steps.Length == 1)
        {
            if (string.Equals(
                    primitiveKind,
                    steps[0].StepType,
                    StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(
                    primitiveKind,
                    "process_mail_letter",
                    StringComparison.Ordinal) &&
                IsExactNativeMailProcessingItem(item, steps[0]);
        }

        var expectedMacroPrimitive = item.OptionId switch
        {
            "executor.sleep" when HasExactStepTypes(
                steps,
                "move_to_bed_adjacent",
                "step_onto_sleep_touch_tile",
                "confirm_sleep_yes") => "sleep",
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(expectedMacroPrimitive) &&
            string.Equals(
                primitiveKind,
                expectedMacroPrimitive,
                StringComparison.Ordinal);
    }

    private static bool IsExactNativeMailProcessingItem(
        ActionQueueItem item,
        CompiledActionStep step)
    {
        if (!string.Equals(
                item.OptionId,
                "executor.close_menu",
                StringComparison.Ordinal) ||
            !string.Equals(
                step.StepType,
                "close_menu",
                StringComparison.Ordinal) ||
            !string.Equals(
                step.Target,
                "active_menu:LetterViewerMenu",
                StringComparison.Ordinal) ||
            !TryReadUniqueParameter(
                item,
                "target_runtime_identity",
                out var runtimeIdentity) ||
            string.IsNullOrWhiteSpace(runtimeIdentity) ||
            !TryReadUniqueParameter(
                item,
                "mail_menu_identity_sha256",
                out var menuIdentity) ||
            string.IsNullOrWhiteSpace(menuIdentity) ||
            !TryReadUniqueParameter(
                item,
                "continuation.mail_menu_identity_sha256",
                out var continuationMenuIdentity) ||
            !string.Equals(
                menuIdentity,
                continuationMenuIdentity,
                StringComparison.Ordinal))
        {
            return false;
        }

        return HasExactParameter(
                item,
                "plan_step_kind",
                "close_menu") &&
            HasExactParameter(
                item,
                "target_runtime_type",
                "LetterViewerMenu") &&
            HasExactParameter(
                item,
                "continuation.option_id",
                "mail.process_letter") &&
            HasExactParameter(
                item,
                "safety_constraint",
                "native_LetterViewerMenu_receiveLeftClick_only") &&
            HasExactParameter(
                item,
                "safety_constraint",
                "reuse_executor.close_menu");
    }

    private static bool TryReadUniqueParameter(
        ActionQueueItem item,
        string name,
        out string value)
    {
        value = string.Empty;
        var values = (item.NormalizedCommand?.Parameters ??
                Array.Empty<SmallModelActionParameter>())
            .Where(parameter => string.Equals(
                parameter.Name,
                name,
                StringComparison.Ordinal))
            .Select(parameter => parameter.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (values.Length != 1)
            return false;

        value = values[0];
        return true;
    }

    private static bool HasExactParameter(
        ActionQueueItem item,
        string name,
        string value) =>
        (item.NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>()).Any(parameter =>
                string.Equals(
                    parameter.Name,
                    name,
                    StringComparison.Ordinal) &&
                string.Equals(
                    parameter.Value,
                    value,
                    StringComparison.Ordinal));

    private static bool HasExactStepTypes(
        CompiledActionStep[] steps,
        params string[] expected) =>
        steps.Select(step => step.StepType)
            .SequenceEqual(expected, StringComparer.Ordinal);
}

internal static class ExecutionReceiptValidationSupport
{
    public static bool EffectiveQueueItemSemanticallyMatches(
        JsonElement effective,
        ActionQueueItem expected)
    {
        if (effective.ValueKind != JsonValueKind.Object ||
            expected.NormalizedCommand is null)
        {
            return false;
        }
        try
        {
            var actual = JsonSerializer.Deserialize<ActionQueueItem>(
                effective.GetRawText(),
                JsonDefaults.Options);
            var normalizedExpected = JsonSerializer.Deserialize<
                ActionQueueItem>(
                    JsonSerializer.Serialize(expected, JsonDefaults.Options),
                    JsonDefaults.Options);
            if (actual is null || normalizedExpected is null)
                return false;
            NormalizeGeneratedIdentity(actual);
            NormalizeGeneratedIdentity(normalizedExpected);
            return string.Equals(
                JsonSerializer.Serialize(actual, JsonDefaults.Options),
                JsonSerializer.Serialize(
                    normalizedExpected,
                    JsonDefaults.Options),
                StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool EffectiveQueueItemMatches(
        JsonElement effective,
        ActionQueueItem expected,
        string reboundStateHash)
    {
        if (effective.ValueKind != JsonValueKind.Object ||
            expected.NormalizedCommand is null)
            return false;
        try
        {
            var actual = JsonSerializer.Deserialize<ActionQueueItem>(
                effective.GetRawText(),
                JsonDefaults.Options);
            var reboundExpected = JsonSerializer.Deserialize<ActionQueueItem>(
                JsonSerializer.Serialize(expected, JsonDefaults.Options),
                JsonDefaults.Options);
            if (reboundExpected is null)
                return false;
            reboundExpected.NormalizedCommand.StateHash = reboundStateHash;
            return actual is not null && string.Equals(
                JsonSerializer.Serialize(actual, JsonDefaults.Options),
                JsonSerializer.Serialize(reboundExpected, JsonDefaults.Options),
                StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void NormalizeGeneratedIdentity(ActionQueueItem item)
    {
        item.QueueItemId = string.Empty;
        item.NormalizedCommand.StateHash = string.Empty;
        foreach (var step in item.NormalizedCommand.Steps ??
                 Array.Empty<CompiledActionStep>())
        {
            step.StepId = string.Empty;
        }
    }
}
