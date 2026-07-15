using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

var options = LiveTrainingOptions.Parse(args);
Directory.CreateDirectory(options.Root);
Directory.CreateDirectory(options.RunDir);
Directory.CreateDirectory(options.SnapshotDir);
Directory.CreateDirectory(Path.GetDirectoryName(options.DatasetPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(options.ProgressLogPath)!);

using var http = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30)
};
using var executorHttp = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(180)
};

AppendProgress(options, "start", 0, string.Empty, string.Empty, "concurrency=1 execution=" + options.ExecutionMode);

var rowsAppended = 0;
var verifiedActions = 0;
var attemptsStarted = 0;
var lastQueueId = string.Empty;
var lastStateHash = string.Empty;
JsonObject? lastTrainingReport = null;
JsonObject? lastPrediction = null;

for (var iteration = 1; iteration <= options.MaxAttempts && (options.RequiredVerifiedActions <= 0 || verifiedActions < options.RequiredVerifiedActions); iteration++)
{
    attemptsStarted++;
    var rawSnapshotJson = iteration == 1 && !string.IsNullOrWhiteSpace(options.SnapshotFile)
        ? await File.ReadAllTextAsync(options.SnapshotFile, Encoding.UTF8)
        : await http.GetStringAsync(options.BridgeSnapshotUrl);
    var beforeSnapshot = JsonNode.Parse(rawSnapshotJson)?.AsObject() ?? new JsonObject();
    var snapshotJson = beforeSnapshot.ToJsonString(JsonlOptions);
    var snapshotPath = Path.Combine(options.SnapshotDir, "before-snapshot-" + iteration.ToString("D4") + ".json");
    await File.WriteAllTextAsync(snapshotPath, snapshotJson, Encoding.UTF8);

    var ingest = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/snapshots", snapshotJson);
    lastStateHash = ReadString(ingest, "state_hash");

    var ready = await http.GetFromJsonAsync<JsonObject>(options.ReadyProbeUrl);
    if (ready is null || ready["ready"]?.GetValue<bool>() != true)
    {
        AppendProgress(options, "blocked", iteration, lastStateHash, string.Empty, "ready_probe_failed");
        continue;
    }

    var modelPlanPath = string.Empty;
    JsonObject queue;
    if (options.UseDailyPlan)
    {
        var dailyPlan = await BuildQueueFromDailyPlanAsync(http, options, lastStateHash);
        modelPlanPath = Path.Combine(options.SnapshotDir, "model-plan-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(modelPlanPath, dailyPlan.Plan.ToJsonString(JsonOptions), Encoding.UTF8);
        var dailyPlanPath = Path.Combine(options.SnapshotDir, "daily-plan-response-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(dailyPlanPath, dailyPlan.Response.ToJsonString(JsonOptions), Encoding.UTF8);
        var rankingPath = Path.Combine(options.SnapshotDir, "ranking-response-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(rankingPath, dailyPlan.Ranking.ToJsonString(JsonOptions), Encoding.UTF8);
        queue = dailyPlan.Queue;
    }
    else if (options.UsePlanOutput)
    {
        var modelPlanJson = BuildMovePlanRequest(options, lastStateHash);
        modelPlanPath = Path.Combine(options.SnapshotDir, "model-plan-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(modelPlanPath, modelPlanJson, Encoding.UTF8);
        queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/plan/action-queue/compile", modelPlanJson);
    }
    else
    {
        queue = await BuildQueueFromMockActionAsync(http, options, lastStateHash);
    }
    var queuePath = Path.Combine(options.SnapshotDir, "compiled-queue-" + iteration.ToString("D4") + ".json");
    await File.WriteAllTextAsync(queuePath, queue.ToJsonString(JsonOptions), Encoding.UTF8);
    lastQueueId = ReadString(queue, "queue_id");
    var queueStatus = ReadString(queue, "status");
    if (options.RequireExecutorFeedback)
    {
        if (!string.Equals(queueStatus, "pending", StringComparison.Ordinal))
        {
            AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, "queue_not_pending executor_feedback_required");
            continue;
        }

        var execution = options.UseRealRuntimeExecutor
            ? await ExecuteRealRuntimeAsync(http, executorHttp, options, iteration, snapshotPath, beforeSnapshot, queue, lastStateHash, lastQueueId)
            : await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/action-queues/" + Uri.EscapeDataString(lastQueueId) + "/execute-training-sandbox", "{}");
        var feedbackAvailable = execution["feedback_available"]?.GetValue<bool>() == true;
        if (!feedbackAvailable && !options.UseRealRuntimeExecutor)
        {
            AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, "executor_feedback_unavailable status=" + ReadString(execution, "status"));
            continue;
        }

        if (options.UseRealRuntimeExecutor)
        {
            var primitiveVerified = string.Equals(ReadString(execution, "primitive_verification_status"), "verified", StringComparison.Ordinal) &&
                string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal);
            if (!primitiveVerified)
            {
                AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, "real_runtime_unverified status=" + ReadString(execution, "status") + " primitive=" + ReadString(execution, "primitive_verification_status"));
                continue;
            }

            var realAppend = AppendRealExecutionRow(options, beforeSnapshot, queue, execution, lastStateHash, lastQueueId);
            WritePlanExecutionEpisode(options, iteration, snapshotPath, modelPlanPath, queuePath, queue, execution, realAppend, lastStateHash, lastQueueId);
            rowsAppended = realAppend.RowCount;
            verifiedActions++;
            AppendProgress(options, "append", iteration, lastStateHash, lastQueueId, "dataset_rows=" + rowsAppended + " verified_actions=" + verifiedActions + " required_verified_actions=" + options.RequiredVerifiedActions + " source=real_runtime_executor");
            var train = await TrainIfNeededAsync(http, options, iteration);
            if (train.TrainingReport is not null)
            {
                lastTrainingReport = train.TrainingReport;
                lastPrediction = train.Prediction;
            }
            continue;
        }
    }

    var appendRequest = JsonSerializer.Serialize(new
    {
        dataset_path = Path.GetFullPath(options.DatasetPath)
    }, JsonOptions);
    var append = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/action-queues/" + Uri.EscapeDataString(lastQueueId) + "/training-feature-row/append", appendRequest);
    rowsAppended = append["row_count"]?.GetValue<int>() ?? rowsAppended;
    AppendProgress(options, "append", iteration, lastStateHash, lastQueueId, "dataset_rows=" + rowsAppended);

    if (iteration % options.TrainEvery == 0 || iteration == options.MaxAttempts)
    {
        var trainRequest = JsonSerializer.Serialize(new
        {
            dataset_path = Path.GetFullPath(options.DatasetPath)
        }, JsonOptions);
        lastTrainingReport = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/training/baseline/train", trainRequest);
        lastPrediction = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", trainRequest);
        var bestOption = lastPrediction["ranked_options"]?[0]?["option_id"]?.GetValue<string>() ?? string.Empty;
        AppendProgress(options, "train", iteration, lastStateHash, lastQueueId, "best_option=" + bestOption);
    }

    if (options.SleepMs > 0 && iteration < options.MaxAttempts)
    {
        await Task.Delay(options.SleepMs);
    }
}

static async Task<JsonObject> BuildQueueFromMockActionAsync(HttpClient http, LiveTrainingOptions options, string stateHash)
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

static async Task<(JsonObject Response, JsonObject Plan, JsonObject Queue, JsonObject Ranking)> BuildQueueFromDailyPlanAsync(
    HttpClient http,
    LiveTrainingOptions options,
    string stateHash)
{
    var rankRequest = JsonSerializer.Serialize(new
    {
        dataset_path = Path.GetFullPath(options.DatasetPath),
        state_hash = stateHash,
        candidate_option_ids = options.DailyPlanCandidateOptionIds,
        include_blocked_options = false
    }, JsonOptions);
    var ranking = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", rankRequest);
    var rankedCandidates = ranking["ranked_event_candidates"]?.AsArray() ?? new JsonArray();
    var compileRequest = JsonSerializer.Serialize(new
    {
        state_hash = stateHash,
        goal_id = options.Goal,
        execution_mode = "training_singleplayer",
        max_candidates = options.DailyPlanMaxCandidates,
        compile_action_queue = true,
        ranked_event_candidates = JsonNode.Parse(rankedCandidates.ToJsonString(JsonOptions))
    }, JsonOptions);
    var response = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/daily-plan/compile", compileRequest);
    var plan = response["plan"]?.AsObject() ?? throw new InvalidOperationException("daily plan response did not include plan");
    var queue = response["action_queue"]?.AsObject() ?? throw new InvalidOperationException("daily plan response did not include action_queue");
    return (response, plan, queue, ranking);
}

static string BuildMovePlanRequest(LiveTrainingOptions options, string stateHash)
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

var verifiedTargetMet = options.RequiredVerifiedActions <= 0 || verifiedActions >= options.RequiredVerifiedActions;
var loopStatus = verifiedTargetMet ? "ok" : "incomplete";
if (!verifiedTargetMet)
{
    Environment.ExitCode = 2;
}

