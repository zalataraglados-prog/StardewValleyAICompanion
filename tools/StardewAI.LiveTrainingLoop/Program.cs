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

var options = LiveTrainingOptions.Parse(args);
options.ValidateFormalExecutionBoundary();
Directory.CreateDirectory(options.Root);
Directory.CreateDirectory(options.RunDir);
Directory.CreateDirectory(options.SnapshotDir);
Directory.CreateDirectory(Path.GetDirectoryName(options.DatasetPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(options.ProgressLogPath)!);
var trainingDataTransaction = FormalTrainingDataTransaction.Begin(options);

using var http = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(Math.Max(180, options.ExecutorTimeoutSeconds))
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
var suppressedObjectiveContinuations = new List<JsonObject>();
var suppressionDayKey = string.Empty;
var objectiveCompleted = false;
var socialObjectiveCompleted = false;
var leasedDecisionModelPlanPath = string.Empty;
var leasedDecisionRankingPath = string.Empty;
var leasedDecisionCompiledQueuePath = string.Empty;
var leasedDecisionSnapshotPath = string.Empty;
var leasedDecisionSourceStateHash = string.Empty;
var leasedDecisionGoalId = string.Empty;
var leasedSelectedCandidateId = string.Empty;
var leasedSelectedQueueIndex = -1;
var persistedIterationCount = Directory.EnumerateFiles(
    options.SnapshotDir,
    "before-snapshot-*.json",
    SearchOption.TopDirectoryOnly).Count();
var nextArtifactIteration = NextArtifactIteration(options.SnapshotDir);
var consecutiveErrors = 0;
var lastErrorIteration = 0;
var stopReason = string.Empty;
var primaryAttemptsStarted = 0;
var nativeSaveBoundaryAttemptsStarted = 0;
var nativeSaveBoundaryPhaseStarted = false;
var nativeSaveBoundaryVerified = false;
var nativeSaveBoundaryInitialDayKey = string.Empty;
var nativeSaveBoundaryCurrentDayKey = string.Empty;
var initialSaveFingerprint = options.RequireNativeSaveBoundary
    ? await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
        options.SaveIsolationPath,
        options.SaveSlot)
    : null;
var currentSaveFingerprint = initialSaveFingerprint;
var hasExplicitPrimaryTarget =
    options.RequiredVerifiedActions > 0 ||
    options.StopAfterObjectiveComplete ||
    options.StopAfterSocialObjectiveComplete;
var noProgressBackoff = new NoProgressBackoffPolicy(
    options.NoProgressBackoffMs,
    options.NoProgressMaxBackoffMs);
var unverifiedExecutionBackoff = new NoProgressBackoffPolicy(
    options.NoProgressBackoffMs,
    options.NoProgressMaxBackoffMs);

