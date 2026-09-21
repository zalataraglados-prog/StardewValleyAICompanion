using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteExecutionBindingInputs
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
    public string TargetDateFishingProbabilityPath { get; init; } = string.Empty;
    public string TargetDateStochasticRetryPath { get; init; } = string.Empty;
    public string TargetDateDailyTimeEnergyPath { get; init; } = string.Empty;
    public string TargetDateOpportunityCostPath { get; init; } = string.Empty;
    public string FishingForecastManifestPath { get; init; } = string.Empty;
    public string StrategyLedgerPath { get; init; } = string.Empty;
    public string BeforeSnapshotPath { get; init; } = string.Empty;
    public string RouteTimingCalibrationPath { get; init; } = string.Empty;
    public string PortfolioProposalPath { get; init; } = string.Empty;
    public string PortfolioAdmissionPath { get; init; } = string.Empty;
    public string PortfolioPreferenceRequestPath { get; init; } = string.Empty;
    public string PortfolioTeacherPreferencePath { get; init; } = string.Empty;
    public string PortfolioCommitReceiptPath { get; init; } = string.Empty;
    public string CommittedStrategyLedgerPath { get; init; } = string.Empty;
    public string PortfolioCommitResultPath { get; init; } = string.Empty;
    public string ActionQueuePath { get; init; } = string.Empty;
    public string RouteOccurrenceId { get; init; } = string.Empty;
}

public sealed class AcquisitionRouteExecutionBinding
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_execution_binding.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("requirement_set_id")]
    public string RequirementSetId { get; set; } = string.Empty;

    [JsonPropertyName("requirement_id")]
    public string RequirementId { get; set; } = string.Empty;

    [JsonPropertyName("alternative_index")]
    public int AlternativeIndex { get; set; }

    [JsonPropertyName("route_index")]
    public int RouteIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("match_kind")]
    public string MatchKind { get; set; } = string.Empty;

    [JsonPropertyName("required_amount")]
    public int RequiredAmount { get; set; }

    [JsonPropertyName("minimum_quality")]
    public int MinimumQuality { get; set; }

    [JsonPropertyName("route_kind")]
    public string RouteKind { get; set; } = string.Empty;

    [JsonPropertyName("source_id")]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("opportunity_cost_sha256")]
    public string OpportunityCostSha256 { get; set; } = string.Empty;

    [JsonPropertyName("portfolio_commit_receipt_sha256")]
    public string PortfolioCommitReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("portfolio_teacher_preference_sha256")]
    public string PortfolioTeacherPreferenceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("reservation_portfolio_id")]
    public string ReservationPortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("committed_strategy_ledger_sha256")]
    public string CommittedStrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("committed_strategy_ledger_revision")]
    public int CommittedStrategyLedgerRevision { get; set; }

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_sha256")]
    public string BeforeSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("before_state_hash")]
    public string BeforeStateHash { get; set; } = string.Empty;

    [JsonPropertyName("action_queue_sha256")]
    public string ActionQueueSha256 { get; set; } = string.Empty;

    [JsonPropertyName("queue_id")]
    public string QueueId { get; set; } = string.Empty;

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("queue_item_ids")]
    public string[] QueueItemIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("primitive_option_ids")]
    public string[] PrimitiveOptionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("endpoint_option_ids")]
    public string[] EndpointOptionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("supporting_option_ids")]
    public string[] SupportingOptionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("terminal_receipt_kind")]
    public string TerminalReceiptKind { get; set; } = string.Empty;

    [JsonPropertyName("selected_from_complete_pareto_frontier")]
    public bool SelectedFromCompleteParetoFrontier { get; set; }

    [JsonPropertyName("queue_options_bound_to_route")]
    public bool QueueOptionsBoundToRoute { get; set; }

    [JsonPropertyName("portfolio_reservation_commit_verified")]
    public bool PortfolioReservationCommitVerified { get; set; }

    [JsonPropertyName("portfolio_teacher_preference_verified")]
    public bool PortfolioTeacherPreferenceVerified { get; set; }

    [JsonPropertyName("dispatch_binding_ready")]
    public bool DispatchBindingReady { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A target-date route may be dispatched only after an independent, complete-denominator Teacher preference selects its exact proposal/admission and a deterministically rebuilt receipt verifies the resulting reservation portfolio commit. The immutable pending action queue and every normalized command repeat the exact route/requirement/source/quantity/quality identity plus the committed portfolio ID and ledger revision. Every option belongs to the route's authoritative endpoint/support set, and the queue retains the same fresh source state. Item identity, a caller-provided proposal, candidate alias, uncommitted preflight or stale ledger may never infer preference, ownership or execution. This pre-dispatch binding predicts no terminal receipt and cannot authorize formal training.";
}