var report = new LiveTrainingLoopReport
{
    RunId = options.RunId,
    ManifestPath = options.ManifestPath,
    BackendUrl = options.BackendUrl,
    BridgeSnapshotUrl = options.BridgeSnapshotUrl,
    SnapshotFile = options.SnapshotFile,
    DatasetPath = options.DatasetPath,
    ProgressLogPath = options.ProgressLogPath,
    SnapshotDir = options.SnapshotDir,
    Iterations = options.MaxAttempts,
    MaxAttempts = options.MaxAttempts,
    AttemptsStarted = attemptsStarted,
    RowsAppended = rowsAppended,
    VerifiedActions = verifiedActions,
    RequiredVerifiedActions = options.RequiredVerifiedActions,
    LastStateHash = lastStateHash,
    LastQueueId = lastQueueId,
    Concurrency = 1,
    Execution = options.ExecutionMode,
    ExecutorFeedbackRequired = options.RequireExecutorFeedback,
    TrainingReport = lastTrainingReport,
    Prediction = lastPrediction
};

var reportPath = Path.Combine(options.RunDir, "live-training-loop-report.json");
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), Encoding.UTF8);
AppendProgress(options, verifiedTargetMet ? "complete" : "incomplete", attemptsStarted, lastStateHash, lastQueueId, "report=" + reportPath + " verified_actions=" + verifiedActions + " required_verified_actions=" + options.RequiredVerifiedActions + " max_attempts=" + options.MaxAttempts);

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = loopStatus,
    run_id = options.RunId,
    iterations = options.MaxAttempts,
    max_attempts = options.MaxAttempts,
    attempts_started = attemptsStarted,
    rows_appended = rowsAppended,
    verified_actions = verifiedActions,
    required_verified_actions = options.RequiredVerifiedActions,
    dataset_path = options.DatasetPath,
    report_path = reportPath,
    progress_log_path = options.ProgressLogPath,
    concurrency = 1,
    execution = options.ExecutionMode,
    executor_feedback_required = options.RequireExecutorFeedback
}, JsonOptions));

static async Task<JsonObject> ExecuteRealRuntimeAsync(
    HttpClient http,
    HttpClient executorHttp,
    LiveTrainingOptions options,
    int iteration,
    string beforeSnapshotPath,
    JsonObject beforeSnapshot,
    JsonObject queue,
    string stateHash,
    string queueId)
{
    var queueItems = ExecutableQueueItems(queue);
    if (!string.IsNullOrWhiteSpace(options.ExecutorOptionId))
    {
        queueItems = queueItems.Take(1).ToArray();
    }
    if (queueItems.Length == 0)
    {
        throw new InvalidOperationException("compiled queue did not include executable queue items");
    }

    var aggregateExecutionPath = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
    var aggregateAfterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
    var stepResults = new JsonArray();
    var currentBeforeSnapshot = beforeSnapshot;
    var currentBeforeSnapshotPath = beforeSnapshotPath;
    var currentStateHash = stateHash;
    var originalPlannedItemCount = queueItems.Length;
    var finalAfterJson = beforeSnapshot.ToJsonString(JsonOptions);
    JsonObject? finalExecution = null;
    JsonObject finalAfterSnapshot = beforeSnapshot;
    var attemptedCount = 0;
    var attemptedSemanticKeys = new HashSet<string>(StringComparer.Ordinal);

    for (var itemIndex = 0; itemIndex < queueItems.Length && attemptedCount < options.MaxQueueItemAttempts; itemIndex++)
    {
        var item = queueItems[itemIndex];
        var itemSemanticKey = QueueReplanFilter.SemanticQueueItemKey(item);
        var effectiveBeforeSnapshot = currentBeforeSnapshot;
        var effectiveStateHash = currentStateHash;
        var executionRequest = BuildExecutionRequest(options, item, currentStateHash, queueId);
        var request = JsonSerializer.Serialize(executionRequest, JsonOptions);
        var execution = await PostJsonStringAsync(executorHttp, options.ExecutorUrl + "/api/v1/training/execute", request);
        attemptedCount++;

        var afterSnapshot = await ReadAfterExecutionSnapshotAsync(http, options, currentBeforeSnapshot);
        finalAfterJson = afterSnapshot.Json;
        finalAfterSnapshot = afterSnapshot.Snapshot;
        var itemSuffix = "-item-" + attemptedCount.ToString("D4");
        var executionPath = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + itemSuffix + ".json");
        var afterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + itemSuffix + ".json");
        await File.WriteAllTextAsync(afterPath, finalAfterJson, Encoding.UTF8);
        await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/snapshots", finalAfterJson);

        execution["queue_execution_mode"] = "sequential_queue_items";
        execution["queue_item_index"] = itemIndex;
        execution["queue_item_count"] = queueItems.Length;
        execution["queue_original_planned_item_count"] = originalPlannedItemCount;
        execution["queue_item_semantic_key"] = itemSemanticKey;
        execution["effective_queue_id"] = executionRequest.QueueId;
        execution["effective_queue_item"] = JsonNode.Parse(item.ToJsonString(JsonOptions));
        execution["effective_before_state_hash"] = effectiveStateHash;
        execution["effective_before_snapshot_path"] = currentBeforeSnapshotPath;
        execution["effective_before_snapshot"] = JsonNode.Parse(effectiveBeforeSnapshot.ToJsonString(JsonOptions));
        execution["queue_continue_after_blocked"] = options.ContinueAfterBlockedQueueItems;
        execution["after_snapshot_path"] = afterPath;
        execution["execution_path"] = executionPath;
        execution["after_state_hash"] = ReadString(afterSnapshot.Snapshot, "state_hash");
        execution["before_game_tick"] = ReadLong(currentBeforeSnapshot, "game_tick");
        execution["after_game_tick"] = ReadLong(afterSnapshot.Snapshot, "game_tick");
        execution["state_hash_changed"] = !string.Equals(currentStateHash, ReadString(afterSnapshot.Snapshot, "state_hash"), StringComparison.Ordinal);
        execution["after_snapshot_fresh"] = afterSnapshot.Fresh;
        execution["after_snapshot_note"] = afterSnapshot.Note;
        if (string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal) && !afterSnapshot.Fresh)
        {
            execution["primitive_verification_status"] = "stale_after_snapshot";
            execution["primitive_verification_reasons"] = new JsonArray("after_snapshot_not_fresh");
        }
        execution["source"] = "real_runtime_executor";
        currentBeforeSnapshot = afterSnapshot.Snapshot;
        currentBeforeSnapshotPath = afterPath;
        currentStateHash = ReadString(afterSnapshot.Snapshot, "state_hash");
        attemptedSemanticKeys.Add(itemSemanticKey);

        var executionStatus = ReadString(execution, "status");

        var replanDecision = QueueReplanFilter.DecideAfterExecution(
            executionStatus,
            options.ContinueAfterBlockedQueueItems,
            options.UseDailyPlan,
            !string.IsNullOrWhiteSpace(options.ExecutorOptionId),
            afterSnapshot.Fresh,
            attemptedCount < options.MaxQueueItemAttempts);

        if (replanDecision.ShouldStop)
        {
            execution["queue_replan_applied"] = false;
            execution["queue_replan_stop_reason"] = replanDecision.Reason;
            await File.WriteAllTextAsync(executionPath, execution.ToJsonString(JsonOptions), Encoding.UTF8);
            stepResults.Add(JsonNode.Parse(execution.ToJsonString(JsonOptions)));
            finalExecution = execution;
            break;
        }

        if (replanDecision.ShouldReplan)
        {
            var replan = await BuildQueueFromDailyPlanAsync(http, options, currentStateHash);
            var replanSuffix = "-item-" + (attemptedCount + 1).ToString("D4");
            var replanPlanPath = Path.Combine(options.SnapshotDir, "replan-model-plan-" + iteration.ToString("D4") + replanSuffix + ".json");
            var replanDailyPlanPath = Path.Combine(options.SnapshotDir, "replan-daily-plan-response-" + iteration.ToString("D4") + replanSuffix + ".json");
            var replanQueuePath = Path.Combine(options.SnapshotDir, "replan-compiled-queue-" + iteration.ToString("D4") + replanSuffix + ".json");
            var replanRankingPath = Path.Combine(options.SnapshotDir, "replan-ranking-response-" + iteration.ToString("D4") + replanSuffix + ".json");
            await File.WriteAllTextAsync(replanPlanPath, replan.Plan.ToJsonString(JsonOptions), Encoding.UTF8);
            await File.WriteAllTextAsync(replanDailyPlanPath, replan.Response.ToJsonString(JsonOptions), Encoding.UTF8);
            await File.WriteAllTextAsync(replanQueuePath, replan.Queue.ToJsonString(JsonOptions), Encoding.UTF8);
            await File.WriteAllTextAsync(replanRankingPath, replan.Ranking.ToJsonString(JsonOptions), Encoding.UTF8);

            queue = replan.Queue;
            queueId = ReadString(queue, "queue_id");
            var replanItems = ExecutableQueueItems(queue);
            var replanItemsBeforeFiltering = replanItems.Length;
            queueItems = QueueReplanFilter.FilterUnattempted(replanItems, attemptedSemanticKeys);
            execution["queue_replan_applied"] = true;
            execution["queue_replan_trigger_status"] = executionStatus;
            execution["queue_replan_trigger_reason"] = replanDecision.Reason;
            execution["queue_replan_source_state_hash"] = currentStateHash;
            execution["queue_replan_previous_queue_id"] = executionRequest.QueueId;
            execution["queue_replan_queue_id"] = queueId;
            execution["queue_replan_trigger_queue_item_id"] = executionRequest.QueueItemId;
            execution["queue_replan_trigger_semantic_key"] = itemSemanticKey;
            execution["queue_replan_remaining_before_filter"] = replanItemsBeforeFiltering;
            execution["queue_replan_remaining_after_filter"] = queueItems.Length;
            execution["queue_replan_attempted_semantic_key_count"] = attemptedSemanticKeys.Count;
            execution["queue_replan_item_count"] = queueItems.Length;
            execution["queue_replan_plan_path"] = replanPlanPath;
            execution["queue_replan_response_path"] = replanDailyPlanPath;
            execution["queue_replan_queue_path"] = replanQueuePath;
            execution["queue_replan_ranking_path"] = replanRankingPath;
            itemIndex = -1;
        }
        else
        {
            execution["queue_replan_applied"] = false;
            execution["queue_replan_skip_reason"] = replanDecision.Reason;
        }

        await File.WriteAllTextAsync(executionPath, execution.ToJsonString(JsonOptions), Encoding.UTF8);
        stepResults.Add(JsonNode.Parse(execution.ToJsonString(JsonOptions)));
        finalExecution = execution;
    }

    await File.WriteAllTextAsync(aggregateAfterPath, finalAfterJson, Encoding.UTF8);
    var aggregate = JsonNode.Parse((finalExecution ?? new JsonObject()).ToJsonString(JsonOptions))?.AsObject() ?? new JsonObject();
    aggregate["queue_execution_mode"] = "sequential_queue_items";
    aggregate["planned_item_count"] = originalPlannedItemCount;
    aggregate["final_pending_item_count"] = queueItems.Length;
    aggregate["executed_item_count"] = attemptedCount;
    aggregate["max_queue_item_attempts"] = options.MaxQueueItemAttempts;
    aggregate["step_results"] = stepResults;
    aggregate["after_snapshot_path"] = aggregateAfterPath;
    aggregate["execution_path"] = aggregateExecutionPath;
    aggregate["after_state_hash"] = ReadString(finalAfterSnapshot, "state_hash");
    aggregate["before_game_tick"] = ReadLong(beforeSnapshot, "game_tick");
    aggregate["after_game_tick"] = ReadLong(finalAfterSnapshot, "game_tick");
    aggregate["state_hash_changed"] = !string.Equals(stateHash, ReadString(finalAfterSnapshot, "state_hash"), StringComparison.Ordinal);
    aggregate["source"] = "real_runtime_executor";
    await File.WriteAllTextAsync(aggregateExecutionPath, aggregate.ToJsonString(JsonOptions), Encoding.UTF8);
    return aggregate;
}

