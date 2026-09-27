using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
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

    [JsonPropertyName("support_transition_kind")]
    public string SupportTransitionKind { get; set; } = string.Empty;

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

    [JsonPropertyName("input_qualified_item_id")]
    public string InputQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("input_slot_index")]
    public int? InputSlotIndex { get; set; }

    [JsonPropertyName("input_required_quantity")]
    public int? InputRequiredQuantity { get; set; }

    [JsonPropertyName("machine_qualified_item_id")]
    public string MachineQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("predicted_processing_minutes")]
    public int? PredictedProcessingMinutes { get; set; }

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

    [JsonPropertyName("support_material_consumptions")]
    public AcquisitionSupportMaterialConsumption[] SupportMaterialConsumptions
    { get; set; } = Array.Empty<AcquisitionSupportMaterialConsumption>();

    [JsonPropertyName("support_material_relocations")]
    public AcquisitionSupportMaterialRelocation[] SupportMaterialRelocations
    { get; set; } = Array.Empty<AcquisitionSupportMaterialRelocation>();

    [JsonPropertyName("material_transfer_intent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaterialTransferIntent? MaterialTransferIntent { get; set; }

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
        "A support request may select only one current source-bound candidate from the shared acquisition route. Crop planting retains exact growth, season, seed-slot and deadline proof. Machine input loading additionally requires a unique native output route, exact machine location/tile/type, probe-derived required input count and effective processing minutes matching the authoritative processing schedule; its exact input slot must be covered by the route material claims and its completion day must fit the explicit deadline. A machine claim in a current-location ordinary unlocked chest cannot compile as a load: it first emits one deterministic inventory.transfer_item support transition whose native projection fits exactly one player-inventory slot and whose relocation preserves the full active claim. All route claims are preflighted through the shared reservation-portfolio ledger service and must be atomically committed before compilation. This artifact neither mutates the ledger nor authorizes execution or training.";
}

public sealed class AcquisitionSupportMaterialRelocation
{
    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("source_node_id")]
    public string SourceNodeId { get; set; } = string.Empty;

    [JsonPropertyName("source_slot_index")]
    public int SourceSlotIndex { get; set; }

    [JsonPropertyName("destination_node_id")]
    public string DestinationNodeId { get; set; } = string.Empty;

    [JsonPropertyName("destination_slot_index")]
    public int DestinationSlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public sealed class AcquisitionSupportMaterialConsumption
{
    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("consumed_quantity")]
    public int ConsumedQuantity { get; set; }

    [JsonPropertyName("input_role")]
    public string InputRole { get; set; } = string.Empty;
}
