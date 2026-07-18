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
    private static async Task<JsonObject> ExecuteRealRuntimeAsync(
        HttpClient http,
        HttpClient executorHttp,
        LiveTrainingOptions options,
        int iteration,
        string beforeSnapshotPath,
        JsonObject beforeSnapshot,
        JsonObject queue,
        string stateHash,
        string queueId,
        JsonObject? socialContinuation)
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
        var activeSocialContinuation = socialContinuation is null
            ? null
            : JsonNode.Parse(socialContinuation.ToJsonString(JsonOptions))?.AsObject();
        var socialObjectiveCompleted = false;

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
            if (QueueReplanFilter.CompletesSocialContinuation(item, activeSocialContinuation, executionStatus))
            {
                socialObjectiveCompleted = true;
                activeSocialContinuation = null;
            }
            else if (string.Equals(executionStatus, "applied", StringComparison.Ordinal))
            {
                activeSocialContinuation = QueueReplanFilter.ReadSocialContinuation(item) ?? activeSocialContinuation;
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
                var replan = await BuildQueueFromDailyPlanAsync(http, options, currentStateHash, activeSocialContinuation);
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
        aggregate["social_objective_completed"] = socialObjectiveCompleted;
        aggregate["social_objective_continuation"] = activeSocialContinuation is null
            ? null
            : JsonNode.Parse(activeSocialContinuation.ToJsonString(JsonOptions));
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
        var requiredWeaponEnchantmentRuntimeType = ReadQueueParameterString(item, "required_weapon_enchantment_runtime_type");
        var combatWeaponSlotIndex = ReadQueueParameterInt(item, "combat_weapon_slot_index");
        var combatMethod = ReadQueueParameterString(item, "combat_method");
        var combatTerminalState = ReadQueueParameterString(item, "combat_terminal_state");
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
        var expectedStatIncrementsJson = ReadQueueParameterString(item, "expected_stat_increments_json");
        var expectedSkillId = ReadQueueParameterString(item, "expected_skill_id");
        var expectedSkillExperienceDelta = ReadQueueParameterInt(item, "expected_skill_experience_delta");
        var expectedSkillExperienceDeltasJson = ReadQueueParameterString(item, "expected_skill_experience_deltas_json");
        var expectedMasteryExperienceDelta = ReadQueueParameterInt(item, "expected_mastery_experience_delta");
        var expectedStardropMaxStaminaDelta = ReadQueueParameterInt(item, "expected_stardrop_max_stamina_delta");
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
        var connectorKind = ReadQueueParameterString(item, "connector_kind");
        var expectedTargetLocation = ReadQueueParameterString(item, "expected_target_location");
        var expectedArrivalTileX = ReadQueueParameterInt(item, "expected_arrival_tile_x");
        var expectedArrivalTileY = ReadQueueParameterInt(item, "expected_arrival_tile_y");
        var shopItemId = ReadQueueParameterString(item, "shop_item_id");
        var qualifiedItemId = ReadQueueParameterString(item, "qualified_item_id");
        var itemId = ReadQueueParameterString(item, "item_id");
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
        executionRequest.ExpectedStatIncrementsJson = expectedStatIncrementsJson;
        executionRequest.ExpectedSkillId = expectedSkillId;
        executionRequest.ExpectedSkillExperienceDelta = expectedSkillExperienceDelta;
        executionRequest.ExpectedSkillExperienceDeltasJson = expectedSkillExperienceDeltasJson;
        executionRequest.ExpectedMasteryExperienceDelta = expectedMasteryExperienceDelta;
        executionRequest.ExpectedStardropMaxStaminaDelta = expectedStardropMaxStaminaDelta;
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
}