static TrainingExecutionRequest BuildExecutionRequest(
    LiveTrainingOptions options,
    JsonObject? item,
    string stateHash,
    string queueId)
{
    var compiledExecutionOptionId = ReadQueueParameterString(item, "execution_option_id");
    var optionId = string.IsNullOrWhiteSpace(options.ExecutorOptionId)
        ? string.IsNullOrWhiteSpace(compiledExecutionOptionId) ? ReadStringOrEmpty(item, "option_id") : compiledExecutionOptionId
        : options.ExecutorOptionId;
    var queueItemId = ReadStringOrEmpty(item, "queue_item_id");
    if (string.IsNullOrWhiteSpace(queueItemId))
    {
        throw new InvalidOperationException("compiled queue did not include queue_item_id");
    }

    var executionRequest = new TrainingExecutionRequest
    {
        RunId = options.RunId,
        QueueId = queueId,
        QueueItemId = queueItemId,
        BeforeStateHash = stateHash,
        OptionId = optionId,
        ExecutionMode = "training_singleplayer",
        Actor = "training_farmer.main",
        SaveIsolationPath = options.SaveIsolationPath,
        RequestNonce = Guid.NewGuid().ToString("N"),
        CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
        MaxCrops = options.MaxCropsPerExecution
    };

    var targetTileX = options.TargetTileX ?? ReadQueueParameterInt(item, "target_tile_x");
    var targetTileY = options.TargetTileY ?? ReadQueueParameterInt(item, "target_tile_y");
    var targetRuntimeType = ReadQueueParameterString(item, "target_runtime_type");
    var targetRuntimeIdentity = ReadQueueParameterString(item, "target_runtime_identity");
    var targetName = ReadQueueParameterString(item, "target_name");
    var maxAttacks = ReadQueueParameterInt(item, "max_attacks");
    var direction = options.Direction ?? ReadQueueParameterInt(item, "direction");
    var waitTicks = options.WaitTicks ?? ReadQueueParameterInt(item, "wait_ticks");
    var maxCrops = ReadQueueParameterInt(item, "max_crops") ?? ReadQueueParameterInt(item, "max_tool_swings");
    var maxMovementTiles = ReadQueueParameterInt(item, "max_movement_tiles");
    var safeSlotIndex = ReadQueueParameterInt(item, "safe_slot_index");
    var interactionKind = ReadQueueParameterString(item, "interaction_kind");
    var expectedActionType = ReadQueueParameterString(item, "expected_action_type");
    var connectorKind = ReadQueueParameterString(item, "connector_kind");
    var expectedTargetLocation = ReadQueueParameterString(item, "expected_target_location");
    var expectedArrivalTileX = ReadQueueParameterInt(item, "expected_arrival_tile_x");
    var expectedArrivalTileY = ReadQueueParameterInt(item, "expected_arrival_tile_y");
    var shopItemId = ReadQueueParameterString(item, "shop_item_id");
    var qualifiedItemId = ReadQueueParameterString(item, "qualified_item_id");
    var quantity = ReadQueueParameterInt(item, "quantity");
    var maxUnitPrice = ReadQueueParameterInt(item, "max_unit_price");
    var expectedShopId = ReadQueueParameterString(item, "expected_shop_id");
    var expectedDialogueKey = ReadQueueParameterString(item, "expected_dialogue_key");
    var dialogueResponseKey = ReadQueueParameterString(item, "dialogue_response_key");
    var seedId = ReadQueueParameterString(item, "seed_id");
    var harvestMethod = ReadQueueParameterString(item, "harvest_method");
    var giantCropId = ReadQueueParameterString(item, "giant_crop_id");
    var debrisIndex = ReadQueueParameterInt(item, "debris_index");
    var inputSlotIndex = ReadQueueParameterInt(item, "input_slot_index");
    var slotIndex = ReadQueueParameterInt(item, "slot_index");
    var fishingLocationId = ReadQueueParameterString(item, "location_id");
    var fishingStandTileX = ReadQueueParameterInt(item, "stand_tile_x");
    var fishingStandTileY = ReadQueueParameterInt(item, "stand_tile_y");
    var fishingBobberTileX = ReadQueueParameterInt(item, "bobber_tile_x");
    var fishingBobberTileY = ReadQueueParameterInt(item, "bobber_tile_y");
    var fishingRodSlotIndex = ReadQueueParameterInt(item, "rod_slot_index");
    var fishingRuleKey = ReadQueueParameterString(item, "rule_key");
    var fishingExpectedQualifiedItemId = ReadQueueParameterString(item, "expected_qualified_item_id");
    var fishingOutcomeDistributionComplete = bool.TryParse(ReadQueueParameterString(item, "outcome_distribution_complete"), out var parsedFishingDistributionComplete) && parsedFishingDistributionComplete;
    var fishingOutcomeDistributionJson = ReadQueueParameterString(item, "outcome_distribution_json");
    var fishingPossibleQualifiedItemIdsJson = ReadQueueParameterString(item, "possible_qualified_item_ids_json");
    var fishingOutcomeProbabilityStatus = ReadQueueParameterString(item, "outcome_probability_status");
    if (targetTileX.HasValue && targetTileY.HasValue)
    {
        executionRequest.TargetTileX = targetTileX.Value;
        executionRequest.TargetTileY = targetTileY.Value;
    }
    if (!string.IsNullOrWhiteSpace(targetRuntimeType))
    {
        executionRequest.TargetRuntimeType = targetRuntimeType;
    }
    if (!string.IsNullOrWhiteSpace(targetRuntimeIdentity))
    {
        executionRequest.TargetRuntimeIdentity = targetRuntimeIdentity;
    }
    if (!string.IsNullOrWhiteSpace(targetName))
    {
        executionRequest.TargetName = targetName;
    }
    if (maxAttacks.HasValue)
    {
        executionRequest.MaxAttacks = maxAttacks.Value;
    }
    if (direction.HasValue)
    {
        executionRequest.Direction = direction.Value;
    }
    if (waitTicks.HasValue)
    {
        executionRequest.WaitTicks = waitTicks.Value;
    }
    if (maxCrops.HasValue)
    {
        executionRequest.MaxCrops = maxCrops.Value;
    }
    if (maxMovementTiles.HasValue)
    {
        executionRequest.MaxMovementTiles = maxMovementTiles.Value;
    }
    if (safeSlotIndex.HasValue)
    {
        executionRequest.SafeSlotIndex = safeSlotIndex.Value;
    }
    if (!string.IsNullOrWhiteSpace(interactionKind))
    {
        executionRequest.InteractionKind = interactionKind;
    }
    if (!string.IsNullOrWhiteSpace(expectedActionType))
    {
        executionRequest.ExpectedActionType = expectedActionType;
    }
    if (!string.IsNullOrWhiteSpace(connectorKind))
    {
        executionRequest.ConnectorKind = connectorKind;
    }
    if (!string.IsNullOrWhiteSpace(expectedTargetLocation))
    {
        executionRequest.ExpectedTargetLocation = expectedTargetLocation;
    }
    if (expectedArrivalTileX.HasValue && expectedArrivalTileY.HasValue)
    {
        executionRequest.ExpectedArrivalTileX = expectedArrivalTileX.Value;
        executionRequest.ExpectedArrivalTileY = expectedArrivalTileY.Value;
    }
    if (!string.IsNullOrWhiteSpace(shopItemId))
    {
        executionRequest.ShopItemId = shopItemId;
    }
    if (!string.IsNullOrWhiteSpace(qualifiedItemId))
    {
        executionRequest.QualifiedItemId = qualifiedItemId;
    }
    if (quantity.HasValue)
    {
        executionRequest.Quantity = quantity.Value;
    }
    if (maxUnitPrice.HasValue)
    {
        executionRequest.MaxUnitPrice = maxUnitPrice.Value;
    }
    if (!string.IsNullOrWhiteSpace(expectedShopId))
    {
        executionRequest.ExpectedShopId = expectedShopId;
    }
    if (!string.IsNullOrWhiteSpace(expectedDialogueKey))
    {
        executionRequest.ExpectedDialogueKey = expectedDialogueKey;
    }
    if (!string.IsNullOrWhiteSpace(dialogueResponseKey))
    {
        executionRequest.DialogueResponseKey = dialogueResponseKey;
    }
    if (!string.IsNullOrWhiteSpace(seedId))
    {
        executionRequest.SeedId = seedId;
    }
    if (!string.IsNullOrWhiteSpace(harvestMethod))
    {
        executionRequest.HarvestMethod = harvestMethod;
    }
    if (!string.IsNullOrWhiteSpace(giantCropId))
    {
        executionRequest.GiantCropId = giantCropId;
    }
    if (debrisIndex.HasValue)
    {
        executionRequest.DebrisIndex = debrisIndex.Value;
    }
    if (inputSlotIndex.HasValue)
    {
        executionRequest.InputSlotIndex = inputSlotIndex.Value;
    }
    if (slotIndex.HasValue)
    {
        executionRequest.SlotIndex = slotIndex.Value;
    }
    if (!string.IsNullOrWhiteSpace(fishingLocationId))
    {
        executionRequest.LocationId = fishingLocationId;
    }
    var socialTargetLocation = SocialLocationMapping.ResolveLocationId(item, optionId);
    if (!string.IsNullOrWhiteSpace(socialTargetLocation))
    {
        executionRequest.LocationId = socialTargetLocation;
    }
    if (fishingStandTileX.HasValue && fishingStandTileY.HasValue)
    {
        executionRequest.StandTileX = fishingStandTileX.Value;
        executionRequest.StandTileY = fishingStandTileY.Value;
    }
    if (fishingBobberTileX.HasValue && fishingBobberTileY.HasValue)
    {
        executionRequest.BobberTileX = fishingBobberTileX.Value;
        executionRequest.BobberTileY = fishingBobberTileY.Value;
    }
    if (fishingRodSlotIndex.HasValue)
    {
        executionRequest.RodSlotIndex = fishingRodSlotIndex.Value;
    }
    if (!string.IsNullOrWhiteSpace(fishingRuleKey))
    {
        executionRequest.RuleKey = fishingRuleKey;
    }
    if (!string.IsNullOrWhiteSpace(fishingExpectedQualifiedItemId))
    {
        executionRequest.ExpectedQualifiedItemId = fishingExpectedQualifiedItemId;
    }
    executionRequest.OutcomeDistributionComplete = fishingOutcomeDistributionComplete;
    if (!string.IsNullOrWhiteSpace(fishingOutcomeDistributionJson))
    {
        executionRequest.OutcomeDistributionJson = fishingOutcomeDistributionJson;
    }
    if (!string.IsNullOrWhiteSpace(fishingPossibleQualifiedItemIdsJson))
    {
        executionRequest.PossibleQualifiedItemIdsJson = fishingPossibleQualifiedItemIdsJson;
    }
    if (!string.IsNullOrWhiteSpace(fishingOutcomeProbabilityStatus))
    {
        executionRequest.OutcomeProbabilityStatus = fishingOutcomeProbabilityStatus;
    }

    var socialNpcName = ReadQueueParameterString(item, "npc_name");
    var socialActionKind = ReadQueueParameterString(item, "social_action_kind");
    var socialObservedNpcTileX = ReadQueueParameterInt(item, "npc_tile_x");
    var socialObservedNpcTileY = ReadQueueParameterInt(item, "npc_tile_y");
    var socialGiftSlotIndex = ReadQueueParameterInt(item, "slot_index");
    var socialGiftQualifiedItemId = ReadQueueParameterString(item, "qualified_item_id");
    var socialExpectedFriendshipDelta = ReadQueueParameterString(item, "expected_friendship_delta");
    var socialExpectedTalkedToTodayBefore = ReadQueueParameterString(item, "expected_talked_to_today_before");
    if (!string.IsNullOrWhiteSpace(socialNpcName))
    {
        executionRequest.SocialNpcName = socialNpcName;
    }
    if (!string.IsNullOrWhiteSpace(socialActionKind))
    {
        executionRequest.SocialActionKind = socialActionKind;
    }
    if (socialObservedNpcTileX.HasValue && socialObservedNpcTileY.HasValue)
    {
        executionRequest.SocialObservedNpcTileX = socialObservedNpcTileX.Value;
        executionRequest.SocialObservedNpcTileY = socialObservedNpcTileY.Value;
    }
    if (socialGiftSlotIndex.HasValue)
    {
        executionRequest.SocialGiftSlotIndex = socialGiftSlotIndex.Value;
    }
    if (!string.IsNullOrWhiteSpace(socialGiftQualifiedItemId))
    {
        executionRequest.SocialGiftQualifiedItemId = socialGiftQualifiedItemId;
    }
    if (!string.IsNullOrWhiteSpace(socialExpectedFriendshipDelta))
    {
        executionRequest.SocialExpectedFriendshipDelta = socialExpectedFriendshipDelta;
    }
    if (!string.IsNullOrWhiteSpace(socialExpectedTalkedToTodayBefore))
    {
        executionRequest.SocialExpectedTalkedToTodayBefore = bool.TryParse(socialExpectedTalkedToTodayBefore, out var parsedTalked) && parsedTalked;
    }

    return executionRequest;
}

