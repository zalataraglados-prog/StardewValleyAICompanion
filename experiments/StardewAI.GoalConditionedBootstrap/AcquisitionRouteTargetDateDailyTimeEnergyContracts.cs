using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateDailyTimeEnergyReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_daily_time_energy_budget.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_stochastic_retry_sha256")]
    public string TargetDateStochasticRetrySha256 { get; set; } = string.Empty;

    [JsonPropertyName("target_date_fishing_probability_sha256")]
    public string TargetDateFishingProbabilitySha256 { get; set; } = string.Empty;

    [JsonPropertyName("static_calendar_resolution_sha256")]
    public string StaticCalendarResolutionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("route_timing_calibration_sha256")]
    public string RouteTimingCalibrationSha256 { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("daily_time_energy_axis_resolved_count")]
    public int DailyTimeEnergyAxisResolvedCount { get; set; }

    [JsonPropertyName("daily_time_energy_match_count")]
    public int DailyTimeEnergyMatchCount { get; set; }

    [JsonPropertyName("daily_time_budget_miss_count")]
    public int DailyTimeBudgetMissCount { get; set; }

    [JsonPropertyName("daily_energy_budget_miss_count")]
    public int DailyEnergyBudgetMissCount { get; set; }

    [JsonPropertyName("not_applicable_upstream_count")]
    public int NotApplicableUpstreamCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_budget_evidence_count")]
    public int BlockedBudgetEvidenceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("daily_time_energy_axis_resolution_complete")]
    public bool DailyTimeEnergyAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateDailyTimeEnergy[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateDailyTimeEnergy>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The daily_time_energy_budget axis runs only after an exact stochastic-retry match. Every active terminal must be reached from the same snapshot through the locked route graph, date evidence and movement calibration, and its complete conservative action duration must fit one retained authoritative source window. Fishing routes use the selected legal stand tile, complete retry count, locked native cast/bite/perfect-lock/settlement bound and FishingRod stamina formula; Efficient credit is never guessed. Shop routes use an exact authoritative action tile, an adjacent reachable stand and the current rolling compiler's maximum menu wait plus interact/dialogue/purchase/close budget for every required purchase. Unsupported terminal kinds fail closed. This axis does not select a route, combine competing routes, reserve opportunity cost or prove a fresh terminal receipt, and training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateDailyTimeEnergy(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateStochasticRetry UpstreamRoute,
    [property: JsonPropertyName("daily_budget_kind")]
    string DailyBudgetKind,
    [property: JsonPropertyName("daily_time_energy_axis_status")]
    string DailyTimeEnergyAxisStatus,
    [property: JsonPropertyName("daily_time_energy_axis_resolved")]
    bool DailyTimeEnergyAxisResolved,
    [property: JsonPropertyName("daily_time_energy_matches_target_date")]
    bool? DailyTimeEnergyMatchesTargetDate,
    [property: JsonPropertyName("evaluation")]
    AcquisitionDailyTimeEnergyEvaluation? Evaluation,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionDailyTimeEnergyEvaluation(
    [property: JsonPropertyName("target_location_id")]
    string TargetLocationId,
    [property: JsonPropertyName("target_tile_x")]
    int? TargetTileX,
    [property: JsonPropertyName("target_tile_y")]
    int? TargetTileY,
    [property: JsonPropertyName("stand_tile_x")]
    int? StandTileX,
    [property: JsonPropertyName("stand_tile_y")]
    int? StandTileY,
    [property: JsonPropertyName("snapshot_start_time")]
    int SnapshotStartTime,
    [property: JsonPropertyName("guaranteed_arrival_by_time")]
    int? GuaranteedArrivalByTime,
    [property: JsonPropertyName("selected_window_start_time")]
    int? SelectedWindowStartTime,
    [property: JsonPropertyName("selected_window_end_time")]
    int? SelectedWindowEndTime,
    [property: JsonPropertyName("terminal_action_game_minutes")]
    int? TerminalActionGameMinutes,
    [property: JsonPropertyName("guaranteed_completion_by_time")]
    int? GuaranteedCompletionByTime,
    [property: JsonPropertyName("required_attempt_count")]
    int? RequiredAttemptCount,
    [property: JsonPropertyName("effective_fishing_level")]
    int? EffectiveFishingLevel,
    [property: JsonPropertyName("available_energy")]
    double? AvailableEnergy,
    [property: JsonPropertyName("energy_per_attempt")]
    double? EnergyPerAttempt,
    [property: JsonPropertyName("required_energy")]
    double? RequiredEnergy,
    [property: JsonPropertyName("minimum_energy_reserve")]
    double MinimumEnergyReserve,
    [property: JsonPropertyName("time_budget_matches")]
    bool? TimeBudgetMatches,
    [property: JsonPropertyName("energy_budget_matches")]
    bool? EnergyBudgetMatches,
    [property: JsonPropertyName("terminal_execution_assumption")]
    string TerminalExecutionAssumption,
    [property: JsonPropertyName("timing_evidence_id")]
    string TimingEvidenceId,
    [property: JsonPropertyName("evidence_paths")]
    string[] EvidencePaths);
