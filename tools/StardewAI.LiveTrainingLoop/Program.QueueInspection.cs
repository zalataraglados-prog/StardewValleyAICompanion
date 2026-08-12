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
    private static bool IsExecutableQueueItem(JsonObject? item)
    {
        var status = ReadStringOrEmpty(item, "status");
        return string.IsNullOrWhiteSpace(status) || string.Equals(status, "pending", StringComparison.Ordinal);
    }

    private static JsonObject[] ExecutableQueueItems(JsonObject queue)
    {
        return (queue["items"] as JsonArray)?
            .Select(node => node?.AsObject())
            .TakeWhile(item => item is not null && IsExecutableQueueItem(item))
            .Cast<JsonObject>()
            .ToArray() ?? Array.Empty<JsonObject>();
    }

    private static async Task<(string Json, JsonObject Snapshot, bool Fresh, string Note)> ReadAfterExecutionSnapshotAsync(
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

    private static TrainingDatasetAppendResult AppendRealExecutionRow(
        LiveTrainingOptions options,
        JsonObject beforeSnapshot,
        JsonObject queue,
        JsonObject execution,
        string stateHash,
        string queueId,
        bool appendToDataset = true)
    {
        var item = execution["effective_queue_item"]?.AsObject() ?? FindQueueItemForExecution(queue, execution) ?? queue["items"]?.AsArray().FirstOrDefault()?.AsObject();
        var effectiveBeforeSnapshot = ReadEffectiveBeforeSnapshot(execution, beforeSnapshot);
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
        var isVolcanoCooling = string.Equals(optionId, "executor.cool_volcano_lava", StringComparison.Ordinal);
        var isVolcanoObstacle = string.Equals(optionId, "executor.break_volcano_stone", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.break_volcano_container", StringComparison.Ordinal);
        var isVolcanoCombat = string.Equals(optionId, "executor.combat_volcano_monster", StringComparison.Ordinal);
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
        var isPrimitive = isMove ||
            isVolcanoCooling ||
            isVolcanoObstacle ||
            isVolcanoCombat ||
            string.Equals(optionId, "executor.face_direction", StringComparison.Ordinal) ||
            string.Equals(optionId, "executor.wait_ticks", StringComparison.Ordinal);
        var applied = string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal);
        var reward = isMove
            ? applied ? 0.05 : -0.05
            : isVolcanoCooling ? applied ? 0.10 : -0.10
            : isVolcanoObstacle ? applied ? 0.08 : -0.08
            : isVolcanoCombat ? applied ? 0.12 : -0.12
            : isAnvilReforgeSample && applied
                ? ReadDouble(
                    execution,
                    "anvil_reforge_realized_utility_delta")
            : isPrimitive ? applied ? 0.02 : -0.02
            : Math.Round(watered * 0.10 - energyCost * 0.005, 4);
        var blocked = !string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal) &&
            !string.Equals(ReadString(execution, "status"), "no_op", StringComparison.Ordinal);
        var requiredMinutes = isMove ||
            isVolcanoCooling ||
            isVolcanoObstacle ||
            isAnvilReforgeSample
            ? 1
            : isVolcanoCombat ? 2 : 30;
        var primitiveVerificationStatus = ReadString(execution, "primitive_verification_status");
        var primitiveVerified = string.Equals(primitiveVerificationStatus, "verified", StringComparison.Ordinal);
        var failureCategory = ReadString(execution, "failure_category");
        var afterSnapshotFresh = execution["after_snapshot_fresh"]?.GetValue<bool>() == true;
        var stateHashChanged = execution["state_hash_changed"]?.GetValue<bool>() == true;
        var tickDelta = Math.Max(0, ReadLong(execution, "after_game_tick") - ReadLong(execution, "before_game_tick"));
        var trainingRole =
            isAnvilReforgeSample && applied
                ? TrainingRoles.StrategyValue
                : TrainingRoles.ExecutorCalibration;
        var learningScope =
            trainingRole ==
            TrainingRoles.StrategyValue
                ? "policy_ranker"
                : "calibration_only";
        var excludeFromPolicyTraining =
            trainingRole ==
            TrainingRoles.ExecutorCalibration;
        var behaviorCategory =
            isAnvilReforgeSample
                ? OptionBehaviorCategories
                    .EconomicStrategic
                : OptionBehaviorCategories
                    .Mechanical;

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
                TrainingRole = trainingRole,
                LearningScope = learningScope,
                ExcludeFromPolicyTraining =
                    excludeFromPolicyTraining,
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
                        Number("execution.tool_use_count", ReadInt(execution, "tool_use_count")),
                        Number("machine.anvil.reforge.current_utility", ReadDouble(execution, "anvil_reforge_current_utility")),
                        Number("machine.anvil.reforge.expected_utility", ReadDouble(execution, "anvil_reforge_expected_utility")),
                        Number("machine.anvil.reforge.realized_utility", ReadDouble(execution, "anvil_reforge_realized_utility")),
                        Number("machine.anvil.reforge.realized_utility_delta", ReadDouble(execution, "anvil_reforge_realized_utility_delta")),
                        Number("machine.anvil.reforge.planned_expected_utility_delta", ReadQueueParameterDouble(item, "anvil_reforge_expected_utility_delta") ?? 0),
                        Number("machine.anvil.reforge.planned_improvement_probability", ReadQueueParameterDouble(item, "anvil_reforge_improvement_probability") ?? 0),
                        Number("machine.anvil.reforge.additional_material_sale_value", ReadQueueParameterDouble(item, "machine_additional_consumed_total_value") ?? 0),
                        Number("machine.anvil.reforge.unlocked_slot_count", ReadQueueParameterDouble(item, "anvil_reforge_unlocked_slot_count") ?? 0),
                        Number("machine.anvil.reforge.occupied_slot_count", ReadQueueParameterDouble(item, "anvil_reforge_occupied_slot_count") ?? 0),
                        Number("machine.anvil.reforge.empty_unlocked_slot_count", ReadQueueParameterDouble(item, "anvil_reforge_empty_unlocked_slot_count") ?? 0),
                        Number("machine.anvil.reforge.same_type_equipped_count", ReadQueueParameterDouble(item, "anvil_reforge_same_type_equipped_count") ?? 0),
                        Number("machine.anvil.reforge.other_type_equipped_count", ReadQueueParameterDouble(item, "anvil_reforge_other_type_equipped_count") ?? 0),
                        Number("machine.anvil.reforge.goal_capability_affinity", ReadQueueParameterDouble(item, "anvil_reforge_goal_capability_affinity") ?? 0),
                        Number("machine.anvil.reforge.loadout_adjustment", ReadQueueParameterDouble(item, "anvil_reforge_loadout_adjustment") ?? 0),
                        Number("machine.anvil.reforge.effective_demand_score", ReadQueueParameterDouble(item, "anvil_reforge_effective_demand_score") ?? 0),
                        Number("goal.support.gross_benefit", ReadQueueParameterDouble(item, "goal_support_gross_benefit") ?? 0),
                        Number("goal.support.opportunity_cost", ReadQueueParameterDouble(item, "goal_support_opportunity_cost") ?? 0),
                        Number("goal.support.net_benefit", ReadQueueParameterDouble(item, "goal_support_net_benefit") ?? 0),
                        Number("goal.support.score", ReadQueueParameterDouble(item, "goal_support_score") ?? 0),
                        Number(
                            "machine.support.original_net_benefit",
                            ReadQueueParameterDouble(
                                item,
                                "machine_support_original_net_benefit") ??
                            0),
                        Number(
                            "machine.support.current_input_net_benefit",
                            ReadQueueParameterDouble(
                                item,
                                "machine_support_current_input_net_benefit") ??
                            0),
                        Number(
                            "machine.support.continuation_score",
                            ReadQueueParameterDouble(
                                item,
                                "machine_support_continuation_score") ??
                            0),
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
                        Category("action.intent_category", behaviorCategory),
                        Category("action.behavior_category", behaviorCategory),
                        Category("action.training_role", trainingRole),
                        Category("action.learning_scope", learningScope),
                        Category("action.execution_mode", options.TargetExecutionMode),
                        Category("action.actor_type", options.TargetActor.ActorType),
                        Category("action.execution_profile", isMove ? "runtime_test_move_harness" : "runtime_test_harness"),
                        Category("execution.primitive_kind", ReadString(execution, "primitive_kind")),
                        Category("execution.primitive_verification_status", primitiveVerificationStatus),
                        Category("execution.failure_category", failureCategory),
                        Category("execution.tool_qualified_item_id", ReadString(execution, "tool_qualified_item_id")),
                        Category("execution.training_impact_scope", ReadString(execution, "training_impact_scope")),
                        Category("execution.after_snapshot_note", ReadString(execution, "after_snapshot_note")),
                        Category("machine.anvil.reforge.outcome_kind", ReadString(execution, "machine_output_distribution_outcome_kind")),
                        Category("machine.anvil.reforge.utility_metric", ReadString(execution, "anvil_reforge_utility_metric")),
                        Category("machine.anvil.reforge.loadout_status", ReadQueueParameterString(item, "anvil_reforge_loadout_status")),
                        Category("machine.anvil.reforge.capability_class", ReadQueueParameterString(item, "anvil_reforge_capability_class")),
                        Category("machine.anvil.reforge.kill_credit_policy", ReadQueueParameterString(item, "anvil_reforge_kill_credit_policy")),
                        Category("machine.anvil.reforge.loot_policy", ReadQueueParameterString(item, "anvil_reforge_loot_policy")),
                        Category("machine.anvil.reforge.loadout_relation", ReadQueueParameterString(item, "anvil_reforge_loadout_relation")),
                        Category("machine.anvil.reforge.goal_demand_status", ReadQueueParameterString(item, "anvil_reforge_goal_demand_status")),
                        Category("machine.anvil.reforge.goal_family", ReadQueueParameterString(item, "anvil_reforge_goal_family")),
                        Category("machine.anvil.reforge.goal_demand_reason", ReadQueueParameterString(item, "anvil_reforge_goal_demand_reason")),
                        Category("goal.support.status", ReadQueueParameterString(item, "goal_support_status")),
                        Category("goal.support.kind", ReadQueueParameterString(item, "goal_support_kind")),
                        Category("goal.support.parent_goal_id", ReadQueueParameterString(item, "goal_support_parent_goal_id")),
                        Category("goal.support.evidence_status", ReadQueueParameterString(item, "goal_support_evidence_status")),
                        Category("goal.support.reason", ReadQueueParameterString(item, "goal_support_reason")),
                        Category(
                            "machine.support.continuation_status",
                            ReadQueueParameterString(
                                item,
                                "machine_support_continuation_status")),
                        Category(
                            "machine.support.continuation_kind",
                            ReadQueueParameterString(
                                item,
                                "machine_support_continuation_kind")),
                        Category(
                            "machine.support.intent_stage",
                            ReadQueueParameterString(
                                item,
                                "machine_support_intent_stage")),
                        Category(
                            "machine.support.goal_id",
                            ReadQueueParameterString(
                                item,
                                "machine_support_goal_id")),
                        Category(
                            "machine.support.continuation_reason",
                            ReadQueueParameterString(
                                item,
                                "machine_support_continuation_reason")),
                        Category("fishing.observed_qualified_item_id", ReadChangedFactString(execution, "fishing.caught_qualified_item_id")),
                        Category("fishing.terminal_result", ReadChangedFactString(execution, "fishing.terminal_result"))
                    },
                    Boolean = new[]
                    {
                        Flag("action.hard_blocked", blocked),
                        Flag(
                            "goal.support.active",
                            string.Equals(
                                ReadQueueParameterString(
                                    item,
                                    "goal_support_status"),
                                "supported_bounded_positive_net_benefit",
                                StringComparison.Ordinal)),
                        Flag(
                            "machine.support.continuation_active",
                            string.Equals(
                                ReadQueueParameterString(
                                    item,
                                    "machine_support_continuation_status"),
                                "active",
                                StringComparison.Ordinal)),
                        Flag("action.exclude_from_policy_training", excludeFromPolicyTraining),
                        Flag("execution.primitive_verified", primitiveVerified),
                        Flag("execution.after_snapshot_fresh", afterSnapshotFresh),
                        Flag("machine.anvil.reforge.realized_improved", execution["anvil_reforge_realized_improved"]?.GetValue<bool>() == true),
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
                RewardTermNames = RewardTerms(
                    optionId,
                    isMove,
                    applied,
                    watered,
                    isAnvilReforgeSample),
                BlockReasons = ReadArrayStrings(execution, "block_reasons")
            },
            Audit = new TrainingFeatureRowAudit
            {
                Exporter = "StardewAI.LiveTrainingLoop.RuntimeTestHarnessExecutor",
                Policy = "Feature row labels are derived from RuntimeTestHarness execution result and before/after transparent snapshots; no simulator endpoint used."
            }
        };

        if (appendToDataset)
        {
            return AppendJsonl(options.DatasetPath, row);
        }

        var datasetPath = Path.GetFullPath(options.DatasetPath);
        return new TrainingDatasetAppendResult
        {
            DatasetPath = datasetPath,
            RowId = string.Empty,
            EpisodeId = row.EpisodeId,
            BytesWritten = 0,
            RowCount = File.Exists(datasetPath)
                ? File.ReadLines(datasetPath)
                    .Count(line => !string.IsNullOrWhiteSpace(line))
                : 0
        };
    }

    private static JsonObject ReadEffectiveBeforeSnapshot(JsonObject execution, JsonObject fallback)
    {
        if (execution["effective_before_snapshot"] is JsonObject embedded)
        {
            return embedded;
        }

        var path = ReadString(execution, "effective_before_snapshot_path");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return fallback;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))?.AsObject() ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
    }
}