static bool IsExecutableQueueItem(JsonObject? item)
{
    var status = ReadStringOrEmpty(item, "status");
    return string.IsNullOrWhiteSpace(status) || string.Equals(status, "pending", StringComparison.Ordinal);
}

static JsonObject[] ExecutableQueueItems(JsonObject queue)
{
    return (queue["items"] as JsonArray)?
        .Select(node => node?.AsObject())
        .Where(item => item is not null && IsExecutableQueueItem(item))
        .Cast<JsonObject>()
        .ToArray() ?? Array.Empty<JsonObject>();
}

static async Task<(string Json, JsonObject Snapshot, bool Fresh, string Note)> ReadAfterExecutionSnapshotAsync(
    HttpClient http,
    LiveTrainingOptions options,
    JsonObject beforeSnapshot)
{
    var beforeHash = ReadString(beforeSnapshot, "state_hash");
    var beforeTick = ReadLong(beforeSnapshot, "game_tick");
    JsonObject latest = new();
    var latestJson = "{}";
    var deadline = DateTimeOffset.UtcNow.AddMilliseconds(options.AfterSnapshotWaitMs);

    do
    {
        latestJson = await http.GetStringAsync(options.BridgeSnapshotUrl);
        latest = JsonNode.Parse(latestJson)?.AsObject() ?? new JsonObject();
        var latestHash = ReadString(latest, "state_hash");
        var latestTick = ReadLong(latest, "game_tick");
        if (!string.Equals(latestHash, beforeHash, StringComparison.Ordinal))
        {
            return (latestJson, latest, true, "state_hash_changed");
        }

        if (latestTick > beforeTick)
        {
            return (latestJson, latest, true, "game_tick_advanced_without_hash_change");
        }

        await Task.Delay(options.AfterSnapshotPollMs);
    }
    while (DateTimeOffset.UtcNow < deadline);

    return (latestJson, latest, false, "after_snapshot_wait_timed_out_same_hash_and_tick");
}

