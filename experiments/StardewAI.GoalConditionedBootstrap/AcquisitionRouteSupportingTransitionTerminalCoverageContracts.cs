using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteSupportingTransitionTerminalCoverageRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } =
        "acquisition_route_supporting_transition_terminal_coverage_request.v1";

    [JsonPropertyName("sources")]
    public AcquisitionRouteSupportingTransitionTerminalCoverageSource[] Sources
    { get; init; } = Array.Empty<
        AcquisitionRouteSupportingTransitionTerminalCoverageSource>();
}

public sealed class AcquisitionRouteSupportingTransitionTerminalCoverageSource
{
    [JsonPropertyName("rollout_proof_manifest_path")]
    public string RolloutProofManifestPath { get; init; } = string.Empty;

    [JsonPropertyName("rollout_proof_receipt_path")]
    public string RolloutProofReceiptPath { get; init; } = string.Empty;

    [JsonPropertyName("rollout_admission_receipt_path")]
    public string RolloutAdmissionReceiptPath { get; init; } = string.Empty;

    [JsonPropertyName("supervision_dataset_path")]
    public string SupervisionDatasetPath { get; init; } = string.Empty;
}

public sealed class AcquisitionRouteSupportingTransitionTerminalCoverageRow
{
    [JsonPropertyName("support_transition_kind")]
    public string SupportTransitionKind { get; init; } = string.Empty;

    [JsonPropertyName("support_request_id")]
    public string SupportRequestId { get; init; } = string.Empty;

    [JsonPropertyName("support_route_occurrence_id")]
    public string SupportRouteOccurrenceId { get; init; } = string.Empty;

    [JsonPropertyName("support_run_id")]
    public string SupportRunId { get; init; } = string.Empty;

    [JsonPropertyName("terminal_route_occurrence_ids")]
    public string[] TerminalRouteOccurrenceIds { get; init; } =
        Array.Empty<string>();

    [JsonPropertyName("terminal_run_ids")]
    public string[] TerminalRunIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; init; } = string.Empty;

    [JsonPropertyName("support_chain_recomputed")]
    public bool SupportChainRecomputed { get; init; }

    [JsonPropertyName("terminal_rollout_recomputed")]
    public bool TerminalRolloutRecomputed { get; init; }

    [JsonPropertyName("supervision_recomputed")]
    public bool SupervisionRecomputed { get; init; }

    [JsonPropertyName("support_excluded_from_terminal_outcomes")]
    public bool SupportExcludedFromTerminalOutcomes { get; init; }

    [JsonPropertyName("coverage_verified")]
    public bool CoverageVerified { get; init; }
}

public sealed class AcquisitionRouteSupportingTransitionTerminalCoverageReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } =
        "acquisition_route_supporting_transition_terminal_coverage.v1";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "blocked";

    [JsonPropertyName("required_support_transition_kinds")]
    public string[] RequiredSupportTransitionKinds { get; init; } =
        Array.Empty<string>();

    [JsonPropertyName("covered_support_transition_kinds")]
    public string[] CoveredSupportTransitionKinds { get; init; } =
        Array.Empty<string>();

    [JsonPropertyName("missing_support_transition_kinds")]
    public string[] MissingSupportTransitionKinds { get; init; } =
        Array.Empty<string>();

    [JsonPropertyName("rows")]
    public AcquisitionRouteSupportingTransitionTerminalCoverageRow[] Rows
    { get; init; } = Array.Empty<
        AcquisitionRouteSupportingTransitionTerminalCoverageRow>();

    [JsonPropertyName("terminal_lineage_coverage_complete")]
    public bool TerminalLineageCoverageComplete { get; init; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; init; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; init; } = Array.Empty<string>();

    [JsonPropertyName("coverage_policy")]
    public string CoveragePolicy { get; init; } =
        "Coverage is derived from controller-recomputed file-backed support, terminal rollout and supervision artifacts. Callers cannot declare a support kind. Every canonical support family must have exactly one verified row, and its support execution run must be absent from terminal native outcomes even when both legitimately share one high-level route occurrence. Complete coverage is a regression gate only and never authorizes formal product training.";
}
