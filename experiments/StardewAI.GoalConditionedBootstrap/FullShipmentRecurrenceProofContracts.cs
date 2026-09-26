using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class FullShipmentRecurrenceSettlementKinds
{
    public const string Ordinary = "ordinary";
    public const string Terminal = "terminal";
}

public sealed class FullShipmentRecurrenceProofManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "full_shipment_recurrence_proof_manifest.v1";

    [JsonPropertyName("requirement_inventory_path")]
    public string RequirementInventoryPath { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_path")]
    public string AcquisitionLoweringPath { get; set; } = string.Empty;

    [JsonPropertyName("initial_snapshot_path")]
    public string InitialSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("iterations")]
    public FullShipmentRecurrenceIterationProof[] Iterations { get; set; } =
        Array.Empty<FullShipmentRecurrenceIterationProof>();

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }
}

public sealed class FullShipmentRecurrenceIterationProof
{
    [JsonPropertyName("requirement_id")]
    public string RequirementId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_path")]
    public string AcquisitionRolloutProofManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_receipt_path")]
    public string AcquisitionRolloutProofReceiptPath { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_after_snapshot_path")]
    public string AcquisitionAfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("deposit")]
    public FullShipmentDepositTeacherProof Deposit { get; set; } = new();

    [JsonPropertyName("settlement")]
    public FullShipmentRecurrenceSettlementProof Settlement { get; set; } =
        new();
}

public sealed class FullShipmentDepositTeacherProof
{
    [JsonPropertyName("ranking_path")]
    public string RankingPath { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("master_angler_target_date_intents_path")]
    public string MasterAnglerTargetDateIntentsPath { get; set; } =
        string.Empty;

    [JsonPropertyName("preference_path")]
    public string PreferencePath { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_path")]
    public string ExecutionReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("teacher_receipt_path")]
    public string TeacherReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("trajectory_id")]
    public string TrajectoryId { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("knowledge_dictionary_version")]
    public string KnowledgeDictionaryVersion { get; set; } = string.Empty;

    [JsonPropertyName("executor_version")]
    public string ExecutorVersion { get; set; } = string.Empty;
}

public sealed class FullShipmentRecurrenceSettlementProof
{
    [JsonPropertyName("settlement_kind")]
    public string SettlementKind { get; set; } = string.Empty;

    [JsonPropertyName("queue_path")]
    public string QueuePath { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_path")]
    public string ExecutionReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("settlement_receipt_path")]
    public string SettlementReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("executor_version")]
    public string ExecutorVersion { get; set; } = string.Empty;

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;
}

public sealed class FullShipmentRecurrenceProofReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "full_shipment_recurrence_proof_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("required_item_count")]
    public int RequiredItemCount { get; set; }

    [JsonPropertyName("verified_iteration_count")]
    public int VerifiedIterationCount { get; set; }

    [JsonPropertyName("initial_state_hash")]
    public string InitialStateHash { get; set; } = string.Empty;

    [JsonPropertyName("final_state_hash")]
    public string FinalStateHash { get; set; } = string.Empty;

    [JsonPropertyName("initial_total_day")]
    public int? InitialTotalDay { get; set; }

    [JsonPropertyName("terminal_settlement_start_total_day")]
    public int? TerminalSettlementStartTotalDay { get; set; }

    [JsonPropertyName("final_total_day")]
    public int? FinalTotalDay { get; set; }

    [JsonPropertyName("achievement_34_verified")]
    public bool Achievement34Verified { get; set; }

    [JsonPropertyName("recurrence_proof_verified")]
    public bool RecurrenceProofVerified { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("iterations")]
    public FullShipmentRecurrenceIterationEvidence[] Iterations { get; set; } =
        Array.Empty<FullShipmentRecurrenceIterationEvidence>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed record FullShipmentRecurrenceIterationEvidence(
    [property: JsonPropertyName("iteration_index")] int IterationIndex,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("acquisition_rollout_proof_sha256")]
        string AcquisitionRolloutProofSha256,
    [property: JsonPropertyName("deposit_teacher_receipt_sha256")]
        string DepositTeacherReceiptSha256,
    [property: JsonPropertyName("settlement_receipt_sha256")]
        string SettlementReceiptSha256,
    [property: JsonPropertyName("before_shipped_item_count")]
        int BeforeShippedItemCount,
    [property: JsonPropertyName("after_shipped_item_count")]
        int AfterShippedItemCount,
    [property: JsonPropertyName("settlement_start_total_day")]
        int SettlementStartTotalDay,
    [property: JsonPropertyName("settlement_end_total_day")]
        int SettlementEndTotalDay,
    [property: JsonPropertyName("terminal_transition")]
        bool TerminalTransition);
