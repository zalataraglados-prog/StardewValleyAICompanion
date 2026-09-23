using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioCommitReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_commit_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("proposal_id")]
    public string ProposalId { get; set; } = string.Empty;

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("community_center_provenance")]
    public AcquisitionRouteCommunityCenterProvenance
        CommunityCenterProvenance { get; set; } = new();

    [JsonPropertyName("prior_rollout_checkpoint_sha256")]
    public string PriorRolloutCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("completed_alternatives")]
    public AcquisitionRoutePortfolioCompletedAlternatives[]
        CompletedAlternatives
    { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioCompletedAlternatives>();

    [JsonPropertyName("base_ledger_revision")]
    public int BaseLedgerRevision { get; set; }

    [JsonPropertyName("committed_ledger_revision")]
    public int CommittedLedgerRevision { get; set; }

    [JsonPropertyName("portfolio_admission_sha256")]
    public string PortfolioAdmissionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("base_ledger_sha256")]
    public string BaseLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("committed_ledger_sha256")]
    public string CommittedLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("commit_result_sha256")]
    public string CommitResultSha256 { get; set; } = string.Empty;

    [JsonPropertyName("selected_route_occurrence_ids")]
    public string[] SelectedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("active_reservation_ids")]
    public string[] ActiveReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("released_reservation_ids")]
    public string[] ReleasedReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("atomic_mutation_observed")]
    public bool AtomicMutationObserved { get; set; }

    [JsonPropertyName("exact_active_claim_set_verified")]
    public bool ExactActiveClaimSetVerified { get; set; }

    [JsonPropertyName("single_revision_commit_verified")]
    public bool SingleRevisionCommitVerified { get; set; }

    [JsonPropertyName("portfolio_commit_verified")]
    public bool PortfolioCommitVerified { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The receipt deterministically rebuilds the pre-commit route portfolio admission and verifies the exact committed ledger. Every admitted selection must match one accepted commit result, advance exactly one revision, preserve every requested claim as an exact active row, cancel every explicit release and record all component history plus one portfolio marker at the committed revision. A claimless selection has no component rows but still requires that unique marker. This proves reservation ownership only; route execution and fresh terminal outcomes remain separate, so formal training authorization stays false.";
}