static TrainingDatasetAppendResult AppendRealExecutionRow(
    LiveTrainingOptions options,
    JsonObject beforeSnapshot,
    JsonObject queue,
    JsonObject execution,
    string stateHash,
    string queueId)
{
    var item = execution["effective_queue_item"]?.AsObject() ?? FindQueueItemForExecution(queue, execution) ?? queue["items"]?.AsArray().FirstOrDefault()?.AsObject();
    var effectiveBeforeSnapshot = execution["effective_before_snapshot"]?.AsObject() ?? beforeSnapshot;
    var effectiveStateHash = ReadString(execution, "effective_before_state_hash");
    if (string.IsNullOrWhiteSpace(effectiveStateHash))
    {
        effectiveStateHash = stateHash;
    }
    var optionId = string.IsNullOrWhiteSpace(options.ExecutorOptionId)
        ? ReadStringOrEmpty(execution, "option_id")
        : options.ExecutorOptionId;
    if (string.IsNullOrWhiteSpace(optionId))
    {
        optionId = ReadStringOrEmpty(item, "option_id");
    }
    var watered = ReadInt(execution, "watered_count");
    var energyBefore = ReadDouble(execution, "energy_before");
    var energyAfter = ReadDouble(execution, "energy_after");
    var energyCost = Math.Max(0, energyBefore - energyAfter);
    var targetTileX = options.TargetTileX ?? ReadQueueParameterInt(item, "target_tile_x");
    var targetTileY = options.TargetTileY ?? ReadQueueParameterInt(item, "target_tile_y");
    var direction = options.Direction ?? ReadQueueParameterInt(item, "direction");
    var waitTicks = options.WaitTicks ?? ReadQueueParameterInt(item, "wait_ticks");
    var isMove = string.Equals(optionId, "executor.move_to_tile", StringComparison.Ordinal) ||
        string.Equals(optionId, "debug.visible_walk", StringComparison.Ordinal);
    var isPrimitive = isMove ||
        string.Equals(optionId, "executor.face_direction", StringComparison.Ordinal) ||
        string.Equals(optionId, "executor.wait_ticks", StringComparison.Ordinal);
    var applied = string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal);
    var reward = isMove
        ? applied ? 0.05 : -0.05
        : isPrimitive ? applied ? 0.02 : -0.02
        : Math.Round(watered * 0.10 - energyCost * 0.005, 4);
    var blocked = !string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal) &&
        !string.Equals(ReadString(execution, "status"), "no_op", StringComparison.Ordinal);
    var requiredMinutes = isMove ? 1 : 30;
    var primitiveVerificationStatus = ReadString(execution, "primitive_verification_status");
    var primitiveVerified = string.Equals(primitiveVerificationStatus, "verified", StringComparison.Ordinal);
    var failureCategory = ReadString(execution, "failure_category");
    var afterSnapshotFresh = execution["after_snapshot_fresh"]?.GetValue<bool>() == true;
    var stateHashChanged = execution["state_hash_changed"]?.GetValue<bool>() == true;
    var tickDelta = Math.Max(0, ReadLong(execution, "after_game_tick") - ReadLong(execution, "before_game_tick"));

    var row = new TrainingFeatureRowEnvelope
    {
        RowId = "feature-row." + Guid.NewGuid().ToString("N"),
        EpisodeId = "episode.real." + Guid.NewGuid().ToString("N"),
        SourceStateHash = effectiveStateHash,
        QueueId = ReadString(execution, "effective_queue_id") is { Length: > 0 } effectiveQueueId ? effectiveQueueId : queueId,
        StateFeatures = BuildStateFeatures(effectiveBeforeSnapshot),
        ActionFeatures = new ActionFeatureVector
        {
            OptionIds = new[] { optionId },
            TrainingRole = TrainingRoles.ExecutorCalibration,
            LearningScope = "calibration_only",
            ExcludeFromPolicyTraining = true,
            NormalizedParameters = item?["normalized_command"]?["parameters"] is JsonNode normalizedParameters
                ? JsonSerializer.Deserialize<SmallModelActionParameter[]>(normalizedParameters.ToJsonString(), JsonOptions) ?? Array.Empty<SmallModelActionParameter>()
                : Array.Empty<SmallModelActionParameter>(),
            PrimitiveVerificationReasons = ReadArrayStrings(execution, "primitive_verification_reasons"),
            RequestedEffect = ReadString(execution, "requested_effect"),
            ObservedEffect = ReadString(execution, "observed_effect"),
            ChangedFacts = execution["changed_facts"] is JsonNode changedFacts
                ? JsonSerializer.Deserialize<SimulatedFactChange[]>(changedFacts.ToJsonString(), JsonOptions) ?? Array.Empty<SimulatedFactChange>()
                : Array.Empty<SimulatedFactChange>(),
            Features = new FeatureVector
            {
                Numeric = new[]
                {
                    Number("action.option_count", 1),
                    Number("action.required_minutes", requiredMinutes),
                    Number("action.optional_minutes", 0),
                    Number("action.target_tile_x", targetTileX ?? -1),
                    Number("action.target_tile_y", targetTileY ?? -1),
                    Number("action.direction", direction ?? -1),
                    Number("action.wait_ticks", waitTicks ?? 0),
                    Number("execution.moved_tile", isMove && applied ? 1 : 0),
                    Number("execution.after_snapshot_fresh", afterSnapshotFresh ? 1 : 0),
                    Number("execution.state_hash_changed", stateHashChanged ? 1 : 0),
                    Number("execution.tick_delta", tickDelta),
                    Number("execution.water_before", ReadInt(execution, "water_before")),
                    Number("execution.water_after", ReadInt(execution, "water_after")),
                    Number("execution.estimated_ticks", ReadInt(execution, "estimated_ticks")),
                    Number("execution.actual_ticks", ReadInt(execution, "actual_ticks")),
                    Number("combat.attack_count", ReadInt(execution, "combat_attack_count")),
                    Number("combat.hit_count", ReadInt(execution, "combat_hit_count")),
                    Number("combat.damage_taken", ReadInt(execution, "combat_damage_taken")),
                    Number("fishing.target_casting_power", ReadChangedFactDouble(execution, "fishing.target_casting_power")),
                    Number("fishing.observed_peak_casting_power", ReadChangedFactDouble(execution, "fishing.observed_peak_casting_power")),
                    Number("fishing.observed_release_casting_power", ReadChangedFactDouble(execution, "fishing.observed_release_casting_power")),
                    Number("fishing.hook_attempt_count", ReadChangedFactDouble(execution, "fishing.hook_attempt_count")),
                    Number("fishing.bobber_bar_tick_count", ReadChangedFactDouble(execution, "fishing.bobber_bar_tick_count")),
                    Number("fishing.bobber_bar_in_bar_ratio", ReadChangedFactDouble(execution, "fishing.bobber_bar_in_bar_ratio")),
                    Number("fishing.terminal_progress", ReadChangedFactDouble(execution, "fishing.terminal_progress"))
                },
                Categorical = new[]
                {
                    Category("action.primary_option_id", optionId),
                    Category("action.intent_category", OptionBehaviorCategories.Mechanical),
                    Category("action.behavior_category", OptionBehaviorCategories.Mechanical),
                    Category("action.training_role", TrainingRoles.ExecutorCalibration),
                    Category("action.learning_scope", "calibration_only"),
                    Category("action.execution_mode", "training_singleplayer"),
                    Category("action.actor_type", "training_farmer"),
                    Category("action.execution_profile", isMove ? "real_runtime_move_harness" : "real_runtime_harness"),
                    Category("execution.primitive_kind", ReadString(execution, "primitive_kind")),
                    Category("execution.primitive_verification_status", primitiveVerificationStatus),
                    Category("execution.failure_category", failureCategory),
                    Category("execution.tool_qualified_item_id", ReadString(execution, "tool_qualified_item_id")),
                    Category("execution.training_impact_scope", ReadString(execution, "training_impact_scope")),
                    Category("execution.after_snapshot_note", ReadString(execution, "after_snapshot_note")),
                    Category("fishing.observed_qualified_item_id", ReadChangedFactString(execution, "fishing.caught_qualified_item_id")),
                    Category("fishing.terminal_result", ReadChangedFactString(execution, "fishing.terminal_result"))
                },
                Boolean = new[]
                {
                    Flag("action.hard_blocked", blocked),
                    Flag("action.exclude_from_policy_training", true),
                    Flag("execution.primitive_verified", primitiveVerified),
                    Flag("execution.after_snapshot_fresh", afterSnapshotFresh),
                    Flag("combat.target_defeated", execution["combat_target_defeated"]?.GetValue<bool>() == true),
                    Flag("fishing.max_cast_requested", ReadChangedFactBool(execution, "fishing.max_cast_requested")),
                    Flag("fishing.max_cast_observed", ReadChangedFactBool(execution, "fishing.max_cast_observed")),
                    Flag("fishing.action_idle_cleanup_complete", ReadChangedFactBool(execution, "fishing.action_idle_cleanup_complete"))
                }
            }
        },
        Labels = new TrainingLabelVector
        {
            GoalProgressDelta = reward,
            TotalReward = reward,
            HardBlocked = blocked,
            RequiredMinutes = requiredMinutes,
            AvailableMinutes = AvailableMinutes(effectiveBeforeSnapshot),
            RewardTermNames = RewardTerms(optionId, isMove, applied, watered),
            BlockReasons = ReadArrayStrings(execution, "block_reasons")
        },
        Audit = new TrainingFeatureRowAudit
        {
            Exporter = "StardewAI.LiveTrainingLoop.RealRuntimeExecutor",
            Policy = "Feature row labels are derived from RuntimeTestHarness execution result and before/after transparent snapshots; no simulator endpoint used."
        }
    };

    return AppendJsonl(options.DatasetPath, row);
}

