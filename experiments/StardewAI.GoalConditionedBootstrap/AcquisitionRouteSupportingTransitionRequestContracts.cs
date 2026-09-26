using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteSupportingTransitionRequestInputs
{
    public string RequirementInventoryPath { get; init; } = string.Empty;
    public string AcquisitionLoweringPath { get; init; } = string.Empty;
    public string MasterAnglerWindowsPath { get; init; } = string.Empty;
    public string CalendarResolutionPath { get; init; } = string.Empty;
    public string TargetDateCalendarPath { get; init; } = string.Empty;
    public string TargetDateUnlockPath { get; init; } = string.Empty;
    public string TargetDateFestivalPath { get; init; } = string.Empty;
    public string TargetDateLocationPath { get; init; } = string.Empty;
    public string TargetDateFacilityPath { get; init; } = string.Empty;
    public string TargetDateResourcePath { get; init; } = string.Empty;
    public string TargetDateCurrencyPath { get; init; } = string.Empty;
    public string TargetDateReservationPath { get; init; } = string.Empty;
    public string TargetDateProcessingPath { get; init; } = string.Empty;
    public string StrategyLedgerPath { get; init; } = string.Empty;
    public string SnapshotPath { get; init; } = string.Empty;
    public string RouteTimingCalibrationPath { get; init; } = string.Empty;
    public string RankingPath { get; init; } = string.Empty;
    public string RouteOccurrenceId { get; init; } = string.Empty;
    public int SupportDeadlineTotalDay { get; init; }
}

public sealed class AcquisitionRouteSupportingTransitionRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_supporting_transition_request.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("support_request_id")]
    public string SupportRequestId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("route_kind")]
    public string RouteKind { get; set; } = string.Empty;

    [JsonPropertyName("source_id")]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("current_total_day")]
    public int? CurrentTotalDay { get; set; }

    [JsonPropertyName("support_deadline_total_day")]
    public int SupportDeadlineTotalDay { get; set; }

    [JsonPropertyName("expected_ready_total_day")]
    public int? ExpectedReadyTotalDay { get; set; }

    [JsonPropertyName("adjusted_grow_days")]
    public int? AdjustedGrowDays { get; set; }

    [JsonPropertyName("days_remaining_in_season")]
    public int? DaysRemainingInSeason { get; set; }

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int? TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int? TargetTileY { get; set; }

    [JsonPropertyName("seed_id")]
    public string SeedId { get; set; } = string.Empty;

    [JsonPropertyName("seed_slot_index")]
    public int? SeedSlotIndex { get; set; }

    [JsonPropertyName("base_ledger_revision")]
    public int BaseLedgerRevision { get; set; }

    [JsonPropertyName("target_date_processing_sha256")]
    public string TargetDateProcessingSha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("base_ledger_sha256")]
    public string BaseLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("ranking_sha256")]
    public string RankingSha256 { get; set; } = string.Empty;

    [JsonPropertyName("reservation_claim_ids")]
    public string[] ReservationClaimIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("reservation_material_claims")]
    public MaterialReservationUpsertRequest[] ReservationMaterialClaims
    { get; set; } = Array.Empty<MaterialReservationUpsertRequest>();

    [JsonPropertyName("reservation_currency_claims")]
    public CurrencyReservationUpsertRequest[] ReservationCurrencyClaims
    { get; set; } = Array.Empty<CurrencyReservationUpsertRequest>();

    [JsonPropertyName("deadline_proof_verified")]
    public bool DeadlineProofVerified { get; set; }

    [JsonPropertyName("reservation_claim_bound_to_candidate")]
    public bool ReservationClaimBoundToCandidate { get; set; }

    [JsonPropertyName("atomic_commit_preflight_passed")]
    public bool AtomicCommitPreflightPassed { get; set; }

    [JsonPropertyName("atomic_commit_request")]
    public ReservationPortfolioCommitRequest? AtomicCommitRequest { get; set; }

    [JsonPropertyName("support_request_ready")]
    public bool SupportRequestReady { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A support request may select only a current source-bound planting candidate for an authoritative crop route that passed calendar, unlock, location, capacity, seed-resource and reservation axes but is not yet harvest-ready. Its transparent adjusted growth duration must fit both the live remaining season and an explicit future deadline. The candidate seed slot must be covered by the route's exact material claim. The request is preflighted through the shared reservation-portfolio ledger service and must be atomically committed before compilation. This artifact neither mutates the ledger nor authorizes execution or training.";
}
