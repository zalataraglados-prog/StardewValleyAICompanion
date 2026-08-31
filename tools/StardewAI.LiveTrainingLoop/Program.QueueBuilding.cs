using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
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
        JsonObject? objectiveContinuation)
    {
        var explicitCandidates =
            objectiveContinuation is null &&
            options.DailyPlanCandidateParameters.Count > 0
                ? new object[]
                {
                    new
                    {
                        option_id = options.DailyPlanCandidateOptionIds[0],
                        explicit_confirmation_granted = options.DailyPlanExplicitConfirmationGranted,
                        invocation_source = options.DailyPlanInvocationSource,
                        parameters = options.DailyPlanCandidateParameters
                    }
                }
                : Array.Empty<object>();
        var rankRequest = JsonSerializer.Serialize(new
        {
            goal_id = options.Goal,
            execution_mode = options.TargetExecutionMode,
            policy_checkpoint_path = string.IsNullOrWhiteSpace(options.PolicyCheckpointPath)
                ? null
                : Path.GetFullPath(options.PolicyCheckpointPath),
            require_structured_policy = options.RequireStructuredPolicy,
            dataset_path = Path.GetFullPath(options.DatasetPath),
            state_hash = stateHash,
            candidate_option_ids = objectiveContinuation is null && explicitCandidates.Length == 0
                ? options.DailyPlanCandidateOptionIds
                : Array.Empty<string>(),
            candidates = objectiveContinuation is null
                ? explicitCandidates
                : new object[]
                {
                    new
                    {
                        option_id = ReadString(objectiveContinuation, "option_id"),
                        explicit_confirmation_granted = options.DailyPlanExplicitConfirmationGranted,
                        invocation_source = options.DailyPlanInvocationSource,
                        parameters = new[]
                        {
                            new { name = "continuation.option_id", value = ReadString(objectiveContinuation, "option_id") },
                            new { name = "continuation.npc_name", value = ReadString(objectiveContinuation, "npc_name") },
                            new { name = "continuation.target_location", value = ReadString(objectiveContinuation, "target_location") },
                            new { name = "continuation.slot_index", value = ReadString(objectiveContinuation, "slot_index") },
                            new { name = "continuation.qualified_item_id", value = ReadString(objectiveContinuation, "qualified_item_id") },
                            new { name = "continuation.shop_id", value = ReadString(objectiveContinuation, "shop_id") },
                            new { name = "continuation.item_id", value = ReadString(objectiveContinuation, "item_id") },
                            new { name = "continuation.max_unit_price", value = ReadString(objectiveContinuation, "max_unit_price") },
                            new { name = "continuation.expected_unit_price", value = ReadString(objectiveContinuation, "expected_unit_price") },
                            new { name = "continuation.quantity", value = ReadString(objectiveContinuation, "quantity") },
                            new { name = "continuation.bin_location", value = ReadString(objectiveContinuation, "bin_location") },
                            new { name = "continuation.bin_tile_x", value = ReadString(objectiveContinuation, "bin_tile_x") },
                            new { name = "continuation.bin_tile_y", value = ReadString(objectiveContinuation, "bin_tile_y") },
                            new { name = "continuation.stand_tile_x", value = ReadString(objectiveContinuation, "stand_tile_x") },
                            new { name = "continuation.stand_tile_y", value = ReadString(objectiveContinuation, "stand_tile_y") },
                            new { name = "continuation.quest_candidate_id", value = ReadString(objectiveContinuation, "quest_candidate_id") },
                            new { name = "continuation.execution_option_id", value = ReadString(objectiveContinuation, "execution_option_id") },
                            new { name = "continuation.machine_location_id", value = ReadString(objectiveContinuation, "machine_location_id") },
                            new { name = "continuation.machine_tile_x", value = ReadString(objectiveContinuation, "machine_tile_x") },
                            new { name = "continuation.machine_tile_y", value = ReadString(objectiveContinuation, "machine_tile_y") },
                            new { name = "continuation.machine_inventory_slot_index", value = ReadString(objectiveContinuation, "machine_inventory_slot_index") },
                            new { name = "continuation.machine_qualified_item_id", value = ReadString(objectiveContinuation, "machine_qualified_item_id") },
                            new { name = "continuation.machine_item_id", value = ReadString(objectiveContinuation, "machine_item_id") },
                            new { name = "continuation.renovation_id", value = ReadString(objectiveContinuation, "renovation_id") },
                            new { name = "continuation.selected_index", value = ReadString(objectiveContinuation, "selected_index") },
                            new { name = "continuation.renovation_reason", value = ReadString(objectiveContinuation, "renovation_reason") },
                            new { name = "continuation.confirm_renovation", value = ReadString(objectiveContinuation, "confirm_renovation") },
                            new { name = "continuation.confirm_destructive", value = ReadString(objectiveContinuation, "confirm_destructive") },
                            new { name = "continuation.expected_prize_level", value = ReadString(objectiveContinuation, "expected_prize_level") },
                            new { name = "continuation.expected_reward_fingerprint", value = ReadString(objectiveContinuation, "expected_reward_fingerprint") },
                            new { name = "continuation.movie_id", value = ReadString(objectiveContinuation, "movie_id") },
                            new { name = "continuation.movie_guest_name", value = ReadString(objectiveContinuation, "movie_guest_name") },
                            new { name = "continuation.movie_concession_id", value = ReadString(objectiveContinuation, "movie_concession_id") },
                            new { name = "continuation.movie_objective_key", value = ReadString(objectiveContinuation, "movie_objective_key") },
                            new { name = "continuation.movie_friendship_effective", value = ReadString(objectiveContinuation, "movie_friendship_effective") },
                            new { name = "continuation.movie_concession_friendship_effective", value = ReadString(objectiveContinuation, "movie_concession_friendship_effective") }
                        }.Where(parameter => !string.IsNullOrWhiteSpace(parameter.value)).ToArray()
                    }
                },
            include_blocked_options = false
        }, JsonOptions);
        var ranking = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", rankRequest);
        var resolvedGoalId = ranking["goal_resolution"]?[
            "effective_goal_id"]?.GetValue<string>();
        var effectiveGoalId = string.IsNullOrWhiteSpace(resolvedGoalId)
            ? options.Goal
            : resolvedGoalId;
        var rankedCandidates = ranking["ranked_event_candidates"]?.AsArray() ?? new JsonArray();
        var continuationCandidates =
            QueueReplanFilter.FilterRankedCandidates(
                rankedCandidates,
                objectiveContinuation);
        var effectiveCandidateKind =
            QueueReplanFilter.EffectiveCandidateKindFilter(
                options.DailyPlanCandidateKind,
                objectiveContinuation);
        var selectedCandidates =
            QueueReplanFilter.FilterCandidateKind(
                continuationCandidates,
                effectiveCandidateKind);
        var effectiveCandidateId =
            QueueReplanFilter.EffectiveCandidateIdFilter(
                options.DailyPlanCandidateId,
                objectiveContinuation);
        selectedCandidates =
            QueueReplanFilter.FilterCandidateId(
                selectedCandidates,
                effectiveCandidateId);
        var objectiveContinuationFilter = new JsonObject
        {
            ["active"] = objectiveContinuation is not null,
            ["objective"] = objectiveContinuation is null ? null : JsonNode.Parse(objectiveContinuation.ToJsonString(JsonOptions)),
            ["input_candidate_count"] = rankedCandidates.Count,
            ["selected_candidate_count"] = continuationCandidates.Count,
            ["policy"] = "typed_objective_identity_match;fail_closed_no_objective_switch"
        };
        ranking["objective_continuation_filter"] = objectiveContinuationFilter;
        ranking["social_continuation_filter"] = JsonNode.Parse(
            objectiveContinuationFilter.ToJsonString(JsonOptions));
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
