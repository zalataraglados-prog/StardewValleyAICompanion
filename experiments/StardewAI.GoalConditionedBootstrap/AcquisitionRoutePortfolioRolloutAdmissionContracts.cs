using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioRolloutAdmissionReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_rollout_admission_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("authorization_scope")]
    public string AuthorizationScope { get; set; } =
        "verified_acquisition_route_portfolio_teacher_evidence";

    [JsonPropertyName("controller_admission_granted")]
    public bool ControllerAdmissionGranted { get; set; }

    [JsonPropertyName("teacher_training_evidence_eligible")]
    public bool TeacherTrainingEvidenceEligible { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("proof_manifest_sha256")]
    public string ProofManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("proof_receipt_sha256")]
    public string ProofReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("latest_checkpoint_sha256")]
    public string LatestCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("latest_state_hash")]
    public string LatestStateHash { get; set; } = string.Empty;

    [JsonPropertyName("latest_ledger_revision")]
    public int LatestLedgerRevision { get; set; }

    [JsonPropertyName("latest_ledger_sha256")]
    public string LatestLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The controller recomputes the ordered rollout proof from authoritative artifacts and requires the supplied proof receipt to equal that recomputation byte-for-byte at the typed JSON level. Only a verified terminal portfolio is admitted as scoped Teacher evidence. This receipt does not authorize a formal_product_training process, bypass option admission, or replace dataset, checkpoint, Product Executor, version, and native-save gates.";
}
