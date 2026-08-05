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
    Timeout = TimeSpan.FromSeconds(180)
};
using var executorHttp = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(options.ExecutorTimeoutSeconds)
};

AppendProgress(options, "start", 0, string.Empty, string.Empty, "concurrency=1 target=" + options.TargetExecutionMode + " feedback=" + options.FeedbackMode);

var rowsAppended = 0;
var verifiedActions = 0;
var attemptsStarted = 0;
var lastQueueId = string.Empty;
var lastStateHash = string.Empty;
JsonObject? lastTrainingReport = null;
JsonObject? lastPrediction = null;
JsonObject? activeObjectiveContinuation = null;
var objectiveCompleted = false;
var socialObjectiveCompleted = false;
var persistedIterationCount = Directory.EnumerateFiles(
    options.SnapshotDir,
    "before-snapshot-*.json",
    SearchOption.TopDirectoryOnly).Count();
var nextArtifactIteration = NextArtifactIteration(options.SnapshotDir);
var consecutiveErrors = 0;
var lastErrorIteration = 0;
var stopReason = string.Empty;
var noProgressBackoff = new NoProgressBackoffPolicy(
    options.NoProgressBackoffMs,
    options.NoProgressMaxBackoffMs);

for (var attemptOrdinal = 1;
    attemptOrdinal <= options.MaxAttempts &&
    (options.RequiredVerifiedActions <= 0 || verifiedActions < options.RequiredVerifiedActions) &&
    (!options.StopAfterObjectiveComplete || !objectiveCompleted) &&
    (!options.StopAfterSocialObjectiveComplete || !socialObjectiveCompleted);
    attemptOrdinal++)
{
    var iteration = nextArtifactIteration + attemptOrdinal - 1;
    try
    {
        persistedIterationCount = ApplyRollingArtifactRetention(options, iteration);
        var artifactBudgetBlock = GetArtifactBudgetBlock(options, persistedIterationCount);
        if (!string.IsNullOrWhiteSpace(artifactBudgetBlock))
        {
            stopReason = artifactBudgetBlock;
            AppendProgress(options, "stopped", iteration, lastStateHash, lastQueueId, stopReason);
            break;
        }
        attemptsStarted++;
        var rawSnapshotJson = iteration == 1 && !string.IsNullOrWhiteSpace(options.SnapshotFile)
            ? await File.ReadAllTextAsync(options.SnapshotFile, Encoding.UTF8)
            : await http.GetStringAsync(options.BridgeSnapshotUrl);
        var beforeSnapshot = JsonNode.Parse(rawSnapshotJson)?.AsObject() ?? new JsonObject();
        var snapshotJson = beforeSnapshot.ToJsonString(JsonlOptions);
        var snapshotPath = Path.Combine(options.SnapshotDir, "before-snapshot-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(snapshotPath, snapshotJson, Encoding.UTF8);
        persistedIterationCount++;

    var ingest = await PostJsonStringAsync(
        http,
        SnapshotIngestUrl(options),
        snapshotJson);
    lastStateHash = ReadString(ingest, "state_hash");

    var ready = await http.GetFromJsonAsync<JsonObject>(options.ReadyProbeUrl);
    if (ready is null || ready["ready"]?.GetValue<bool>() != true)
    {
        AppendProgress(options, "blocked", iteration, lastStateHash, string.Empty, "ready_probe_failed");
        await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
        continue;
    }

    var modelPlanPath = string.Empty;
    var rankingPath = string.Empty;
    JsonObject queue;
    if (options.UseDailyPlan)
    {
        var dailyPlan = await BuildQueueFromDailyPlanAsync(http, options, lastStateHash, activeObjectiveContinuation);
        modelPlanPath = Path.Combine(options.SnapshotDir, "model-plan-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(modelPlanPath, dailyPlan.Plan.ToJsonString(JsonOptions), Encoding.UTF8);
        var dailyPlanPath = Path.Combine(options.SnapshotDir, "daily-plan-response-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(dailyPlanPath, dailyPlan.Response.ToJsonString(JsonOptions), Encoding.UTF8);
        rankingPath = Path.Combine(options.SnapshotDir, "ranking-response-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(rankingPath, dailyPlan.Ranking.ToJsonString(JsonOptions), Encoding.UTF8);
        queue = dailyPlan.Queue;
    }
    else if (options.UseParameterizedAction)
    {
        var modelActionJson = BuildParameterizedActionRequest(options, lastStateHash);
        modelPlanPath = Path.Combine(options.SnapshotDir, "model-action-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(modelPlanPath, modelActionJson, Encoding.UTF8);
        queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/action-queue/compile", modelActionJson);
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
    var noProgressDecision = noProgressBackoff.Observe(queue);
    if (options.RequireExecutorFeedback)
    {
        if (!string.Equals(queueStatus, "pending", StringComparison.Ordinal))
        {
            var executableSubsetCount = ExecutableQueueItems(queue).Length;
            if (!options.ContinueAfterBlockedQueueItems || executableSubsetCount == 0)
            {
                AppendProgress(
                    options,
                    "blocked",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "queue_not_pending executor_feedback_required" +
                    NoProgressDetail(noProgressDecision));
                await DelayBeforeNextAttemptAsync(
                    options,
                    attemptOrdinal,
                    noProgressDecision.DelayMs);
                continue;
            }

            AppendProgress(
                options,
                "partial_queue",
                iteration,
                lastStateHash,
                lastQueueId,
                "executing_pending_subset count=" + executableSubsetCount + " queue_status=" + queueStatus);
        }

        var execution = options.UseRuntimeTestHarnessExecutor
            ? await ExecuteRuntimeTestHarnessAsync(http, executorHttp, options, iteration, snapshotPath, beforeSnapshot, queue, lastStateHash, lastQueueId, modelPlanPath, rankingPath, queuePath, activeObjectiveContinuation)
            : await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/action-queues/" + Uri.EscapeDataString(lastQueueId) + "/execute-training-sandbox", "{}");
        var feedbackAvailable = execution["feedback_available"]?.GetValue<bool>() == true;
        if (!feedbackAvailable && !options.UseRuntimeTestHarnessExecutor)
        {
            AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, "executor_feedback_unavailable status=" + ReadString(execution, "status"));
            await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
            continue;
        }

        if (options.UseRuntimeTestHarnessExecutor)
        {
            var primitiveVerified = string.Equals(ReadString(execution, "primitive_verification_status"), "verified", StringComparison.Ordinal) &&
                string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal);
            if (!primitiveVerified)
            {
                AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, "runtime_test_harness_unverified status=" + ReadString(execution, "status") + " primitive=" + ReadString(execution, "primitive_verification_status"));
                await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
                continue;
            }

            var executionNoProgress =
                noProgressBackoff.ObserveExecution(queue, execution);
            var realAppend = AppendRealExecutionRow(
                options,
                beforeSnapshot,
                queue,
                execution,
                lastStateHash,
                lastQueueId,
                appendToDataset: !executionNoProgress.NoProgress);
            activeObjectiveContinuation = execution["objective_continuation"] is JsonObject continuation
                ? JsonNode.Parse(continuation.ToJsonString(JsonOptions))?.AsObject()
                : null;
            objectiveCompleted = execution["objective_continuation_completed"]?.GetValue<bool>() == true;
            socialObjectiveCompleted = execution["social_objective_completed"]?.GetValue<bool>() == true;
            WritePlanExecutionEpisode(options, iteration, snapshotPath, modelPlanPath, queuePath, queue, execution, realAppend, lastStateHash, lastQueueId);
            var horizonObservations = AppendClosedHorizonObservations(options, beforeSnapshot, execution);
            var policyAppend = executionNoProgress.NoProgress
                ? new PolicyTrajectoryAppendBatchResult()
                : AppendPolicyDecisionTrajectories(options, iteration, execution, realAppend);
            rowsAppended = realAppend.RowCount;
            if (!executionNoProgress.NoProgress)
            {
                verifiedActions++;
                AppendProgress(options, "append", iteration, lastStateHash, lastQueueId, "dataset_rows=" + rowsAppended + " policy_trajectories=" + policyAppend.AppendedCount + " policy_trajectory_skips=" + policyAppend.SkippedCount + " policy_trajectory_first_skip=" + policyAppend.FirstSkipReason + " horizon_observations=" + horizonObservations + " verified_actions=" + verifiedActions + " required_verified_actions=" + options.RequiredVerifiedActions + " source=runtime_test_harness_executor");
                var train = await TrainIfNeededAsync(http, options, iteration);
                if (train.TrainingReport is not null)
                {
                    lastTrainingReport = train.TrainingReport;
                    lastPrediction = train.Prediction;
                }
            }
            else
            {
                AppendProgress(
                    options,
                    "idle_backoff",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "recovery_refresh_wait_only dataset_rows=" +
                    rowsAppended +
                    NoProgressDetail(executionNoProgress));
            }

            await DelayBeforeNextAttemptAsync(
                options,
                attemptOrdinal,
                executionNoProgress.DelayMs);
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

    await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
    }
    catch (Exception ex)
    {
        consecutiveErrors = lastErrorIteration == iteration - 1 ? consecutiveErrors + 1 : 1;
        lastErrorIteration = iteration;
        var detail = "error_type=" + ex.GetType().Name + " message=" + SanitizeProgressValue(ex.Message);
        AppendProgress(options, "error", iteration, lastStateHash, lastQueueId, detail);
        Console.Error.WriteLine("Live training iteration " + iteration + " failed: " + ex);
        if (consecutiveErrors >= options.MaxConsecutiveErrors)
        {
            stopReason = "max_consecutive_errors_reached count=" + consecutiveErrors;
            AppendProgress(options, "stopped", iteration, lastStateHash, lastQueueId, stopReason);
            break;
        }
        await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
    }
}

var verifiedTargetMet = string.IsNullOrWhiteSpace(stopReason) &&
    (options.RequiredVerifiedActions <= 0 || verifiedActions >= options.RequiredVerifiedActions) &&
    (!options.StopAfterObjectiveComplete || objectiveCompleted) &&
    (!options.StopAfterSocialObjectiveComplete || socialObjectiveCompleted);
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
    StopReason = stopReason,
    ObjectiveCompleted = objectiveCompleted,
    ActiveObjectiveContinuation = activeObjectiveContinuation,
    SocialObjectiveCompleted = socialObjectiveCompleted,
    ActiveSocialContinuation = string.Equals(ReadString(activeObjectiveContinuation, "kind"), "social", StringComparison.Ordinal)
        ? activeObjectiveContinuation
        : null,
    LastStateHash = lastStateHash,
    LastQueueId = lastQueueId,
    Concurrency = 1,
    Execution = options.TargetExecutionMode + ":" + options.FeedbackMode,
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
    stop_reason = stopReason,
    dataset_path = options.DatasetPath,
    report_path = reportPath,
    progress_log_path = options.ProgressLogPath,
    concurrency = 1,
    execution = options.TargetExecutionMode,
    feedback = options.FeedbackMode,
    executor_feedback_required = options.RequireExecutorFeedback,
    objective_completed = objectiveCompleted,
    social_objective_completed = socialObjectiveCompleted
}, JsonOptions));

static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonlOptions = new(JsonSerializerDefaults.Web);

    private static async Task DelayBeforeNextAttemptAsync(
        LiveTrainingOptions options,
        int attemptOrdinal,
        int minimumDelayMs = 0)
    {
        var delayMs = Math.Max(options.SleepMs, minimumDelayMs);
        if (delayMs > 0 && attemptOrdinal < options.MaxAttempts)
        {
            await Task.Delay(delayMs);
        }
    }

    private static string NoProgressDetail(
        NoProgressBackoffDecision decision)
    {
        return decision.NoProgress
            ? " no_progress_streak=" + decision.Streak +
                " retry_delay_ms=" + decision.DelayMs
            : string.Empty;
    }

    private static string SanitizeProgressValue(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
