using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.Contracts.Training
{
    public sealed class TrainingEpisodeEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_episode.v1";

        [JsonPropertyName("episode_id")]
        public string EpisodeId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("source_model")]
        public string SourceModel { get; set; } = string.Empty;

        [JsonPropertyName("goal_id")]
        public string GoalId { get; set; } = string.Empty;

        [JsonPropertyName("action_summary")]
        public EpisodeActionSummary ActionSummary { get; set; } = new();

        [JsonPropertyName("strategy_value")]
        public StrategyValueFeedback StrategyValue { get; set; } = new();

        [JsonPropertyName("hard_feasibility")]
        public HardFeasibilityFeedback HardFeasibility { get; set; } = new();

        [JsonPropertyName("executor_calibration")]
        public ExecutorCalibrationFeedback ExecutorCalibration { get; set; } = new();

        [JsonPropertyName("candidate_audit")]
        public SmallModelPlanCandidateAudit[] CandidateAudit { get; set; } = Array.Empty<SmallModelPlanCandidateAudit>();

        [JsonPropertyName("audit")]
        public TrainingEpisodeAudit Audit { get; set; } = new();
    }

    public sealed class EpisodeActionSummary
    {
        [JsonPropertyName("option_ids")]
        public string[] OptionIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("execution_mode")]
        public string ExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("actor")]
        public ActionActorRef Actor { get; set; } = new();
    }

    public sealed class StrategyValueFeedback
    {
        [JsonPropertyName("goal_progress_delta")]
        public double GoalProgressDelta { get; set; }

        [JsonPropertyName("reward_terms")]
        public EpisodeRewardTerm[] RewardTerms { get; set; } = Array.Empty<EpisodeRewardTerm>();

        [JsonPropertyName("excluded_executor_failures")]
        public string[] ExcludedExecutorFailures { get; set; } = Array.Empty<string>();
    }

    public sealed class EpisodeRewardTerm
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    public sealed class HardFeasibilityFeedback
    {
        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("time_budget")]
        public TimeBudgetReport TimeBudget { get; set; } = new();
    }

    public sealed class ExecutorCalibrationFeedback
    {
        [JsonPropertyName("execution_profile")]
        public string ExecutionProfile { get; set; } = "perfect_human_player";

        [JsonPropertyName("before_state_hash")]
        public string BeforeStateHash { get; set; } = string.Empty;

        [JsonPropertyName("after_state_hash")]
        public string AfterStateHash { get; set; } = string.Empty;

        [JsonPropertyName("applied_option_ids")]
        public string[] AppliedOptionIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("changed_facts")]
        public SimulatedFactChange[] ChangedFacts { get; set; } = Array.Empty<SimulatedFactChange>();

        [JsonPropertyName("resource_costs")]
        public SimulatedResourceCost[] ResourceCosts { get; set; } = Array.Empty<SimulatedResourceCost>();

        [JsonPropertyName("duration_items")]
        public TimeBudgetItem[] DurationItems { get; set; } = Array.Empty<TimeBudgetItem>();

        [JsonPropertyName("calibration_notes")]
        public string[] CalibrationNotes { get; set; } = Array.Empty<string>();
    }

    public sealed class TrainingEpisodeAudit
    {
        [JsonPropertyName("adapter")]
        public string Adapter { get; set; } = "StardewAI.Core.Training.TrainingEpisodeAdapter";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Strategy value, hard feasibility, and executor calibration are separated to avoid training preference from low-level executor errors.";
    }

    public sealed class PlanExecutionEpisodeEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "plan_execution_episode.v1";

        [JsonPropertyName("episode_id")]
        public string EpisodeId { get; set; } = string.Empty;

        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("after_state_hash")]
        public string AfterStateHash { get; set; } = string.Empty;

        [JsonPropertyName("state_hash_changed")]
        public bool StateHashChanged { get; set; }

        [JsonPropertyName("before_game_tick")]
        public long BeforeGameTick { get; set; }

        [JsonPropertyName("after_game_tick")]
        public long AfterGameTick { get; set; }

        [JsonPropertyName("after_snapshot_fresh")]
        public bool AfterSnapshotFresh { get; set; }

        [JsonPropertyName("after_snapshot_note")]
        public string AfterSnapshotNote { get; set; } = string.Empty;

        [JsonPropertyName("model_plan_path")]
        public string ModelPlanPath { get; set; } = string.Empty;

        [JsonPropertyName("compiled_queue_path")]
        public string CompiledQueuePath { get; set; } = string.Empty;

        [JsonPropertyName("execution_result_path")]
        public string ExecutionResultPath { get; set; } = string.Empty;

        [JsonPropertyName("before_snapshot_path")]
        public string BeforeSnapshotPath { get; set; } = string.Empty;

        [JsonPropertyName("after_snapshot_path")]
        public string AfterSnapshotPath { get; set; } = string.Empty;

        [JsonPropertyName("dataset_path")]
        public string DatasetPath { get; set; } = string.Empty;

        [JsonPropertyName("row_id")]
        public string RowId { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("reward")]
        public double Reward { get; set; }

        [JsonPropertyName("training_role")]
        public string TrainingRole { get; set; } = "executor_calibration";

        [JsonPropertyName("failure_attribution")]
        public string FailureAttribution { get; set; } = string.Empty;

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("effective_queue_item")]
        public JsonElement? EffectiveQueueItem { get; set; }

        [JsonPropertyName("primitive_kind")]
        public string PrimitiveKind { get; set; } = string.Empty;

        [JsonPropertyName("primitive_verification_status")]
        public string PrimitiveVerificationStatus { get; set; } = string.Empty;

        [JsonPropertyName("primitive_verification_reasons")]
        public string[] PrimitiveVerificationReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("requested_effect")]
        public string RequestedEffect { get; set; } = string.Empty;

        [JsonPropertyName("observed_effect")]
        public string ObservedEffect { get; set; } = string.Empty;

        [JsonPropertyName("changed_facts")]
        public JsonElement ChangedFacts { get; set; }

        [JsonPropertyName("combat_target_runtime_type")]
        public string CombatTargetRuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("combat_target_runtime_identity")]
        public string CombatTargetRuntimeIdentity { get; set; } = string.Empty;

        [JsonPropertyName("combat_target_name")]
        public string CombatTargetName { get; set; } = string.Empty;

        [JsonPropertyName("combat_attack_count")]
        public int? CombatAttackCount { get; set; }

        [JsonPropertyName("combat_hit_count")]
        public int? CombatHitCount { get; set; }

        [JsonPropertyName("combat_target_health_sequence")]
        public int[] CombatTargetHealthSequence { get; set; } = Array.Empty<int>();

        [JsonPropertyName("combat_player_health_sequence")]
        public int[] CombatPlayerHealthSequence { get; set; } = Array.Empty<int>();

        [JsonPropertyName("combat_damage_taken")]
        public int? CombatDamageTaken { get; set; }

        [JsonPropertyName("combat_target_defeated")]
        public bool? CombatTargetDefeated { get; set; }

        [JsonPropertyName("combat_intent")]
        public string CombatIntent { get; set; } = string.Empty;

        [JsonPropertyName("recovery_food_slot_index")]
        public int? RecoveryFoodSlotIndex { get; set; }

        [JsonPropertyName("recovery_food_qualified_item_id")]
        public string RecoveryFoodQualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("recovery_food_stack_before")]
        public int? RecoveryFoodStackBefore { get; set; }

        [JsonPropertyName("recovery_food_stack_after")]
        public int? RecoveryFoodStackAfter { get; set; }

        [JsonPropertyName("recovery_health_before")]
        public int? RecoveryHealthBefore { get; set; }

        [JsonPropertyName("recovery_health_after")]
        public int? RecoveryHealthAfter { get; set; }

        [JsonPropertyName("recovery_restore_slot_index")]
        public int? RecoveryRestoreSlotIndex { get; set; }

        [JsonPropertyName("recovery_safety_status")]
        public string RecoverySafetyStatus { get; set; } = string.Empty;

        [JsonPropertyName("shaft_mine_level_before")]
        public int? ShaftMineLevelBefore { get; set; }

        [JsonPropertyName("shaft_mine_level_after")]
        public int? ShaftMineLevelAfter { get; set; }

        [JsonPropertyName("shaft_level_delta")]
        public int? ShaftLevelDelta { get; set; }

        [JsonPropertyName("shaft_health_before")]
        public int? ShaftHealthBefore { get; set; }

        [JsonPropertyName("shaft_health_after")]
        public int? ShaftHealthAfter { get; set; }

        [JsonPropertyName("shaft_native_dialogue_handled")]
        public bool? ShaftNativeDialogueHandled { get; set; }

        [JsonPropertyName("retreat_reason")]
        public string RetreatReason { get; set; } = string.Empty;

        [JsonPropertyName("retreat_mine_level_before")]
        public int? RetreatMineLevelBefore { get; set; }

        [JsonPropertyName("retreat_time_before")]
        public int? RetreatTimeBefore { get; set; }

        [JsonPropertyName("retreat_health_before")]
        public int? RetreatHealthBefore { get; set; }

        [JsonPropertyName("retreat_energy_before")]
        public double? RetreatEnergyBefore { get; set; }

        [JsonPropertyName("retreat_destination")]
        public string RetreatDestination { get; set; } = string.Empty;

        [JsonPropertyName("retreat_native_dialogue_handled")]
        public bool? RetreatNativeDialogueHandled { get; set; }

        [JsonPropertyName("dialogue_native_handled")]
        public bool? DialogueNativeHandled { get; set; }

        [JsonPropertyName("dialogue_press_attempts")]
        public int? DialoguePressAttempts { get; set; }

        [JsonPropertyName("dialogue_advance_ticks")]
        public int? DialogueAdvanceTicks { get; set; }

        [JsonPropertyName("dialogue_menu_type_before")]
        public string DialogueMenuTypeBefore { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_menu_type_after")]
        public string DialogueMenuTypeAfter { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_is_question_before")]
        public bool? DialogueIsQuestionBefore { get; set; }

        [JsonPropertyName("dialogue_is_question_after")]
        public bool? DialogueIsQuestionAfter { get; set; }

        [JsonPropertyName("dialogue_response_count_before")]
        public int? DialogueResponseCountBefore { get; set; }

        [JsonPropertyName("dialogue_response_count_after")]
        public int? DialogueResponseCountAfter { get; set; }

        [JsonPropertyName("dialogue_speaker_name_before")]
        public string DialogueSpeakerNameBefore { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_speaker_name_after")]
        public string DialogueSpeakerNameAfter { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_event_up_before")]
        public bool? DialogueEventUpBefore { get; set; }

        [JsonPropertyName("dialogue_event_up_after")]
        public bool? DialogueEventUpAfter { get; set; }

        [JsonPropertyName("material_transfer_intent")]
        public MaterialTransferIntent? MaterialTransferIntent { get; set; }

        [JsonPropertyName("material_transfer_projection")]
        public MaterialTransferProjection? MaterialTransferProjection { get; set; }

        [JsonPropertyName("material_transfer_click_count")]
        public int? MaterialTransferClickCount { get; set; }

        [JsonPropertyName("material_transfer_source_stack_before")]
        public int? MaterialTransferSourceStackBefore { get; set; }

        [JsonPropertyName("material_transfer_source_stack_after")]
        public int? MaterialTransferSourceStackAfter { get; set; }

        [JsonPropertyName("material_transfer_destination_quantity_before")]
        public int? MaterialTransferDestinationQuantityBefore { get; set; }

        [JsonPropertyName("material_transfer_destination_quantity_after")]
        public int? MaterialTransferDestinationQuantityAfter { get; set; }

        [JsonPropertyName("material_transfer_native_menu_opened")]
        public bool? MaterialTransferNativeMenuOpened { get; set; }

        [JsonPropertyName("material_transfer_native_lock_released")]
        public bool? MaterialTransferNativeLockReleased { get; set; }

        [JsonPropertyName("audit")]
        public PlanExecutionEpisodeAudit Audit { get; set; } = new();
    }

    public sealed class PlanExecutionEpisodeAudit
    {
        [JsonPropertyName("writer")]
        public string Writer { get; set; } = "StardewAI.LiveTrainingLoop";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Plan execution episodes preserve model plan, compiled queue, runtime execution result, before/after snapshots, and labels so model training is grounded in execution feedback.";
    }
}
