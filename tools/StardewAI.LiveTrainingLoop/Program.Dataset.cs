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
    private static JsonObject? FindQueueItemForExecution(JsonObject queue, JsonObject execution)
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

    private static void WritePlanExecutionEpisode(
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
        var isAnvilReforgeSample =
            string.Equals(
                optionId,
                "executor.load_machine_input",
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(
                ReadString(
                    execution,
                    "machine_output_distribution_outcome_kind")) &&
            execution["anvil_reforge_realized_utility"] is
                not null;
        var reward = CalculateExecutionReward(execution, optionId, applied);
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
            TrainingRole =
                isAnvilReforgeSample && applied
                    ? TrainingRoles.StrategyValue
                    : TrainingRoles
                        .ExecutorCalibration,
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
            CombatIntent = ReadString(execution, "combat_intent"),
            RecoveryFoodSlotIndex = execution["recovery_food_slot_index"]?.GetValue<int>(),
            RecoveryFoodQualifiedItemId = ReadString(execution, "recovery_food_qualified_item_id"),
            RecoveryFoodStackBefore = execution["recovery_food_stack_before"]?.GetValue<int>(),
            RecoveryFoodStackAfter = execution["recovery_food_stack_after"]?.GetValue<int>(),
            RecoveryHealthBefore = execution["recovery_health_before"]?.GetValue<int>(),
            RecoveryHealthAfter = execution["recovery_health_after"]?.GetValue<int>(),
            RecoveryRestoreSlotIndex = execution["recovery_restore_slot_index"]?.GetValue<int>(),
            RecoverySafetyStatus = ReadString(execution, "recovery_safety_status"),
            ShaftMineLevelBefore = execution["shaft_mine_level_before"]?.GetValue<int>(),
            ShaftMineLevelAfter = execution["shaft_mine_level_after"]?.GetValue<int>(),
            ShaftLevelDelta = execution["shaft_level_delta"]?.GetValue<int>(),
            ShaftHealthBefore = execution["shaft_health_before"]?.GetValue<int>(),
            ShaftHealthAfter = execution["shaft_health_after"]?.GetValue<int>(),
            ShaftNativeDialogueHandled = execution["shaft_native_dialogue_handled"]?.GetValue<bool>(),
            RetreatReason = ReadString(execution, "retreat_reason"),
            RetreatMineLevelBefore = execution["retreat_mine_level_before"]?.GetValue<int>(),
            RetreatTimeBefore = execution["retreat_time_before"]?.GetValue<int>(),
            RetreatHealthBefore = execution["retreat_health_before"]?.GetValue<int>(),
            RetreatEnergyBefore = execution["retreat_energy_before"]?.GetValue<double>(),
            RetreatDestination = ReadString(execution, "retreat_destination"),
            RetreatNativeDialogueHandled = execution["retreat_native_dialogue_handled"]?.GetValue<bool>(),
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
            DialogueEventUpAfter = execution["dialogue_event_up_after"]?.GetValue<bool>(),
            MaterialTransferIntent = ReadExecutionObject<MaterialTransferIntent>(
                execution,
                "material_transfer_intent"),
            MaterialTransferProjection = ReadExecutionObject<MaterialTransferProjection>(
                execution,
                "material_transfer_projection"),
            MaterialTransferClickCount = execution["material_transfer_click_count"]?.GetValue<int>(),
            MaterialTransferSourceStackBefore = execution["material_transfer_source_stack_before"]?.GetValue<int>(),
            MaterialTransferSourceStackAfter = execution["material_transfer_source_stack_after"]?.GetValue<int>(),
            MaterialTransferDestinationQuantityBefore = execution["material_transfer_destination_quantity_before"]?.GetValue<int>(),
            MaterialTransferDestinationQuantityAfter = execution["material_transfer_destination_quantity_after"]?.GetValue<int>(),
            MaterialTransferNativeMenuOpened = execution["material_transfer_native_menu_opened"]?.GetValue<bool>(),
            MaterialTransferNativeLockReleased = execution["material_transfer_native_lock_released"]?.GetValue<bool>()
        };

        var episodePath = Path.Combine(options.SnapshotDir, "plan-execution-episode-" + iteration.ToString("D4") + ".json");
        File.WriteAllText(episodePath, JsonSerializer.Serialize(episode, JsonOptions), Encoding.UTF8);
    }

    private static double CalculateExecutionReward(
        JsonObject execution,
        string optionId,
        bool applied)
    {
        if (string.Equals(optionId, "executor.move_to_tile", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.collect_spawned_object", StringComparison.Ordinal))
        {
            return applied ? 0.05 : -0.05;
        }

        if (string.Equals(optionId, "executor.cool_volcano_lava", StringComparison.Ordinal))
        {
            return applied ? 0.10 : -0.10;
        }

        if (string.Equals(optionId, "executor.break_volcano_stone", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.break_volcano_container", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.break_resource_clump", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.break_farm_resource_clump", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.break_current_location_resource_clump", StringComparison.Ordinal))
        {
            return applied ? 0.08 : -0.08;
        }

        if (string.Equals(optionId, "executor.harvest_ginger", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.harvest_bush", StringComparison.Ordinal))
        {
            return applied ? 0.06 : -0.06;
        }

        if (string.Equals(optionId, "executor.combat_volcano_monster", StringComparison.Ordinal))
        {
            return applied ? 0.12 : -0.12;
        }

        if (string.Equals(optionId, "executor.face_direction", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.wait_ticks", StringComparison.Ordinal))
        {
            return applied ? 0.02 : -0.02;
        }

        if (string.Equals(optionId, "executor.load_machine_input", StringComparison.Ordinal) &&
            applied &&
            !string.IsNullOrWhiteSpace(ReadString(execution, "machine_output_distribution_outcome_kind")) &&
            execution["anvil_reforge_realized_utility"] is not null)
        {
            return ReadDouble(execution, "anvil_reforge_realized_utility_delta");
        }

        return 0;
    }

    private static T? ReadExecutionObject<T>(JsonObject execution, string property)
        where T : class
    {
        return execution[property] is { } node
            ? JsonSerializer.Deserialize<T>(node.ToJsonString(), JsonOptions)
            : null;
    }

    private static string[] RewardTerms(
        string optionId,
        bool isMove,
        bool applied,
        int watered,
        bool isAnvilReforgeSample)
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

        if (string.Equals(optionId, "executor.cool_volcano_lava", StringComparison.Ordinal))
        {
            return applied
                ? new[] { "real_volcano_lava_cooled", "native_watering_can_lifecycle" }
                : new[] { "real_volcano_lava_cooling_blocked" };
        }

        if (string.Equals(optionId, "executor.break_volcano_stone", StringComparison.Ordinal))
        {
            return applied
                ? new[] { "real_volcano_stone_removed", "native_pickaxe_lifecycle" }
                : new[] { "real_volcano_stone_removal_blocked" };
        }

        if (string.Equals(optionId, "executor.break_volcano_container", StringComparison.Ordinal))
        {
            return applied
                ? new[] { "real_volcano_container_removed", "native_heavy_hitter_lifecycle" }
                : new[] { "real_volcano_container_removal_blocked" };
        }

        if (string.Equals(optionId, "executor.combat_volcano_monster", StringComparison.Ordinal))
        {
            return applied
                ? new[] { "real_volcano_monster_defeated", "native_melee_lifecycle" }
                : new[] { "real_volcano_combat_blocked" };
        }

        if (string.Equals(
                optionId,
                "executor.load_machine_input",
                StringComparison.Ordinal) &&
            isAnvilReforgeSample)
        {
            return applied
                ? new[]
                {
                    "machine_input_native_load_verified",
                    "anvil_reforge_realized_utility_delta"
                }
                : new[]
                {
                    "machine_input_native_load_blocked"
                };
        }

        return watered > 0 ? new[] { "real_crop_watered", "real_energy_spent" } : Array.Empty<string>();
    }

    private static FeatureVector BuildStateFeatures(JsonObject snapshot)
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
                Number("volcano.level", ReadNestedFieldDouble(snapshot, "volcano", "current_level", "level")),
                Number("volcano.layout_index", ReadNestedFieldDouble(snapshot, "volcano", "current_level", "layout_index")),
                Number("volcano.coolable_uncooled_tile_count", CountNestedArray(snapshot, "volcano", "tiles", "coolable_uncooled_tiles")),
                Number("volcano.cooled_lava_tile_count", CountNestedArray(snapshot, "volcano", "tiles", "cooled_lava_tiles")),
                Number("volcano.gate_count", CountFieldArray(snapshot, "volcano", "gates")),
                Number("volcano.object_count", CountFieldArray(snapshot, "volcano", "objects")),
                Number("volcano.monster_count", CountFieldArray(snapshot, "volcano", "monsters")),
                Number("volcano.watering_can_water_left", ReadFirstNestedArrayDouble(snapshot, "volcano", "player_resources", "watering_can_slots", "water_left")),
                Number("volcano.pickaxe_slot_count", CountNestedArray(snapshot, "volcano", "player_resources", "pickaxe_slots")),
                Number("volcano.weapon_slot_count", CountNestedArray(snapshot, "volcano", "player_resources", "weapon_slots")),
                Number("volcano.heavy_hitter_slot_count", CountNestedArray(snapshot, "volcano", "player_resources", "heavy_hitter_slots")),
                Number("completeness.unavailable_count", ReadUnavailableCount(snapshot)),
                Number("completeness.required_readable_ratio", 1)
            },
            Categorical = new[]
            {
                Category("game.season", ReadFieldString(snapshot, "time", "season")),
                Category("game.weather", ReadFieldString(snapshot, "time", "weather")),
                Category("player.location_id", ReadFieldString(snapshot, "player", "location_id")),
                Category("volcano.level_kind", ReadNestedFieldString(snapshot, "volcano", "current_level", "level_kind")),
                Category("world.mode", "training")
            },
            Boolean = new[]
            {
                Flag("completeness.all_required_facts_readable", true),
                Flag("planner_inputs.blocked", false)
            }
        };
    }

    private static TrainingDatasetAppendResult AppendJsonl(string datasetPath, TrainingFeatureRowEnvelope row)
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
}
