using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<JsonObject> BuildQueueFromMockActionAsync(HttpClient http, LiveTrainingOptions options, string stateHash)
    {
        var mockRequest = JsonSerializer.Serialize(new
        {
            goal = options.Goal,
            state_hash = stateHash,
            execution_mode = options.TargetExecutionMode
        }, JsonOptions);
        var modelOutput = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/mock-model/small-model-action", mockRequest);
        return await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/action-queue/compile", modelOutput.ToJsonString(JsonOptions));
    }

    private static string BuildParameterizedActionRequest(LiveTrainingOptions options, string stateHash)
    {
        if (string.IsNullOrWhiteSpace(options.ActionOptionId))
        {
            throw new InvalidOperationException("parameterized action requires --action-option-id.");
        }

        return JsonSerializer.Serialize(new
        {
            schema_version = "small_model_action.v1",
            model_output_id = "model-output.live." + Guid.NewGuid().ToString("N"),
            source_model = "local-parameterized-action.v1",
            state_hash = stateHash,
            goal_id = options.Goal,
            execution_mode = options.TargetExecutionMode,
            actor = options.TargetActor,
            actions = new[]
            {
                new
                {
                    action_id = "action.live." + Guid.NewGuid().ToString("N"),
                    option_id = options.ActionOptionId,
                    rationale = "runtime parameterized action acceptance",
                    parameters = options.ActionParameters
                }
            }
        }, JsonOptions);
    }

    private static async Task<(JsonObject Response, JsonObject Plan, JsonObject Queue, JsonObject Ranking)> BuildQueueFromDailyPlanAsync(
        HttpClient http,
        LiveTrainingOptions options,
        string stateHash,
        IReadOnlyCollection<JsonObject> suppressedObjectiveContinuations,
        IReadOnlyList<string>? candidateOptionIdsOverride = null,
        IReadOnlyList<SmallModelActionParameter>? candidateParametersOverride = null)
    {
        var candidateOptionIds = candidateOptionIdsOverride ??
            options.DailyPlanCandidateOptionIds;
        var candidateParameters = candidateParametersOverride ??
            options.DailyPlanCandidateParameters;
        var explicitCandidates =
            candidateParameters.Count > 0
                ? new object[]
                {
                    new
                    {
                        option_id = candidateOptionIds[0],
                        explicit_confirmation_granted = options.DailyPlanExplicitConfirmationGranted,
                        invocation_source = options.DailyPlanInvocationSource,
                        parameters = candidateParameters
                    }
                }
                : Array.Empty<object>();
        var rankRequest = JsonSerializer.Serialize(new
        {
            goal_id = options.Goal,
            execution_mode = options.TargetExecutionMode,
            policy_checkpoint_path = string.IsNullOrWhiteSpace(options.EffectivePolicyCheckpointPath)
                ? null
                : Path.GetFullPath(options.EffectivePolicyCheckpointPath),
            require_structured_policy = options.RequireStructuredPolicy,
            dataset_path = Path.GetFullPath(options.DatasetPath),
            state_hash = stateHash,
            candidate_option_ids = explicitCandidates.Length == 0
                ? candidateOptionIds
                : Array.Empty<string>(),
            candidates = explicitCandidates,
            include_blocked_options = false
        }, JsonOptions);
        var ranking = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", rankRequest);
        var resolvedGoalId = ranking["goal_resolution"]?[
            "effective_goal_id"]?.GetValue<string>();
        var effectiveGoalId = string.IsNullOrWhiteSpace(resolvedGoalId)
            ? options.Goal
            : resolvedGoalId;
        var rankedCandidates = ranking["ranked_event_candidates"]?.AsArray() ?? new JsonArray();
        var exclusiveRecovery = rankedCandidates.Any(node =>
        {
            var candidate = node?.AsObject();
            return string.Equals(
                    ReadString(candidate, "option_id"),
                    "recovery.stabilize_day",
                    StringComparison.Ordinal) ||
                ReadString(candidate, "kind").StartsWith(
                    "recovery_",
                    StringComparison.Ordinal);
        });
        var unsuppressedCandidates = QueueReplanFilter.FilterSuppressedContinuations(
            rankedCandidates,
            suppressedObjectiveContinuations);
        var continuationCandidates = JsonNode.Parse(
            unsuppressedCandidates.ToJsonString())?.AsArray() ?? new JsonArray();
        var effectiveCandidateKind = exclusiveRecovery
            ? string.Empty
            : options.DailyPlanCandidateKind;
        var selectedCandidates =
            QueueReplanFilter.FilterCandidateKind(
                continuationCandidates,
                effectiveCandidateKind);
        var effectiveCandidateId = exclusiveRecovery
            ? string.Empty
            : options.DailyPlanCandidateId;
        selectedCandidates =
            QueueReplanFilter.FilterCandidateId(
                selectedCandidates,
                effectiveCandidateId);
        var objectiveContinuationFilter = new JsonObject
        {
            ["active"] = false,
            ["exclusive_recovery_override"] = exclusiveRecovery,
            ["objective"] = null,
            ["input_candidate_count"] = rankedCandidates.Count,
            ["selected_candidate_count"] = continuationCandidates.Count,
            ["policy"] = exclusiveRecovery
                ? "exclusive_recovery_preempts_objective_continuation"
                : "typed_objective_identity_match;fail_closed_no_objective_switch"
        };
        ranking["objective_continuation_filter"] = objectiveContinuationFilter;
        ranking["social_continuation_filter"] = JsonNode.Parse(
            objectiveContinuationFilter.ToJsonString(JsonOptions));
        ranking["released_objective_suppression"] = new JsonObject
        {
            ["active"] = suppressedObjectiveContinuations.Count > 0,
            ["suppressed_objective_count"] = suppressedObjectiveContinuations.Count,
            ["input_candidate_count"] = rankedCandidates.Count,
            ["selected_candidate_count"] = unsuppressedCandidates.Count,
            ["policy"] = "same_typed_objective_suppressed_until_game_day_changes"
        };
        ranking["candidate_kind_filter"] = new JsonObject
        {
            ["active"] = !string.IsNullOrWhiteSpace(
                effectiveCandidateKind),
            ["requested_kind"] = options.DailyPlanCandidateKind,
            ["required_kind"] = effectiveCandidateKind,
            ["input_candidate_count"] = continuationCandidates.Count,
            ["selected_candidate_count"] = selectedCandidates.Count,
            ["policy"] =
                "explicit_runtime_calibration_initial_slice_only;" +
                "typed_objective_continuation_overrides_kind_filter"
        };
        ranking["candidate_id_filter"] = new JsonObject
        {
            ["active"] = !string.IsNullOrWhiteSpace(
                effectiveCandidateId),
            ["requested_candidate_id"] =
                options.DailyPlanCandidateId,
            ["required_candidate_id"] = effectiveCandidateId,
            ["selected_candidate_count"] = selectedCandidates.Count,
            ["policy"] =
                "explicit_runtime_calibration_initial_slice_only;" +
                "exact_candidate_id;" +
                "typed_objective_continuation_overrides_id_filter"
        };
        var compileRequest = JsonSerializer.Serialize(new
        {
            state_hash = stateHash,
            goal_id = effectiveGoalId,
            execution_mode = options.TargetExecutionMode,
            max_candidates = options.DailyPlanMaxCandidates,
            compile_action_queue = true,
            ranked_event_candidates = JsonNode.Parse(selectedCandidates.ToJsonString(JsonOptions))
        }, JsonOptions);
        var response = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/daily-plan/compile", compileRequest);
        var plan = response["plan"]?.AsObject() ?? throw new InvalidOperationException("daily plan response did not include plan");
        var queue = response["action_queue"]?.AsObject() ?? throw new InvalidOperationException("daily plan response did not include action_queue");
        return (response, plan, queue, ranking);
    }

    private static Task<(JsonObject Response, JsonObject Plan, JsonObject Queue, JsonObject Ranking)> BuildNativeSaveBoundaryQueueAsync(
        HttpClient http,
        LiveTrainingOptions options,
        string stateHash)
    {
        return BuildQueueFromDailyPlanAsync(
            http,
            options,
            stateHash,
            Array.Empty<JsonObject>(),
            new[] { "recovery.stabilize_day" },
            new[]
            {
                new SmallModelActionParameter
                {
                    Name = "control_plane.native_save_boundary",
                    Value = "true"
                }
            });
    }

    private static async Task<(JsonObject Response, JsonObject Plan, JsonObject Queue, JsonObject Evidence)> BuildQueueFromSelectedCandidateAsync(
        HttpClient http,
        LiveTrainingOptions options,
        string stateHash,
        string effectiveGoalId,
        SelectedQueueCandidateLock selectedCandidate,
        JsonObject? objectiveContinuation = null)
    {
        var effectiveContinuation = objectiveContinuation ??
            selectedCandidate.ObjectiveContinuation;
        var requestParameters = effectiveContinuation is null
            ? JsonNode.Parse(
                selectedCandidate.RankedCandidate["parameters"]?.ToJsonString() ?? "[]")?.AsArray() ??
                new JsonArray()
            : ContinuationRequestParameters(effectiveContinuation);
        var availabilityRequest = JsonSerializer.Serialize(new
        {
            state_hash = stateHash,
            candidates = new[]
            {
                new
                {
                    option_id = selectedCandidate.OptionId,
                    explicit_confirmation_granted = options.DailyPlanExplicitConfirmationGranted,
                    invocation_source = options.DailyPlanInvocationSource,
                    parameters = requestParameters
                }
            }
        }, JsonOptions);
        var availabilityNode = await PostJsonStringAsync(
            http,
            options.BackendUrl + "/api/v1/planner/options/availability",
            availabilityRequest);
        var availability = JsonSerializer.Deserialize<OptionAvailabilityEnvelope>(
            availabilityNode.ToJsonString(JsonOptions),
            JsonOptions) ?? throw new InvalidOperationException(
                "selected queue candidate availability response is empty");

        // This is deterministic candidate materialization. It must never invoke
        // the structured policy that selected the high-level queue.
        var materialized = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            effectiveGoalId);
        var materializedNodes = JsonNode.Parse(
            JsonSerializer.Serialize(materialized, JsonOptions))?.AsArray() ?? new JsonArray();
        var effectiveLock = selectedCandidate with
        {
            ObjectiveContinuation = effectiveContinuation
        };
        var selectedCandidates = SelectedQueueCandidateMatcher.FilterMaterializedCandidates(
            materializedNodes,
            effectiveLock);
        var evidence = new JsonObject
        {
            ["schema_version"] = "selected_queue_candidate_refresh.v1",
            ["state_hash"] = stateHash,
            ["goal_id"] = effectiveGoalId,
            ["policy_model_invoked"] = false,
            ["selection_locked"] = true,
            ["selected_queue_index"] = selectedCandidate.QueueIndex,
            ["selected_candidate_id"] = selectedCandidate.CandidateId,
            ["selected_option_id"] = selectedCandidate.OptionId,
            ["objective"] = effectiveContinuation is null
                ? null
                : JsonNode.Parse(effectiveContinuation.ToJsonString(JsonOptions)),
            ["availability"] = JsonNode.Parse(availabilityNode.ToJsonString(JsonOptions)),
            ["materialized_candidate_count"] = materializedNodes.Count,
            ["selected_candidate_count"] = selectedCandidates.Count,
            ["selected_candidate_filter"] = new JsonObject
            {
                ["active"] = true,
                ["selected_candidate_count"] = selectedCandidates.Count,
                ["policy"] = effectiveContinuation is null
                    ? "locked_exact_candidate_identity;fresh_state_rebind;fail_closed;no_policy_rerank"
                    : "locked_typed_objective_identity;fresh_state_rebind;fail_closed;no_policy_rerank"
            }
        };
        var compileRequest = JsonSerializer.Serialize(new
        {
            state_hash = stateHash,
            goal_id = effectiveGoalId,
            execution_mode = options.TargetExecutionMode,
            max_candidates = 1,
            compile_action_queue = true,
            ranked_event_candidates = JsonNode.Parse(selectedCandidates.ToJsonString(JsonOptions))
        }, JsonOptions);
        var response = await PostJsonStringAsync(
            http,
            options.BackendUrl + "/api/v1/planner/daily-plan/compile",
            compileRequest);
        var plan = response["plan"]?.AsObject() ?? throw new InvalidOperationException(
            "selected queue candidate response did not include plan");
        var queue = response["action_queue"]?.AsObject() ?? throw new InvalidOperationException(
            "selected queue candidate response did not include action_queue");
        return (response, plan, queue, evidence);
    }

    private static JsonArray ContinuationRequestParameters(
        JsonObject continuation)
    {
        var parameters = new JsonArray();
        foreach (var property in continuation)
        {
            if (string.Equals(property.Key, "kind", StringComparison.Ordinal) ||
                property.Value is not JsonValue value)
            {
                continue;
            }

            var text = value.TryGetValue<string>(out var stringValue)
                ? stringValue
                : value.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            parameters.Add(new JsonObject
            {
                ["name"] = "continuation." + property.Key,
                ["value"] = text
            });
        }
        return parameters;
    }

    private static string BuildMovePlanRequest(LiveTrainingOptions options, string stateHash)
    {
        if (options.PlanStepKind == "move_to_tile" && (!options.TargetTileX.HasValue || !options.TargetTileY.HasValue))
        {
            throw new InvalidOperationException("move_to_tile plan output requires --target-tile-x and --target-tile-y.");
        }
        if (options.PlanStepKind == "face_direction" && !options.Direction.HasValue)
        {
            throw new InvalidOperationException("face_direction plan output requires --direction.");
        }
        if (options.PlanStepKind == "wait_ticks" && !options.WaitTicks.HasValue)
        {
            throw new InvalidOperationException("wait_ticks plan output requires --wait-ticks.");
        }

        return JsonSerializer.Serialize(new
        {
            schema_version = "small_model_plan.v1",
            plan_id = "plan.live." + Guid.NewGuid().ToString("N"),
            source_model = "local-plan-smoke.v1",
            state_hash = stateHash,
            goal_id = "goal.autonomous.singleplayer",
            execution_mode = options.TargetExecutionMode,
            actor = options.TargetActor,
            plan_type = "mechanical_plan",
            steps = new[]
            {
                new
                {
                    step_id = "plan.step." + options.PlanStepKind + ".1",
                    kind = options.PlanStepKind,
                    target_location = "current_location",
                    target_tile_x = options.TargetTileX,
                    target_tile_y = options.TargetTileY,
                    direction = options.Direction,
                    wait_ticks = options.WaitTicks,
                    estimated_minutes = 1,
                    preconditions = new[] { "world_ready", options.PlanStepKind + "_parameters_specified" },
                    expected_effects = new[] { options.PlanStepKind + "_applied_or_blocked" },
                    safety_constraints = new[] { "validated_executor_primitive", "no_direct_state_cheat" },
                    failure_policy = new[] { "stop_execution", "record_executor_calibration", "request_replan" }
                }
            }
        }, JsonOptions);
    }
}
