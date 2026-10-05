using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class FullShipmentRuntimeSampleEvidenceManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } =
        "full_shipment_runtime_sample_evidence_manifest.v1";

    [JsonPropertyName("shared_shipping_recurrence_manifest_path")]
    public string SharedShippingRecurrenceManifestPath { get; init; } =
        string.Empty;

    [JsonPropertyName("shared_shipping_prefix_checkpoint_path")]
    public string SharedShippingPrefixCheckpointPath { get; init; } =
        string.Empty;

    [JsonPropertyName("samples")]
    public FullShipmentRuntimeSampleEvidenceSource[] Samples { get; init; } =
        Array.Empty<FullShipmentRuntimeSampleEvidenceSource>();

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; init; }
}

public sealed class FullShipmentRuntimeSampleEvidenceSource
{
    [JsonPropertyName("stratum_id")]
    public string StratumId { get; init; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; init; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_path")]
    public string AcquisitionRolloutProofManifestPath { get; init; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_receipt_path")]
    public string AcquisitionRolloutProofReceiptPath { get; init; } =
        string.Empty;
}

public sealed class FullShipmentRuntimeSampleEvidenceIndexReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } =
        "full_shipment_runtime_sample_evidence_index.v1";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "blocked";

    [JsonPropertyName("static_inventory_sha256")]
    public string StaticInventorySha256 { get; init; } = string.Empty;

    [JsonPropertyName("evidence_manifest_sha256")]
    public string EvidenceManifestSha256 { get; init; } = string.Empty;

    [JsonPropertyName("shared_shipping_evidence_verified")]
    public bool SharedShippingEvidenceVerified { get; init; }

    [JsonPropertyName("shared_shipping_recurrence_manifest_sha256")]
    public string SharedShippingRecurrenceManifestSha256 { get; init; } =
        string.Empty;

    [JsonPropertyName("shared_shipping_prefix_checkpoint_sha256")]
    public string SharedShippingPrefixCheckpointSha256 { get; init; } =
        string.Empty;

    [JsonPropertyName("runtime_sample_stratum_count")]
    public int RuntimeSampleStratumCount { get; init; }

    [JsonPropertyName("verified_runtime_sample_stratum_count")]
    public int VerifiedRuntimeSampleStratumCount { get; init; }

    [JsonPropertyName("missing_runtime_sample_stratum_count")]
    public int MissingRuntimeSampleStratumCount { get; init; }

    [JsonPropertyName("runtime_sample_evidence_complete")]
    public bool RuntimeSampleEvidenceComplete { get; init; }

    [JsonPropertyName("full_recurrence_required_for_training")]
    public bool FullRecurrenceRequiredForTraining { get; init; }

    [JsonPropertyName("full_recurrence_retained_for_acceptance")]
    public bool FullRecurrenceRetainedForAcceptance { get; init; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; init; }

    [JsonPropertyName("strata")]
    public FullShipmentRuntimeSampleEvidenceRow[] Strata { get; init; } =
        Array.Empty<FullShipmentRuntimeSampleEvidenceRow>();

    [JsonPropertyName("remaining_stratum_ids")]
    public string[] RemainingStratumIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("remaining_evidence_gaps")]
    public string[] RemainingEvidenceGaps { get; init; } =
        Array.Empty<string>();

    [JsonPropertyName("evidence_policy")]
    public string EvidencePolicy { get; init; } =
        "All 154 requirements remain statically reconciled. Runtime evidence is required once per route-kind and execution-signature stratum, while one independently rebuilt Full Shipment recurrence prefix proves the shared shipping and native settlement tail. Existing exact rollout proofs may be reused. Action-only smoke claims, unchecked paths and repeated items in an already covered stratum do not count. The complete 154-item recurrence is retained for optional acceptance and gameplay, not required for formal-training admission.";
}

public sealed record FullShipmentRuntimeSampleEvidenceRow(
    [property: JsonPropertyName("stratum_id")] string StratumId,
    [property: JsonPropertyName("route_kind")] string RouteKind,
    [property: JsonPropertyName("route_occurrence_count")] int RouteOccurrenceCount,
    [property: JsonPropertyName("evidence_status")] string EvidenceStatus,
    [property: JsonPropertyName("verified_route_occurrence_id")] string VerifiedRouteOccurrenceId,
    [property: JsonPropertyName("verified_requirement_id")] string VerifiedRequirementId,
    [property: JsonPropertyName("verified_qualified_item_id")] string VerifiedQualifiedItemId,
    [property: JsonPropertyName("rollout_proof_manifest_sha256")] string RolloutProofManifestSha256,
    [property: JsonPropertyName("rollout_proof_receipt_sha256")] string RolloutProofReceiptSha256,
    [property: JsonPropertyName("rollout_id")] string RolloutId);

internal sealed record FullShipmentVerifiedRuntimeSample(
    string StratumId,
    string RouteOccurrenceId,
    string RequirementId,
    string QualifiedItemId,
    string RolloutProofManifestSha256,
    string RolloutProofReceiptSha256,
    string RolloutId);

internal sealed record FullShipmentVerifiedSharedShippingEvidence(
    string RecurrenceManifestSha256,
    string PrefixCheckpointSha256);
