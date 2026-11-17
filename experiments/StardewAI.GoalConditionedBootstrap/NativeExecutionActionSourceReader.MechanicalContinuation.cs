using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    private sealed record MechanicalContinuation(string[] QueuePaths, ActionQueueEnvelope[] Queues,
        IReadOnlyDictionary<string, string> Inputs);

    private static MechanicalContinuation? ReadMechanicalContinuation(string rootPath, ActionQueueEnvelope root,
        QueueExecutionReceiptEnvelope receipt, JsonElement raw, string candidateId)
    {
        var steps = receipt.StepResults ?? Array.Empty<QueueExecutionStepReceipt>();
        if (steps.All(step => step.QueueId == root.QueueId && step.CompiledCommandStateHash == root.StateHash)) return null;
        var rows = raw.GetProperty("step_results");
        Require(steps.Length > 0 && steps.Length <= TeacherEvidenceRolloutLimits.MaxQueueItems &&
                rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() == steps.Length,
            "Native mechanical continuation has an invalid step journal.");
        var paths = new string[steps.Length];
        var queues = new ActionQueueEnvelope[steps.Length];
        var inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        T ReadPinned<T>(string path, string schema)
        {
            if (!inputs.TryGetValue(path, out var hash)) inputs[path] = hash = CurrentTeacherFrontierSupport.HashFile(path);
            var value = ReadTyped<T>(path, schema);
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Native mechanical continuation artifact changed during binding.");
            return value;
        }
        var previousRaw = default(JsonElement);
        var activePath = rootPath;
        var active = root;
        var rootModel = default(SmallModelPlanEnvelope);
        var selectedIndex = -1;
        for (var index = 0; index < steps.Length; index++)
        {
            var stepRaw = ReadPinned<JsonElement>(RequiredFile(Text(rows[index], "execution_path")), "training_execution_result.v1");
            var path = RequiredFile(stepRaw.TryGetProperty("mechanical_compiled_queue_path", out var recorded) &&
                recorded.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(recorded.GetString())
                    ? recorded.GetString()! : Text(stepRaw, "effective_compiled_queue_path"));
            if (index == 0)
            {
                Require(SamePath(path, rootPath), "Native mechanical continuation does not start at its original queue.");
                var modelPath = RequiredFile(Text(stepRaw, "effective_model_plan_path"));
                rootModel = ReadPinned<SmallModelPlanEnvelope>(modelPath, "small_model_plan.v1");
                Require(rootModel.StateHash == root.StateHash && rootModel.GoalId == root.GoalId && rootModel.ExecutionMode == root.ExecutionMode,
                    "Native mechanical continuation changed its original model source.");
                selectedIndex = stepRaw.GetProperty("effective_selected_queue_index").GetInt32();
            }
            Require(selectedIndex >= 0 && Text(stepRaw, "effective_selected_candidate_id") == candidateId &&
                    stepRaw.GetProperty("effective_selected_queue_index").GetInt32() == selectedIndex &&
                    stepRaw.GetProperty("policy_model_invoked_for_step").ValueKind == JsonValueKind.False &&
                    SamePath(RequiredFile(Text(stepRaw, "effective_compiled_queue_path")), rootPath),
                "Native mechanical continuation changed the original selected decision.");
            if (!SamePath(path, activePath))
            {
                Require(index > 0 && previousRaw.GetProperty("queue_replan_applied").ValueKind == JsonValueKind.True &&
                        Text(previousRaw, "queue_replan_kind") == "selected_queue_mechanical_continuation" &&
                        previousRaw.GetProperty("policy_model_invoked").ValueKind == JsonValueKind.False &&
                        previousRaw.GetProperty("selected_queue_lock_preserved").ValueKind == JsonValueKind.True &&
                        Text(previousRaw, "queue_replan_trigger_status") == "applied" &&
                        Text(previousRaw, "queue_replan_source_state_hash") == steps[index - 1].AfterStateHash &&
                        steps[index].SourceStateHash == steps[index - 1].AfterStateHash &&
                        Text(previousRaw, "queue_replan_previous_queue_id") == active.QueueId &&
                        Text(previousRaw, "queue_replan_trigger_queue_item_id") == steps[index - 1].QueueItemId &&
                        SamePath(RequiredFile(Text(previousRaw, "queue_replan_queue_path")), path),
                    "Native mechanical continuation lost its preceding applied frame.");
                active = ReadPinned<ActionQueueEnvelope>(path, "action_queue.v1");
                Require(active.Items is { Length: > 0 } && active.Items.Length <= TeacherEvidenceRolloutLimits.MaxQueueItems &&
                        active.QueueId == Text(previousRaw, "queue_replan_queue_id") && active.StateHash == steps[index].SourceStateHash &&
                        active.GoalId == root.GoalId,
                    "Native mechanical continuation queue is stale or foreign.");
                var planPath = RequiredFile(Text(previousRaw, "queue_replan_plan_path"));
                var responsePath = RequiredFile(Text(previousRaw, "queue_replan_response_path"));
                var evidencePath = RequiredFile(Text(previousRaw, "queue_replan_evidence_path"));
                var plan = ReadPinned<SmallModelPlanEnvelope>(planPath, "small_model_plan.v1");
                var response = ReadPinned<JsonElement>(responsePath, "daily_plan_compile_response.v1");
                var evidence = ReadPinned<JsonElement>(evidencePath, "selected_queue_candidate_refresh.v1");
                Require(plan.StateHash == active.StateHash && plan.GoalId == rootModel!.GoalId && plan.ExecutionMode == root.ExecutionMode &&
                        Text(evidence, "state_hash") == active.StateHash && Text(evidence, "goal_id") == plan.GoalId &&
                        Text(evidence, "selected_candidate_id") == candidateId &&
                        evidence.GetProperty("selected_queue_index").GetInt32() == selectedIndex &&
                        evidence.GetProperty("selected_candidate_count").GetInt32() == 1 &&
                        evidence.GetProperty("policy_model_invoked").ValueKind == JsonValueKind.False &&
                        evidence.GetProperty("selection_locked").ValueKind == JsonValueKind.True &&
                        Text(evidence.GetProperty("availability"), "state_hash") == active.StateHash &&
                        Text(evidence.GetProperty("objective"), "option_id") == Text(evidence, "selected_option_id") &&
                        Text(evidence, "selected_option_id") == Text(NativeObjectiveContinuationJournal.ReadRecordedObjective(raw), "option_id") &&
                        EqualJson(response.GetProperty("plan"), ReadPinned<JsonElement>(planPath, "small_model_plan.v1")) &&
                        EqualJson(response.GetProperty("action_queue"), ReadPinned<JsonElement>(path, "action_queue.v1")),
                    "Native mechanical continuation lost its selected fresh compile evidence.");
                activePath = path;
            }
            paths[index] = activePath;
            queues[index] = active;
            previousRaw = stepRaw;
        }
        foreach (var (path, hash) in inputs)
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Native mechanical continuation artifact changed after binding.");
        return new(paths, queues, inputs);
    }
}