static JsonObject? FindQueueItemForExecution(JsonObject queue, JsonObject execution)
{
    var executedQueueItemId = ReadStringOrEmpty(execution, "queue_item_id");
    if (string.IsNullOrWhiteSpace(executedQueueItemId))
    {
        return null;
    }

    return (queue["items"] as JsonArray)?
        .Select(node => node?.AsObject())
        .FirstOrDefault(item => item is not null &&
            string.Equals(ReadStringOrEmpty(item, "queue_item_id"), executedQueueItemId, StringComparison.Ordinal));
}

static void WritePlanExecutionEpisode(
    LiveTrainingOptions options,
    int iteration,
    string beforeSnapshotPath,
    string modelPlanPath,
    string queuePath,
    JsonObject queue,
    JsonObject execution,
    TrainingDatasetAppendResult appendResult,
    string stateHash,
    string queueId)
{
    var item = execution["effective_queue_item"]?.AsObject() ?? FindQueueItemForExecution(queue, execution) ?? queue["items"]?.AsArray().FirstOrDefault()?.AsObject();
    var optionId = ReadStringOrEmpty(execution, "option_id");
    if (string.IsNullOrWhiteSpace(optionId))
    {
        optionId = ReadStringOrEmpty(item, "option_id");
    }
    var status = ReadString(execution, "status");
    var applied = string.Equals(status, "applied", StringComparison.Ordinal);
    var blocked = !applied && !string.Equals(status, "no_op", StringComparison.Ordinal);
    var reward = string.Equals(optionId, "executor.move_to_tile", StringComparison.Ordinal)
        ? applied ? 0.05 : -0.05
        : string.Equals(optionId, "executor.face_direction", StringComparison.Ordinal) || string.Equals(optionId, "executor.wait_ticks", StringComparison.Ordinal)
            ? applied ? 0.02 : -0.02
        : 0;
    var episode = new PlanExecutionEpisodeEnvelope
    {
        EpisodeId = appendResult.EpisodeId,
        RunId = options.RunId,
        SourceStateHash = ReadString(execution, "effective_before_state_hash") is { Length: > 0 } effectiveStateHash ? effectiveStateHash : stateHash,
        AfterStateHash = ReadString(execution, "after_state_hash"),
        StateHashChanged = execution["state_hash_changed"]?.GetValue<bool>() == true,
        BeforeGameTick = ReadLong(execution, "before_game_tick"),
        AfterGameTick = ReadLong(execution, "after_game_tick"),
        AfterSnapshotFresh = execution["after_snapshot_fresh"]?.GetValue<bool>() == true,
        AfterSnapshotNote = ReadString(execution, "after_snapshot_note"),
        ModelPlanPath = modelPlanPath,
        CompiledQueuePath = queuePath,
        ExecutionResultPath = ReadString(execution, "execution_path"),
        BeforeSnapshotPath = ReadString(execution, "effective_before_snapshot_path") is { Length: > 0 } effectiveBeforePath ? effectiveBeforePath : beforeSnapshotPath,
        AfterSnapshotPath = ReadString(execution, "after_snapshot_path"),
        DatasetPath = appendResult.DatasetPath,
        RowId = appendResult.RowId,
        QueueId = ReadString(execution, "effective_queue_id") is { Length: > 0 } effectiveQueueId ? effectiveQueueId : queueId,
        OptionId = optionId,
        Status = status,
        Success = applied || string.Equals(status, "no_op", StringComparison.Ordinal),
        Reward = reward,
        TrainingRole = TrainingRoles.ExecutorCalibration,
        FailureAttribution = blocked ? "executor_calibration" : string.Empty,
        BlockReasons = ReadArrayStrings(execution, "block_reasons"),
        EffectiveQueueItem = item is null
            ? JsonDocument.Parse("{}").RootElement.Clone()
            : JsonSerializer.Deserialize<JsonElement>(item.ToJsonString(JsonOptions)),
        PrimitiveKind = ReadString(execution, "primitive_kind"),
        PrimitiveVerificationStatus = ReadString(execution, "primitive_verification_status"),
        PrimitiveVerificationReasons = ReadArrayStrings(execution, "primitive_verification_reasons"),
        RequestedEffect = ReadString(execution, "requested_effect"),
        ObservedEffect = ReadString(execution, "observed_effect"),
        ChangedFacts = execution["changed_facts"] is null
            ? JsonDocument.Parse("[]").RootElement.Clone()
            : JsonSerializer.Deserialize<JsonElement>(execution["changed_facts"]!.ToJsonString()),
        CombatTargetRuntimeType = ReadString(execution, "combat_target_runtime_type"),
        CombatTargetRuntimeIdentity = ReadString(execution, "combat_target_runtime_identity"),
        CombatTargetName = ReadString(execution, "combat_target_name"),
        CombatAttackCount = execution["combat_attack_count"]?.GetValue<int>(),
        CombatHitCount = execution["combat_hit_count"]?.GetValue<int>(),
        CombatTargetHealthSequence = ReadArrayInts(execution, "combat_target_health_sequence"),
        CombatPlayerHealthSequence = ReadArrayInts(execution, "combat_player_health_sequence"),
        CombatDamageTaken = execution["combat_damage_taken"]?.GetValue<int>(),
        CombatTargetDefeated = execution["combat_target_defeated"]?.GetValue<bool>(),
        RecoveryFoodSlotIndex = execution["recovery_food_slot_index"]?.GetValue<int>(),
        RecoveryFoodQualifiedItemId = ReadString(execution, "recovery_food_qualified_item_id"),
        RecoveryFoodStackBefore = execution["recovery_food_stack_before"]?.GetValue<int>(),
        RecoveryFoodStackAfter = execution["recovery_food_stack_after"]?.GetValue<int>(),
        RecoveryHealthBefore = execution["recovery_health_before"]?.GetValue<int>(),
        RecoveryHealthAfter = execution["recovery_health_after"]?.GetValue<int>(),
        RecoveryRestoreSlotIndex = execution["recovery_restore_slot_index"]?.GetValue<int>(),
        RecoverySafetyStatus = ReadString(execution, "recovery_safety_status"),
        DialogueNativeHandled = execution["dialogue_native_handled"]?.GetValue<bool>(),
        DialoguePressAttempts = execution["dialogue_press_attempts"]?.GetValue<int>(),
        DialogueAdvanceTicks = execution["dialogue_advance_ticks"]?.GetValue<int>(),
        DialogueMenuTypeBefore = ReadString(execution, "dialogue_menu_type_before"),
        DialogueMenuTypeAfter = ReadString(execution, "dialogue_menu_type_after"),
        DialogueIsQuestionBefore = execution["dialogue_is_question_before"]?.GetValue<bool>(),
        DialogueIsQuestionAfter = execution["dialogue_is_question_after"]?.GetValue<bool>(),
        DialogueResponseCountBefore = execution["dialogue_response_count_before"]?.GetValue<int>(),
        DialogueResponseCountAfter = execution["dialogue_response_count_after"]?.GetValue<int>(),
        DialogueSpeakerNameBefore = ReadString(execution, "dialogue_speaker_name_before"),
        DialogueSpeakerNameAfter = ReadString(execution, "dialogue_speaker_name_after"),
        DialogueEventUpBefore = execution["dialogue_event_up_before"]?.GetValue<bool>(),
        DialogueEventUpAfter = execution["dialogue_event_up_after"]?.GetValue<bool>()
    };

    var episodePath = Path.Combine(options.SnapshotDir, "plan-execution-episode-" + iteration.ToString("D4") + ".json");
    File.WriteAllText(episodePath, JsonSerializer.Serialize(episode, JsonOptions), Encoding.UTF8);
}

static string[] RewardTerms(string optionId, bool isMove, bool applied, int watered)
{
    if (isMove)
    {
        return applied
            ? new[] { "real_move_applied", "collision_safe_tile_step" }
            : new[] { "real_move_blocked" };
    }

    if (string.Equals(optionId, "executor.face_direction", StringComparison.Ordinal))
    {
        return applied ? new[] { "real_face_direction_applied" } : new[] { "real_face_direction_blocked" };
    }

    if (string.Equals(optionId, "executor.wait_ticks", StringComparison.Ordinal))
    {
        return applied ? new[] { "real_wait_ticks_applied" } : new[] { "real_wait_ticks_blocked" };
    }

    return watered > 0 ? new[] { "real_crop_watered", "real_energy_spent" } : Array.Empty<string>();
}

