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
            execution_mode = "training_singleplayer"
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
            execution_mode = "training_singleplayer",
            actor = new
            {
                actor_id = "training_farmer.main",
                actor_type = "training_farmer",
                control_surface = "training_sandbox"
            },
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
        JsonObject? socialContinuation)
    {
        var rankRequest = JsonSerializer.Serialize(new
        {
            dataset_path = Path.GetFullPath(options.DatasetPath),
            state_hash = stateHash,
            candidate_option_ids = socialContinuation is null ? options.DailyPlanCandidateOptionIds : Array.Empty<string>(),
            candidates = socialContinuation is null
                ? Array.Empty<object>()
                : new object[]
                {
                    new
                    {
                        option_id = ReadString(socialContinuation, "option_id"),
                        parameters = new[]
                        {
                            new { name = "continuation.option_id", value = ReadString(socialContinuation, "option_id") },
                            new { name = "continuation.npc_name", value = ReadString(socialContinuation, "npc_name") },
                            new { name = "continuation.target_location", value = ReadString(socialContinuation, "target_location") },
                            new { name = "continuation.slot_index", value = ReadString(socialContinuation, "slot_index") },
                            new { name = "continuation.qualified_item_id", value = ReadString(socialContinuation, "qualified_item_id") }
                        }.Where(parameter => !string.IsNullOrWhiteSpace(parameter.value)).ToArray()
                    }
                },
            include_blocked_options = false
        }, JsonOptions);
        var ranking = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", rankRequest);
        var rankedCandidates = ranking["ranked_event_candidates"]?.AsArray() ?? new JsonArray();
        var selectedCandidates = QueueReplanFilter.FilterRankedCandidates(rankedCandidates, socialContinuation);
        ranking["social_continuation_filter"] = new JsonObject
        {
            ["active"] = socialContinuation is not null,
            ["objective"] = socialContinuation is null ? null : JsonNode.Parse(socialContinuation.ToJsonString(JsonOptions)),
            ["input_candidate_count"] = rankedCandidates.Count,
            ["selected_candidate_count"] = selectedCandidates.Count,
            ["policy"] = "same_option_and_npc_with_optional_gift_identity;fail_closed_no_objective_switch"
        };
        var compileRequest = JsonSerializer.Serialize(new
        {
            state_hash = stateHash,
            goal_id = options.Goal,
            execution_mode = "training_singleplayer",
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
            execution_mode = "training_singleplayer",
            actor = new
            {
                actor_id = "training_farmer.main",
                actor_type = "training_farmer",
                control_surface = "training_sandbox"
            },
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