for (var attemptOrdinal = 1; ; attemptOrdinal++)
{
    var explicitPrimaryTargetMet =
        (options.RequiredVerifiedActions <= 0 ||
         verifiedActions >= options.RequiredVerifiedActions) &&
        (!options.StopAfterObjectiveComplete || objectiveCompleted) &&
        (!options.StopAfterSocialObjectiveComplete || socialObjectiveCompleted);
    var phaseDecision = TrainingCompletionPolicy.Decide(
        primaryAttemptsStarted,
        options.MaxAttempts,
        nativeSaveBoundaryAttemptsStarted,
        options.SaveBoundaryMaxAttempts,
        hasExplicitPrimaryTarget,
        explicitPrimaryTargetMet,
        options.RequireNativeSaveBoundary,
        nativeSaveBoundaryVerified);
    if (phaseDecision.Phase is LiveTrainingPhase.Complete or LiveTrainingPhase.Incomplete)
    {
        if (phaseDecision.Phase == LiveTrainingPhase.Incomplete &&
            string.IsNullOrWhiteSpace(stopReason))
        {
            stopReason = phaseDecision.StopReason;
        }
        break;
    }

    var closingNativeSaveBoundary =
        phaseDecision.Phase == LiveTrainingPhase.NativeSaveBoundary;
    if (closingNativeSaveBoundary && !nativeSaveBoundaryPhaseStarted)
    {
        nativeSaveBoundaryPhaseStarted = true;
        activeObjectiveContinuation = null;
        leasedDecisionModelPlanPath = string.Empty;
        leasedDecisionRankingPath = string.Empty;
        leasedDecisionCompiledQueuePath = string.Empty;
        leasedDecisionSnapshotPath = string.Empty;
        leasedDecisionSourceStateHash = string.Empty;
        leasedDecisionGoalId = string.Empty;
        leasedSelectedCandidateId = string.Empty;
        leasedSelectedQueueIndex = -1;
        AppendProgress(
            options,
            "native_save_boundary_start",
            attemptOrdinal,
            lastStateHash,
            lastQueueId,
            "primary_attempts=" + primaryAttemptsStarted +
            " verified_actions=" + verifiedActions);
    }

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
        if (closingNativeSaveBoundary)
        {
            nativeSaveBoundaryAttemptsStarted++;
        }
        else
        {
            primaryAttemptsStarted++;
        }
        var rawSnapshotJson = iteration == 1 && !string.IsNullOrWhiteSpace(options.SnapshotFile)
            ? await File.ReadAllTextAsync(options.SnapshotFile, Encoding.UTF8)
            : await http.GetStringAsync(options.BridgeSnapshotUrl);
        var beforeSnapshot = JsonNode.Parse(rawSnapshotJson)?.AsObject() ?? new JsonObject();
        var currentDayKey = QueueReplanFilter.SnapshotDayKey(beforeSnapshot);
        if (options.RequireNativeSaveBoundary)
        {
            if (string.IsNullOrWhiteSpace(nativeSaveBoundaryInitialDayKey))
            {
                if (string.IsNullOrWhiteSpace(currentDayKey))
                {
                    throw new InvalidOperationException(
                        "native_save_boundary_initial_day_key_unavailable");
                }
                nativeSaveBoundaryInitialDayKey = currentDayKey;
            }
            nativeSaveBoundaryCurrentDayKey = currentDayKey;
        }
        if (closingNativeSaveBoundary &&
            !string.Equals(
                nativeSaveBoundaryInitialDayKey,
                nativeSaveBoundaryCurrentDayKey,
                StringComparison.Ordinal))
        {
            currentSaveFingerprint = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
                options.SaveIsolationPath,
                options.SaveSlot);
            var observedBoundary = NativeSaveBoundaryVerifier.Evaluate(
                nativeSaveBoundaryInitialDayKey,
                nativeSaveBoundaryCurrentDayKey,
                initialSaveFingerprint!,
                currentSaveFingerprint);
            nativeSaveBoundaryVerified = observedBoundary.Verified;
            AppendProgress(
                options,
                nativeSaveBoundaryVerified
                    ? "native_save_boundary_verified"
                    : "native_save_boundary_waiting_for_durable_save",
                iteration,
                lastStateHash,
                lastQueueId,
                "day_advanced=" + observedBoundary.DayAdvanced +
                " save_changed=" + observedBoundary.SaveChanged +
                " initial_day=" + nativeSaveBoundaryInitialDayKey +
                " current_day=" + nativeSaveBoundaryCurrentDayKey);
            await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
            continue;
        }
        if (!string.IsNullOrWhiteSpace(currentDayKey) &&
            !string.Equals(currentDayKey, suppressionDayKey, StringComparison.Ordinal))
        {
            unverifiedExecutionBackoff.Reset();
            var clearedCount = suppressedObjectiveContinuations.Count;
            suppressedObjectiveContinuations.Clear();
            suppressionDayKey = currentDayKey;
            if (clearedCount > 0)
            {
                AppendProgress(
                    options,
                    "objective_suppression_reset",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "reason=game_day_changed cleared_count=" + clearedCount +
                    " day_key=" + currentDayKey);
            }
        }
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
    var decisionModelPlanPath = string.Empty;
    var decisionRankingPath = string.Empty;
    var decisionCompiledQueuePath = string.Empty;
    var decisionSnapshotPath = snapshotPath;
    var decisionSourceStateHash = lastStateHash;
    var selectedCandidateId = string.Empty;
    var selectedQueueIndex = -1;
    var resumedSelectedQueueDecision = false;
    JsonObject? dailyPlanRanking = null;
    JsonObject queue;
    if (options.UseDailyPlan)
    {
        var hasDecisionLease = File.Exists(leasedDecisionModelPlanPath) &&
            File.Exists(leasedDecisionRankingPath) &&
            File.Exists(leasedDecisionCompiledQueuePath) &&
            File.Exists(leasedDecisionSnapshotPath) &&
            !string.IsNullOrWhiteSpace(leasedDecisionSourceStateHash) &&
            !string.IsNullOrWhiteSpace(leasedDecisionGoalId) &&
            !string.IsNullOrWhiteSpace(leasedSelectedCandidateId) &&
            leasedSelectedQueueIndex >= 0;
        if (hasDecisionLease)
        {
            var decisionLease = SelectedQueueDecisionLease.Load(
                leasedDecisionCompiledQueuePath,
                leasedDecisionRankingPath);
            var selectedCandidate = decisionLease.CandidateAt(
                leasedSelectedQueueIndex);
            var refresh = await BuildQueueFromSelectedCandidateAsync(
                http,
                options,
                lastStateHash,
                leasedDecisionGoalId,
                selectedCandidate,
                activeObjectiveContinuation);
            modelPlanPath = Path.Combine(options.SnapshotDir, "candidate-compiled-plan-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(modelPlanPath, refresh.Plan.ToJsonString(JsonOptions), Encoding.UTF8);
            var dailyPlanPath = Path.Combine(options.SnapshotDir, "candidate-compile-response-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(dailyPlanPath, refresh.Response.ToJsonString(JsonOptions), Encoding.UTF8);
            var evidencePath = Path.Combine(options.SnapshotDir, "candidate-refresh-evidence-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(evidencePath, refresh.Evidence.ToJsonString(JsonOptions), Encoding.UTF8);
            dailyPlanRanking = refresh.Evidence;
            queue = refresh.Queue;
            decisionModelPlanPath = leasedDecisionModelPlanPath;
            decisionRankingPath = leasedDecisionRankingPath;
            decisionCompiledQueuePath = leasedDecisionCompiledQueuePath;
            decisionSnapshotPath = leasedDecisionSnapshotPath;
            decisionSourceStateHash = leasedDecisionSourceStateHash;
            selectedCandidateId = selectedCandidate.CandidateId;
            selectedQueueIndex = selectedCandidate.QueueIndex;
            resumedSelectedQueueDecision = true;
        }
        else if (closingNativeSaveBoundary)
        {
            var saveBoundary = await BuildNativeSaveBoundaryQueueAsync(
                http,
                options,
                lastStateHash);
            modelPlanPath = Path.Combine(
                options.SnapshotDir,
                "native-save-boundary-plan-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(
                modelPlanPath,
                saveBoundary.Plan.ToJsonString(JsonOptions),
                Encoding.UTF8);
            var responsePath = Path.Combine(
                options.SnapshotDir,
                "native-save-boundary-response-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(
                responsePath,
                saveBoundary.Response.ToJsonString(JsonOptions),
                Encoding.UTF8);
            rankingPath = Path.Combine(
                options.SnapshotDir,
                "native-save-boundary-ranking-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(
                rankingPath,
                saveBoundary.Ranking.ToJsonString(JsonOptions),
                Encoding.UTF8);
            dailyPlanRanking = saveBoundary.Ranking;
            queue = saveBoundary.Queue;
            decisionModelPlanPath = modelPlanPath;
            decisionRankingPath = rankingPath;
        }
        else
        {
            var dailyPlan = await BuildQueueFromDailyPlanAsync(
                http,
                options,
                lastStateHash,
                suppressedObjectiveContinuations: suppressedObjectiveContinuations);
            modelPlanPath = Path.Combine(options.SnapshotDir, "model-plan-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(modelPlanPath, dailyPlan.Plan.ToJsonString(JsonOptions), Encoding.UTF8);
            var dailyPlanPath = Path.Combine(options.SnapshotDir, "daily-plan-response-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(dailyPlanPath, dailyPlan.Response.ToJsonString(JsonOptions), Encoding.UTF8);
            rankingPath = Path.Combine(options.SnapshotDir, "ranking-response-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(rankingPath, dailyPlan.Ranking.ToJsonString(JsonOptions), Encoding.UTF8);
            dailyPlanRanking = dailyPlan.Ranking;
            queue = dailyPlan.Queue;
            decisionModelPlanPath = modelPlanPath;
            decisionRankingPath = rankingPath;
        }
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
    if (options.UseDailyPlan && !resumedSelectedQueueDecision)
    {
        var firstSelectedItem = ExecutableQueueItems(queue).FirstOrDefault();
        selectedCandidateId = EffectiveDecisionArtifactTracker.ReadQueueItemCandidateId(firstSelectedItem);
        selectedQueueIndex = QueueReplanFilter.ReadAcceptedCandidateIndex(firstSelectedItem);
        decisionCompiledQueuePath = queuePath;
        leasedDecisionModelPlanPath = decisionModelPlanPath;
        leasedDecisionRankingPath = decisionRankingPath;
        leasedDecisionCompiledQueuePath = decisionCompiledQueuePath;
        leasedDecisionSnapshotPath = decisionSnapshotPath;
        leasedDecisionSourceStateHash = decisionSourceStateHash;
        leasedDecisionGoalId = ReadString(queue, "goal_id");
        if (string.IsNullOrWhiteSpace(leasedDecisionGoalId))
        {
            leasedDecisionGoalId = options.Goal;
        }
        leasedSelectedCandidateId = selectedCandidateId;
        leasedSelectedQueueIndex = selectedQueueIndex;
    }
    lastQueueId = ReadString(queue, "queue_id");
    var queueStatus = ReadString(queue, "status");
    var noProgressDecision = noProgressBackoff.Observe(queue);
    if (options.RequireExecutorFeedback)
    {
        if (!string.Equals(queueStatus, "pending", StringComparison.Ordinal))
        {
            var executableSubsetCount = ExecutableQueueItems(queue).Length;
            if (options.UseDailyPlan &&
                resumedSelectedQueueDecision &&
                executableSubsetCount == 0)
            {
                var invalidatedCandidateId = leasedSelectedCandidateId;
                activeObjectiveContinuation = null;
                leasedDecisionModelPlanPath = string.Empty;
                leasedDecisionRankingPath = string.Empty;
                leasedDecisionCompiledQueuePath = string.Empty;
                leasedDecisionSnapshotPath = string.Empty;
                leasedDecisionSourceStateHash = string.Empty;
                leasedDecisionGoalId = string.Empty;
                leasedSelectedCandidateId = string.Empty;
                leasedSelectedQueueIndex = -1;
                noProgressBackoff.Reset();
                AppendProgress(
                    options,
                    "selected_queue_invalidated",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "reason=locked_candidate_unavailable_on_fresh_snapshot candidate_id=" +
                    invalidatedCandidateId +
                    " policy_model_invoked=false");
                await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
                continue;
            }
            if (!options.ContinueAfterBlockedQueueItems || executableSubsetCount == 0)
            {
                if (executableSubsetCount == 0 &&
                    QueueReplanFilter.ShouldReleaseUnavailableContinuation(
                        activeObjectiveContinuation,
                        dailyPlanRanking))
                {
                    var releasedContinuation = activeObjectiveContinuation!;
                    var releasedOptionId = ReadString(
                        releasedContinuation,
                        "option_id");
                    QueueReplanFilter.AddSuppressedContinuation(
                        suppressedObjectiveContinuations,
                        releasedContinuation);
                    activeObjectiveContinuation = null;
                    noProgressBackoff.Reset();
                    AppendProgress(
                        options,
                        "continuation_released",
                        iteration,
                        lastStateHash,
                        lastQueueId,
                        "reason=no_available_continuation_candidate option_id=" +
                        releasedOptionId +
                        " suppressed_until_day_change=true suppressed_count=" +
                        suppressedObjectiveContinuations.Count);
                    await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
                    continue;
                }
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
            ? await ExecuteRuntimeTestHarnessAsync(http, executorHttp, options, iteration, snapshotPath, beforeSnapshot, queue, lastStateHash, lastQueueId, decisionModelPlanPath, decisionRankingPath, decisionCompiledQueuePath, decisionSnapshotPath, decisionSourceStateHash, selectedCandidateId, selectedQueueIndex, queuePath, activeObjectiveContinuation, suppressedObjectiveContinuations)
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
            var completedCandidateRepresentative =
                QueueReplanFilter.LastTrainingVerifiedCompletedSelectedCandidateStep(
                    execution);
            var primitiveVerified =
                QueueReplanFilter.IsTrainingVerifiedExecution(execution) ||
                completedCandidateRepresentative is not null;
            if (!primitiveVerified)
            {
                var unverifiedDecision =
                    unverifiedExecutionBackoff.ObserveUnverifiedExecution(
                        queue,
                        execution);
                AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, options.ExecutorUnverifiedSource + " status=" + ReadString(execution, "status") + " primitive=" + ReadString(execution, "primitive_verification_status") + NoProgressDetail(unverifiedDecision));
                await DelayBeforeNextAttemptAsync(
                    options,
                    attemptOrdinal,
                    unverifiedDecision.DelayMs);
                continue;
            }

            unverifiedExecutionBackoff.Reset();

            var trainingRepresentative =
                QueueReplanFilter.IsTrainingVerifiedExecution(execution)
                    ? execution
                    : completedCandidateRepresentative!;
            var executionNoProgress =
                noProgressBackoff.ObserveExecution(queue, trainingRepresentative);
            var realAppend = AppendRealExecutionRow(
                options,
                beforeSnapshot,
                queue,
                trainingRepresentative,
                lastStateHash,
                lastQueueId,
                appendToDataset: !executionNoProgress.NoProgress);
            objectiveCompleted |= execution[
                "objective_continuation_completed"]?.GetValue<bool>() == true;
            socialObjectiveCompleted |= execution[
                "social_objective_completed"]?.GetValue<bool>() == true;
            if (execution["completed_objective_continuations"] is JsonArray completedContinuations)
            {
                foreach (var completedContinuation in completedContinuations
                    .Select(node => node as JsonObject)
                    .Where(node => node is not null))
                {
                    QueueReplanFilter.AddSuppressedContinuation(
                        suppressedObjectiveContinuations,
                        completedContinuation);
                }
            }
            activeObjectiveContinuation = execution["objective_continuation"] is JsonObject continuation
                ? JsonNode.Parse(continuation.ToJsonString(JsonOptions))?.AsObject()
                : null;
            var selectedQueueDecisionComplete = execution[
                "selected_queue_decision_complete"]?.GetValue<bool>() == true;
            var selectedQueueRedecisionRequired = execution[
                "selected_queue_redecision_required"]?.GetValue<bool>() == true;
            var selectedQueueResumeIndex = execution[
                "selected_queue_resume_index"]?.GetValue<int>() ?? -1;
            if (options.UseDailyPlan &&
                !selectedQueueDecisionComplete &&
                !selectedQueueRedecisionRequired &&
                selectedQueueResumeIndex >= 0)
            {
                leasedSelectedCandidateId = ReadString(
                    execution,
                    "selected_queue_resume_candidate_id");
                leasedSelectedQueueIndex = selectedQueueResumeIndex;
            }
            else
            {
                leasedDecisionModelPlanPath = string.Empty;
                leasedDecisionRankingPath = string.Empty;
                leasedDecisionCompiledQueuePath = string.Empty;
                leasedDecisionSnapshotPath = string.Empty;
                leasedDecisionSourceStateHash = string.Empty;
                leasedDecisionGoalId = string.Empty;
                leasedSelectedCandidateId = string.Empty;
                leasedSelectedQueueIndex = -1;
            }
            WritePlanExecutionEpisode(options, iteration, snapshotPath, modelPlanPath, queuePath, queue, execution, realAppend, lastStateHash, lastQueueId);
            var horizonObservations = AppendClosedHorizonObservations(options, beforeSnapshot, execution);
            var policyAppend = executionNoProgress.NoProgress ||
                closingNativeSaveBoundary
                ? new PolicyTrajectoryAppendBatchResult()
                : AppendPolicyDecisionTrajectories(options, iteration, execution, realAppend);
            rowsAppended = realAppend.RowCount;
            if (!executionNoProgress.NoProgress)
            {
                if (!closingNativeSaveBoundary)
                {
                    verifiedActions++;
                }
                AppendProgress(options, "append", iteration, lastStateHash, lastQueueId, "dataset_rows=" + rowsAppended + " policy_trajectories=" + policyAppend.AppendedCount + " policy_trajectory_skips=" + policyAppend.SkippedCount + " policy_trajectory_first_skip=" + policyAppend.FirstSkipReason + " horizon_observations=" + horizonObservations + " verified_actions=" + verifiedActions + " required_verified_actions=" + options.RequiredVerifiedActions + " source=" + options.ExecutorFeedbackSource);
                var train = options.RequireStructuredPolicy &&
                    policyAppend.AppendedCount == 0 &&
                    horizonObservations == 0
                        ? (TrainingReport: (JsonObject?)null, Prediction: (JsonObject?)null)
                        : await TrainIfNeededAsync(http, options, iteration);
                if (train.TrainingReport is not null)
                {
                    lastTrainingReport = train.TrainingReport;
                    lastPrediction = train.Prediction;
                }
                if (closingNativeSaveBoundary)
                {
                    var afterSnapshotPath = ReadString(
                        execution,
                        "after_snapshot_path");
                    var afterSnapshot = string.IsNullOrWhiteSpace(afterSnapshotPath) ||
                        !File.Exists(afterSnapshotPath)
                            ? new JsonObject()
                            : JsonNode.Parse(
                                await File.ReadAllTextAsync(
                                    afterSnapshotPath,
                                    Encoding.UTF8))?.AsObject() ?? new JsonObject();
                    nativeSaveBoundaryCurrentDayKey =
                        QueueReplanFilter.SnapshotDayKey(afterSnapshot);
                    currentSaveFingerprint = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
                        options.SaveIsolationPath,
                        options.SaveSlot);
                    var saveBoundaryObservation =
                        NativeSaveBoundaryVerifier.Evaluate(
                            nativeSaveBoundaryInitialDayKey,
                            nativeSaveBoundaryCurrentDayKey,
                            initialSaveFingerprint!,
                            currentSaveFingerprint);
                    nativeSaveBoundaryVerified =
                        saveBoundaryObservation.Verified;
                    AppendProgress(
                        options,
                        nativeSaveBoundaryVerified
                            ? "native_save_boundary_verified"
                            : "native_save_boundary_pending",
                        iteration,
                        lastStateHash,
                        lastQueueId,
                        "day_advanced=" + saveBoundaryObservation.DayAdvanced +
                        " save_changed=" + saveBoundaryObservation.SaveChanged +
                        " initial_day=" + nativeSaveBoundaryInitialDayKey +
                        " current_day=" + nativeSaveBoundaryCurrentDayKey);
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
    (!options.StopAfterSocialObjectiveComplete || socialObjectiveCompleted) &&
    (!options.RequireNativeSaveBoundary || nativeSaveBoundaryVerified);
var loopStatus = verifiedTargetMet ? "ok" : "incomplete";
if (!verifiedTargetMet)
{
    Environment.ExitCode = 2;
}

trainingDataTransaction.Complete(verifiedTargetMet);
if (trainingDataTransaction.CanonicalArtifactsUpdated &&
    lastTrainingReport is not null)
{
    var canonicalDatasetManifestPath = Path.Combine(
        options.Root,
        "datasets",
        "formal-policy",
        "policy-dataset-manifest.json");
    var checkpointSha256 = StructuredPolicyCheckpointStore.HashFile(
        options.PolicyCheckpointPath);
    new FormalTrainingManifestStore().UpdateArtifacts(
        options.ManifestPath,
        options.RunId,
        canonicalDatasetManifestPath,
        options.PolicyCheckpointPath,
        checkpointSha256);
    lastTrainingReport["checkpoint_path"] = Path.GetFullPath(
        options.PolicyCheckpointPath);
    lastTrainingReport["checkpoint_sha256"] = checkpointSha256;
    lastTrainingReport["dataset_manifest_path"] = Path.GetFullPath(
        canonicalDatasetManifestPath);
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
    Iterations = attemptsStarted,
    MaxAttempts = options.MaxAttempts,
    AttemptsStarted = attemptsStarted,
    RowsAppended = rowsAppended,
    VerifiedActions = verifiedActions,
    RequiredVerifiedActions = options.RequiredVerifiedActions,
    PrimaryAttemptsStarted = primaryAttemptsStarted,
    NativeSaveBoundaryRequired = options.RequireNativeSaveBoundary,
    NativeSaveBoundaryAttemptsStarted = nativeSaveBoundaryAttemptsStarted,
    NativeSaveBoundaryVerified = nativeSaveBoundaryVerified,
    TrainingDataTransactionStatus = trainingDataTransaction.Status,
    TrainingDataTransactionPath = trainingDataTransaction.StagingRoot,
    CanonicalTrainingArtifactsUpdated =
        trainingDataTransaction.CanonicalArtifactsUpdated,
    NativeSaveBoundaryInitialDayKey = nativeSaveBoundaryInitialDayKey,
    NativeSaveBoundaryCurrentDayKey = nativeSaveBoundaryCurrentDayKey,
    NativeSaveBoundaryInitialSaveSha256 =
        initialSaveFingerprint?.Sha256 ?? string.Empty,
    NativeSaveBoundaryCurrentSaveSha256 =
        currentSaveFingerprint?.Sha256 ?? string.Empty,
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
    iterations = attemptsStarted,
    max_attempts = options.MaxAttempts,
    attempts_started = attemptsStarted,
    rows_appended = rowsAppended,
    verified_actions = verifiedActions,
    required_verified_actions = options.RequiredVerifiedActions,
    primary_attempts_started = primaryAttemptsStarted,
    native_save_boundary_required = options.RequireNativeSaveBoundary,
    native_save_boundary_attempts_started = nativeSaveBoundaryAttemptsStarted,
    native_save_boundary_verified = nativeSaveBoundaryVerified,
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
        if (delayMs > 0)
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
