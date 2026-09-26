using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteSupportingTransitionCommitReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_supporting_transition_commit_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("support_request_id")]
    public string SupportRequestId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("base_ledger_revision")]
    public int BaseLedgerRevision { get; set; }

    [JsonPropertyName("committed_ledger_revision")]
    public int CommittedLedgerRevision { get; set; }

    [JsonPropertyName("support_request_sha256")]
    public string SupportRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("base_ledger_sha256")]
    public string BaseLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("committed_ledger_sha256")]
    public string CommittedLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("commit_result_sha256")]
    public string CommitResultSha256 { get; set; } = string.Empty;

    [JsonPropertyName("reservation_claim_ids")]
    public string[] ReservationClaimIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("exact_active_claim_set_verified")]
    public bool ExactActiveClaimSetVerified { get; set; }

    [JsonPropertyName("single_revision_commit_verified")]
    public bool SingleRevisionCommitVerified { get; set; }

    [JsonPropertyName("support_reservation_commit_verified")]
    public bool SupportReservationCommitVerified { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The support commit receipt deterministically rebuilds the deadline-aware request, requires one accepted shared reservation-portfolio transaction, replays that transaction from the exact base ledger, and verifies every expected crop-seed claim as an exact active row in the committed ledger. A verified commit grants reservation ownership only. Supporting-transition compilation, execution, after-state verification, consumed-claim settlement and fresh replanning remain separate gates, so formal training authorization stays false.";
}
