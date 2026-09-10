using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherReceiptBuilder
{
    private static string[] ValidatePreferenceArtifact(
        CurrentStageOneCollectionTeacherPreferenceLabel actual,
        CurrentStageOneCollectionTeacherPreferenceLabel expected,
        string inventoryPath,
        string loweringPath,
        string rankingPath,
        string beforeSnapshotPath,
        string intentsPath)
    {
        var reasons = new List<string>();
        if (!string.Equals(
                actual.SchemaVersion,
                "current_stage_one_collection_teacher_preference.v1",
                StringComparison.Ordinal) ||
            !string.Equals(actual.Status, "ready", StringComparison.Ordinal) ||
            !actual.TeacherPreferenceLabelEligible ||
            actual.FormalTrainingAuthorized ||
            actual.UsesLearnerRankOrScore ||
            actual.EmitsNegativeLabelsForUnavailableRoutes ||
            actual.CandidateMembership?.SelectionContract is null ||
            actual.OrderedCandidateEvaluations is null ||
            !string.Equals(
                actual.PreferenceProvenanceClass,
                expected.PreferenceProvenanceClass,
                StringComparison.Ordinal) ||
            !string.Equals(
                actual.SelectionPolicyId,
                expected.SelectionPolicyId,
                StringComparison.Ordinal))
            reasons.Add("teacher_preference_policy_not_ready");
        if (!expected.TeacherPreferenceLabelEligible ||
            expected.SelectedCandidate is null ||
            expected.CompiledQueue is null)
            reasons.Add("recomputed_teacher_preference_not_ready");
        if (!SameHash(actual.RequirementInventorySha256,
                CurrentTeacherFrontierSupport.HashFile(inventoryPath)) ||
            !SameHash(actual.AcquisitionLoweringSha256,
                CurrentTeacherFrontierSupport.HashFile(loweringPath)) ||
            !SameHash(actual.RankingSha256,
                CurrentTeacherFrontierSupport.HashFile(rankingPath)) ||
            !SameHash(actual.SnapshotSha256,
                CurrentTeacherFrontierSupport.HashFile(beforeSnapshotPath)) ||
            !SameHash(actual.MasterAnglerTargetDateIntentsSha256,
                CurrentTeacherFrontierSupport.HashFile(intentsPath)))
            reasons.Add("teacher_preference_source_hash_mismatch");
        if (actual.SelectedCandidate is null ||
            actual.CompiledPlan is null ||
            actual.CompiledQueue?.Items is not { Length: 1 } ||
            actual.CompiledQueue.Items[0].Status != "pending" ||
            actual.CompiledQueue.Status != "pending")
            reasons.Add("teacher_preference_single_pending_queue_missing");
        if (actual.SelectedCandidate is not null &&
            expected.SelectedCandidate is not null &&
            !string.Equals(
                JsonSerializer.Serialize(
                    actual.SelectedCandidate,
                    JsonDefaults.Options),
                JsonSerializer.Serialize(
                    expected.SelectedCandidate,
                    JsonDefaults.Options),
                StringComparison.Ordinal))
            reasons.Add("teacher_preference_selected_candidate_drifted");
        if (actual.CandidateMembership is not null && !string.Equals(
                JsonSerializer.Serialize(
                    actual.CandidateMembership,
                    JsonDefaults.Options),
                JsonSerializer.Serialize(
                    expected.CandidateMembership,
                    JsonDefaults.Options),
                StringComparison.Ordinal))
            reasons.Add("teacher_preference_candidate_membership_drifted");
        var actualOrder = (actual.OrderedCandidateEvaluations ??
                Array.Empty<CurrentCollectionTeacherCandidateEvaluation>())
            .OrderBy(value => value.SelectionOrder)
            .Select(value => value.CandidateId + ":" + value.SelectionOrder)
            .ToArray();
        var expectedOrder = expected.OrderedCandidateEvaluations
            .OrderBy(value => value.SelectionOrder)
            .Select(value => value.CandidateId + ":" + value.SelectionOrder)
            .ToArray();
        if (!actualOrder.SequenceEqual(expectedOrder, StringComparer.Ordinal))
            reasons.Add("teacher_preference_candidate_order_drifted");
        if (actual.CompiledQueue?.Items.Length == 1 &&
            expected.CompiledQueue?.Items.Length == 1 &&
            !string.Equals(
                QueueItemSemantic(actual.CompiledQueue.Items[0]),
                QueueItemSemantic(expected.CompiledQueue.Items[0]),
                StringComparison.Ordinal))
            reasons.Add("teacher_preference_compiled_queue_semantics_drifted");
        if (actual.CompiledPlan is not null &&
            actual.SelectedCandidate is not null &&
            !actual.CompiledPlan.CandidateAudit.Any(value =>
                string.Equals(
                    value.CandidateId,
                    actual.SelectedCandidate.CandidateId,
                    StringComparison.Ordinal) &&
                string.Equals(value.Decision, "accepted", StringComparison.Ordinal)))
            reasons.Add("teacher_preference_selected_candidate_not_compiled");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string QueueItemSemantic(ActionQueueItem item) =>
        JsonSerializer.Serialize(new
        {
            item.OptionId,
            item.Status,
            item.PermissionRequired,
            item.BehaviorCategory,
            item.CompilerResponsibility,
            item.TrainingRole,
            item.RequiredStateFactors,
            item.MissingStateFactors,
            item.BlockingReasons,
            command = new
            {
                item.NormalizedCommand.CommandType,
                item.NormalizedCommand.OptionId,
                item.NormalizedCommand.BehaviorCategory,
                item.NormalizedCommand.CompilerResponsibility,
                item.NormalizedCommand.TrainingRole,
                item.NormalizedCommand.StateHash,
                item.NormalizedCommand.ExecutionMode,
                item.NormalizedCommand.Parameters,
                steps = item.NormalizedCommand.Steps.Select(value => new
                {
                    value.StepType,
                    value.Target,
                    value.ExpectedEffect,
                    value.EstimatedTicks
                })
            }
        }, JsonDefaults.Options);

    private static bool SameHash(string left, string right) => string.Equals(
        left,
        right,
        StringComparison.Ordinal);

    private static string[] ValidateReceipt(
        CurrentStageOneCollectionTeacherPreferenceLabel preference,
        SnapshotEnvelope before,
        PlanExecutionEpisodeEnvelope receipt,
        SnapshotEnvelope after,
        string runId,
        string executorVersion)
    {
        var reasons = new List<string>();
        var queue = preference.CompiledQueue!;
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
        if (queue.Items.Length != 1)
            reasons.Add("teacher_receipt_slice_requires_exactly_one_queue_item");
        var queueItem = queue.Items.SingleOrDefault();
        var commandStep = queueItem?.NormalizedCommand.Steps is { Length: 1 } steps
            ? steps[0]
            : null;
        if (commandStep is null)
            reasons.Add("teacher_receipt_slice_requires_exactly_one_primitive_step");
        if (!string.Equals(receipt.SchemaVersion, "plan_execution_episode.v1",
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_schema_mismatch");
        if (string.IsNullOrWhiteSpace(receipt.EpisodeId) ||
            string.IsNullOrWhiteSpace(receipt.OptionId) ||
            string.IsNullOrWhiteSpace(receipt.PrimitiveKind))
            reasons.Add("execution_receipt_identity_missing");
        if (string.IsNullOrWhiteSpace(runId) ||
            !string.Equals(receipt.RunId, runId, StringComparison.Ordinal))
            reasons.Add("execution_receipt_run_id_mismatch");
        if (!PolicyTrajectoryVersionPins.IsKnownExecutor(executorVersion))
            reasons.Add("execution_receipt_executor_version_unknown");
        if (!string.Equals(before.StateHash, preference.SourceStateHash,
                StringComparison.Ordinal) ||
            !string.Equals(receipt.SourceStateHash, before.StateHash,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_before_state_hash_mismatch");
        if (string.IsNullOrWhiteSpace(after.StateHash) ||
            string.Equals(after.StateHash, before.StateHash,
                StringComparison.Ordinal) ||
            !string.Equals(receipt.AfterStateHash, after.StateHash,
                StringComparison.Ordinal) ||
            !receipt.StateHashChanged)
            reasons.Add("execution_receipt_after_state_hash_mismatch");
        if (receipt.BeforeGameTick != before.GameTick ||
            receipt.AfterGameTick != after.GameTick ||
            receipt.AfterGameTick <= receipt.BeforeGameTick)
            reasons.Add("execution_receipt_tick_boundary_mismatch");
        if (!string.Equals(receipt.QueueId, queue.QueueId,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_queue_id_mismatch");
        if (queueItem is not null &&
            !string.Equals(receipt.OptionId, queueItem.OptionId,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_primitive_option_mismatch");
        if (commandStep is not null &&
            !string.Equals(receipt.PrimitiveKind, commandStep.StepType,
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_primitive_kind_mismatch");
        if (!string.Equals(receipt.Status, "applied", StringComparison.Ordinal) ||
            !receipt.Success ||
            !receipt.AfterSnapshotFresh)
            reasons.Add("execution_receipt_not_applied_verified_fresh");
        if (!string.Equals(
                receipt.PrimitiveVerificationStatus,
                "verified",
                StringComparison.Ordinal))
            reasons.Add("execution_receipt_primitive_not_verified");
        if ((receipt.BlockReasons?.Length ?? 0) > 0 ||
            (receipt.PrimitiveVerificationReasons?.Length ?? 0) > 0 ||
            !string.IsNullOrWhiteSpace(receipt.FailureAttribution))
            reasons.Add("execution_receipt_contains_block_or_verification_reasons");
        if (receipt.ChangedFacts.ValueKind != JsonValueKind.Array ||
            receipt.ChangedFacts.GetArrayLength() == 0)
            reasons.Add("execution_receipt_changed_facts_missing");
        if (receipt.EffectiveQueueItem is not { } effective ||
            queueItem is null ||
            !EffectiveQueueItemMatches(effective, queueItem))
            reasons.Add("execution_receipt_effective_queue_item_mismatch");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool EffectiveQueueItemMatches(
        JsonElement effective,
        ActionQueueItem expected)
    {
        if (effective.ValueKind != JsonValueKind.Object)
            return false;
        try
        {
            var actual = JsonSerializer.Deserialize<ActionQueueItem>(
                effective.GetRawText(),
                JsonDefaults.Options);
            return actual is not null && string.Equals(
                JsonSerializer.Serialize(actual, JsonDefaults.Options),
                JsonSerializer.Serialize(expected, JsonDefaults.Options),
                StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int RequiredStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!TryStateValue(snapshot, section, field, out var value) ||
            !value.TryGetInt32(out var result))
            throw new InvalidDataException(
                $"Snapshot field {section}.{field} is unavailable or not an integer.");
        return result;
    }

    private static string RequiredStateString(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!TryStateValue(snapshot, section, field, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException(
                $"Snapshot field {section}.{field} is unavailable or not a string.");
        return value.GetString()!;
    }

    private static bool TryStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out value);
    }
}
