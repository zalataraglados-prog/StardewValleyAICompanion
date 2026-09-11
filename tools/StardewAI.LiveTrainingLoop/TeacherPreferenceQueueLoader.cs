using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.LiveTrainingLoop;

public static class TeacherPreferenceQueueLoader
{
    public static async Task<JsonObject> LoadAsync(
        string path,
        string currentStateHash,
        string executionMode)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                "Precompiled queue artifact was not found.",
                fullPath);

        var preference = JsonNode.Parse(await File.ReadAllTextAsync(fullPath))
            as JsonObject ?? throw new InvalidDataException(
                "Teacher preference artifact must contain one JSON object.");
        ValidatePreference(preference, currentStateHash);
        var queue = preference["compiled_queue"] as JsonObject ??
            throw new InvalidDataException(
                "teacher_preference_compiled_queue_missing");
        Validate(queue, currentStateHash, executionMode);
        ValidateCompiledSelection(preference, queue, currentStateHash);
        return JsonNode.Parse(queue.ToJsonString())!.AsObject();
    }

    public static void Validate(
        JsonObject queue,
        string currentStateHash,
        string executionMode)
    {
        var reasons = new List<string>();
        RequireEqual(queue, "schema_version", "action_queue.v1", reasons,
            "precompiled_queue_schema_mismatch");
        RequireNonEmpty(queue, "queue_id", reasons,
            "precompiled_queue_id_missing");
        RequireNonEmpty(queue, "goal_id", reasons,
            "precompiled_queue_goal_missing");
        RequireEqual(queue, "status", "pending", reasons,
            "precompiled_queue_not_pending");
        RequireEqual(queue, "state_hash", currentStateHash, reasons,
            "precompiled_queue_state_hash_mismatch");
        RequireEqual(queue, "execution_mode", executionMode, reasons,
            "precompiled_queue_execution_mode_mismatch");
        ValidateActor(queue["actor"] as JsonObject, executionMode, reasons,
            "precompiled_queue_actor_mismatch");

        if (queue["items"] is not JsonArray items ||
            items.Count is < 1 or > TeacherEvidenceRolloutLimits.MaxQueueItems ||
            items.Any(node => node is not JsonObject))
        {
            reasons.Add("precompiled_queue_item_count_out_of_bounds");
        }
        else
        {
            foreach (var item in items.OfType<JsonObject>())
                ValidateItem(item, currentStateHash, executionMode, reasons);
            if (items.OfType<JsonObject>()
                    .Select(item => ReadString(item, "queue_item_id"))
                    .Distinct(StringComparer.Ordinal)
                    .Count() != items.Count)
            {
                reasons.Add("precompiled_queue_item_ids_not_unique");
            }
        }

        if (reasons.Count > 0)
        {
            throw new InvalidDataException(string.Join(
                ";",
                reasons.Distinct(StringComparer.Ordinal)));
        }
    }

    private static void ValidatePreference(
        JsonObject preference,
        string currentStateHash)
    {
        var reasons = new List<string>();
        RequireEqual(
            preference,
            "schema_version",
            "current_stage_one_collection_teacher_preference.v1",
            reasons,
            "teacher_preference_schema_mismatch");
        RequireEqual(preference, "status", "ready", reasons,
            "teacher_preference_not_ready");
        RequireEqual(
            preference,
            "preference_provenance_class",
            "independent_deterministic_teacher_preference",
            reasons,
            "teacher_preference_provenance_mismatch");
        RequireEqual(preference, "source_state_hash", currentStateHash,
            reasons, "teacher_preference_state_hash_mismatch");
        if (preference["teacher_preference_label_eligible"]?.GetValue<bool>() !=
                true)
            reasons.Add("teacher_preference_not_eligible");
        if (preference["formal_training_authorized"]?.GetValue<bool>() == true)
            reasons.Add("teacher_preference_formal_training_must_remain_false");
        if (preference["uses_learner_rank_or_score"]?.GetValue<bool>() == true)
            reasons.Add("teacher_preference_uses_learner_signal");
        RequireNonEmpty(
            preference["selected_candidate"] as JsonObject,
            "candidate_id",
            reasons,
            "teacher_preference_selected_candidate_missing");
        RequireNonEmpty(
            preference["selected_candidate"] as JsonObject,
            "option_id",
            reasons,
            "teacher_preference_selected_option_missing");
        if (reasons.Count > 0)
        {
            throw new InvalidDataException(string.Join(
                ";",
                reasons.Distinct(StringComparer.Ordinal)));
        }
    }

    private static void ValidateCompiledSelection(
        JsonObject preference,
        JsonObject queue,
        string currentStateHash)
    {
        var reasons = new List<string>();
        var selectedCandidate = preference["selected_candidate"] as JsonObject;
        var selectedCandidateId = ReadString(selectedCandidate, "candidate_id");
        if (preference["compiled_plan"] is not JsonObject plan)
        {
            throw new InvalidDataException(
                "teacher_preference_compiled_plan_missing");
        }

        RequireEqual(plan, "state_hash", currentStateHash, reasons,
            "teacher_preference_compiled_plan_state_hash_mismatch");
        RequireEqual(
            queue,
            "source_model_output_id",
            ReadString(plan, "plan_id"),
            reasons,
            "teacher_preference_queue_plan_id_mismatch");
        RequireEqual(
            queue,
            "goal_id",
            ReadString(plan, "goal_id"),
            reasons,
            "teacher_preference_queue_plan_goal_mismatch");

        if (plan["candidate_audit"] is not JsonArray audit ||
            !audit.OfType<JsonObject>().Any(row =>
                string.Equals(
                    ReadString(row, "candidate_id"),
                    selectedCandidateId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(row, "decision"),
                    "accepted",
                    StringComparison.Ordinal)))
        {
            reasons.Add("teacher_preference_selected_candidate_not_compiled");
        }

        if (plan["steps"] is not JsonArray planSteps ||
            queue["items"] is not JsonArray queueItems ||
            planSteps.Count is < 1 or >
                TeacherEvidenceRolloutLimits.MaxQueueItems ||
            planSteps.Count != queueItems.Count ||
            planSteps.Any(node => node is not JsonObject) ||
            queueItems.Any(node => node is not JsonObject))
        {
            reasons.Add("teacher_preference_compiled_step_count_mismatch");
        }
        else
        {
            if (planSteps.OfType<JsonObject>()
                    .Select(step => ReadString(step, "step_id"))
                    .Distinct(StringComparer.Ordinal)
                    .Count() != planSteps.Count)
            {
                reasons.Add("teacher_preference_plan_step_ids_not_unique");
            }
            for (var index = 0; index < planSteps.Count; index++)
            {
                var planStep = planSteps[index]!.AsObject();
                var queueItem = queueItems[index]!.AsObject();
                RequireEqual(
                    queueItem,
                    "source_action_id",
                    ReadString(planStep, "step_id"),
                    reasons,
                    "teacher_preference_queue_source_step_mismatch");
                var command = queueItem["normalized_command"] as JsonObject;
                var planStepKind = ReadParameter(command, "plan_step_kind");
                if (!string.Equals(
                        planStepKind,
                        ReadString(planStep, "kind"),
                        StringComparison.Ordinal))
                {
                    reasons.Add(
                        "teacher_preference_queue_plan_step_kind_mismatch");
                }
                if (!string.Equals(
                        ReadCandidateId(command),
                        selectedCandidateId,
                        StringComparison.Ordinal))
                {
                    reasons.Add(
                        "teacher_preference_queue_candidate_binding_mismatch");
                }
            }
        }

        if (reasons.Count > 0)
        {
            throw new InvalidDataException(string.Join(
                ";",
                reasons.Distinct(StringComparer.Ordinal)));
        }
    }

    public static JsonObject RebindQueueItemToState(
        JsonObject item,
        string currentStateHash)
    {
        if (string.IsNullOrWhiteSpace(currentStateHash))
            throw new ArgumentException("Current state hash is required.",
                nameof(currentStateHash));
        var rebound = JsonNode.Parse(item.ToJsonString())?.AsObject() ??
            throw new InvalidDataException(
                "teacher_preference_queue_item_clone_failed");
        if (rebound["normalized_command"] is not JsonObject command)
            throw new InvalidDataException(
                "precompiled_queue_normalized_command_missing");
        command["state_hash"] = currentStateHash;
        return rebound;
    }

    private static void ValidateItem(
        JsonObject item,
        string currentStateHash,
        string executionMode,
        List<string> reasons)
    {
        RequireNonEmpty(item, "queue_item_id", reasons,
            "precompiled_queue_item_id_missing");
        RequireNonEmpty(item, "option_id", reasons,
            "precompiled_queue_item_option_missing");
        RequireEqual(item, "status", "pending", reasons,
            "precompiled_queue_item_not_pending");
        RequireEmptyArray(item, "missing_state_factors", reasons,
            "precompiled_queue_item_missing_state_factors");
        RequireEmptyArray(item, "blocking_reasons", reasons,
            "precompiled_queue_item_blocked");

        if (item["normalized_command"] is not JsonObject command)
        {
            reasons.Add("precompiled_queue_normalized_command_missing");
            return;
        }

        RequireEqual(command, "option_id", ReadString(item, "option_id"),
            reasons, "precompiled_queue_command_option_mismatch");
        RequireEqual(command, "state_hash", currentStateHash, reasons,
            "precompiled_queue_command_state_hash_mismatch");
        RequireEqual(command, "execution_mode", executionMode, reasons,
            "precompiled_queue_command_execution_mode_mismatch");
        ValidateActor(command["actor"] as JsonObject, executionMode, reasons,
            "precompiled_queue_command_actor_mismatch");
        if (command["steps"] is not JsonArray steps || steps.Count != 1 ||
            steps[0] is not JsonObject step)
        {
            reasons.Add("precompiled_queue_requires_exactly_one_primitive_step");
        }
        else
        {
            RequireNonEmpty(step, "step_type", reasons,
                "precompiled_queue_primitive_step_type_missing");
        }
    }

    private static void ValidateActor(
        JsonObject? actor,
        string executionMode,
        List<string> reasons,
        string reason)
    {
        var expected = ExecutionTargetProfiles.CreateActor(executionMode);
        if (actor is null ||
            !string.Equals(ReadString(actor, "actor_id"), expected.ActorId,
                StringComparison.Ordinal) ||
            !string.Equals(ReadString(actor, "actor_type"), expected.ActorType,
                StringComparison.Ordinal) ||
            !string.Equals(ReadString(actor, "control_surface"),
                expected.ControlSurface, StringComparison.Ordinal))
        {
            reasons.Add(reason);
        }
    }

    private static void RequireEmptyArray(
        JsonObject value,
        string property,
        List<string> reasons,
        string reason)
    {
        if (value[property] is not JsonArray items || items.Count != 0)
            reasons.Add(reason);
    }

    private static void RequireNonEmpty(
        JsonObject? value,
        string property,
        List<string> reasons,
        string reason)
    {
        if (value is null || string.IsNullOrWhiteSpace(ReadString(value, property)))
            reasons.Add(reason);
    }

    private static void RequireEqual(
        JsonObject value,
        string property,
        string expected,
        List<string> reasons,
        string reason)
    {
        if (!string.Equals(
                ReadString(value, property),
                expected,
                StringComparison.Ordinal))
        {
            reasons.Add(reason);
        }
    }

    private static string ReadParameter(JsonObject? command, string name) =>
        command?["parameters"] is JsonArray parameters
            ? parameters.OfType<JsonObject>()
                .Where(value => string.Equals(
                    ReadString(value, "name"),
                    name,
                    StringComparison.Ordinal))
                .Select(value => ReadString(value, "value"))
                .SingleOrDefault() ?? string.Empty
            : string.Empty;

    private static string ReadCandidateId(JsonObject? command)
    {
        const string prefix = "candidate_id:";
        if (command?["parameters"] is not JsonArray parameters)
            return string.Empty;
        return parameters.OfType<JsonObject>()
            .Where(value => string.Equals(
                ReadString(value, "name"),
                "precondition",
                StringComparison.Ordinal))
            .Select(value => ReadString(value, "value"))
            .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value[prefix.Length..])
            .SingleOrDefault() ?? string.Empty;
    }

    private static string ReadString(JsonObject? value, string property) =>
        value?[property] is JsonValue jsonValue &&
        jsonValue.TryGetValue<string>(out var result)
            ? result
            : string.Empty;
}
