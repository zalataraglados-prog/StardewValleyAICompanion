using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropSeedAcquisitionProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_seed_acquisition_proof_request.v1";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_path")]
    public string AcquisitionRolloutProofManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_receipt_path")]
    public string AcquisitionRolloutProofReceiptPath { get; set; } =
        string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropSeedAcquisitionProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_seed_acquisition_proof.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_sha256")]
    public string CropProductionPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("evidence_request_id")]
    public string EvidenceRequestId { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_path")]
    public string AcquisitionRolloutProofManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_sha256")]
    public string AcquisitionRolloutProofManifestSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_receipt_path")]
    public string AcquisitionRolloutProofReceiptPath { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_receipt_sha256")]
    public string AcquisitionRolloutProofReceiptSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_sha256")]
    public string BeforeSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("before_state_hash")]
    public string BeforeStateHash { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_state_hash")]
    public string AfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_path")]
    public string FreshTerminalReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_sha256")]
    public string FreshTerminalReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("seed_qualified_item_id")]
    public string SeedQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("required_shortfall_quantity")]
    public int RequiredShortfallQuantity { get; set; }

    [JsonPropertyName("verified_quantity_increase")]
    public int VerifiedQuantityIncrease { get; set; }

    [JsonPropertyName("seed_supply_deadline_total_day")]
    public int SeedSupplyDeadlineTotalDay { get; set; }

    [JsonPropertyName("acquisition_total_day")]
    public int AcquisitionTotalDay { get; set; }

    [JsonPropertyName("seed_acquisition_verified")]
    public bool SeedAcquisitionVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "The proof adapts exactly one independently rebuilt acquisition-route rollout " +
        "to one crop commitment shortfall. The rollout must start at the exact crop-plan " +
        "snapshot, finish one verified terminal quantity transition for the exact seed and " +
        "shortfall before the planting deadline, and contain no unrelated continuation. It " +
        "reuses the shared acquisition planner, compiler, executor, fresh-terminal verifier " +
        "and settlement chain; it creates no purchase implementation, Teacher label, " +
        "production cell or formal training authorization.";
}
