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
        string initialModelPlanPath,
        string initialRankingPath,
        string initialCompiledQueuePath,
        JsonObject? objectiveContinuation)
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
        var dispatchGateReplanCount = 0;
        var attemptedSemanticKeys = new HashSet<string>(StringComparer.Ordinal);
        var activeObjectiveContinuation = objectiveContinuation is null
            ? null
            : JsonNode.Parse(objectiveContinuation.ToJsonString(JsonOptions))?.AsObject();
        var objectiveContinuationKind = ReadString(activeObjectiveContinuation, "kind");
        var objectiveContinuationCompleted = false;
        var effectiveDecisionArtifacts = new EffectiveDecisionArtifactTracker(
            initialModelPlanPath,
            initialRankingPath,
            initialCompiledQueuePath,
            stateHash);

        for (var itemIndex = 0; itemIndex < queueItems.Length && attemptedCount < options.MaxQueueItemAttempts; itemIndex++)
        {
            var item = queueItems[itemIndex];
            var dispatchReadiness = await ReadDispatchReadinessAsync(
                http,
                options,
                item,
                currentStateHash,
                queueId);
            if (dispatchReadiness is not null &&
                dispatchReadiness["ready"]?.GetValue<bool>() != true)
            {
                var rejectedQueueId = queueId;
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

                var canReplan = options.UseDailyPlan &&
                    string.IsNullOrWhiteSpace(options.ExecutorOptionId) &&
                    dispatchGateReplanCount < options.MaxQueueItemAttempts;
                if (!canReplan)
                {
                    finalExecution = DispatchRejectedExecution(
                        item,
                        dispatchReadiness,
                        queueId,
                        currentStateHash,
                        "dispatch_guard_replan_unavailable_or_exhausted");
                    stepResults.Add(JsonNode.Parse(finalExecution.ToJsonString(JsonOptions)));
                    break;
                }

                var replan = await BuildQueueFromDailyPlanAsync(
                    http,
                    options,
                    currentStateHash,
                    activeObjectiveContinuation);
                var replanPlanPath = Path.Combine(
                    options.SnapshotDir,
                    "replan-model-plan-" + iteration.ToString("D4") +
                    dispatchSuffix + ".json");
                var replanDailyPlanPath = Path.Combine(
                    options.SnapshotDir,
                    "replan-daily-plan-response-" + iteration.ToString("D4") +
                    dispatchSuffix + ".json");
                var replanQueuePath = Path.Combine(
                    options.SnapshotDir,
                    "replan-compiled-queue-" + iteration.ToString("D4") +
                    dispatchSuffix + ".json");
                var replanRankingPath = Path.Combine(
                    options.SnapshotDir,
                    "replan-ranking-response-" + iteration.ToString("D4") +
                    dispatchSuffix + ".json");
                await File.WriteAllTextAsync(
                    replanPlanPath,
                    replan.Plan.ToJsonString(JsonOptions),
                    Encoding.UTF8);
                await File.WriteAllTextAsync(
                    replanDailyPlanPath,
                    replan.Response.ToJsonString(JsonOptions),
                    Encoding.UTF8);
                await File.WriteAllTextAsync(
                    replanQueuePath,
                    replan.Queue.ToJsonString(JsonOptions),
                    Encoding.UTF8);
                await File.WriteAllTextAsync(
                    replanRankingPath,
                    replan.Ranking.ToJsonString(JsonOptions),
                    Encoding.UTF8);

                effectiveDecisionArtifacts.Replace(
                    replanPlanPath,
                    replanRankingPath,
                    replanQueuePath,
                    currentStateHash);

                queue = replan.Queue;
                queueId = ReadString(queue, "queue_id");
                dispatchReadiness["replan_queue_id"] = queueId;
                dispatchReadiness["replan_plan_path"] = replanPlanPath;
                dispatchReadiness["replan_response_path"] = replanDailyPlanPath;
                dispatchReadiness["replan_queue_path"] = replanQueuePath;
                dispatchReadiness["replan_ranking_path"] = replanRankingPath;
                await File.WriteAllTextAsync(
                    dispatchPath,
                    dispatchReadiness.ToJsonString(JsonOptions),
                    Encoding.UTF8);
                queueItems = QueueReplanFilter.FilterUnattempted(
                    ExecutableQueueItems(queue),
                    attemptedSemanticKeys);
                if (queueItems.Length == 0)
                {
                    finalExecution = DispatchRejectedExecution(
                        item,
                        dispatchReadiness,
                        rejectedQueueId,
                        currentStateHash,
                        "dispatch_guard_replan_has_no_executable_items");
                    stepResults.Add(JsonNode.Parse(finalExecution.ToJsonString(JsonOptions)));
                    break;
                }
                itemIndex = -1;
                continue;
            }

            var itemSemanticKey = QueueReplanFilter.SemanticQueueItemKey(item);
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
            execution["effective_before_snapshot_path"] = currentBeforeSnapshotPath;
            effectiveDecisionArtifacts.Stamp(execution);
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
            execution["source"] = "runtime_test_harness_executor";
            currentBeforeSnapshot = afterSnapshot.Snapshot;
            currentBeforeSnapshotPath = afterPath;
            currentStateHash = ReadString(afterSnapshot.Snapshot, "state_hash");
            attemptedSemanticKeys.Add(itemSemanticKey);

            var executionStatus = ReadString(execution, "status");
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
                    executionStatus))
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
                activeObjectiveContinuation = null;
            }
            else if (continuationForCompletion is not null &&
                string.Equals(executionStatus, "applied", StringComparison.Ordinal))
            {
                activeObjectiveContinuation = continuationForCompletion;
                objectiveContinuationKind = ReadString(
                    continuationForCompletion,
                    "kind");
            }

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
                var replan = await BuildQueueFromDailyPlanAsync(http, options, currentStateHash, activeObjectiveContinuation);
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
                effectiveDecisionArtifacts.Replace(
                    replanPlanPath,
                    replanRankingPath,
                    replanQueuePath,
                    currentStateHash);
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
        aggregate["dispatch_gate_replan_count"] = dispatchGateReplanCount;
        aggregate["max_queue_item_attempts"] = options.MaxQueueItemAttempts;
        aggregate["step_results"] = stepResults;
        aggregate["after_snapshot_path"] = aggregateAfterPath;
        aggregate["execution_path"] = aggregateExecutionPath;
        aggregate["after_state_hash"] = ReadString(finalAfterSnapshot, "state_hash");
        aggregate["before_game_tick"] = ReadLong(beforeSnapshot, "game_tick");
        aggregate["after_game_tick"] = ReadLong(finalAfterSnapshot, "game_tick");
        aggregate["state_hash_changed"] = !string.Equals(stateHash, ReadString(finalAfterSnapshot, "state_hash"), StringComparison.Ordinal);
        aggregate["source"] = "runtime_test_harness_executor";
        aggregate["objective_continuation_completed"] = objectiveContinuationCompleted;
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
        await File.WriteAllTextAsync(aggregateExecutionPath, aggregate.ToJsonString(JsonOptions), Encoding.UTF8);
        return aggregate;
    }

    private static TrainingExecutionRequest BuildExecutionRequest(
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
            ExecutionMode = options.TargetExecutionMode,
            Actor = options.TargetActor.ActorId,
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
        var requiredWeaponEnchantmentRuntimeType = ReadQueueParameterString(item, "required_weapon_enchantment_runtime_type");
        var combatWeaponSlotIndex = ReadQueueParameterInt(item, "combat_weapon_slot_index");
        var combatMethod = ReadQueueParameterString(item, "combat_method");
        var combatTerminalState = ReadQueueParameterString(item, "combat_terminal_state");
        var combatIntent = ReadQueueParameterString(item, "combat_intent");
        var slingshotSlotIndex = ReadQueueParameterInt(item, "slingshot_slot_index");
        var slingshotAmmoQualifiedItemId = ReadQueueParameterString(item, "slingshot_ammo_qualified_item_id");
        var bombSlotIndex = ReadQueueParameterInt(item, "bomb_slot_index");
        var bombQualifiedItemId = ReadQueueParameterString(item, "bomb_qualified_item_id");
        var bombRadiusTiles = ReadQueueParameterInt(item, "bomb_radius_tiles");
        var escapeTileX = ReadQueueParameterInt(item, "escape_tile_x");
        var escapeTileY = ReadQueueParameterInt(item, "escape_tile_y");
        var expectedBombObjectHits = ReadQueueParameterInt(item, "expected_bomb_object_hits");
        var expectedBombMonsterHits = ReadQueueParameterInt(item, "expected_bomb_monster_hits");
        var direction = options.Direction ?? ReadQueueParameterInt(item, "direction");
        var waitTicks = options.WaitTicks ?? ReadQueueParameterInt(item, "wait_ticks");
        var sleepResumeMode = ReadQueueParameterString(
            item,
            "sleep_resume_mode");
        var maxCrops = ReadQueueParameterInt(item, "max_crops") ?? ReadQueueParameterInt(item, "max_tool_swings");
        var maxMovementTiles = ReadQueueParameterInt(item, "max_movement_tiles");
        var expectedMineLevelDelta = ReadQueueParameterInt(item, "expected_mine_level_delta");
        var expectedMineLevelAfter = ReadQueueParameterInt(item, "expected_mine_level_after");
        var expectedHealthCost = ReadQueueParameterInt(item, "expected_health_cost");
        var expectedHealthAfter = ReadQueueParameterInt(item, "expected_health_after");
        var miningStepReason = ReadQueueParameterString(item, "mining_step_reason");
        var safeSlotIndex = ReadQueueParameterInt(item, "safe_slot_index");
        var restoreSlotIndex = ReadQueueParameterInt(item, "restore_slot_index");
        var wateringCanSlotIndex = ReadQueueParameterInt(item, "watering_can_slot_index");
        var toolSlotIndex = ReadQueueParameterInt(item, "tool_slot_index");
        var requiredToolKind = ReadQueueParameterString(item, "required_tool_kind");
        var panUpgradeLevel = ReadQueueParameterInt(item, "pan_upgrade_level");
        var panEnchantmentsJson = ReadQueueParameterString(item, "pan_enchantments_json");
        var clickPixelX = ReadQueueParameterInt(item, "click_pixel_x");
        var clickPixelY = ReadQueueParameterInt(item, "click_pixel_y");
        var expectedTimesPannedBefore = ReadQueueParameterInt(item, "expected_times_panned_before");
        var expectedTimesPannedAfter = ReadQueueParameterInt(item, "expected_times_panned_after");
        var expectedMiningExperienceBefore = ReadQueueParameterInt(item, "expected_mining_experience_before");
        var expectedMiningExperienceDelta = ReadQueueParameterInt(item, "expected_mining_experience_delta");
        var expectedMiningExperienceAfter = ReadQueueParameterInt(item, "expected_mining_experience_after");
        var expectedForagingExperienceBefore = ReadQueueParameterInt(item, "expected_foraging_experience_before");
        var expectedForagingExperienceDelta = ReadQueueParameterInt(item, "expected_foraging_experience_delta");
        var expectedForagingExperienceAfter = ReadQueueParameterInt(item, "expected_foraging_experience_after");
        var expectedFarmingExperienceDelta = ReadQueueParameterInt(item, "expected_farming_experience_delta");
        var postUseOrePanPointStatus = ReadQueueParameterString(item, "post_use_ore_pan_point_status");
        var postUseRespawnAttempts = ReadQueueParameterInt(item, "post_use_respawn_attempts");
        var clearOutputProjectionStatus = ReadQueueParameterString(item, "clear_output_projection_status");
        var clearOutputItemsJson = ReadQueueParameterString(item, "clear_output_items_json");
        var expectedOutputItemsJson = ReadQueueParameterString(item, "expected_output_items_json");
        var expectedOutputQuality = ReadQueueParameterInt(item, "expected_output_quality");
        var expectedAnimalCrackerMultiplier = ReadQueueParameterInt(item, "expected_animal_cracker_multiplier");
        var expectedEnergyDelta = ReadQueueParameterInt(item, "expected_energy_delta");
        var expectedFriendshipBefore = ReadQueueParameterInt(item, "expected_friendship_before");
        var expectedFriendshipAfter = ReadQueueParameterInt(item, "expected_friendship_after");
        var expectedLastPetDayBeforeText = ReadQueueParameterString(item, "expected_last_pet_day_before");
        var expectedLastPetDayBefore = ReadQueueParameterInt(item, "expected_last_pet_day_before");
        var expectedLastPetDayAfter = ReadQueueParameterInt(item, "expected_last_pet_day_after");
        var expectedTimesPetBefore = ReadQueueParameterInt(item, "expected_times_pet_before");
        var expectedTimesPetAfter = ReadQueueParameterInt(item, "expected_times_pet_after");
        var expectedGrantedFriendshipBefore = ReadNullableBoolQueueParameter(item, "expected_granted_friendship_before");
        var expectedGrantedFriendshipAfter = ReadNullableBoolQueueParameter(item, "expected_granted_friendship_after");
        var expectedPetLoveMailBefore = ReadNullableBoolQueueParameter(item, "expected_pet_love_mail_before");
        var expectedPetLoveMailAfter = ReadNullableBoolQueueParameter(item, "expected_pet_love_mail_after");
        var expectedMarniePetAdoptionMailBeforeOrPending = ReadNullableBoolQueueParameter(item, "expected_marnie_pet_adoption_mail_before_or_pending");
        var expectedMarniePetAdoptionMailAfterOrPending = ReadNullableBoolQueueParameter(item, "expected_marnie_pet_adoption_mail_after_or_pending");
        var petGiftTriggerExpected = ReadNullableBoolQueueParameter(item, "pet_gift_trigger_expected");
        var petGiftSelectionStatus = ReadQueueParameterString(item, "pet_gift_selection_status");
        var expectedBowlWateredBefore = ReadNullableBoolQueueParameter(item, "expected_bowl_watered_before");
        var expectedBowlWateredAfter = ReadNullableBoolQueueParameter(item, "expected_bowl_watered_after");
        var expectedWaterBefore = ReadQueueParameterInt(item, "expected_water_before");
        var expectedWaterAfter = ReadQueueParameterInt(item, "expected_water_after");
        var expectedWateringCanBottomless = ReadNullableBoolQueueParameter(item, "expected_watering_can_bottomless");
        var expectedNextDayFriendshipAfter = ReadQueueParameterInt(item, "expected_next_day_friendship_after");
        var expectedNextDayPetLoveMail = ReadNullableBoolQueueParameter(item, "expected_next_day_pet_love_mail");
        var expectedNextDayMarniePetAdoptionMail = ReadNullableBoolQueueParameter(item, "expected_next_day_marnie_pet_adoption_mail");
        var delayedSettlement = ReadQueueParameterString(item, "delayed_settlement");
        var inventorySlotIndex = ReadQueueParameterInt(item, "inventory_slot_index");
        var donationTileX = ReadQueueParameterInt(item, "donation_tile_x");
        var donationTileY = ReadQueueParameterInt(item, "donation_tile_y");
        var expectedStackBefore = ReadQueueParameterInt(item, "expected_stack_before");
        var expectedStackAfter = ReadQueueParameterInt(item, "expected_stack_after");
        var expectedDonatedCountBefore = ReadQueueParameterInt(item, "expected_donated_count_before");
        var expectedDonatedCountAfter = ReadQueueParameterInt(item, "expected_donated_count_after");
        var museumTotalDonatableItems = ReadQueueParameterInt(item, "museum_total_donatable_items");
        var expectedCollectionCompleteAfter = ReadNullableBoolQueueParameter(item, "expected_collection_complete_after");
        var expectedCompleteCollectionAchievementAfter = ReadNullableBoolQueueParameter(item, "expected_complete_collection_achievement_after");
        var fieldGuideQuestPresentBefore = ReadNullableBoolQueueParameter(item, "field_guide_quest_present_before");
        var fieldGuideQuestCompletedBefore = ReadNullableBoolQueueParameter(item, "field_guide_quest_completed_before");
        var expectedFieldGuideQuestCompletedAfter = ReadNullableBoolQueueParameter(item, "expected_field_guide_quest_completed_after");
        var pendingRewardIdsBeforeJson = ReadQueueParameterString(item, "pending_reward_ids_before_json");
        var pendingRewardIdsAfterJson = ReadQueueParameterString(item, "pending_reward_ids_after_json");
        var newlyPendingRewardIdsJson = ReadQueueParameterString(item, "newly_pending_reward_ids_json");
        var autoAppliedRewardIdsJson = ReadQueueParameterString(item, "auto_applied_reward_ids_json");
        var autoAppliedRewardActionsJson = ReadQueueParameterString(item, "auto_applied_reward_actions_json");
        var rewardProjectionStatus = ReadQueueParameterString(item, "reward_projection_status");
        var rustyKeyDonationThreshold = ReadQueueParameterInt(item, "rusty_key_donation_threshold");
        var reachesRustyKeyThreshold = ReadNullableBoolQueueParameter(item, "reaches_rusty_key_threshold");
        var rustyKeyRewardAction = ReadQueueParameterString(item, "rusty_key_reward_action");
        var routeState = ReadQueueParameterString(item, "route_state");
        var purchaseKind = ReadQueueParameterString(item, "purchase_kind");
        var joinActionRaw = ReadQueueParameterString(item, "join_action_raw");
        if (string.IsNullOrWhiteSpace(joinActionRaw))
        {
            joinActionRaw = ReadQueueParameterString(item, "carpenter_action_raw");
        }
        var projectId = ReadQueueParameterString(item, "project_id");
        var buttonNumber = ReadQueueParameterInt(item, "button_number");
        var ccMailId = ReadQueueParameterString(item, "cc_mail_id");
        var jojaMailId = ReadQueueParameterString(item, "joja_mail_id");
        var expectedMoneyBefore = ReadQueueParameterInt(item, "expected_money_before");
        var price = ReadQueueParameterInt(item, "price") ??
            ReadQueueParameterInt(item, "expected_price");
        var expectedMoneyAfter = ReadQueueParameterInt(item, "expected_money_after");
        var expectedMailForTomorrow = ReadQueueParameterString(item, "expected_mail_for_tomorrow");
        var expectedGreetingBefore = ReadNullableBoolQueueParameter(item, "expected_greeting_before");
        var expectedGreetingAfter = ReadNullableBoolQueueParameter(item, "expected_greeting_after");
        var requiredEventId = ReadQueueParameterString(item, "required_event_id");
        var nativeContract = ReadQueueParameterString(item, "native_contract");
        var expectedHouseUpgradeLevelBefore = ReadQueueParameterInt(item, "expected_house_upgrade_level_before");
        var expectedHouseUpgradeLevelAfterConstruction = ReadQueueParameterInt(item, "expected_house_upgrade_level_after_construction");
        var expectedDaysUntilHouseUpgradeBefore = ReadQueueParameterInt(item, "expected_days_until_house_upgrade_before");
        var expectedDaysUntilHouseUpgradeAfter = ReadQueueParameterInt(item, "expected_days_until_house_upgrade_after");
        var constructionBuildingType = ReadQueueParameterString(item, "construction_building_type");
        var constructionBuildDays = ReadQueueParameterInt(item, "construction_build_days");
        var constructionMaterialsJson = ReadQueueParameterString(item, "construction_materials_json");
        var placementLocationId = ReadQueueParameterString(item, "placement_location_id");
        var placementVerification = ReadQueueParameterString(item, "placement_verification");
        var constructionPurpose = ReadQueueParameterString(item, "construction_purpose");
        var constructionReason = ReadQueueParameterString(item, "construction_reason");
        var constructionBuilder = ReadQueueParameterString(item, "construction_builder");
        var builderActionRaw = ReadQueueParameterString(item, "builder_action_raw");
        var appearanceReason = ReadQueueParameterString(item, "appearance_reason");
        var buildingIdentity = ReadQueueParameterString(item, "building_identity");
        var buildingLocationId = ReadQueueParameterString(item, "building_location_id");
        var buildingType = ReadQueueParameterString(item, "building_type");
        var currentSkinKey = ReadQueueParameterString(item, "current_skin_key");
        var currentSkinId = ReadQueueParameterString(item, "current_skin_id");
        var currentSkinIndex = ReadQueueParameterInt(item, "current_skin_index");
        var targetSkinKey = ReadQueueParameterString(item, "target_skin_key");
        var targetSkinId = ReadQueueParameterString(item, "target_skin_id");
        var targetSkinIndex = ReadQueueParameterInt(item, "target_skin_index");
        var availableSkinCount = ReadQueueParameterInt(item, "available_skin_count");
        var availableSkinKeysJson = ReadQueueParameterString(item, "available_skin_keys_json");
        var shortestClickDirection = ReadQueueParameterString(item, "shortest_click_direction");
        var shortestClickCount = ReadQueueParameterInt(item, "shortest_click_count");
        var entryRoute = ReadQueueParameterString(item, "entry_route");
        var resetsPaintColors = ReadNullableBoolQueueParameter(item, "skin_change_resets_all_paint_colors_to_default") == true;
        var paintDataKey = ReadQueueParameterString(item, "paint_data_key");
        var paintRegionCount = ReadQueueParameterInt(item, "paint_region_count");
        var paintRegionIndex = ReadQueueParameterInt(item, "paint_region_index");
        var paintRegionId = ReadQueueParameterString(item, "paint_region_id");
        var currentPaintDefault = ReadNullableBoolQueueParameter(item, "current_paint_default") == true;
        var currentPaintHue = ReadQueueParameterInt(item, "current_hue");
        var currentPaintSaturation = ReadQueueParameterInt(item, "current_saturation");
        var currentPaintLightness = ReadQueueParameterInt(item, "current_lightness");
        var paintHueMin = ReadQueueParameterInt(item, "hue_min");
        var paintHueMax = ReadQueueParameterInt(item, "hue_max");
        var paintSaturationMin = ReadQueueParameterInt(item, "saturation_min");
        var paintSaturationMax = ReadQueueParameterInt(item, "saturation_max");
        var paintLightnessMin = ReadQueueParameterInt(item, "lightness_min");
        var paintLightnessMax = ReadQueueParameterInt(item, "lightness_max");
        var nativePaintSliderLogicalWidth = ReadQueueParameterInt(item, "native_slider_logical_width");
        var paintTargetMode = ReadQueueParameterString(item, "paint_target_mode");
        var targetPaintHue = ReadQueueParameterInt(item, "target_hue");
        var targetPaintSaturation = ReadQueueParameterInt(item, "target_saturation");
        var targetPaintLightness = ReadQueueParameterInt(item, "target_lightness");
        var bundleDataKey = ReadQueueParameterString(item, "bundle_data_key");
        var communityCenterNoteTileX = ReadQueueParameterInt(item, "community_center_note_tile_x");
        var communityCenterNoteTileY = ReadQueueParameterInt(item, "community_center_note_tile_y");
        var bundleId = ReadQueueParameterInt(item, "bundle_id");
        var bundleAreaId = ReadQueueParameterInt(item, "bundle_area_id");
        var bundleAreaName = ReadQueueParameterString(item, "bundle_area_name");
        var bundleIngredientIndex = ReadQueueParameterInt(item, "bundle_ingredient_index");
        var expectedItemQuality = ReadQueueParameterInt(item, "expected_item_quality");
        var requiredStack = ReadQueueParameterInt(item, "required_stack");
        var inventoryItemTotalBefore = ReadQueueParameterInt(item, "inventory_item_total_before");
        var inventoryItemTotalAfter = ReadQueueParameterInt(item, "inventory_item_total_after");
        var bundleRequiredSlotCount = ReadQueueParameterInt(item, "bundle_required_slot_count");
        var expectedBundleCompletedCountBefore = ReadQueueParameterInt(item, "expected_bundle_completed_count_before");
        var expectedBundleCompletedCountAfter = ReadQueueParameterInt(item, "expected_bundle_completed_count_after");
        var expectedBundleCompleteAfter = ReadNullableBoolQueueParameter(item, "expected_bundle_complete_after");
        var expectedBundleRewardAvailableAfter = ReadNullableBoolQueueParameter(item, "expected_bundle_reward_available_after");
        var expectedCompleteBundleCountAfter = ReadQueueParameterInt(item, "expected_complete_bundle_count_after");
        var completesArea = ReadNullableBoolQueueParameter(item, "completes_area");
        var expectedAreaCompleteAfter = ReadNullableBoolQueueParameter(item, "expected_area_complete_after");
        var areaCompletionMailId = ReadQueueParameterString(item, "area_completion_mail_id");
        var expectedAreaCompletionMailPendingAfter = ReadNullableBoolQueueParameter(item, "expected_area_completion_mail_pending_after");
        var expectedBulletinThankYouPendingAfter = ReadNullableBoolQueueParameter(item, "expected_bulletin_thank_you_pending_after");
        var expectedAllAreasCompleteAfter = ReadNullableBoolQueueParameter(item, "expected_all_areas_complete_after");
        var newlyAppearingNoteAreaIdsJson = ReadQueueParameterString(item, "newly_appearing_note_area_ids_json");
        var expectedStatIncrementsJson = ReadQueueParameterString(item, "expected_stat_increments_json");
        var expectedSkillId = ReadQueueParameterString(item, "expected_skill_id");
        var expectedSkillExperienceDelta = ReadQueueParameterInt(item, "expected_skill_experience_delta");
        var expectedSkillExperienceDeltasJson = ReadQueueParameterString(item, "expected_skill_experience_deltas_json");
        var expectedMasteryExperienceDelta = ReadQueueParameterInt(item, "expected_mastery_experience_delta");
        var expectedStardropMaxStaminaDelta = ReadQueueParameterInt(item, "expected_stardrop_max_stamina_delta");
        var rewardBranch = ReadQueueParameterString(item, "reward_branch");
        var nativeGainExperienceCallAmount = ReadQueueParameterInt(item, "native_gain_experience_call_amount");
        var buildingTileX = ReadQueueParameterInt(item, "building_tile_x");
        var buildingTileY = ReadQueueParameterInt(item, "building_tile_y");
        var fishTypeItemId = ReadQueueParameterString(item, "fish_type_item_id");
        var expectedFishCount = ReadQueueParameterInt(item, "expected_fish_count");
        var expectedMaximumOccupantsBefore = ReadQueueParameterInt(item, "expected_maximum_occupants_before");
        var expectedMaximumOccupantsAfter = ReadQueueParameterInt(item, "expected_maximum_occupants_after");
        var expectedLastUnlockedPopulationGateBefore = ReadQueueParameterInt(item, "expected_last_unlocked_population_gate_before");
        var expectedLastUnlockedPopulationGateAfter = ReadQueueParameterInt(item, "expected_last_unlocked_population_gate_after");
        var expectedDaysSinceSpawnBefore = ReadQueueParameterInt(item, "expected_days_since_spawn_before");
        var expectedDaysSinceSpawnAfter = ReadQueueParameterInt(item, "expected_days_since_spawn_after");
        var expectedNeededItemCountAfter = ReadQueueParameterInt(item, "expected_needed_item_count_after");
        var expectedHasCompletedRequestAfter = ReadQueueParameterInt(item, "expected_has_completed_request_after");
        var requestItemRuntimeType = ReadQueueParameterString(item, "request_item_runtime_type");
        var requestItemToolbarSlotsJson = ReadQueueParameterString(item, "request_item_toolbar_slots_json");
        var nativeReceiptCallbacksStatus = ReadQueueParameterString(item, "native_receipt_callbacks_status");
        var expectedContainerBaitQualifiedItemId = ReadQueueParameterString(item, "expected_container_bait_qualified_item_id");
        var expectedContainerBaitUnitStateSha256 = ReadQueueParameterString(item, "expected_container_bait_unit_state_sha256");
        var expectedContainerOwnerPlayerIdBefore = ReadQueueParameterLong(item, "expected_container_owner_player_id_before");
        var expectedContainerOwnerPlayerIdAfter = ReadQueueParameterLong(item, "expected_container_owner_player_id_after");
        var baitRuntimeType = ReadQueueParameterString(item, "bait_runtime_type");
        var baitQuality = ReadQueueParameterInt(item, "bait_quality");
        var expectedFishCollectionEligible = ReadQueueParameterInt(item, "expected_fish_collection_eligible");
        var expectedFishCaughtCountBefore = ReadQueueParameterInt(item, "expected_fish_caught_count_before");
        var expectedFishCaughtCountAfter = ReadQueueParameterInt(item, "expected_fish_caught_count_after");
        var expectedFishCaughtMaxSizeBefore = ReadQueueParameterInt(item, "expected_fish_caught_max_size_before");
        var expectedCatchSizeMin = ReadQueueParameterInt(item, "expected_catch_size_min");
        var expectedCatchSizeMax = ReadQueueParameterInt(item, "expected_catch_size_max");
        var catchSizeProjectionStatus = ReadQueueParameterString(item, "catch_size_projection_status");
        var artifactSpotsDugBefore = ReadQueueParameterInt(item, "artifact_spots_dug_before");
        var artifactSpotsDugDelta = ReadQueueParameterInt(item, "artifact_spots_dug_delta");
        var artifactSpotsDugExpectedAfter = ReadQueueParameterInt(item, "artifact_spots_dug_expected_after");
        var clearTerrainFeatureExpectedAfter = ReadQueueParameterString(item, "clear_terrain_feature_expected_after");
        var defenseBookMailBefore = ReadQueueParameterInt(item, "defense_book_mail_before");
        var defenseBookMailExpectedAfter = ReadQueueParameterInt(item, "defense_book_mail_expected_after");
        var resourceClumpTileX = ReadQueueParameterInt(item, "resource_clump_tile_x");
        var resourceClumpTileY = ReadQueueParameterInt(item, "resource_clump_tile_y");
        var resourceClumpWidth = ReadQueueParameterInt(item, "resource_clump_width");
        var resourceClumpHeight = ReadQueueParameterInt(item, "resource_clump_height");
        var resourceClumpParentSheetIndex = ReadQueueParameterInt(item, "resource_clump_parent_sheet_index");
        var interactionKind = ReadQueueParameterString(item, "interaction_kind");
        var expectedActionType = ReadQueueParameterString(item, "expected_action_type");
        var socialContinuationDialogueRecovery = bool.TryParse(
            ReadQueueParameterString(item, "social_continuation_dialogue_recovery"),
            out var parsedSocialContinuationDialogueRecovery) && parsedSocialContinuationDialogueRecovery;
        var expectedMenuTypeAfterDialogue = ReadQueueParameterString(item, "expected_menu_type_after_dialogue");
        var professionChoiceId = ReadQueueParameterInt(item, "profession_choice_id");
        var professionChoiceSource = ReadQueueParameterString(item, "profession_choice_source");
        var connectorKind = ReadQueueParameterString(item, "connector_kind");
        var expectedTargetLocation = ReadQueueParameterString(item, "expected_target_location");
        var expectedArrivalTileX = ReadQueueParameterInt(item, "expected_arrival_tile_x");
        var expectedArrivalTileY = ReadQueueParameterInt(item, "expected_arrival_tile_y");
        var shopItemId = ReadQueueParameterString(item, "shop_item_id");
        var qualifiedItemId = ReadQueueParameterString(item, "qualified_item_id");
        var itemId = ReadQueueParameterString(item, "item_id");
        var quantity = ReadQueueParameterInt(item, "quantity");
        var maxUnitPrice = ReadQueueParameterInt(item, "max_unit_price");
        var expectedUnitPrice = ReadQueueParameterInt(item, "expected_unit_price");
        var expectedShopId = ReadQueueParameterString(item, "expected_shop_id");
        var expectedDialogueKey = ReadQueueParameterString(item, "expected_dialogue_key");
        var dialogueResponseKey = ReadQueueParameterString(item, "dialogue_response_key");
        var expectedMenuTypeAfter = ReadQueueParameterString(item, "expected_menu_type_after");
        var animalTypeId = ReadQueueParameterString(item, "animal_type_id");
        var possibleActualTypeIdsJson = ReadQueueParameterString(item, "possible_actual_type_ids_json");
        var animalPurchaseTargetLocationId = ReadQueueParameterString(item, "target_location_id");
        var animalHomeBuildingType = ReadQueueParameterString(item, "home_building_type");
        var animalHomeBuildingTileX = ReadQueueParameterInt(item, "home_building_tile_x");
        var animalHomeBuildingTileY = ReadQueueParameterInt(item, "home_building_tile_y");
        var animalHomeIndoorLocationId = ReadQueueParameterString(item, "home_indoor_location_id");
        var generatedAnimalName = ReadQueueParameterString(item, "generated_animal_name");
        var expectedAnimalHomeOccupantCountBefore = ReadQueueParameterInt(item, "expected_home_occupant_count_before");
        var expectedAnimalHomeCapacity = ReadQueueParameterInt(item, "expected_home_capacity");
        var animalPurchaseCandidateIdentitySha256 = ReadQueueParameterString(item, "candidate_identity_sha256");
        var animalManagementIntent = ReadQueueParameterString(item, "management_intent");
        var animalManagementReason = ReadQueueParameterString(item, "management_reason");
        var managedAnimalId = ReadQueueParameterLong(item, "animal_id");
        var expectedAnimalNameBefore = ReadQueueParameterString(item, "expected_name_before");
        var targetAnimalName = ReadQueueParameterString(item, "target_name");
        var animalManagementRequiresInitialPet = ReadQueueParameterBool(item, "requires_initial_pet");
        var expectedAllowReproductionBefore = ReadQueueParameterBool(item, "expected_allow_reproduction_before");
        var targetAllowReproduction = ReadQueueParameterBool(item, "target_allow_reproduction");
        var expectedAnimalSellPrice = ReadQueueParameterInt(item, "expected_sell_price");
        var confirmIrreversibleAnimalSale = ReadQueueParameterBool(item, "confirm_irreversible_sale") == true;
        var expectedAnimalHomeBuildingTypeBefore = ReadQueueParameterString(item, "expected_home_building_type_before");
        var expectedAnimalHomeBuildingTileXBefore = ReadQueueParameterInt(item, "expected_home_building_tile_x_before");
        var expectedAnimalHomeBuildingTileYBefore = ReadQueueParameterInt(item, "expected_home_building_tile_y_before");
        var targetAnimalHomeBuildingType = ReadQueueParameterString(item, "target_home_building_type");
        var targetAnimalHomeBuildingTileX = ReadQueueParameterInt(item, "target_home_building_tile_x");
        var targetAnimalHomeBuildingTileY = ReadQueueParameterInt(item, "target_home_building_tile_y");
        var targetAnimalHomeIndoorLocationId = ReadQueueParameterString(item, "target_home_indoor_location_id");
        var expectedTargetAnimalHomeOccupantCountBefore = ReadQueueParameterInt(item, "expected_target_home_occupant_count_before");
        var expectedTargetAnimalHomeCapacity = ReadQueueParameterInt(item, "expected_target_home_capacity");
        var seedId = ReadQueueParameterString(item, "seed_id");
        var harvestMethod = ReadQueueParameterString(item, "harvest_method");
        var giantCropId = ReadQueueParameterString(item, "giant_crop_id");
        var debrisIndex = ReadQueueParameterInt(item, "debris_index");
        var inputSlotIndex = ReadQueueParameterInt(item, "input_slot_index");
        var machinePredictionContractFingerprint =
            ReadQueueParameterString(
                item,
                "machine_prediction_contract_fingerprint");
        var machinePredictionTrainingKind =
            ReadQueueParameterString(
                item,
                "machine_prediction_training_kind");
        var machineOutputDistributionOutcomeKind =
            ReadQueueParameterString(
                item,
                "machine_output_distribution_outcome_kind");
        var predictedOutputQualifiedItemId =
            ReadQueueParameterString(
                item,
                "predicted_output_qualified_item_id");
        var predictedOutputContextTagsJson =
            ReadQueueParameterString(
                item,
                "predicted_output_context_tags_json");
        var predictedOutputAdditionalConsumedItemCount =
            ReadQueueParameterInt(
                item,
                "predicted_output_additional_consumed_item_count");
        var anvilReforgeUtilityMetric =
            ReadQueueParameterString(
                item,
                "anvil_reforge_utility_metric");
        var anvilReforgeCurrentUtility =
            ReadQueueParameterDouble(
                item,
                "anvil_reforge_current_utility");
        var anvilReforgeExpectedUtility =
            ReadQueueParameterDouble(
                item,
                "anvil_reforge_expected_utility");
        var anvilReforgeExpectedUtilityDelta =
            ReadQueueParameterDouble(
                item,
                "anvil_reforge_expected_utility_delta");
        var anvilReforgeImprovementProbability =
            ReadQueueParameterDouble(
                item,
                "anvil_reforge_improvement_probability");
        var relocationIntentId = ReadQueueParameterString(
            item,
            "relocation_intent_id");
        var machineRemovalProjectionFingerprint =
            ReadQueueParameterString(
                item,
                "machine_removal_projection_fingerprint");
        var toolQualifiedItemId = ReadQueueParameterString(
            item,
            "tool_qualified_item_id");
        var recipeName = ReadQueueParameterString(item, "recipe_name");
        var outputQualifiedItemId = ReadQueueParameterString(item, "output_qualified_item_id");
        var outputItemId = ReadQueueParameterString(item, "output_item_id");
        var outputCount = ReadQueueParameterInt(item, "output_count");
        var timesCraftedBefore = ReadQueueParameterInt(item, "times_crafted_before");
        var ingredientRowsJson = ReadQueueParameterString(item, "ingredient_rows_json");
        var craftingSource = ReadQueueParameterString(item, "crafting_source");
        var workbenchAccessPointId = ReadQueueParameterString(item, "workbench_access_point_id");
        var workbenchContainerNodeIdsJson = ReadQueueParameterString(item, "workbench_container_node_ids_json");
        var craftCount = ReadQueueParameterInt(item, "craft_count");
        var cookingReason = ReadQueueParameterString(item, "cooking_reason");
        var cookingSourceId = ReadQueueParameterString(item, "cooking_source_id");
        var cookingSourceKind = ReadQueueParameterString(item, "cooking_source_kind");
        var recipesCookedBefore = ReadQueueParameterInt(item, "recipes_cooked_before");
        var seasoningRowsJson = ReadQueueParameterString(item, "seasoning_rows_json");
        var materialContainerIdsJson = ReadQueueParameterString(item, "material_container_ids_json");
        var expectedOutputOrderData = ReadQueueParameterString(item, "expected_output_order_data");
        var forgeCandidateId = ReadQueueParameterString(item, "forge_candidate_id");
        var forgeOperation = ReadQueueParameterString(item, "forge_operation");
        var forgeReason = ReadQueueParameterString(item, "forge_reason");
        var forgeSourceId = ReadQueueParameterString(item, "forge_source_id");
        var forgeSourceKind = ReadQueueParameterString(item, "forge_source_kind");
        var leftSourceId = ReadQueueParameterString(item, "left_source_id");
        var leftStateJson = ReadQueueParameterString(item, "left_state_json");
        var rightSourceId = ReadQueueParameterString(item, "right_source_id");
        var rightStateJson = ReadQueueParameterString(item, "right_state_json");
        var forgeShardCost = ReadQueueParameterInt(item, "forge_shard_cost");
        var forgeShardRefund = ReadQueueParameterInt(item, "forge_shard_refund");
        var forgeShardCountBefore = ReadQueueParameterInt(item, "forge_shard_count_before");
        var timesEnchantedBefore = ReadQueueParameterInt(item, "times_enchanted_before");
        var timesEnchantedAfter = ReadQueueParameterInt(item, "times_enchanted_after");
        var forgeOutputContractKind = ReadQueueParameterString(item, "forge_output_contract_kind");
        var expectedOutputStateJson = ReadQueueParameterString(item, "expected_output_state_json");
        var randomOutcomeContractJson = ReadQueueParameterString(item, "random_outcome_contract_json");
        var slotIndex = ReadQueueParameterInt(item, "slot_index");
        var bookRuntimeType = ReadQueueParameterString(item, "book_runtime_type");
        var bookCategory = ReadQueueParameterInt(item, "book_category");
        var bookStackBefore = ReadQueueParameterInt(item, "book_stack_before");
        var bookStackAfter = ReadQueueParameterInt(item, "book_stack_after");
        var bookNativeBranch = ReadQueueParameterString(item, "book_native_branch");
        var bookNativeBranchStatus = ReadQueueParameterString(item, "book_native_branch_status");
        var bookContextTagsNativeOrderJson = ReadQueueParameterString(item, "book_context_tags_native_order_json");
        var bookMatchedExperienceTag = ReadQueueParameterString(item, "book_matched_experience_tag");
        var bookSkillLevelDeltasJson = ReadQueueParameterString(item, "book_skill_level_deltas_json");
        var bookNewLevelsBeforeJson = ReadQueueParameterString(item, "book_new_levels_before_json");
        var bookNewLevelsAfterJson = ReadQueueParameterString(item, "book_new_levels_after_json");
        var bookNativeFeedbackCallbacks = ReadQueueParameterString(item, "book_native_feedback_callbacks");
        var bookStatKey = ReadQueueParameterString(item, "book_stat_key");
        var bookStatBefore = ReadQueueParameterString(item, "book_stat_before");
        var bookStatAfter = ReadQueueParameterString(item, "book_stat_after");
        var readABookMailBefore = bool.TryParse(ReadQueueParameterString(item, "read_a_book_mail_before"), out var parsedReadABookMailBefore) ? parsedReadABookMailBefore : (bool?)null;
        var readABookMailAfter = bool.TryParse(ReadQueueParameterString(item, "read_a_book_mail_after"), out var parsedReadABookMailAfter) ? parsedReadABookMailAfter : (bool?)null;
        var wellReadAchievementBefore = bool.TryParse(ReadQueueParameterString(item, "well_read_achievement_before"), out var parsedWellReadAchievementBefore) ? parsedWellReadAchievementBefore : (bool?)null;
        var wellReadAchievementAfter = bool.TryParse(ReadQueueParameterString(item, "well_read_achievement_after"), out var parsedWellReadAchievementAfter) ? parsedWellReadAchievementAfter : (bool?)null;
        var wellReadAchievementWillUnlock = bool.TryParse(ReadQueueParameterString(item, "well_read_achievement_will_unlock"), out var parsedWellReadAchievementWillUnlock) ? parsedWellReadAchievementWillUnlock : (bool?)null;
        var wellReadHatterMailBefore = bool.TryParse(ReadQueueParameterString(item, "well_read_hatter_mail_before"), out var parsedWellReadHatterMailBefore) ? parsedWellReadHatterMailBefore : (bool?)null;
        var wellReadHatterMailAfter = bool.TryParse(ReadQueueParameterString(item, "well_read_hatter_mail_after"), out var parsedWellReadHatterMailAfter) ? parsedWellReadHatterMailAfter : (bool?)null;
        var wellReadDialogueEventSeenBefore = bool.TryParse(ReadQueueParameterString(item, "well_read_dialogue_event_seen_before"), out var parsedWellReadDialogueEventSeenBefore) ? parsedWellReadDialogueEventSeenBefore : (bool?)null;
        var wellReadDialogueEventSeenAfter = bool.TryParse(ReadQueueParameterString(item, "well_read_dialogue_event_seen_after"), out var parsedWellReadDialogueEventSeenAfter) ? parsedWellReadDialogueEventSeenAfter : (bool?)null;
        var wellReadUiSoundPlatformCallbacks = ReadQueueParameterString(item, "well_read_ui_sound_platform_callbacks");
        var cookingRecipesAddedJson = ReadQueueParameterString(item, "cooking_recipes_added_json");
        var cookingRecipesAddedCount = ReadQueueParameterInt(item, "cooking_recipes_added_count");
        var fishingLocationId = ReadQueueParameterString(item, "location_id");
        var fishingStandTileX = ReadQueueParameterInt(item, "stand_tile_x");
        var fishingStandTileY = ReadQueueParameterInt(item, "stand_tile_y");
        var interactionTileX = ReadQueueParameterInt(item, "interaction_tile_x");
        var interactionTileY = ReadQueueParameterInt(item, "interaction_tile_y");
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
        if (!string.IsNullOrWhiteSpace(requiredWeaponEnchantmentRuntimeType))
        {
            executionRequest.RequiredWeaponEnchantmentRuntimeType = requiredWeaponEnchantmentRuntimeType;
        }
        if (combatWeaponSlotIndex.HasValue)
        {
            executionRequest.CombatWeaponSlotIndex = combatWeaponSlotIndex.Value;
        }
        executionRequest.CombatMethod = combatMethod;
        executionRequest.CombatTerminalState = combatTerminalState;
        if (!string.IsNullOrWhiteSpace(combatIntent))
        {
            executionRequest.CombatIntent = combatIntent;
        }
        executionRequest.SlingshotSlotIndex = slingshotSlotIndex;
        executionRequest.SlingshotAmmoQualifiedItemId = slingshotAmmoQualifiedItemId;
        executionRequest.BombSlotIndex = bombSlotIndex;
        executionRequest.BombQualifiedItemId = bombQualifiedItemId;
        executionRequest.BombRadiusTiles = bombRadiusTiles;
        executionRequest.EscapeTileX = escapeTileX;
        executionRequest.EscapeTileY = escapeTileY;
        executionRequest.ExpectedBombObjectHits = expectedBombObjectHits;
        executionRequest.ExpectedBombMonsterHits = expectedBombMonsterHits;
        if (direction.HasValue)
        {
            executionRequest.Direction = direction.Value;
        }
        if (waitTicks.HasValue)
        {
            executionRequest.WaitTicks = waitTicks.Value;
        }
        executionRequest.SleepResumeMode = sleepResumeMode;
        if (maxCrops.HasValue)
        {
            executionRequest.MaxCrops = maxCrops.Value;
        }
        if (maxMovementTiles.HasValue)
        {
            executionRequest.MaxMovementTiles = maxMovementTiles.Value;
        }
        executionRequest.ExpectedMineLevelDelta = expectedMineLevelDelta;
        executionRequest.ExpectedMineLevelAfter = expectedMineLevelAfter;
        executionRequest.ExpectedHealthCost = expectedHealthCost;
        executionRequest.ExpectedHealthAfter = expectedHealthAfter;
        executionRequest.RetreatReason = miningStepReason;
        if (safeSlotIndex.HasValue)
        {
            executionRequest.SafeSlotIndex = safeSlotIndex.Value;
        }
        if (restoreSlotIndex.HasValue)
        {
            executionRequest.RestoreSlotIndex = restoreSlotIndex.Value;
        }
        if (wateringCanSlotIndex.HasValue)
        {
            executionRequest.WateringCanSlotIndex = wateringCanSlotIndex.Value;
        }
        if (toolSlotIndex.HasValue)
        {
            executionRequest.ToolSlotIndex = toolSlotIndex.Value;
        }
        executionRequest.RequiredToolKind = requiredToolKind;
        executionRequest.PanUpgradeLevel = panUpgradeLevel;
        executionRequest.PanEnchantmentsJson = panEnchantmentsJson;
        executionRequest.ClickPixelX = clickPixelX;
        executionRequest.ClickPixelY = clickPixelY;
        executionRequest.ExpectedTimesPannedBefore = expectedTimesPannedBefore;
        executionRequest.ExpectedTimesPannedAfter = expectedTimesPannedAfter;
        executionRequest.ExpectedMiningExperienceBefore = expectedMiningExperienceBefore;
        executionRequest.ExpectedMiningExperienceDelta = expectedMiningExperienceDelta;
        executionRequest.ExpectedMiningExperienceAfter = expectedMiningExperienceAfter;
        executionRequest.ExpectedForagingExperienceBefore = expectedForagingExperienceBefore;
        executionRequest.ExpectedForagingExperienceDelta = expectedForagingExperienceDelta;
        executionRequest.ExpectedForagingExperienceAfter = expectedForagingExperienceAfter;
        executionRequest.ExpectedFarmingExperienceDelta = expectedFarmingExperienceDelta;
        executionRequest.PostUseOrePanPointStatus = postUseOrePanPointStatus;
        executionRequest.PostUseRespawnAttempts = postUseRespawnAttempts;
        executionRequest.ClearOutputProjectionStatus = clearOutputProjectionStatus;
        executionRequest.ClearOutputItemsJson = clearOutputItemsJson;
        executionRequest.ExpectedOutputItemsJson = expectedOutputItemsJson;
        executionRequest.ExpectedOutputQuality = expectedOutputQuality;
        executionRequest.ExpectedAnimalCrackerMultiplier = expectedAnimalCrackerMultiplier;
        executionRequest.ExpectedEnergyDelta = expectedEnergyDelta;
        executionRequest.ExpectedFriendshipBefore = expectedFriendshipBefore;
        executionRequest.ExpectedFriendshipAfter = expectedFriendshipAfter;
        executionRequest.ExpectedLastPetDayBefore = expectedLastPetDayBefore;
        executionRequest.ExpectedLastPetDayBeforeMissing = expectedLastPetDayBeforeText == "missing";
        executionRequest.ExpectedLastPetDayAfter = expectedLastPetDayAfter;
        executionRequest.ExpectedTimesPetBefore = expectedTimesPetBefore;
        executionRequest.ExpectedTimesPetAfter = expectedTimesPetAfter;
        executionRequest.ExpectedGrantedFriendshipBefore = expectedGrantedFriendshipBefore;
        executionRequest.ExpectedGrantedFriendshipAfter = expectedGrantedFriendshipAfter;
        executionRequest.ExpectedPetLoveMailBefore = expectedPetLoveMailBefore;
        executionRequest.ExpectedPetLoveMailAfter = expectedPetLoveMailAfter;
        executionRequest.ExpectedMarniePetAdoptionMailBeforeOrPending = expectedMarniePetAdoptionMailBeforeOrPending;
        executionRequest.ExpectedMarniePetAdoptionMailAfterOrPending = expectedMarniePetAdoptionMailAfterOrPending;
        executionRequest.PetGiftTriggerExpected = petGiftTriggerExpected;
        executionRequest.PetGiftSelectionStatus = petGiftSelectionStatus;
        executionRequest.ExpectedBowlWateredBefore = expectedBowlWateredBefore;
        executionRequest.ExpectedBowlWateredAfter = expectedBowlWateredAfter;
        executionRequest.ExpectedWaterBefore = expectedWaterBefore;
        executionRequest.ExpectedWaterAfter = expectedWaterAfter;
        executionRequest.ExpectedWateringCanBottomless = expectedWateringCanBottomless;
        executionRequest.ExpectedNextDayFriendshipAfter = expectedNextDayFriendshipAfter;
        executionRequest.ExpectedNextDayPetLoveMail = expectedNextDayPetLoveMail;
        executionRequest.ExpectedNextDayMarniePetAdoptionMail = expectedNextDayMarniePetAdoptionMail;
        executionRequest.DelayedSettlement = delayedSettlement;
        executionRequest.InventorySlotIndex = inventorySlotIndex;
        executionRequest.DonationTileX = donationTileX;
        executionRequest.DonationTileY = donationTileY;
        executionRequest.ExpectedStackBefore = expectedStackBefore;
        executionRequest.ExpectedStackAfter = expectedStackAfter;
        executionRequest.ExpectedDonatedCountBefore = expectedDonatedCountBefore;
        executionRequest.ExpectedDonatedCountAfter = expectedDonatedCountAfter;
        executionRequest.MuseumTotalDonatableItems = museumTotalDonatableItems;
        executionRequest.ExpectedCollectionCompleteAfter = expectedCollectionCompleteAfter;
        executionRequest.ExpectedCompleteCollectionAchievementAfter = expectedCompleteCollectionAchievementAfter;
        executionRequest.FieldGuideQuestPresentBefore = fieldGuideQuestPresentBefore;
        executionRequest.FieldGuideQuestCompletedBefore = fieldGuideQuestCompletedBefore;
        executionRequest.ExpectedFieldGuideQuestCompletedAfter = expectedFieldGuideQuestCompletedAfter;
        executionRequest.PendingRewardIdsBeforeJson = pendingRewardIdsBeforeJson;
        executionRequest.PendingRewardIdsAfterJson = pendingRewardIdsAfterJson;
        executionRequest.NewlyPendingRewardIdsJson = newlyPendingRewardIdsJson;
        executionRequest.AutoAppliedRewardIdsJson = autoAppliedRewardIdsJson;
        executionRequest.AutoAppliedRewardActionsJson = autoAppliedRewardActionsJson;
        executionRequest.RewardProjectionStatus = rewardProjectionStatus;
        executionRequest.RustyKeyDonationThreshold = rustyKeyDonationThreshold;
        executionRequest.ReachesRustyKeyThreshold = reachesRustyKeyThreshold;
        executionRequest.RustyKeyRewardAction = rustyKeyRewardAction;
        executionRequest.RouteState = routeState;
        executionRequest.PurchaseKind = purchaseKind;
        executionRequest.JoinActionRaw = joinActionRaw;
        executionRequest.ProjectId = projectId;
        executionRequest.ButtonNumber = buttonNumber;
        executionRequest.CcMailId = ccMailId;
        executionRequest.JojaMailId = jojaMailId;
        executionRequest.ExpectedMoneyBefore = expectedMoneyBefore;
        executionRequest.Price = price;
        executionRequest.ExpectedMoneyAfter = expectedMoneyAfter;
        executionRequest.ExpectedMailForTomorrow = expectedMailForTomorrow;
        executionRequest.ExpectedGreetingBefore = expectedGreetingBefore;
        executionRequest.ExpectedGreetingAfter = expectedGreetingAfter;
        executionRequest.RequiredEventId = requiredEventId;
        executionRequest.NativeContract = nativeContract;
        executionRequest.ExpectedHouseUpgradeLevelBefore = expectedHouseUpgradeLevelBefore;
        executionRequest.ExpectedHouseUpgradeLevelAfterConstruction = expectedHouseUpgradeLevelAfterConstruction;
        executionRequest.ExpectedDaysUntilHouseUpgradeBefore = expectedDaysUntilHouseUpgradeBefore;
        executionRequest.ExpectedDaysUntilHouseUpgradeAfter = expectedDaysUntilHouseUpgradeAfter;
        executionRequest.ConstructionBuildingType = constructionBuildingType;
        executionRequest.ConstructionBuildDays = constructionBuildDays;
        executionRequest.ConstructionMaterialsJson = constructionMaterialsJson;
        executionRequest.PlacementLocationId = placementLocationId;
        executionRequest.PlacementVerification = placementVerification;
        executionRequest.ConstructionPurpose = constructionPurpose;
        executionRequest.ConstructionReason = constructionReason;
        executionRequest.ConstructionBuilder = constructionBuilder;
        executionRequest.BuilderActionRaw = builderActionRaw;
        executionRequest.AppearanceReason = appearanceReason;
        executionRequest.BuildingIdentity = buildingIdentity;
        executionRequest.BuildingLocationId = buildingLocationId;
        executionRequest.BuildingType = buildingType;
        executionRequest.CurrentSkinKey = currentSkinKey;
        executionRequest.CurrentSkinId = currentSkinId;
        executionRequest.CurrentSkinIndex = currentSkinIndex;
        executionRequest.TargetSkinKey = targetSkinKey;
        executionRequest.TargetSkinId = targetSkinId;
        executionRequest.TargetSkinIndex = targetSkinIndex;
        executionRequest.AvailableSkinCount = availableSkinCount;
        executionRequest.AvailableSkinKeysJson = availableSkinKeysJson;
        executionRequest.ShortestClickDirection = shortestClickDirection;
        executionRequest.ShortestClickCount = shortestClickCount;
        executionRequest.EntryRoute = entryRoute;
        executionRequest.SkinChangeResetsAllPaintColorsToDefault = resetsPaintColors;
        executionRequest.PaintDataKey = paintDataKey;
        executionRequest.PaintRegionCount = paintRegionCount;
        executionRequest.PaintRegionIndex = paintRegionIndex;
        executionRequest.PaintRegionId = paintRegionId;
        executionRequest.CurrentPaintDefault = currentPaintDefault;
        executionRequest.CurrentPaintHue = currentPaintHue;
        executionRequest.CurrentPaintSaturation = currentPaintSaturation;
        executionRequest.CurrentPaintLightness = currentPaintLightness;
        executionRequest.PaintHueMin = paintHueMin;
        executionRequest.PaintHueMax = paintHueMax;
        executionRequest.PaintSaturationMin = paintSaturationMin;
        executionRequest.PaintSaturationMax = paintSaturationMax;
        executionRequest.PaintLightnessMin = paintLightnessMin;
        executionRequest.PaintLightnessMax = paintLightnessMax;
        executionRequest.NativePaintSliderLogicalWidth = nativePaintSliderLogicalWidth;
        executionRequest.PaintTargetMode = paintTargetMode;
        executionRequest.TargetPaintHue = targetPaintHue;
        executionRequest.TargetPaintSaturation = targetPaintSaturation;
        executionRequest.TargetPaintLightness = targetPaintLightness;
        executionRequest.BundleDataKey = bundleDataKey;
        executionRequest.CommunityCenterNoteTileX = communityCenterNoteTileX;
        executionRequest.CommunityCenterNoteTileY = communityCenterNoteTileY;
        executionRequest.BundleId = bundleId;
        executionRequest.BundleAreaId = bundleAreaId;
        executionRequest.BundleAreaName = bundleAreaName;
        executionRequest.BundleIngredientIndex = bundleIngredientIndex;
        executionRequest.ExpectedItemQuality = expectedItemQuality;
        executionRequest.RequiredStack = requiredStack;
        executionRequest.InventoryItemTotalBefore = inventoryItemTotalBefore;
        executionRequest.InventoryItemTotalAfter = inventoryItemTotalAfter;
        executionRequest.BundleRequiredSlotCount = bundleRequiredSlotCount;
        executionRequest.ExpectedBundleCompletedCountBefore = expectedBundleCompletedCountBefore;
        executionRequest.ExpectedBundleCompletedCountAfter = expectedBundleCompletedCountAfter;
        executionRequest.ExpectedBundleCompleteAfter = expectedBundleCompleteAfter;
        executionRequest.ExpectedBundleRewardAvailableAfter = expectedBundleRewardAvailableAfter;
        executionRequest.ExpectedCompleteBundleCountAfter = expectedCompleteBundleCountAfter;
        executionRequest.CompletesArea = completesArea;
        executionRequest.ExpectedAreaCompleteAfter = expectedAreaCompleteAfter;
        executionRequest.AreaCompletionMailId = areaCompletionMailId;
        executionRequest.ExpectedAreaCompletionMailPendingAfter = expectedAreaCompletionMailPendingAfter;
        executionRequest.ExpectedBulletinThankYouPendingAfter = expectedBulletinThankYouPendingAfter;
        executionRequest.ExpectedAllAreasCompleteAfter = expectedAllAreasCompleteAfter;
        executionRequest.NewlyAppearingNoteAreaIdsJson = newlyAppearingNoteAreaIdsJson;
        executionRequest.ExpectedStatIncrementsJson = expectedStatIncrementsJson;
        executionRequest.ExpectedSkillId = expectedSkillId;
        executionRequest.ExpectedSkillExperienceDelta = expectedSkillExperienceDelta;
        executionRequest.ExpectedSkillExperienceDeltasJson = expectedSkillExperienceDeltasJson;
        executionRequest.ExpectedMasteryExperienceDelta = expectedMasteryExperienceDelta;
        executionRequest.ExpectedStardropMaxStaminaDelta = expectedStardropMaxStaminaDelta;
        executionRequest.RewardBranch = rewardBranch;
        executionRequest.NativeGainExperienceCallAmount = nativeGainExperienceCallAmount;
        executionRequest.BuildingTileX = buildingTileX;
        executionRequest.BuildingTileY = buildingTileY;
        executionRequest.FishTypeItemId = fishTypeItemId;
        executionRequest.ExpectedFishCount = expectedFishCount;
        executionRequest.ExpectedMaximumOccupantsBefore = expectedMaximumOccupantsBefore;
        executionRequest.ExpectedMaximumOccupantsAfter = expectedMaximumOccupantsAfter;
        executionRequest.ExpectedLastUnlockedPopulationGateBefore = expectedLastUnlockedPopulationGateBefore;
        executionRequest.ExpectedLastUnlockedPopulationGateAfter = expectedLastUnlockedPopulationGateAfter;
        executionRequest.ExpectedDaysSinceSpawnBefore = expectedDaysSinceSpawnBefore;
        executionRequest.ExpectedDaysSinceSpawnAfter = expectedDaysSinceSpawnAfter;
        executionRequest.ExpectedNeededItemCountAfter = expectedNeededItemCountAfter;
        executionRequest.ExpectedHasCompletedRequestAfter = expectedHasCompletedRequestAfter;
        executionRequest.RequestItemRuntimeType = requestItemRuntimeType;
        executionRequest.RequestItemToolbarSlotsJson = requestItemToolbarSlotsJson;
        executionRequest.NativeReceiptCallbacksStatus = nativeReceiptCallbacksStatus;
        executionRequest.ExpectedContainerBaitQualifiedItemId = expectedContainerBaitQualifiedItemId;
        executionRequest.ExpectedContainerBaitUnitStateSha256 = expectedContainerBaitUnitStateSha256;
        executionRequest.ExpectedContainerOwnerPlayerIdBefore = expectedContainerOwnerPlayerIdBefore;
        executionRequest.ExpectedContainerOwnerPlayerIdAfter = expectedContainerOwnerPlayerIdAfter;
        executionRequest.BaitRuntimeType = baitRuntimeType;
        executionRequest.BaitQuality = baitQuality;
        executionRequest.ExpectedFishCollectionEligible = expectedFishCollectionEligible;
        executionRequest.ExpectedFishCaughtCountBefore = expectedFishCaughtCountBefore;
        executionRequest.ExpectedFishCaughtCountAfter = expectedFishCaughtCountAfter;
        executionRequest.ExpectedFishCaughtMaxSizeBefore = expectedFishCaughtMaxSizeBefore;
        executionRequest.ExpectedCatchSizeMin = expectedCatchSizeMin;
        executionRequest.ExpectedCatchSizeMax = expectedCatchSizeMax;
        executionRequest.CatchSizeProjectionStatus = catchSizeProjectionStatus;
        executionRequest.ArtifactSpotsDugBefore = artifactSpotsDugBefore;
        executionRequest.ArtifactSpotsDugDelta = artifactSpotsDugDelta;
        executionRequest.ArtifactSpotsDugExpectedAfter = artifactSpotsDugExpectedAfter;
        executionRequest.ClearTerrainFeatureExpectedAfter = clearTerrainFeatureExpectedAfter;
        executionRequest.DefenseBookMailBefore = defenseBookMailBefore;
        executionRequest.DefenseBookMailExpectedAfter = defenseBookMailExpectedAfter;
        executionRequest.ResourceClumpTileX = resourceClumpTileX;
        executionRequest.ResourceClumpTileY = resourceClumpTileY;
        executionRequest.ResourceClumpWidth = resourceClumpWidth;
        executionRequest.ResourceClumpHeight = resourceClumpHeight;
        executionRequest.ResourceClumpParentSheetIndex = resourceClumpParentSheetIndex;
        if (!string.IsNullOrWhiteSpace(interactionKind))
        {
            executionRequest.InteractionKind = interactionKind;
        }
        if (!string.IsNullOrWhiteSpace(expectedActionType))
        {
            executionRequest.ExpectedActionType = expectedActionType;
        }
        executionRequest.SocialContinuationDialogueRecovery = socialContinuationDialogueRecovery;
        executionRequest.ExpectedMenuTypeAfterDialogue = expectedMenuTypeAfterDialogue;
        executionRequest.ProfessionChoiceId = professionChoiceId;
        executionRequest.ProfessionChoiceSource = professionChoiceSource;
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
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            executionRequest.ItemId = itemId;
        }
        if (quantity.HasValue)
        {
            executionRequest.Quantity = quantity.Value;
        }
        if (maxUnitPrice.HasValue)
        {
            executionRequest.MaxUnitPrice = maxUnitPrice.Value;
        }
        if (expectedUnitPrice.HasValue)
        {
            executionRequest.ExpectedUnitPrice = expectedUnitPrice.Value;
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
        executionRequest.ExpectedMenuTypeAfter = expectedMenuTypeAfter;
        executionRequest.AnimalTypeId = animalTypeId;
        executionRequest.PossibleActualTypeIdsJson = possibleActualTypeIdsJson;
        executionRequest.AnimalPurchaseTargetLocationId = animalPurchaseTargetLocationId;
        executionRequest.AnimalHomeBuildingType = animalHomeBuildingType;
        executionRequest.AnimalHomeBuildingTileX = animalHomeBuildingTileX;
        executionRequest.AnimalHomeBuildingTileY = animalHomeBuildingTileY;
        executionRequest.AnimalHomeIndoorLocationId = animalHomeIndoorLocationId;
        executionRequest.GeneratedAnimalName = generatedAnimalName;
        executionRequest.ExpectedAnimalHomeOccupantCountBefore = expectedAnimalHomeOccupantCountBefore;
        executionRequest.ExpectedAnimalHomeCapacity = expectedAnimalHomeCapacity;
        executionRequest.AnimalPurchaseCandidateIdentitySha256 = animalPurchaseCandidateIdentitySha256;
        executionRequest.AnimalManagementIntent = animalManagementIntent;
        executionRequest.AnimalManagementReason = animalManagementReason;
        executionRequest.ManagedAnimalId = managedAnimalId;
        executionRequest.ExpectedAnimalNameBefore = expectedAnimalNameBefore;
        executionRequest.TargetName = targetAnimalName;
        executionRequest.AnimalManagementRequiresInitialPet = animalManagementRequiresInitialPet;
        executionRequest.ExpectedAllowReproductionBefore = expectedAllowReproductionBefore;
        executionRequest.TargetAllowReproduction = targetAllowReproduction;
        executionRequest.ExpectedAnimalSellPrice = expectedAnimalSellPrice;
        executionRequest.ConfirmIrreversibleAnimalSale = confirmIrreversibleAnimalSale;
        executionRequest.ExpectedAnimalHomeBuildingTypeBefore = expectedAnimalHomeBuildingTypeBefore;
        executionRequest.ExpectedAnimalHomeBuildingTileXBefore = expectedAnimalHomeBuildingTileXBefore;
        executionRequest.ExpectedAnimalHomeBuildingTileYBefore = expectedAnimalHomeBuildingTileYBefore;
        executionRequest.TargetAnimalHomeBuildingType = targetAnimalHomeBuildingType;
        executionRequest.TargetAnimalHomeBuildingTileX = targetAnimalHomeBuildingTileX;
        executionRequest.TargetAnimalHomeBuildingTileY = targetAnimalHomeBuildingTileY;
        executionRequest.TargetAnimalHomeIndoorLocationId = targetAnimalHomeIndoorLocationId;
        executionRequest.ExpectedTargetAnimalHomeOccupantCountBefore = expectedTargetAnimalHomeOccupantCountBefore;
        executionRequest.ExpectedTargetAnimalHomeCapacity = expectedTargetAnimalHomeCapacity;
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
        executionRequest.MachinePredictionContractFingerprint =
            machinePredictionContractFingerprint;
        executionRequest.MachinePredictionTrainingKind =
            machinePredictionTrainingKind;
        executionRequest.MachineOutputDistributionOutcomeKind =
            machineOutputDistributionOutcomeKind;
        executionRequest.PredictedOutputQualifiedItemId =
            predictedOutputQualifiedItemId;
        executionRequest.PredictedOutputContextTagsJson =
            predictedOutputContextTagsJson;
        executionRequest.PredictedOutputAdditionalConsumedItemCount =
            predictedOutputAdditionalConsumedItemCount;
        executionRequest.AnvilReforgeUtilityMetric =
            anvilReforgeUtilityMetric;
        executionRequest.AnvilReforgeCurrentUtility =
            anvilReforgeCurrentUtility;
        executionRequest.AnvilReforgeExpectedUtility =
            anvilReforgeExpectedUtility;
        executionRequest.AnvilReforgeExpectedUtilityDelta =
            anvilReforgeExpectedUtilityDelta;
        executionRequest.AnvilReforgeImprovementProbability =
            anvilReforgeImprovementProbability;
        executionRequest.RelocationIntentId = relocationIntentId;
        executionRequest.MachineRemovalProjectionFingerprint =
            machineRemovalProjectionFingerprint;
        executionRequest.ToolQualifiedItemId = toolQualifiedItemId;
        executionRequest.RecipeName = recipeName;
        executionRequest.OutputQualifiedItemId = outputQualifiedItemId;
        executionRequest.OutputItemId = outputItemId;
        executionRequest.OutputCount = outputCount;
        executionRequest.TimesCraftedBefore = timesCraftedBefore;
        executionRequest.IngredientRowsJson = ingredientRowsJson;
        executionRequest.CraftingSource = craftingSource;
        executionRequest.WorkbenchAccessPointId = workbenchAccessPointId;
        executionRequest.WorkbenchContainerNodeIdsJson = workbenchContainerNodeIdsJson;
        executionRequest.CraftCount = craftCount;
        executionRequest.CookingReason = cookingReason;
        executionRequest.CookingSourceId = cookingSourceId;
        executionRequest.CookingSourceKind = cookingSourceKind;
        executionRequest.RecipesCookedBefore = recipesCookedBefore;
        executionRequest.SeasoningRowsJson = seasoningRowsJson;
        executionRequest.MaterialContainerIdsJson = materialContainerIdsJson;
        executionRequest.ExpectedOutputOrderData = expectedOutputOrderData;
        executionRequest.ForgeCandidateId = forgeCandidateId;
        executionRequest.ForgeOperation = forgeOperation;
        executionRequest.ForgeReason = forgeReason;
        executionRequest.ForgeSourceId = forgeSourceId;
        executionRequest.ForgeSourceKind = forgeSourceKind;
        executionRequest.LeftSourceId = leftSourceId;
        executionRequest.LeftStateJson = leftStateJson;
        executionRequest.RightSourceId = rightSourceId;
        executionRequest.RightStateJson = rightStateJson;
        executionRequest.ForgeShardCost = forgeShardCost;
        executionRequest.ForgeShardRefund = forgeShardRefund;
        executionRequest.ForgeShardCountBefore = forgeShardCountBefore;
        executionRequest.TimesEnchantedBefore = timesEnchantedBefore;
        executionRequest.TimesEnchantedAfter = timesEnchantedAfter;
        executionRequest.ForgeOutputContractKind = forgeOutputContractKind;
        executionRequest.ExpectedOutputStateJson = expectedOutputStateJson;
        executionRequest.RandomOutcomeContractJson = randomOutcomeContractJson;
        if (slotIndex.HasValue)
        {
            executionRequest.SlotIndex = slotIndex.Value;
        }
        executionRequest.BookRuntimeType = bookRuntimeType;
        executionRequest.BookCategory = bookCategory;
        executionRequest.BookStackBefore = bookStackBefore;
        executionRequest.BookStackAfter = bookStackAfter;
        executionRequest.BookNativeBranch = bookNativeBranch;
        executionRequest.BookNativeBranchStatus = bookNativeBranchStatus;
        executionRequest.BookContextTagsNativeOrderJson = bookContextTagsNativeOrderJson;
        executionRequest.BookMatchedExperienceTag = bookMatchedExperienceTag;
        executionRequest.BookSkillLevelDeltasJson = bookSkillLevelDeltasJson;
        executionRequest.BookNewLevelsBeforeJson = bookNewLevelsBeforeJson;
        executionRequest.BookNewLevelsAfterJson = bookNewLevelsAfterJson;
        executionRequest.BookNativeFeedbackCallbacks = bookNativeFeedbackCallbacks;
        executionRequest.BookStatKey = bookStatKey;
        executionRequest.BookStatBefore = bookStatBefore;
        executionRequest.BookStatAfter = bookStatAfter;
        executionRequest.ReadABookMailBefore = readABookMailBefore;
        executionRequest.ReadABookMailAfter = readABookMailAfter;
        executionRequest.WellReadAchievementBefore = wellReadAchievementBefore;
        executionRequest.WellReadAchievementAfter = wellReadAchievementAfter;
        executionRequest.WellReadAchievementWillUnlock = wellReadAchievementWillUnlock;
        executionRequest.WellReadHatterMailBefore = wellReadHatterMailBefore;
        executionRequest.WellReadHatterMailAfter = wellReadHatterMailAfter;
        executionRequest.WellReadDialogueEventSeenBefore = wellReadDialogueEventSeenBefore;
        executionRequest.WellReadDialogueEventSeenAfter = wellReadDialogueEventSeenAfter;
        executionRequest.WellReadUiSoundPlatformCallbacks = wellReadUiSoundPlatformCallbacks;
        executionRequest.CookingRecipesAddedJson = cookingRecipesAddedJson;
        executionRequest.CookingRecipesAddedCount = cookingRecipesAddedCount;
        if (!string.IsNullOrWhiteSpace(fishingLocationId))
        {
            executionRequest.LocationId = fishingLocationId;
        }
        var targetLocationId = ReadQueueParameterString(item, "target_location");
        if (!string.IsNullOrWhiteSpace(targetLocationId))
        {
            executionRequest.LocationId = targetLocationId;
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
        if (interactionTileX.HasValue && interactionTileY.HasValue)
        {
            executionRequest.InteractionTileX = interactionTileX.Value;
            executionRequest.InteractionTileY = interactionTileY.Value;
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

        executionRequest.MinigameId = ReadQueueParameterString(item, "minigame_id");
        executionRequest.MinigameMode = ReadQueueParameterInt(item, "minigame_mode");
        executionRequest.MinigameTargetScore = ReadQueueParameterInt(item, "minigame_target_score");
        executionRequest.MinigameMaxAttempts = ReadQueueParameterInt(item, "minigame_max_attempts");

        var socialNpcName = ReadQueueParameterString(item, "npc_name");
        var socialActionKind = ReadQueueParameterString(item, "social_action_kind");
        var socialObservedNpcTileX = ReadQueueParameterInt(item, "npc_tile_x");
        var socialObservedNpcTileY = ReadQueueParameterInt(item, "npc_tile_y");
        var socialGiftSlotIndex = ReadQueueParameterInt(item, "slot_index");
        var socialGiftQualifiedItemId = ReadQueueParameterString(item, "qualified_item_id");
        var socialExpectedFriendshipDelta = ReadQueueParameterString(item, "expected_friendship_delta");
        var socialExpectedTalkedToTodayBefore = ReadQueueParameterString(item, "expected_talked_to_today_before");
        var questCandidateId = ReadQueueParameterString(item, "quest_candidate_id");
        var questFamily = ReadQueueParameterString(item, "quest_family");
        var questId = ReadQueueParameterString(item, "quest_id");
        var questKey = ReadQueueParameterString(item, "quest_key");
        var questRuntimeType = ReadQueueParameterString(item, "quest_runtime_type");
        var questInteractionKind = ReadQueueParameterString(item, "quest_interaction_kind");
        var questOfferFingerprint = ReadQueueParameterString(item, "quest_offer_fingerprint");
        var questOfferTitle = ReadQueueParameterString(item, "quest_offer_title");
        var questOfferCurrentObjective = ReadQueueParameterString(item, "quest_offer_current_objective");
        var questRewardFingerprint = ReadQueueParameterString(item, "quest_reward_fingerprint");
        var questMoneyRewardExpected = ReadQueueParameterInt(item, "quest_money_reward_expected");
        var questExpectedMoneyBefore = ReadQueueParameterInt(item, "expected_money_before");
        var specialOrderBoardType = ReadQueueParameterString(item, "special_order_board_type");
        var specialOrderSelectionIndex = ReadQueueParameterInt(item, "special_order_selection_index");
        var specialOrderSelectionSide = ReadQueueParameterString(item, "special_order_selection_side");
        var specialOrderGenerationSeed = ReadQueueParameterInt(item, "special_order_generation_seed");
        var specialOrderDueDate = ReadQueueParameterInt(item, "special_order_due_date");
        var specialOrderDuration = ReadQueueParameterString(item, "special_order_duration");
        var questObjectiveIndex = ReadQueueParameterInt(item, "quest_objective_index");
        var questExpectedCurrentCount = ReadQueueParameterInt(item, "quest_expected_current_count");
        var questExpectedTargetCount = ReadQueueParameterInt(item, "quest_expected_target_count");
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
        executionRequest.QuestCandidateId = questCandidateId;
        executionRequest.QuestFamily = questFamily;
        executionRequest.QuestId = questId;
        executionRequest.QuestKey = questKey;
        executionRequest.QuestRuntimeType = questRuntimeType;
        executionRequest.QuestInteractionKind = questInteractionKind;
        executionRequest.QuestOfferFingerprint = questOfferFingerprint;
        executionRequest.QuestOfferTitle = questOfferTitle;
        executionRequest.QuestOfferCurrentObjective = questOfferCurrentObjective;
        executionRequest.QuestRewardFingerprint = questRewardFingerprint;
        executionRequest.QuestMoneyRewardExpected = questMoneyRewardExpected;
        executionRequest.QuestExpectedMoneyBefore = questExpectedMoneyBefore;
        executionRequest.SpecialOrderBoardType = specialOrderBoardType;
        executionRequest.SpecialOrderSelectionIndex = specialOrderSelectionIndex;
        executionRequest.SpecialOrderSelectionSide = specialOrderSelectionSide;
        executionRequest.SpecialOrderGenerationSeed = specialOrderGenerationSeed;
        executionRequest.SpecialOrderDueDate = specialOrderDueDate;
        executionRequest.SpecialOrderDuration = specialOrderDuration;
        executionRequest.QuestObjectiveIndex = questObjectiveIndex;
        executionRequest.QuestExpectedCurrentCount = questExpectedCurrentCount;
        executionRequest.QuestExpectedTargetCount = questExpectedTargetCount;
        executionRequest.QuestSlayTargetStep = string.Equals(
            ReadQueueParameterString(item, "quest_slay_target_step"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        executionRequest.QuestAcquisitionTargetStep = string.Equals(
            ReadQueueParameterString(item, "quest_acquisition_target_step"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        executionRequest.QuestAcquisitionSourceStep = string.Equals(
            ReadQueueParameterString(item, "quest_acquisition_source_step"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        executionRequest.QuestDropBoxId = ReadQueueParameterString(item, "quest_drop_box_id");
        executionRequest.QuestDropBoxSlotIndex = ReadQueueParameterInt(item, "slot_index");
        executionRequest.QuestDropBoxQualifiedItemId = ReadQueueParameterString(item, "qualified_item_id");
        executionRequest.QuestDropBoxExpectedStackBefore = ReadQueueParameterInt(item, "item_stack_before");
        executionRequest.QuestDropBoxExpectedAcceptedCount =
            ReadQueueParameterInt(item, "quest_drop_box_expected_accepted_count");
        var materialTransferIntentJson = ReadQueueParameterString(
            item,
            "material_transfer_intent_json");
        var materialTransferProjectionJson = ReadQueueParameterString(
            item,
            "material_transfer_projection_json");
        if (!string.IsNullOrWhiteSpace(materialTransferIntentJson))
        {
            executionRequest.MaterialTransferIntent =
                JsonSerializer.Deserialize<MaterialTransferIntent>(
                    materialTransferIntentJson,
                    JsonOptions);
        }
        if (!string.IsNullOrWhiteSpace(materialTransferProjectionJson))
        {
            executionRequest.MaterialTransferProjection =
                JsonSerializer.Deserialize<MaterialTransferProjection>(
                    materialTransferProjectionJson,
                    JsonOptions);
        }

        ApplyStoragePlacementRequestFields(
            executionRequest,
            item);
        ApplyFencePlacementRequestFields(
            executionRequest,
            item);
        ApplyFlooringPlacementRequestFields(
            executionRequest,
            item);
        ApplyFurniturePlacementRequestFields(
            executionRequest,
            item);
        ApplySignPlacementRequestFields(
            executionRequest,
            item);
        ApplySignDisplayItemRequestFields(
            executionRequest,
            item);
        return executionRequest;
    }
}