static FeatureVector BuildStateFeatures(JsonObject snapshot)
{
    return new FeatureVector
    {
        Numeric = new[]
        {
            Number("game.time", ReadFieldDouble(snapshot, "time", "time")),
            Number("game.day", ReadFieldDouble(snapshot, "time", "day")),
            Number("game.year", ReadFieldDouble(snapshot, "time", "year")),
            Number("player.money", ReadFieldDouble(snapshot, "player", "money")),
            Number("player.energy", ReadFieldDouble(snapshot, "player", "energy")),
            Number("player.health", ReadFieldDouble(snapshot, "player", "health")),
            Number("player.level", ReadFieldDouble(snapshot, "player", "level")),
            Number("player.total_money_earned", ReadFieldDouble(snapshot, "player", "total_money_earned")),
            Number("farm.crops_needing_watering", CountCropsNeedingWater(snapshot)),
            Number("completeness.unavailable_count", ReadUnavailableCount(snapshot)),
            Number("completeness.required_readable_ratio", 1)
        },
        Categorical = new[]
        {
            Category("game.season", ReadFieldString(snapshot, "time", "season")),
            Category("game.weather", ReadFieldString(snapshot, "time", "weather")),
            Category("player.location_id", ReadFieldString(snapshot, "player", "location_id")),
            Category("world.mode", "training")
        },
        Boolean = new[]
        {
            Flag("completeness.all_required_facts_readable", true),
            Flag("planner_inputs.blocked", false)
        }
    };
}

static TrainingDatasetAppendResult AppendJsonl(string datasetPath, TrainingFeatureRowEnvelope row)
{
    Directory.CreateDirectory(Path.GetDirectoryName(datasetPath)!);
    var payload = JsonSerializer.Serialize(row, JsonlOptions) + Environment.NewLine;
    File.AppendAllText(datasetPath, payload, Encoding.UTF8);
    return new TrainingDatasetAppendResult
    {
        DatasetPath = Path.GetFullPath(datasetPath),
        RowId = row.RowId,
        EpisodeId = row.EpisodeId,
        BytesWritten = Encoding.UTF8.GetByteCount(payload),
        RowCount = File.ReadLines(datasetPath).Count(line => !string.IsNullOrWhiteSpace(line))
    };
}

static async Task<(JsonObject? TrainingReport, JsonObject? Prediction)> TrainIfNeededAsync(HttpClient http, LiveTrainingOptions options, int iteration)
{
    if (iteration % options.TrainEvery != 0 && iteration != options.MaxAttempts)
    {
        return (null, null);
    }

    var trainRequest = JsonSerializer.Serialize(new
    {
        dataset_path = Path.GetFullPath(options.DatasetPath)
    }, JsonOptions);
    var report = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/training/baseline/train", trainRequest);
    var prediction = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", trainRequest);
    var bestOption = prediction["ranked_options"]?[0]?["option_id"]?.GetValue<string>() ?? string.Empty;
    AppendProgress(options, "train", iteration, string.Empty, string.Empty, "best_option=" + bestOption + " source=real_runtime_executor");
    return (report, prediction);
}

static async Task<JsonObject> PostJsonStringAsync(HttpClient http, string url, string json)
{
    using var content = new StringContent(json, Encoding.UTF8, "application/json");
    using var response = await http.PostAsync(url, content);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(url + " failed with " + (int)response.StatusCode + ": " + body);
    }

    return JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
}

static string ReadString(JsonObject value, string property)
{
    return value[property]?.GetValue<string>() ?? string.Empty;
}

static string ReadStringOrEmpty(JsonObject? value, string property)
{
    return value?[property]?.GetValue<string>() ?? string.Empty;
}

static int? ReadQueueParameterInt(JsonObject? item, string name)
{
    var parameters = item?["normalized_command"]?["parameters"]?.AsArray();
    if (parameters is null)
    {
        return null;
    }

    foreach (var parameter in parameters)
    {
        var parameterObject = parameter?.AsObject();
        if (string.Equals(ReadStringOrEmpty(parameterObject, "name"), name, StringComparison.Ordinal) &&
            int.TryParse(ReadStringOrEmpty(parameterObject, "value"), out var result))
        {
            return result;
        }
    }

    return null;
}

static string ReadQueueParameterString(JsonObject? item, string name)
{
    var parameters = item?["normalized_command"]?["parameters"]?.AsArray();
    if (parameters is null)
    {
        return string.Empty;
    }

    foreach (var parameter in parameters)
    {
        var parameterObject = parameter?.AsObject();
        if (string.Equals(ReadStringOrEmpty(parameterObject, "name"), name, StringComparison.Ordinal))
        {
            return ReadStringOrEmpty(parameterObject, "value");
        }
    }

    return string.Empty;
}

static int ReadInt(JsonObject value, string property)
{
    return value[property]?.GetValue<int>() ?? 0;
}

static long ReadLong(JsonObject value, string property)
{
    return value[property]?.GetValue<long>() ?? 0;
}

static double ReadDouble(JsonObject value, string property)
{
    return value[property]?.GetValue<double>() ?? 0;
}

static string ReadChangedFactString(JsonObject execution, string path)
{
    var facts = execution["changed_facts"]?.AsArray();
    if (facts is null)
    {
        return string.Empty;
    }

    foreach (var fact in facts)
    {
        var factObject = fact?.AsObject();
        if (string.Equals(ReadStringOrEmpty(factObject, "path"), path, StringComparison.Ordinal))
        {
            return ReadStringOrEmpty(factObject, "after");
        }
    }

    return string.Empty;
}

