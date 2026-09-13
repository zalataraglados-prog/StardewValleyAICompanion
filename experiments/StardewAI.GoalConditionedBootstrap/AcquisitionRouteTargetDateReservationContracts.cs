using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateReservationReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_inventory_reservation.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_currency_sha256")]
    public string TargetDateCurrencySha256 { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_id")]
    public string StrategyLedgerId { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_revision")]
    public int StrategyLedgerRevision { get; set; }

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("reservation_axis_resolved_count")]
    public int ReservationAxisResolvedCount { get; set; }

    [JsonPropertyName("reservation_match_count")]
    public int ReservationMatchCount { get; set; }

    [JsonPropertyName("reservation_conflict_count")]
    public int ReservationConflictCount { get; set; }

    [JsonPropertyName("reservation_not_required_count")]
    public int ReservationNotRequiredCount { get; set; }

    [JsonPropertyName("claim_proposed_count")]
    public int ClaimProposedCount { get; set; }

    [JsonPropertyName("claim_committed_count")]
    public int ClaimCommittedCount { get; set; }

    [JsonPropertyName("claim_replacement_count")]
    public int ClaimReplacementCount { get; set; }

    [JsonPropertyName("material_claim_count")]
    public int MaterialClaimCount { get; set; }

    [JsonPropertyName("currency_claim_count")]
    public int CurrencyClaimCount { get; set; }

    [JsonPropertyName("not_applicable_upstream_count")]
    public int NotApplicableUpstreamCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_reservation_evidence_count")]
    public int BlockedReservationEvidenceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("reservation_axis_resolution_complete")]
    public bool ReservationAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateReservation[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateReservation>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The inventory_reservation axis runs only after exact resource-input and currency-budget matches. Each route is evaluated independently against current actor-authorized material slots and native currency balances after subtracting every other active controller reservation. It emits a deterministic atomic claim set but does not select alternatives or mutate the ledger. A controller must select one route and commit the complete set at one expected ledger revision before execution. Missing, malformed, stale, wrong-owner or overbooked ledger state fails closed. Attached tool bait and other consumables without an exact ledger address remain blocked. Processing, retries, daily time/energy, opportunity cost and terminal receipts remain downstream. Training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateReservation(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateCurrency UpstreamRoute,
    [property: JsonPropertyName("reservation_axis_status")]
    string ReservationAxisStatus,
    [property: JsonPropertyName("reservation_axis_resolved")]
    bool ReservationAxisResolved,
    [property: JsonPropertyName("inventory_reservation_matches_target_date")]
    bool? InventoryReservationMatchesTargetDate,
    [property: JsonPropertyName("claim_disposition")]
    string ClaimDisposition,
    [property: JsonPropertyName("claim_set")]
    AcquisitionRouteReservationClaimSet? ClaimSet,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionRouteReservationClaimSet(
    [property: JsonPropertyName("source_decision_id")]
    string SourceDecisionId,
    [property: JsonPropertyName("source_state_hash")]
    string SourceStateHash,
    [property: JsonPropertyName("expected_ledger_revision")]
    int ExpectedLedgerRevision,
    [property: JsonPropertyName("atomic_commit_required")]
    bool AtomicCommitRequired,
    [property: JsonPropertyName("replacement_required")]
    bool ReplacementRequired,
    [property: JsonPropertyName("existing_active_reservation_ids")]
    string[] ExistingActiveReservationIds,
    [property: JsonPropertyName("material_claims")]
    MaterialReservationUpsertRequest[] MaterialClaims,
    [property: JsonPropertyName("currency_claims")]
    CurrencyReservationUpsertRequest[] CurrencyClaims);
