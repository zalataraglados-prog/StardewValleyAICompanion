using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static partial class QueueReplanFilter
{
    public static int RemainingQueueItemCount(int queueItemCount, int attemptedCount)
    {
        return Math.Max(0, queueItemCount - attemptedCount);
    }

    private static readonly HashSet<string> TrainingEquivalentOptionIds = new(StringComparer.Ordinal)
    {
        "executor.play_junimo_kart",
        "executor.play_prairie_king"
    };

    private static readonly HashSet<string> NonSemanticParameterNames = new(StringComparer.Ordinal)
    {
        "precondition",
        "safety_constraint",
        "failure_policy",
        "estimated_minutes"
    };

    private static readonly string[] PlayerCustomizationContinuationNames =
    {
        "customization_name", "customization_favorite_thing", "customization_gender",
        "customization_skin_index", "customization_hair_style_id", "customization_accessory_index",
        "customization_eye_hue", "customization_eye_saturation", "customization_eye_value",
        "customization_hair_hue", "customization_hair_saturation", "customization_hair_value"
    };

    private static readonly string[] MasterAnglerContinuationNames =
    {
        "master_angler_target_qualified_item_id",
        "master_angler_target_location",
        "master_angler_source_kind",
        "master_angler_source_key",
        "master_angler_window_first_total_day",
        "master_angler_window_last_total_day",
        "master_angler_target_total_day",
        "master_angler_effective_start_time",
        "master_angler_last_cast_time_exclusive",
        "master_angler_stage_one_deadline_total_day_exclusive",
        "master_angler_window_index_path",
        "master_angler_window_index_sha256",
        "master_angler_validation_status",
        "master_angler_runtime_terminal_validation_required"
    };

    public static JsonObject[] FilterUnattempted(JsonObject[] queueItems, ISet<string> attemptedSemanticKeys)
    {
        return queueItems
            .Where(item => !attemptedSemanticKeys.Contains(SemanticQueueItemKey(item)))
            .ToArray();
    }

    public static int ReadAcceptedCandidateIndex(JsonObject? queueItem)
    {
        if (queueItem?["selected_queue_index"] is JsonValue explicitIndex &&
            explicitIndex.TryGetValue<int>(out var selectedIndex) &&
            selectedIndex >= 0)
        {
            return selectedIndex;
        }

        var compiledIndex = ReadParameter(
            queueItem,
            "budget.accepted_candidate_index");
        return int.TryParse(compiledIndex, out var acceptedIndex) && acceptedIndex >= 0
            ? acceptedIndex
            : -1;
    }

    public static void StampSelectedQueueIndex(JsonObject queueItem, int selectedQueueIndex)
    {
        queueItem["selected_queue_index"] = selectedQueueIndex;
    }

    public static void StampSelectedQueueIdentity(
        JsonObject queueItem,
        string selectedCandidateId,
        int selectedQueueIndex)
    {
        queueItem["selected_queue_candidate_id"] = selectedCandidateId;
        StampSelectedQueueIndex(queueItem, selectedQueueIndex);
    }

    public static bool ShouldReleaseUnavailableContinuation(
        JsonObject? continuation,
        JsonObject? ranking)
    {
        if (continuation is null ||
            ranking?["objective_continuation_filter"] is not JsonObject filter ||
            filter["active"]?.GetValue<bool>() != true)
        {
            return false;
        }

        return filter["selected_candidate_count"]?.GetValue<int>() == 0;
    }

    public static bool ShouldResumeContinuationOnNextIteration(
        JsonObject? continuation,
        int refreshedItemCount)
    {
        return continuation is not null && refreshedItemCount == 0;
    }

    public static bool IsTrainingVerifiedExecution(JsonObject? execution)
    {
        if (!string.Equals(
                ReadString(execution, "status"),
                "applied",
                StringComparison.Ordinal))
        {
            return false;
        }

        var verificationStatus = ReadString(
            execution,
            "primitive_verification_status");
        return string.Equals(
                verificationStatus,
                "verified",
                StringComparison.Ordinal) ||
            string.Equals(
                verificationStatus,
                "simulated_equivalent",
                StringComparison.Ordinal) &&
            TrainingEquivalentOptionIds.Contains(
                ReadString(execution, "option_id"));
    }

    public static JsonObject? LastTrainingVerifiedCompletedSelectedCandidateStep(
        JsonObject? aggregateExecution)
    {
        if (aggregateExecution?["step_results"] is not JsonArray steps)
        {
            return IsTrainingVerifiedExecution(aggregateExecution) &&
                aggregateExecution?["selected_queue_candidate_completed"]?.GetValue<bool>() == true
                    ? aggregateExecution
                    : null;
        }

        return steps
            .OfType<JsonObject>()
            .GroupBy(
                EffectiveDecisionArtifactTracker.ReadCandidateId,
                StringComparer.Ordinal)
            .Where(group =>
                !string.IsNullOrWhiteSpace(group.Key) &&
                group.Last()["selected_queue_candidate_completed"]?.GetValue<bool>() == true &&
                group.All(step =>
                    IsTrainingVerifiedExecution(step) &&
                    step["after_snapshot_fresh"]?.GetValue<bool>() == true))
            .Select(group => group.Last())
            .LastOrDefault();
    }

    public static JsonObject? ReadSocialContinuation(JsonObject? queueItem)
    {
        var continuation = ReadObjectiveContinuation(queueItem);
        return string.Equals(ReadString(continuation, "kind"), "social", StringComparison.Ordinal)
            ? continuation
            : null;
    }

}
