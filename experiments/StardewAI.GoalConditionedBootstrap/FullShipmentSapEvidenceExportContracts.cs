using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class FullShipmentSapEvidenceExport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } =
        "stardewai.runtime_full_shipment_sap_evidence_export.v1";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "passed";

    [JsonPropertyName("scenario")]
    public string Scenario { get; init; } = "sap_prefix";

    [JsonPropertyName("run_id")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("requirement_id")]
    public string RequirementId { get; init; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; init; } = string.Empty;

    [JsonPropertyName("acquisition_route_occurrence_id")]
    public string AcquisitionRouteOccurrenceId { get; init; } = string.Empty;

    [JsonPropertyName("verified_iteration_count")]
    public int VerifiedIterationCount { get; init; }

    [JsonPropertyName("remaining_item_count")]
    public int RemainingItemCount { get; init; }

    [JsonPropertyName("final_total_day")]
    public int FinalTotalDay { get; init; }

    [JsonPropertyName("ready_for_next_iteration")]
    public bool ReadyForNextIteration { get; init; }

    [JsonPropertyName("recurrence_manifest_path")]
    public string RecurrenceManifestPath { get; init; } = string.Empty;

    [JsonPropertyName("recurrence_manifest_sha256")]
    public string RecurrenceManifestSha256 { get; init; } = string.Empty;

    [JsonPropertyName("prefix_checkpoint_path")]
    public string PrefixCheckpointPath { get; init; } = string.Empty;

    [JsonPropertyName("prefix_checkpoint_sha256")]
    public string PrefixCheckpointSha256 { get; init; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_path")]
    public string AcquisitionRolloutProofManifestPath { get; init; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_sha256")]
    public string AcquisitionRolloutProofManifestSha256 { get; init; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_receipt_path")]
    public string AcquisitionRolloutReceiptPath { get; init; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_receipt_sha256")]
    public string AcquisitionRolloutReceiptSha256 { get; init; } =
        string.Empty;

    [JsonPropertyName("evidence_verified")]
    public bool EvidenceVerified { get; init; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; init; }
}