static double ReadChangedFactDouble(JsonObject execution, string path)
{
    return double.TryParse(ReadChangedFactString(execution, path), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
        ? value
        : 0d;
}

static bool ReadChangedFactBool(JsonObject execution, string path)
{
    return bool.TryParse(ReadChangedFactString(execution, path), out var value) && value;
}

static double ReadFieldDouble(JsonObject snapshot, string section, string name)
{
    var value = snapshot["state"]?[section]?[name]?["value"];
    if (value is null)
    {
        return 0;
    }

    return value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : 0;
}

static string ReadFieldString(JsonObject snapshot, string section, string name)
{
    var value = snapshot["state"]?[section]?[name]?["value"];
    if (value is null || value.GetValueKind() != JsonValueKind.String)
    {
        return "unknown";
    }

    return value.GetValue<string>() ?? "unknown";
}

static int CountCropsNeedingWater(JsonObject snapshot)
{
    var crops = snapshot["state"]?["farm"]?["crops"]?["value"]?.AsArray();
    if (crops is null)
    {
        return 0;
    }

    return crops.Count(item => item?["needs_watering"]?.GetValue<bool>() == true);
}

static int ReadUnavailableCount(JsonObject snapshot)
{
    return snapshot["unavailable_fields"]?.AsArray().Count ?? 0;
}

static int AvailableMinutes(JsonObject snapshot)
{
    var time = (int)ReadFieldDouble(snapshot, "time", "time");
    if (time <= 0)
    {
        return 0;
    }

    var hour = time / 100;
    var minute = time % 100;
    var current = hour * 60 + minute;
    return Math.Max(0, 26 * 60 - current);
}

static string[] ReadArrayStrings(JsonObject value, string property)
{
    var array = value[property]?.AsArray();
    if (array is null)
    {
        return Array.Empty<string>();
    }

    return array
        .Select(item => item?.GetValue<string>() ?? string.Empty)
        .Where(item => item.Length > 0)
        .ToArray();
}

static int[] ReadArrayInts(JsonObject value, string property)
{
    var array = value[property]?.AsArray();
    return array is null
        ? Array.Empty<int>()
        : array.Select(item => item?.GetValue<int>() ?? 0).ToArray();
}

static NumericFeature Number(string name, double value)
{
    return new NumericFeature { Name = name, Value = value };
}

static CategoricalFeature Category(string name, string value)
{
    return new CategoricalFeature { Name = name, Value = string.IsNullOrWhiteSpace(value) ? "unknown" : value };
}

static BooleanFeature Flag(string name, bool value)
{
    return new BooleanFeature { Name = name, Value = value };
}

static void AppendProgress(LiveTrainingOptions options, string stage, int iteration, string stateHash, string queueId, string detail)
{
    var line = string.Join(" ", new[]
    {
        DateTimeOffset.Now.ToString("O"),
        "stage=" + stage,
        "iteration=" + iteration,
        "run_id=" + options.RunId,
        "state_hash=" + stateHash,
        "queue_id=" + queueId,
        detail
    });
    File.AppendAllText(options.ProgressLogPath, line + Environment.NewLine);
}

public sealed class LiveTrainingOptions
{
    public string Root { get; set; } = @"E:\StardewAITraining";
    public string BackendUrl { get; set; } = "http://localhost:5108";
    public string BridgeSnapshotUrl { get; set; } = "http://127.0.0.1:8765/api/v1/snapshot";
    public string SnapshotFile { get; set; } = string.Empty;
    public string ExecutorUrl { get; set; } = "http://127.0.0.1:8767";
    public string ManifestPath { get; set; } = ReadTextOrEmpty(@"E:\StardewAITraining\last-manifest-path.txt");
    public string RunId { get; set; } = ReadTextOrEmpty(@"E:\StardewAITraining\last-run-id.txt");
    public string SaveIsolationPath { get; set; } = @"E:\StardewValleyAICompanion-runtime\saves";
    public string Goal { get; set; } = "grandpa_four_candles_year3";
    public int MaxAttempts { get; set; } = 3;
    public int RequiredVerifiedActions { get; set; }
    public int TrainEvery { get; set; } = 1;
    public int SleepMs { get; set; } = 1000;
    public int MaxCropsPerExecution { get; set; } = 16;
    public string ExecutorOptionId { get; set; } = string.Empty;
    public int? TargetTileX { get; set; }
    public int? TargetTileY { get; set; }
    public int? Direction { get; set; }
    public int? WaitTicks { get; set; }
    public string PlanStepKind { get; set; } = "move_to_tile";
    public int AfterSnapshotWaitMs { get; set; } = 2500;
    public int AfterSnapshotPollMs { get; set; } = 100;
    public bool RequireExecutorFeedback { get; set; } = true;
    public bool UseRealRuntimeExecutor { get; set; } = true;
    public bool UsePlanOutput { get; set; }
    public bool UseDailyPlan { get; set; }
    public bool ContinueAfterBlockedQueueItems { get; set; }
    public int MaxQueueItemAttempts { get; set; } = 24;
    public int DailyPlanMaxCandidates { get; set; } = 4;
    public string[] DailyPlanCandidateOptionIds { get; set; } = Array.Empty<string>();
    public string ExecutionMode => RequireExecutorFeedback
        ? UseRealRuntimeExecutor ? "real_runtime_executor" : "training_sandbox_feedback_gate"
        : "disabled";

    public string RunDir => string.IsNullOrWhiteSpace(ManifestPath)
        ? Path.Combine(Root, "runs", string.IsNullOrWhiteSpace(RunId) ? "live-training" : RunId)
        : !string.IsNullOrWhiteSpace(Path.GetDirectoryName(ManifestPath))
            ? Path.GetDirectoryName(ManifestPath)!
            : Path.Combine(Root, "runs", string.IsNullOrWhiteSpace(RunId) ? "live-training" : RunId);
    public string SnapshotDir => Path.Combine(RunDir, "live-snapshots");
    public string DatasetPath => Path.Combine(Root, "datasets", "live-training-feature-rows.jsonl");
    public string ProgressLogPath => Path.Combine(Root, "logs", "live-training-progress.log");
    public string ReadyProbeUrl => BackendUrl + "/api/v1/training/session/ready-probe?manifest_path=" + Uri.EscapeDataString(ManifestPath);

    public static LiveTrainingOptions Parse(string[] args)
    {
        var options = new LiveTrainingOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            if (current == "--root" && i + 1 < args.Length)
            {
                options.Root = args[++i];
            }
            else if (current == "--backend-url" && i + 1 < args.Length)
            {
                options.BackendUrl = args[++i].TrimEnd('/');
            }
            else if (current == "--bridge-snapshot-url" && i + 1 < args.Length)
            {
                options.BridgeSnapshotUrl = args[++i];
            }
            else if (current == "--snapshot-file" && i + 1 < args.Length)
            {
                options.SnapshotFile = args[++i];
            }
            else if (current == "--executor-url" && i + 1 < args.Length)
            {
                options.ExecutorUrl = args[++i].TrimEnd('/');
            }
            else if (current == "--save-isolation-path" && i + 1 < args.Length)
            {
                options.SaveIsolationPath = args[++i];
            }
            else if (current == "--manifest-path" && i + 1 < args.Length)
            {
                options.ManifestPath = args[++i];
            }
            else if (current == "--no-manifest")
            {
                options.ManifestPath = string.Empty;
            }
            else if (current == "--run-id" && i + 1 < args.Length)
            {
                options.RunId = args[++i];
            }
            else if (current == "--goal" && i + 1 < args.Length)
            {
                options.Goal = args[++i];
            }
            else if (current == "--iterations" && i + 1 < args.Length && int.TryParse(args[++i], out var iterations))
            {
                options.MaxAttempts = Math.Max(1, iterations);
            }
            else if (current == "--max-attempts" && i + 1 < args.Length && int.TryParse(args[++i], out var maxAttempts))
            {
                options.MaxAttempts = Math.Max(1, maxAttempts);
            }
            else if (current == "--required-verified-actions" && i + 1 < args.Length && int.TryParse(args[++i], out var requiredVerifiedActions))
            {
                options.RequiredVerifiedActions = Math.Max(0, requiredVerifiedActions);
            }
            else if (current == "--train-every" && i + 1 < args.Length && int.TryParse(args[++i], out var trainEvery))
            {
                options.TrainEvery = Math.Max(1, trainEvery);
            }
            else if (current == "--sleep-ms" && i + 1 < args.Length && int.TryParse(args[++i], out var sleepMs))
            {
                options.SleepMs = Math.Max(0, sleepMs);
            }
            else if (current == "--max-crops" && i + 1 < args.Length && int.TryParse(args[++i], out var maxCrops))
            {
                options.MaxCropsPerExecution = Math.Max(1, maxCrops);
            }
            else if (current == "--executor-option-id" && i + 1 < args.Length)
            {
                options.ExecutorOptionId = args[++i];
            }
            else if (current == "--target-tile-x" && i + 1 < args.Length && int.TryParse(args[++i], out var targetTileX))
            {
                options.TargetTileX = targetTileX;
            }
            else if (current == "--target-tile-y" && i + 1 < args.Length && int.TryParse(args[++i], out var targetTileY))
            {
                options.TargetTileY = targetTileY;
            }
            else if (current == "--direction" && i + 1 < args.Length && int.TryParse(args[++i], out var direction))
            {
                options.Direction = direction;
            }
            else if (current == "--wait-ticks" && i + 1 < args.Length && int.TryParse(args[++i], out var waitTicks))
            {
                options.WaitTicks = waitTicks;
            }
            else if (current == "--after-snapshot-wait-ms" && i + 1 < args.Length && int.TryParse(args[++i], out var afterSnapshotWaitMs))
            {
                options.AfterSnapshotWaitMs = Math.Max(0, afterSnapshotWaitMs);
            }
            else if (current == "--after-snapshot-poll-ms" && i + 1 < args.Length && int.TryParse(args[++i], out var afterSnapshotPollMs))
            {
                options.AfterSnapshotPollMs = Math.Max(1, afterSnapshotPollMs);
            }
            else if (current == "--plan-step-kind" && i + 1 < args.Length)
            {
                options.PlanStepKind = args[++i];
            }
            else if (current == "--no-executor-feedback-required")
            {
                options.RequireExecutorFeedback = false;
            }
            else if (current == "--use-sandbox-executor")
            {
                options.UseRealRuntimeExecutor = false;
            }
            else if (current == "--use-plan-output")
            {
                options.UsePlanOutput = true;
            }
            else if (current == "--use-daily-plan")
            {
                options.UseDailyPlan = true;
            }
            else if (current == "--continue-after-blocked-queue-items")
            {
                options.ContinueAfterBlockedQueueItems = true;
            }
            else if (current == "--max-queue-item-attempts" && i + 1 < args.Length && int.TryParse(args[++i], out var maxQueueItemAttempts))
            {
                options.MaxQueueItemAttempts = Math.Max(1, maxQueueItemAttempts);
            }
            else if (current == "--daily-plan-max-candidates" && i + 1 < args.Length && int.TryParse(args[++i], out var dailyPlanMaxCandidates))
            {
                options.DailyPlanMaxCandidates = Math.Max(1, dailyPlanMaxCandidates);
            }
            else if (current == "--daily-plan-candidate-options" && i + 1 < args.Length)
            {
                options.DailyPlanCandidateOptionIds = args[++i]
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
            }
        }

        if (string.IsNullOrWhiteSpace(options.RunId))
        {
            options.RunId = "live." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        }

        return options;
    }

    private static string ReadTextOrEmpty(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
    }
}

public sealed class LiveTrainingLoopReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "live_training_loop_report.v1";

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("backend_url")]
    public string BackendUrl { get; set; } = string.Empty;

    [JsonPropertyName("bridge_snapshot_url")]
    public string BridgeSnapshotUrl { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_file")]
    public string SnapshotFile { get; set; } = string.Empty;

    [JsonPropertyName("dataset_path")]
    public string DatasetPath { get; set; } = string.Empty;

    [JsonPropertyName("progress_log_path")]
    public string ProgressLogPath { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_dir")]
    public string SnapshotDir { get; set; } = string.Empty;

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    [JsonPropertyName("max_attempts")]
    public int MaxAttempts { get; set; }

    [JsonPropertyName("attempts_started")]
    public int AttemptsStarted { get; set; }

    [JsonPropertyName("rows_appended")]
    public int RowsAppended { get; set; }

    [JsonPropertyName("verified_actions")]
    public int VerifiedActions { get; set; }

    [JsonPropertyName("required_verified_actions")]
    public int RequiredVerifiedActions { get; set; }

    [JsonPropertyName("last_state_hash")]
    public string LastStateHash { get; set; } = string.Empty;

    [JsonPropertyName("last_queue_id")]
    public string LastQueueId { get; set; } = string.Empty;

    [JsonPropertyName("concurrency")]
    public int Concurrency { get; set; }

    [JsonPropertyName("execution")]
    public string Execution { get; set; } = "disabled";

    [JsonPropertyName("executor_feedback_required")]
    public bool ExecutorFeedbackRequired { get; set; }

    [JsonPropertyName("training_report")]
    public JsonObject? TrainingReport { get; set; }

    [JsonPropertyName("prediction")]
    public JsonObject? Prediction { get; set; }
}

static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonlOptions = new(JsonSerializerDefaults.Web);
}
