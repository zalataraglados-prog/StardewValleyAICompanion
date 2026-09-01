using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static SelectedQueueBoundaryDecision ValidateSelectedQueueBoundary(
        LiveTrainingOptions options,
        JsonObject currentSnapshot,
        JsonObject[] remainingItems,
        int previousQueueIndex,
        int nextQueueIndex) =>
        SelectedQueueBoundaryValidator.Validate(
            currentSnapshot,
            remainingItems,
            previousQueueIndex,
            nextQueueIndex,
            options.Goal,
            options.TargetExecutionMode);

    private static JsonObject SelectedQueueBoundaryRejectedExecution(
        JsonObject item,
        string queueId,
        string stateHash,
        SelectedQueueBoundaryDecision boundary)
    {
        var readiness = new JsonObject
        {
            ["ready"] = false,
            ["status"] = "blocked",
            ["state_hash"] = stateHash,
            ["blocking_reasons"] = new JsonArray(
                boundary.Reasons.Select(reason => JsonValue.Create(reason)).ToArray()),
            ["policy"] =
                "locked_selected_queue_order;fresh_boundary_time_energy_validation;fail_closed"
        };
        var execution = DispatchRejectedExecution(
            item,
            readiness,
            queueId,
            stateHash,
            "selected_queue_boundary_rejected");
        execution["selected_queue_redecision_required"] = true;
        execution["policy_model_invoked"] = false;
        execution["selected_queue_previous_index"] = boundary.PreviousQueueIndex;
        execution["selected_queue_next_index"] = boundary.NextQueueIndex;
        execution["selected_queue_remaining_required_minutes"] = boundary.RemainingRequiredMinutes;
        execution["selected_queue_remaining_optional_minutes"] = boundary.RemainingOptionalMinutes;
        execution["selected_queue_remaining_energy_cost"] = boundary.RemainingEnergyCost;
        execution["selected_queue_available_energy"] = boundary.AvailableEnergy;
        return execution;
    }

    private static JsonObject SelectedQueueCandidateUnavailableExecution(
        JsonObject item,
        string queueId,
        string stateHash,
        SelectedQueueCandidateLock selectedCandidate,
        string evidencePath)
    {
        var readiness = new JsonObject
        {
            ["ready"] = false,
            ["status"] = "blocked",
            ["state_hash"] = stateHash,
            ["blocking_reasons"] = new JsonArray(
                "selected_queue_candidate_unavailable_on_fresh_snapshot"),
            ["policy"] =
                "locked_selected_candidate;fresh_state_rebind;fail_closed;request_new_model_decision"
        };
        var execution = DispatchRejectedExecution(
            item,
            readiness,
            queueId,
            stateHash,
            "selected_queue_candidate_refresh_rejected");
        execution["selected_queue_redecision_required"] = true;
        execution["selected_queue_candidate_completed"] = false;
        execution["policy_model_invoked"] = false;
        execution["selected_queue_next_index"] = selectedCandidate.QueueIndex;
        execution["selected_queue_candidate_id"] = selectedCandidate.CandidateId;
        execution["selected_queue_candidate_refresh_evidence_path"] = evidencePath;
        return execution;
    }

    private static JsonObject SelectedQueueContinuationBoundaryRejectedExecution(
        JsonObject item,
        string queueId,
        string stateHash,
        string selectedCandidateId,
        int selectedQueueIndex,
        int encounteredQueueIndex)
    {
        var readiness = new JsonObject
        {
            ["ready"] = false,
            ["status"] = "blocked",
            ["state_hash"] = stateHash,
            ["blocking_reasons"] = new JsonArray(
                "selected_queue_active_continuation_would_be_skipped"),
            ["policy"] =
                "complete_active_selected_candidate_before_next_queue_candidate;fail_closed"
        };
        var execution = DispatchRejectedExecution(
            item,
            readiness,
            queueId,
            stateHash,
            "selected_queue_continuation_boundary_rejected");
        execution["selected_queue_redecision_required"] = true;
        execution["selected_queue_candidate_completed"] = false;
        execution["policy_model_invoked"] = false;
        execution["selected_queue_candidate_id"] = selectedCandidateId;
        execution["selected_queue_current_index"] = selectedQueueIndex;
        execution["selected_queue_encountered_index"] = encounteredQueueIndex;
        return execution;
    }
}
