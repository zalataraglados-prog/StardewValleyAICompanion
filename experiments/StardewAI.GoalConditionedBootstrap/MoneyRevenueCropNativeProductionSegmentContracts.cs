using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropNativeProductionSegmentVersions
{
    public const string Receipt =
        "money_revenue_crop_native_production_segment_receipt.v1";
}

public sealed class MoneyRevenueCropNativeProductionSegmentReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyRevenueCropNativeProductionSegmentVersions.Receipt;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("run_summary_path")]
    public string RunSummaryPath { get; set; } = string.Empty;

    [JsonPropertyName("run_summary_sha256")]
    public string RunSummarySha256 { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("production_input_manifest_path")]
    public string ProductionInputManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("production_input_manifest_sha256")]
    public string ProductionInputManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("decision_id")]
    public string DecisionId { get; set; } = string.Empty;

    [JsonPropertyName("selected_seed_id")]
    public string SelectedSeedId { get; set; } = string.Empty;

    [JsonPropertyName("selected_seed_qualified_item_id")]
    public string SelectedSeedQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("selected_tile_count")]
    public int SelectedTileCount { get; set; }

    [JsonPropertyName("teacher_candidate_count")]
    public int TeacherCandidateCount { get; set; }

    [JsonPropertyName("teacher_minimum_gross_revenue")]
    public long TeacherMinimumGrossRevenue { get; set; }

    [JsonPropertyName("teacher_seed_acquisition_cost")]
    public long TeacherSeedAcquisitionCost { get; set; }

    [JsonPropertyName("teacher_minimum_net_revenue")]
    public long TeacherMinimumNetRevenue { get; set; }

    [JsonPropertyName("source_checkpoint")]
    public MoneyRevenueCheckpoint SourceCheckpoint { get; set; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        0,
        string.Empty,
        0,
        string.Empty);

    [JsonPropertyName("final_checkpoint")]
    public MoneyRevenueCheckpoint FinalCheckpoint { get; set; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        0,
        string.Empty,
        0,
        string.Empty);

    [JsonPropertyName("final_snapshot_path")]
    public string FinalSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("final_snapshot_sha256")]
    public string FinalSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("final_isolated_save_slot_path")]
    public string FinalIsolatedSaveSlotPath { get; set; } = string.Empty;

    [JsonPropertyName("final_isolated_save_sha256")]
    public string FinalIsolatedSaveSha256 { get; set; } = string.Empty;

    [JsonPropertyName("actual_seed_acquisition_cost")]
    public long ActualSeedAcquisitionCost { get; set; }

    [JsonPropertyName("actual_gross_revenue")]
    public long ActualGrossRevenue { get; set; }

    [JsonPropertyName("actual_cash_net_revenue")]
    public long ActualCashNetRevenue { get; set; }

    [JsonPropertyName("crossed_criterion_ids")]
    public string[] CrossedCriterionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("artifacts")]
    public MoneyRevenueArtifactBinding[] Artifacts { get; set; } =
        Array.Empty<MoneyRevenueArtifactBinding>();

    [JsonPropertyName("teacher_decision_verified")]
    public bool TeacherDecisionVerified { get; set; }

    [JsonPropertyName("native_production_segment_verified")]
    public bool NativeProductionSegmentVerified { get; set; }

    [JsonPropertyName("teacher_comparison_verified")]
    public bool TeacherComparisonVerified { get; set; }

    [JsonPropertyName("receipt_sha256")]
    public string ReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("receipt_policy")]
    public string ReceiptPolicy { get; set; } =
        "The receipt independently rebuilds one existing crop-production Teacher decision, obligation/run plan, complete native proof checkpoint and terminal Revenue recurrence. It binds the exact final transparent snapshot and immutable isolated-save tree, then reports observed cash cost, revenue and authoritative threshold crossings. It does not plan or execute actions, create a Teacher comparison, count a Money production cell or authorize formal training.";
}

internal sealed class MoneyCropNativeProductionInputManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("input_id")]
    public string InputId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("deadline_total_day_exclusive")]
    public int DeadlineTotalDayExclusive { get; set; }

    [JsonPropertyName("target_total_money_earned")]
    public long TargetTotalMoneyEarned { get; set; }

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_total_day")]
    public int SourceTotalDay { get; set; }

    [JsonPropertyName("source_save_slot_path")]
    public string SourceSaveSlotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_save_tree_sha256")]
    public string SourceSaveTreeSha256 { get; set; } = string.Empty;

    [JsonPropertyName("decision_id")]
    public string DecisionId { get; set; } = string.Empty;

    [JsonPropertyName("decision_request_path")]
    public string DecisionRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("decision_request_sha256")]
    public string DecisionRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("decision_receipt_path")]
    public string DecisionReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("decision_receipt_sha256")]
    public string DecisionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("teacher_request_path")]
    public string TeacherRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("teacher_request_sha256")]
    public string TeacherRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("teacher_selection_policy")]
    public string TeacherSelectionPolicy { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_path")]
    public string SnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_path")]
    public string StrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_obligation_request_path")]
    public string CropProductionObligationRequestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("crop_production_obligation_request_sha256")]
    public string CropProductionObligationRequestSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("crop_production_obligation_plan_path")]
    public string CropProductionObligationPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_obligation_plan_sha256")]
    public string CropProductionObligationPlanSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("crop_native_run_plan_request_path")]
    public string CropNativeRunPlanRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_native_run_plan_request_sha256")]
    public string CropNativeRunPlanRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_native_run_plan_path")]
    public string CropNativeRunPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_native_run_plan_sha256")]
    public string CropNativeRunPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("commitment_count")]
    public int CommitmentCount { get; set; }

    [JsonPropertyName("max_queue_item_count")]
    public int MaxQueueItemCount { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

internal sealed class MoneyCropNativeProductionRunSummary
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("production_input_manifest_path")]
    public string ProductionInputManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("production_input_manifest_sha256")]
    public string ProductionInputManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("run_plan_path")]
    public string RunPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("run_plan_sha256")]
    public string RunPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("source_save_slot_path")]
    public string SourceSaveSlotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_save_sha256_before")]
    public string SourceSaveSha256Before { get; set; } = string.Empty;

    [JsonPropertyName("source_save_sha256_after")]
    public string SourceSaveSha256After { get; set; } = string.Empty;

    [JsonPropertyName("source_preflight_path")]
    public string SourcePreflightPath { get; set; } = string.Empty;

    [JsonPropertyName("isolated_save_slot_path")]
    public string IsolatedSaveSlotPath { get; set; } = string.Empty;

    [JsonPropertyName("isolated_save_sha256_before")]
    public string IsolatedSaveSha256Before { get; set; } = string.Empty;

    [JsonPropertyName("isolated_save_sha256_after")]
    public string IsolatedSaveSha256After { get; set; } = string.Empty;

    [JsonPropertyName("isolated_mods_root_path")]
    public string IsolatedModsRootPath { get; set; } = string.Empty;

    [JsonPropertyName("isolated_mods_sha256_before")]
    public string IsolatedModsSha256Before { get; set; } = string.Empty;

    [JsonPropertyName("isolated_mods_sha256_after")]
    public string IsolatedModsSha256After { get; set; } = string.Empty;

    [JsonPropertyName("runtime_mod_allowlist")]
    public MoneyThresholdRuntimeModIdentity[] RuntimeModAllowlist { get; set; } =
        Array.Empty<MoneyThresholdRuntimeModIdentity>();

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("executed_crop_batch_count")]
    public int ExecutedCropBatchCount { get; set; }

    [JsonPropertyName("seed_acquisition_proof_count")]
    public int SeedAcquisitionProofCount { get; set; }

    [JsonPropertyName("terminal_settlement_proof_count")]
    public int TerminalSettlementProofCount { get; set; }

    [JsonPropertyName("settlement_capacity_proof_count")]
    public int SettlementCapacityProofCount { get; set; }

    [JsonPropertyName("shipping_deposit_count")]
    public int ShippingDepositCount { get; set; }

    [JsonPropertyName("checkpoint_path")]
    public string CheckpointPath { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_verified_fragment_count")]
    public int CheckpointVerifiedFragmentCount { get; set; }

    [JsonPropertyName("checkpoint_pending_request_count")]
    public int CheckpointPendingRequestCount { get; set; }

    [JsonPropertyName("final_snapshot_path")]
    public string FinalSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("final_snapshot_sha256")]
    public string FinalSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("final_state_hash")]
    public string FinalStateHash { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}
