using System.Text.Json.Serialization;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateLocationReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_location_route.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_festival_sha256")]
    public string TargetDateFestivalSha256 { get; set; } = string.Empty;

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

    [JsonPropertyName("location_route_axis_resolved_count")]
    public int LocationRouteAxisResolvedCount { get; set; }

    [JsonPropertyName("location_route_match_count")]
    public int LocationRouteMatchCount { get; set; }

    [JsonPropertyName("location_route_miss_count")]
    public int LocationRouteMissCount { get; set; }

    [JsonPropertyName("not_applicable_static_window_count")]
    public int NotApplicableStaticWindowCount { get; set; }

    [JsonPropertyName("not_applicable_unlock_state_count")]
    public int NotApplicableUnlockStateCount { get; set; }

    [JsonPropertyName("not_applicable_calendar_condition_count")]
    public int NotApplicableCalendarConditionCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_location_evidence_count")]
    public int BlockedLocationEvidenceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("location_route_axis_resolution_complete")]
    public bool LocationRouteAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateLocation[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateLocation>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The location_route axis binds each currently applicable acquisition source to exact same-snapshot locations and reuses the existing current-date connector search plus versioned conservative movement calibration. Source weather is evaluated in the target location context and arrival must precede a retained source time-window end. Crop season-independent locations, farm variants, live shop endpoints and placed crab-pot locations are never guessed. Unconsumed native location predicates fail closed. This axis proves source-location arrival only; random/live source appearance, terminal interaction, stock, resources and a fresh native receipt remain downstream and training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateLocation(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateFestival UpstreamRoute,
    [property: JsonPropertyName("location_route_axis_status")]
    string LocationRouteAxisStatus,
    [property: JsonPropertyName("location_route_axis_resolved")]
    bool LocationRouteAxisResolved,
    [property: JsonPropertyName("location_route_matches_target_date")]
    bool? LocationRouteMatchesTargetDate,
    [property: JsonPropertyName("target_evaluations")]
    AcquisitionLocationRouteTargetEvaluation[] TargetEvaluations,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionLocationRouteTargetEvaluation(
    [property: JsonPropertyName("binding_kind")]
    string BindingKind,
    [property: JsonPropertyName("source_key")]
    string SourceKey,
    [property: JsonPropertyName("target_location_id")]
    string TargetLocationId,
    [property: JsonPropertyName("location_context_id")]
    string LocationContextId,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("source_weather_mode")]
    string SourceWeatherMode,
    [property: JsonPropertyName("guaranteed_arrival_by_time")]
    int? GuaranteedArrivalByTime,
    [property: JsonPropertyName("matched_window_count")]
    int MatchedWindowCount,
    [property: JsonPropertyName("timing_evidence_kind")]
    string TimingEvidenceKind,
    [property: JsonPropertyName("timing_evidence_id")]
    string TimingEvidenceId,
    [property: JsonPropertyName("path")]
    TransparentRouteEdge[] Path,
    [property: JsonPropertyName("evidence_paths")]
    string[] EvidencePaths,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);
