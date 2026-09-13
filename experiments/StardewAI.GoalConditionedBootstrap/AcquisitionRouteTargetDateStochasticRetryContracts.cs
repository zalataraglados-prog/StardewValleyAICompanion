using System.Text.Json.Serialization;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateStochasticRetryReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_stochastic_retry_budget.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_processing_lead_time_sha256")]
    public string TargetDateProcessingLeadTimeSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("target_date_fishing_probability_sha256")]
    public string TargetDateFishingProbabilitySha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("fishing_forecast_manifest_sha256")]
    public string FishingForecastManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("static_calendar_resolution_sha256")]
    public string StaticCalendarResolutionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("target_success_probability")]
    public double TargetSuccessProbability { get; set; } =
        StochasticRetryPolicy.TargetSuccessProbability;

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("stochastic_retry_axis_resolved_count")]
    public int StochasticRetryAxisResolvedCount { get; set; }

    [JsonPropertyName("stochastic_retry_match_count")]
    public int StochasticRetryMatchCount { get; set; }

    [JsonPropertyName("stochastic_retry_not_required_count")]
    public int StochasticRetryNotRequiredCount { get; set; }

    [JsonPropertyName("materialized_output_count")]
    public int MaterializedOutputCount { get; set; }

    [JsonPropertyName("not_applicable_upstream_count")]
    public int NotApplicableUpstreamCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_probability_evidence_count")]
    public int BlockedProbabilityEvidenceCount { get; set; }

    [JsonPropertyName("blocked_retry_reservation_count")]
    public int BlockedRetryReservationCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("stochastic_retry_axis_resolution_complete")]
    public bool StochasticRetryAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateStochasticRetry[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateStochasticRetry>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The stochastic_retry_budget axis runs only after an exact processing-lead-time match. Deterministic receipts and source-resolved guaranteed outputs require no stochastic retry. An already materialized live output may also be collected without retry. Native stochastic routes require exact target-location, target-terminal probability evidence; unknown probability, outcome quantity, quality, independence, or condition context fails closed. A no-bait fishing projection may use the exact independent binomial lower-bound budget only when every selected-prefix rule proves stable repeated-cast context and independent native RNG. A retry budget that increases consumables cannot authorize execution until its expanded demand is revalidated through resource, currency and atomic reservation ownership. Daily time and energy remain downstream, route selection is unchanged, and training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateStochasticRetry(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateProcessing UpstreamRoute,
    [property: JsonPropertyName("uncertainty_mode")]
    string UncertaintyMode,
    [property: JsonPropertyName("stochastic_retry_axis_status")]
    string StochasticRetryAxisStatus,
    [property: JsonPropertyName("stochastic_retry_axis_resolved")]
    bool StochasticRetryAxisResolved,
    [property: JsonPropertyName("stochastic_retry_budget_matches_target_date")]
    bool? StochasticRetryBudgetMatchesTargetDate,
    [property: JsonPropertyName("retry_budget_kind")]
    string RetryBudgetKind,
    [property: JsonPropertyName("required_output_quantity")]
    int RequiredOutputQuantity,
    [property: JsonPropertyName("minimum_output_quality")]
    int MinimumOutputQuality,
    [property: JsonPropertyName("target_success_probability")]
    double TargetSuccessProbability,
    [property: JsonPropertyName("single_attempt_success_probability")]
    double? SingleAttemptSuccessProbability,
    [property: JsonPropertyName("required_attempt_count")]
    int? RequiredAttemptCount,
    [property: JsonPropertyName("additional_retry_count")]
    int? AdditionalRetryCount,
    [property: JsonPropertyName("retry_expands_reserved_consumables")]
    bool RetryExpandsReservedConsumables,
    [property: JsonPropertyName("retry_expanded_reservation_revalidated")]
    bool RetryExpandedReservationRevalidated,
    [property: JsonPropertyName("evidence_paths")]
    string[] EvidencePaths,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);
