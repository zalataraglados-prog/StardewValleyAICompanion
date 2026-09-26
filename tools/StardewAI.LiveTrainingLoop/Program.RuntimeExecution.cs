using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<JsonObject> ExecuteRuntimeTestHarnessAsync(
        HttpClient http,
        HttpClient executorHttp,
        LiveTrainingOptions options,
        int iteration,
        string beforeSnapshotPath,
        JsonObject beforeSnapshot,
        JsonObject queue,
        string stateHash,
        string queueId,
        string decisionModelPlanPath,
        string decisionRankingPath,
        string decisionCompiledQueuePath,
        string decisionSnapshotPath,
        string decisionSourceStateHash,
        string initialSelectedCandidateId,
        int initialSelectedQueueIndex,
        string initialMechanicalCompiledQueuePath,
        JsonObject? objectiveContinuation,
        List<JsonObject> suppressedObjectiveContinuations)
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

        SelectedQueueDecisionLease? selectedQueueDecision = null;
        if (options.UseDailyPlan)
        {
            selectedQueueDecision = SelectedQueueDecisionLease.Load(
                decisionCompiledQueuePath,
                decisionRankingPath);
            var initialIndex = initialSelectedQueueIndex >= 0
                ? initialSelectedQueueIndex
                : QueueReplanFilter.ReadAcceptedCandidateIndex(queueItems[0]);
            var currentCandidate = selectedQueueDecision.CandidateAt(initialIndex);
            var isFreshSingleCandidateQueue = !string.Equals(
                Path.GetFullPath(decisionCompiledQueuePath),
                Path.GetFullPath(initialMechanicalCompiledQueuePath),
                StringComparison.OrdinalIgnoreCase);
            if (isFreshSingleCandidateQueue)
            {
                foreach (var item in queueItems)
                {
                    QueueReplanFilter.StampSelectedQueueIdentity(
                        item,
                        currentCandidate.CandidateId,
                        currentCandidate.QueueIndex);
                }

                var originalQueue = JsonNode.Parse(
                    await File.ReadAllTextAsync(decisionCompiledQueuePath))?.AsObject()
                    ?? throw new InvalidOperationException(
                        "selected queue decision compiled queue is invalid");
                var lockedSuffix = ExecutableQueueItems(originalQueue)
                    .Where(item =>
                        QueueReplanFilter.ReadAcceptedCandidateIndex(item) > initialIndex)
                    .ToArray();
                queueItems = queueItems.Concat(lockedSuffix).ToArray();
            }

            foreach (var item in queueItems)
            {
                var itemIndex = QueueReplanFilter.ReadAcceptedCandidateIndex(item);
                var selected = selectedQueueDecision.CandidateAt(itemIndex);
                QueueReplanFilter.StampSelectedQueueIdentity(
                    item,
                    selected.CandidateId,
                    selected.QueueIndex);
            }
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
        var originalQueueId = queueId;
        var attemptedCount = 0;
        var dispatchGateReplanCount = 0;
        var attemptedSemanticKeys = new HashSet<string>(StringComparer.Ordinal);
        var activeObjectiveContinuation = objectiveContinuation is null
            ? null
            : JsonNode.Parse(objectiveContinuation.ToJsonString(JsonOptions))?.AsObject();
        var objectiveContinuationKind = ReadString(activeObjectiveContinuation, "kind");
        var objectiveContinuationCompleted = false;
        var completedObjectiveContinuations = new JsonArray();
        var effectiveDecisionArtifacts = new EffectiveDecisionArtifactTracker(
            decisionModelPlanPath,
            decisionRankingPath,
            decisionCompiledQueuePath,
            string.IsNullOrWhiteSpace(decisionSourceStateHash)
                ? stateHash
                : decisionSourceStateHash,
            string.IsNullOrWhiteSpace(decisionSnapshotPath)
                ? beforeSnapshotPath
                : decisionSnapshotPath);
        var selectedQueueGoalId = ReadString(queue, "goal_id");
        if (string.IsNullOrWhiteSpace(selectedQueueGoalId))
        {
            selectedQueueGoalId = options.Goal;
        }
        foreach (var initialItem in queueItems)
        {
            initialItem["runtime_queue_id"] = queueId;
            initialItem["mechanical_compiled_queue_path"] = initialMechanicalCompiledQueuePath;
            var initialIndex = QueueReplanFilter.ReadAcceptedCandidateIndex(initialItem);
            if (initialIndex >= 0 && !options.UseDailyPlan)
            {
                QueueReplanFilter.StampSelectedQueueIndex(initialItem, initialIndex);
            }
        }
        var selectedQueueIndex = initialSelectedQueueIndex >= 0
            ? initialSelectedQueueIndex
            : QueueReplanFilter.ReadAcceptedCandidateIndex(queueItems[0]);
        var selectedCandidateId = selectedQueueDecision is null
            ? string.IsNullOrWhiteSpace(initialSelectedCandidateId)
                ? EffectiveDecisionArtifactTracker.ReadQueueItemCandidateId(queueItems[0])
                : initialSelectedCandidateId
            : selectedQueueDecision.CandidateAt(selectedQueueIndex).CandidateId;
        if (options.UseDailyPlan &&
            (string.IsNullOrWhiteSpace(selectedCandidateId) || selectedQueueIndex < 0))
        {
            throw new InvalidOperationException(
                "daily plan queue item is missing selected candidate identity or order");
        }
        effectiveDecisionArtifacts.SelectCandidate(
            selectedCandidateId,
            selectedQueueIndex);

        for (var itemIndex = 0; itemIndex < queueItems.Length && attemptedCount < options.MaxQueueItemAttempts; itemIndex++)
        {
            var item = queueItems[itemIndex];
            var itemSelectedQueueIndex = QueueReplanFilter.ReadAcceptedCandidateIndex(item);
            if (options.UseDailyPlan &&
                activeObjectiveContinuation is not null &&
                itemSelectedQueueIndex != selectedQueueIndex)
            {
                finalExecution = SelectedQueueContinuationBoundaryRejectedExecution(
                    item,
                    ReadString(item, "runtime_queue_id"),
                    currentStateHash,
                    selectedCandidateId,
                    selectedQueueIndex,
                    itemSelectedQueueIndex);
                effectiveDecisionArtifacts.Stamp(finalExecution);
                stepResults.Add(JsonNode.Parse(finalExecution.ToJsonString(JsonOptions)));
                break;
            }
            if (options.UseDailyPlan &&
                activeObjectiveContinuation is null &&
                itemSelectedQueueIndex != selectedQueueIndex)
            {
                var nextSelectedCandidate = selectedQueueDecision!.CandidateAt(
                    itemSelectedQueueIndex);
                var boundary = ValidateSelectedQueueBoundary(
                    options,
                    currentBeforeSnapshot,
                    queueItems.Skip(itemIndex).ToArray(),
                    selectedQueueIndex,
                    itemSelectedQueueIndex);
                if (!boundary.Allowed)
                {
                    effectiveDecisionArtifacts.SelectCandidate(
                        nextSelectedCandidate.CandidateId,
                        itemSelectedQueueIndex);
                    finalExecution = SelectedQueueBoundaryRejectedExecution(
                        item,
                        ReadString(item, "runtime_queue_id"),
                        currentStateHash,
                        boundary);
                    effectiveDecisionArtifacts.Stamp(finalExecution);
                    stepResults.Add(JsonNode.Parse(finalExecution.ToJsonString(JsonOptions)));
                    break;
                }

                var refresh = await BuildQueueFromSelectedCandidateAsync(
                    http,
                    options,
                    currentStateHash,
                    selectedQueueGoalId,
                    nextSelectedCandidate);
                var refreshSuffix = "-boundary-" +
                    nextSelectedCandidate.QueueIndex.ToString("D4") +
                    "-item-" + (attemptedCount + 1).ToString("D4");
                var refreshPlanPath = Path.Combine(
                    options.SnapshotDir,
                    "candidate-compiled-plan-" + iteration.ToString("D4") + refreshSuffix + ".json");
                var refreshResponsePath = Path.Combine(
                    options.SnapshotDir,
                    "candidate-compile-response-" + iteration.ToString("D4") + refreshSuffix + ".json");
                var refreshQueuePath = Path.Combine(
                    options.SnapshotDir,
                    "candidate-compiled-queue-" + iteration.ToString("D4") + refreshSuffix + ".json");
                var refreshEvidencePath = Path.Combine(
                    options.SnapshotDir,
                    "candidate-refresh-evidence-" + iteration.ToString("D4") + refreshSuffix + ".json");
                await File.WriteAllTextAsync(
                    refreshPlanPath,
                    refresh.Plan.ToJsonString(JsonOptions),
                    Encoding.UTF8);
                await File.WriteAllTextAsync(
                    refreshResponsePath,
                    refresh.Response.ToJsonString(JsonOptions),
                    Encoding.UTF8);
                await File.WriteAllTextAsync(
                    refreshQueuePath,
                    refresh.Queue.ToJsonString(JsonOptions),
                    Encoding.UTF8);
                await File.WriteAllTextAsync(
                    refreshEvidencePath,
                    refresh.Evidence.ToJsonString(JsonOptions),
                    Encoding.UTF8);

                var refreshQueueId = ReadString(refresh.Queue, "queue_id");
                var refreshedItems = ExecutableQueueItems(refresh.Queue);
                foreach (var refreshedItem in refreshedItems)
                {
                    refreshedItem["runtime_queue_id"] = refreshQueueId;
                    refreshedItem["mechanical_compiled_queue_path"] = refreshQueuePath;
                    QueueReplanFilter.StampSelectedQueueIdentity(
                        refreshedItem,
                        nextSelectedCandidate.CandidateId,
                        nextSelectedCandidate.QueueIndex);
                }

                if (refreshedItems.Length == 0)
                {
                    effectiveDecisionArtifacts.SelectCandidate(
                        nextSelectedCandidate.CandidateId,
                        nextSelectedCandidate.QueueIndex);
                    finalExecution = SelectedQueueCandidateUnavailableExecution(
                        item,
                        ReadString(item, "runtime_queue_id"),
                        currentStateHash,
                        nextSelectedCandidate,
                        refreshEvidencePath);
                    effectiveDecisionArtifacts.Stamp(finalExecution);
                    stepResults.Add(JsonNode.Parse(finalExecution.ToJsonString(JsonOptions)));
                    break;
                }

                var retainedSuffix = queueItems
                    .Skip(itemIndex)
                    .Where(candidateItem =>
                        QueueReplanFilter.ReadAcceptedCandidateIndex(candidateItem) !=
                        nextSelectedCandidate.QueueIndex)
                    .ToArray();
                queueItems = queueItems
                    .Take(itemIndex)
                    .Concat(refreshedItems)
                    .Concat(retainedSuffix)
                    .ToArray();
                item = queueItems[itemIndex];
                itemSelectedQueueIndex = nextSelectedCandidate.QueueIndex;
                var refreshedBoundary = ValidateSelectedQueueBoundary(
                    options,
                    currentBeforeSnapshot,
                    queueItems.Skip(itemIndex).ToArray(),
                    selectedQueueIndex,
                    itemSelectedQueueIndex);
                if (!refreshedBoundary.Allowed)
                {
                    effectiveDecisionArtifacts.SelectCandidate(
                        nextSelectedCandidate.CandidateId,
                        nextSelectedCandidate.QueueIndex);
                    finalExecution = SelectedQueueBoundaryRejectedExecution(
                        item,
                        refreshQueueId,
                        currentStateHash,
                        refreshedBoundary);
                    finalExecution["selected_queue_candidate_refresh_evidence_path"] =
                        refreshEvidencePath;
                    effectiveDecisionArtifacts.Stamp(finalExecution);
                    stepResults.Add(JsonNode.Parse(finalExecution.ToJsonString(JsonOptions)));
                    break;
                }
                queueId = refreshQueueId;
                selectedCandidateId = nextSelectedCandidate.CandidateId;
                selectedQueueIndex = itemSelectedQueueIndex;
                effectiveDecisionArtifacts.SelectCandidate(
                    selectedCandidateId,
                    selectedQueueIndex);
            }
            var itemQueueId = ReadString(item, "runtime_queue_id");
            if (string.IsNullOrWhiteSpace(itemQueueId))
            {
                itemQueueId = queueId;
            }
            JsonObject? dispatchReadiness = null;
            if (!QueueContainsRecoveryWork(queue))
            {
                var latest = await ReadCurrentSnapshotAsync(http, options);
                var latestTime = ReadInGameTime(latest.Snapshot);
                if (latestTime >= GameClockBudgetPolicy.AutonomousRecoveryStartTime)
                {
                    latest = await ReadExecutionSnapshotAsync(
                        http,
                        options,
                        forceRefresh: true);
                    latestTime = ReadInGameTime(latest.Snapshot);
                    var recoverySuffix = "-dispatch-recovery-" +
                        (dispatchGateReplanCount + 1).ToString("D4");
                    var recoverySnapshotPath = Path.Combine(
                        options.SnapshotDir,
                        "before-snapshot-" + iteration.ToString("D4") +
                        recoverySuffix + ".json");
                    await ContentAddressedJsonArtifactStore.WriteAsync(
                        recoverySnapshotPath,
                        latest.Json,
                        options.SnapshotArtifactMode);
                    var ingest = await PostJsonStringAsync(
                        http,
                        SnapshotIngestUrl(options),
                        latest.Json);
                    currentBeforeSnapshot = latest.Snapshot;
                    currentBeforeSnapshotPath = recoverySnapshotPath;
                    currentStateHash = ReadString(ingest, "state_hash");
                    dispatchReadiness = new JsonObject
                    {
                        ["ready"] = false,
                        ["status"] = "blocked",
                        ["state_hash"] = currentStateHash,
                        ["current_time"] = latestTime,
                        ["blocking_reasons"] = new JsonArray(
                            "dispatch_recovery_window_started"),
                        ["policy"] =
                            "fresh_snapshot_before_dispatch;exclusive_recovery_replan"
                    };
                }
            }

            var compiledCommandStateHash = ReadString(
                item["normalized_command"] as JsonObject,
                "state_hash");
            if (options.UseTeacherPreferenceQueue)
            {
                item = TeacherPreferenceQueueLoader.RebindQueueItemToState(
                    item,
                    currentStateHash);
                queueItems[itemIndex] = item;
            }

            dispatchReadiness ??= await ReadDispatchReadinessAsync(
                http,
                options,
                item,
                currentStateHash,
                itemQueueId);
            if (dispatchReadiness is not null &&
                dispatchReadiness["ready"]?.GetValue<bool>() != true)
            {
                dispatchGateReplanCount++;
                var dispatchSuffix = "-dispatch-" + dispatchGateReplanCount.ToString("D4");
                var dispatchPath = Path.Combine(
                    options.SnapshotDir,
                    "dispatch-readiness-" + iteration.ToString("D4") +
                    dispatchSuffix + ".json");
                await File.WriteAllTextAsync(
                    dispatchPath,
                    dispatchReadiness.ToJsonString(JsonOptions),
                    Encoding.UTF8);
                finalExecution = DispatchRejectedExecution(
                    item,
                    dispatchReadiness,
                    itemQueueId,
                    currentStateHash,
                    "selected_queue_invalidated_at_dispatch_boundary");
                finalExecution["policy_model_invoked"] = false;
                finalExecution["selected_queue_redecision_required"] = true;
                effectiveDecisionArtifacts.Stamp(finalExecution);
                stepResults.Add(JsonNode.Parse(finalExecution.ToJsonString(JsonOptions)));
                break;
            }

            var itemSemanticKey = QueueReplanFilter.SemanticQueueItemKey(item);
            var effectiveStateHash = currentStateHash;
            var executionRequest = BuildExecutionRequest(options, item, currentStateHash, itemQueueId);
            var request = JsonSerializer.Serialize(executionRequest, JsonOptions);
            var execution = await PostJsonStringAsync(executorHttp, options.ExecutorUrl + options.ExecutorEndpointPath, request);
            attemptedCount++;

            var afterSnapshot = await ReadAfterExecutionSnapshotAsync(
                http,
                options,
                currentBeforeSnapshot,
                execution);
            finalAfterJson = afterSnapshot.Json;
            finalAfterSnapshot = afterSnapshot.Snapshot;
            var itemSuffix = "-item-" + attemptedCount.ToString("D4");
            var executionPath = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + itemSuffix + ".json");
            var afterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + itemSuffix + ".json");
            await ContentAddressedJsonArtifactStore.WriteAsync(
                afterPath,
                finalAfterJson,
                options.SnapshotArtifactMode);
            await PostJsonStringAsync(
                http,
                SnapshotIngestUrl(options),
                finalAfterJson);

            execution["queue_execution_mode"] = "sequential_queue_items";
            execution["queue_item_index"] = itemIndex;
            execution["queue_item_count"] = queueItems.Length;
            execution["queue_original_planned_item_count"] = originalPlannedItemCount;
            execution["queue_item_semantic_key"] = itemSemanticKey;
            execution["effective_queue_id"] = executionRequest.QueueId;
            execution["effective_queue_item"] = JsonNode.Parse(item.ToJsonString(JsonOptions));
            execution["effective_before_state_hash"] = effectiveStateHash;
            execution["compiled_command_state_hash"] = compiledCommandStateHash;
            execution["teacher_preference_state_rebound"] =
                options.UseTeacherPreferenceQueue;
            execution["effective_before_snapshot_path"] = currentBeforeSnapshotPath;
            effectiveDecisionArtifacts.Stamp(execution);
            execution["mechanical_compiled_queue_path"] = ReadString(
                item,
                "mechanical_compiled_queue_path");
            execution["policy_model_invoked_for_step"] = false;
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
            execution["source"] = options.ExecutorFeedbackSource;
            currentBeforeSnapshot = afterSnapshot.Snapshot;
            currentBeforeSnapshotPath = afterPath;
            currentStateHash = ReadString(afterSnapshot.Snapshot, "state_hash");
            attemptedSemanticKeys.Add(itemSemanticKey);

            var executionStatus = ReadString(execution, "status");
            var completedContinuationThisStep = false;
            var continuationForCompletion = activeObjectiveContinuation;
            if (continuationForCompletion is null &&
                string.Equals(executionStatus, "applied", StringComparison.Ordinal))
            {
                continuationForCompletion =
                    QueueReplanFilter.ReadObjectiveContinuation(item);
            }

            if (QueueReplanFilter.CompletesObjectiveContinuation(
                    item,
                    continuationForCompletion,
                    executionStatus,
                    afterSnapshot.Snapshot,
                    afterSnapshot.Fresh))
            {
                if (string.IsNullOrWhiteSpace(objectiveContinuationKind) &&
                    string.Equals(ReadString(item, "option_id"), "executor.social_interact", StringComparison.Ordinal))
                {
                    objectiveContinuationKind = "social";
                }
                else if (string.IsNullOrWhiteSpace(objectiveContinuationKind))
                {
                    objectiveContinuationKind = ReadString(
                        continuationForCompletion,
                        "kind");
                }
                objectiveContinuationCompleted = true;
                completedContinuationThisStep = true;
                if (continuationForCompletion is not null)
                {
                    var completedContinuation = JsonNode.Parse(
                        continuationForCompletion.ToJsonString(JsonOptions))!.AsObject();
                    completedObjectiveContinuations.Add(
                        JsonNode.Parse(completedContinuation.ToJsonString(JsonOptions)));
                    QueueReplanFilter.AddSuppressedContinuation(
                        suppressedObjectiveContinuations,
                        completedContinuation);
                }
                activeObjectiveContinuation = null;
            }
            else if (continuationForCompletion is not null &&
                string.Equals(executionStatus, "applied", StringComparison.Ordinal))
            {
                activeObjectiveContinuation =
                    QueueReplanFilter.RefreshAppliedObjectiveContinuation(
                        item,
                        continuationForCompletion);
                objectiveContinuationKind = ReadString(
                    activeObjectiveContinuation,
                    "kind");
            }

            var replanDecision = QueueReplanFilter.DecideAfterExecution(
                executionStatus,
                options.ContinueAfterBlockedQueueItems,
                options.UseDailyPlan,
                !string.IsNullOrWhiteSpace(options.ExecutorOptionId),
                afterSnapshot.Fresh,
                attemptedCount < options.MaxQueueItemAttempts,
                activeObjectiveContinuation is not null ||
                    QueueReplanFilter.RequiresFreshSnapshotReplan(item),
                completedContinuationThisStep);

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
                if (activeObjectiveContinuation is null)
                {
                    execution["queue_replan_applied"] = false;
                    if (!QueueReplanFilter.IsContinuableExecutionStatus(
                            executionStatus))
                    {
                        execution["selected_queue_redecision_required"] = true;
                        execution["queue_replan_stop_reason"] =
                            "selected_queue_candidate_blocked_without_continuation";
                    }
                    else
                    {
                        execution["queue_replan_skip_reason"] =
                            "selected_candidate_has_no_active_mechanical_continuation";
                    }
                }
                else
                {
                    var selectedCandidate = selectedQueueDecision!.CandidateAt(
                        selectedQueueIndex);
                    var refresh = await BuildQueueFromSelectedCandidateAsync(
                        http,
                        options,
                        currentStateHash,
                        selectedQueueGoalId,
                        selectedCandidate,
                        activeObjectiveContinuation);
                    var refreshSuffix = "-item-" + (attemptedCount + 1).ToString("D4");
                    var refreshPlanPath = Path.Combine(options.SnapshotDir, "continuation-compiled-plan-" + iteration.ToString("D4") + refreshSuffix + ".json");
                    var refreshResponsePath = Path.Combine(options.SnapshotDir, "continuation-compile-response-" + iteration.ToString("D4") + refreshSuffix + ".json");
                    var refreshQueuePath = Path.Combine(options.SnapshotDir, "continuation-compiled-queue-" + iteration.ToString("D4") + refreshSuffix + ".json");
                    var refreshEvidencePath = Path.Combine(options.SnapshotDir, "continuation-refresh-evidence-" + iteration.ToString("D4") + refreshSuffix + ".json");
                    await File.WriteAllTextAsync(refreshPlanPath, refresh.Plan.ToJsonString(JsonOptions), Encoding.UTF8);
                    await File.WriteAllTextAsync(refreshResponsePath, refresh.Response.ToJsonString(JsonOptions), Encoding.UTF8);
                    await File.WriteAllTextAsync(refreshQueuePath, refresh.Queue.ToJsonString(JsonOptions), Encoding.UTF8);
                    await File.WriteAllTextAsync(refreshEvidencePath, refresh.Evidence.ToJsonString(JsonOptions), Encoding.UTF8);

                    var refreshQueueId = ReadString(refresh.Queue, "queue_id");
                    var refreshedItems = QueueReplanFilter.FilterUnattempted(
                        ExecutableQueueItems(refresh.Queue),
                        attemptedSemanticKeys);
                    foreach (var refreshedItem in refreshedItems)
                    {
                        refreshedItem["runtime_queue_id"] = refreshQueueId;
                        refreshedItem["mechanical_compiled_queue_path"] = refreshQueuePath;
                        QueueReplanFilter.StampSelectedQueueIdentity(
                            refreshedItem,
                            selectedCandidateId,
                            selectedQueueIndex);
                    }

                    var retainedSuffix = queueItems
                        .Skip(itemIndex + 1)
                        .Where(candidateItem =>
                            QueueReplanFilter.ReadAcceptedCandidateIndex(candidateItem) !=
                            selectedQueueIndex)
                        .ToArray();
                    queueItems = queueItems
                        .Take(itemIndex + 1)
                        .Concat(refreshedItems)
                        .Concat(retainedSuffix)
                        .ToArray();
                    queueId = refreshQueueId;
                    execution["queue_replan_applied"] = refreshedItems.Length > 0;
                    execution["queue_replan_kind"] = "selected_queue_mechanical_continuation";
                    execution["policy_model_invoked"] = false;
                    execution["selected_queue_lock_preserved"] = true;
                    execution["queue_replan_trigger_status"] = executionStatus;
                    execution["queue_replan_trigger_reason"] = replanDecision.Reason;
                    execution["queue_replan_source_state_hash"] = currentStateHash;
                    execution["queue_replan_previous_queue_id"] = executionRequest.QueueId;
                    execution["queue_replan_queue_id"] = refreshQueueId;
                    execution["queue_replan_trigger_queue_item_id"] = executionRequest.QueueItemId;
                    execution["queue_replan_trigger_semantic_key"] = itemSemanticKey;
                    execution["queue_replan_remaining_after_filter"] = refreshedItems.Length;
                    execution["queue_replan_attempted_semantic_key_count"] = attemptedSemanticKeys.Count;
                    execution["queue_replan_item_count"] = queueItems.Length - itemIndex - 1;
                    execution["queue_replan_plan_path"] = refreshPlanPath;
                    execution["queue_replan_response_path"] = refreshResponsePath;
                    execution["queue_replan_queue_path"] = refreshQueuePath;
                    execution["queue_replan_evidence_path"] = refreshEvidencePath;
                    if (QueueReplanFilter.ShouldResumeContinuationOnNextIteration(
                            activeObjectiveContinuation,
                            refreshedItems.Length))
                    {
                        // A timed wait may remain the same semantic action until
                        // the next clock boundary. Keep the selected task leased.
                        queueItems = queueItems.Take(itemIndex + 1).ToArray();
                        execution["selected_queue_redecision_required"] = false;
                        execution["selected_queue_continuation_retry_next_iteration"] = true;
                        execution["queue_replan_stop_reason"] =
                            "selected_queue_continuation_retry_next_iteration";
                    }
                }
            }
            else
            {
                execution["queue_replan_applied"] = false;
                execution["queue_replan_skip_reason"] = replanDecision.Reason;
            }

            var nextItem = itemIndex + 1 < queueItems.Length
                ? queueItems[itemIndex + 1]
                : null;
            var nextCandidateId = EffectiveDecisionArtifactTracker.ReadQueueItemCandidateId(
                nextItem);
            var selectedCandidateCompleted = options.UseTeacherPreferenceQueue
                ? string.Equals(
                        executionStatus,
                        "applied",
                        StringComparison.Ordinal) &&
                    itemIndex == originalPlannedItemCount - 1 &&
                    attemptedCount == originalPlannedItemCount
                : QueueReplanFilter.CompletesSelectedQueueCandidate(
                    executionStatus,
                    completedContinuationThisStep,
                    activeObjectiveContinuation,
                    selectedCandidateId,
                    nextCandidateId);
            execution["selected_queue_candidate_completed"] = selectedCandidateCompleted;

            await File.WriteAllTextAsync(executionPath, execution.ToJsonString(JsonOptions), Encoding.UTF8);
            stepResults.Add(JsonNode.Parse(execution.ToJsonString(JsonOptions)));
            finalExecution = execution;
            if (execution["selected_queue_redecision_required"]?.GetValue<bool>() == true)
            {
                break;
            }
        }

        await ContentAddressedJsonArtifactStore.WriteAsync(
            aggregateAfterPath,
            finalAfterJson,
            options.SnapshotArtifactMode);
        var aggregate = JsonNode.Parse((finalExecution ?? new JsonObject()).ToJsonString(JsonOptions))?.AsObject() ?? new JsonObject();
        aggregate["queue_execution_mode"] = "sequential_queue_items";
        aggregate["planned_item_count"] = originalPlannedItemCount;
        aggregate["final_pending_item_count"] = QueueReplanFilter.RemainingQueueItemCount(
            queueItems.Length,
            attemptedCount);
        aggregate["executed_item_count"] = attemptedCount;
        aggregate["dispatch_gate_replan_count"] = dispatchGateReplanCount;
        aggregate["max_queue_item_attempts"] = options.MaxQueueItemAttempts;
        aggregate["step_results"] = stepResults;
        aggregate["after_snapshot_path"] = aggregateAfterPath;
        aggregate["execution_path"] = aggregateExecutionPath;
        aggregate["after_state_hash"] = ReadString(finalAfterSnapshot, "state_hash");
        aggregate["before_game_tick"] = ReadLong(beforeSnapshot, "game_tick");
        aggregate["after_game_tick"] = ReadLong(finalAfterSnapshot, "game_tick");
        aggregate["state_hash_changed"] = !string.Equals(stateHash, ReadString(finalAfterSnapshot, "state_hash"), StringComparison.Ordinal);
        if (options.UseTeacherPreferenceQueue ||
            options.EmitQueueExecutionReceipt)
        {
            var receiptSteps = stepResults.OfType<JsonObject>().ToArray();
            var completionMarkerExact = receiptSteps.Length > 0 &&
                receiptSteps[^1]["selected_queue_candidate_completed"]?
                    .GetValue<bool>() == true &&
                receiptSteps.Take(receiptSteps.Length - 1).All(step =>
                    step["selected_queue_candidate_completed"]?
                        .GetValue<bool>() != true);
            var allStepsApplied =
                receiptSteps.Length == originalPlannedItemCount &&
                receiptSteps.All(step => string.Equals(
                    ReadString(step, "status"),
                    "applied",
                    StringComparison.Ordinal));
            var allSnapshotsFresh = receiptSteps.Length > 0 &&
                receiptSteps.All(step =>
                    step["after_snapshot_fresh"]?.GetValue<bool>() == true);
            aggregate["schema_version"] = "queue_execution_receipt.v1";
            aggregate["run_id"] = options.RunId;
            aggregate["queue_id"] = originalQueueId;
            aggregate["source_state_hash"] = stateHash;
            aggregate["after_snapshot_fresh"] = allSnapshotsFresh;
            aggregate["selected_candidate_id"] = selectedCandidateId;
            aggregate["selected_candidate_completed"] = completionMarkerExact;
            aggregate["status"] = allStepsApplied && completionMarkerExact
                ? "applied"
                : "blocked";
            aggregate["success"] = allStepsApplied &&
                allSnapshotsFresh &&
                completionMarkerExact;
            aggregate["block_reasons"] = new JsonArray(receiptSteps
                .SelectMany(step => ReadArrayStrings(step, "block_reasons"))
                .Distinct(StringComparer.Ordinal)
                .Select(reason => JsonValue.Create(reason))
                .ToArray());
        }
        aggregate["source"] = options.ExecutorFeedbackSource;
        aggregate["objective_continuation_completed"] = objectiveContinuationCompleted;
        aggregate["completed_objective_continuations"] = completedObjectiveContinuations;
        aggregate["objective_continuation_suppressed_until_day_change"] =
            completedObjectiveContinuations.Count > 0;
        aggregate["objective_continuation"] = activeObjectiveContinuation is null
            ? null
            : JsonNode.Parse(activeObjectiveContinuation.ToJsonString(JsonOptions));
        var continuationIsSocial = string.Equals(objectiveContinuationKind, "social", StringComparison.Ordinal);
        var continuationIsQuest = string.Equals(objectiveContinuationKind, "quest", StringComparison.Ordinal);
        aggregate["social_objective_completed"] = objectiveContinuationCompleted && continuationIsSocial;
        aggregate["social_objective_continuation"] = continuationIsSocial && activeObjectiveContinuation is not null
            ? JsonNode.Parse(activeObjectiveContinuation.ToJsonString(JsonOptions))
            : null;
        aggregate["quest_objective_completed"] = objectiveContinuationCompleted && continuationIsQuest;
        aggregate["quest_objective_continuation"] = continuationIsQuest && activeObjectiveContinuation is not null
            ? JsonNode.Parse(activeObjectiveContinuation.ToJsonString(JsonOptions))
            : null;
        if (selectedQueueDecision is not null)
        {
            var currentCandidateCompleted = stepResults
                .Select(node => node as JsonObject)
                .Any(step =>
                    step is not null &&
                    step["selected_queue_candidate_completed"]?.GetValue<bool>() == true &&
                    step["effective_selected_queue_index"]?.GetValue<int>() ==
                        selectedQueueIndex);
            var redecisionRequired = aggregate[
                "selected_queue_redecision_required"]?.GetValue<bool>() == true;
            var resumeQueueIndex = activeObjectiveContinuation is not null
                ? selectedQueueIndex
                : currentCandidateCompleted
                    ? selectedQueueIndex + 1
                    : selectedQueueIndex;
            var decisionComplete = !redecisionRequired &&
                resumeQueueIndex >= selectedQueueDecision.Candidates.Count;
            aggregate["selected_queue_decision_complete"] = decisionComplete;
            aggregate["selected_queue_candidate_count"] =
                selectedQueueDecision.Candidates.Count;
            aggregate["selected_queue_resume_index"] = decisionComplete
                ? -1
                : resumeQueueIndex;
            aggregate["selected_queue_resume_candidate_id"] =
                !decisionComplete &&
                resumeQueueIndex >= 0 &&
                resumeQueueIndex < selectedQueueDecision.Candidates.Count
                    ? selectedQueueDecision.CandidateAt(resumeQueueIndex).CandidateId
                    : string.Empty;
        }
        await File.WriteAllTextAsync(aggregateExecutionPath, aggregate.ToJsonString(JsonOptions), Encoding.UTF8);
        return aggregate;
    }

}
