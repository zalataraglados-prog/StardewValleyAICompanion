using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateFestivalReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_festival_state.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_unlock_sha256")]
    public string TargetDateUnlockSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("calendar_condition_axis_resolved_count")]
    public int CalendarConditionAxisResolvedCount { get; set; }

    [JsonPropertyName("calendar_condition_match_count")]
    public int CalendarConditionMatchCount { get; set; }

    [JsonPropertyName("calendar_condition_miss_count")]
    public int CalendarConditionMissCount { get; set; }

    [JsonPropertyName("not_applicable_static_window_count")]
    public int NotApplicableStaticWindowCount { get; set; }

    [JsonPropertyName("not_applicable_unlock_state_count")]
    public int NotApplicableUnlockStateCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_calendar_evidence_count")]
    public int BlockedCalendarEvidenceCount { get; set; }

    [JsonPropertyName("pending_stochastic_condition_count")]
    public int PendingStochasticConditionCount { get; set; }

    [JsonPropertyName("pending_resource_condition_count")]
    public int PendingResourceConditionCount { get; set; }

    [JsonPropertyName("pending_location_condition_count")]
    public int PendingLocationConditionCount { get; set; }

    [JsonPropertyName("unsupported_condition_count")]
    public int UnsupportedConditionCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("calendar_condition_axis_resolution_complete")]
    public bool CalendarConditionAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateFestival[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateFestival>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "DAYS_PLAYED, IS_FESTIVAL_DAY and IS_PASSIVE_FESTIVAL_OPEN are evaluated only from the exact same-day vanilla 1.6.15 calendar snapshot. Location-scoped festival forms remain blocked until the festival-location catalog and any Here/Target source context are carried. Each row embeds its complete typed upstream unlock route. Upstream blocks, missing calendar evidence, invalid syntax and unsupported contexts fail closed. This report cannot authorize a training label.";
}

public sealed record AcquisitionRouteTargetDateFestival(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateUnlock UpstreamRoute,
    [property: JsonPropertyName("calendar_condition_axis_status")]
    string CalendarConditionAxisStatus,
    [property: JsonPropertyName("calendar_condition_axis_resolved")]
    bool CalendarConditionAxisResolved,
    [property: JsonPropertyName("calendar_conditions_match_target_date")]
    bool? CalendarConditionsMatchTargetDate,
    [property: JsonPropertyName("calendar_condition_evaluations")]
    AcquisitionCalendarConditionEvaluation[] CalendarConditionEvaluations,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionCalendarConditionEvaluation(
    [property: JsonPropertyName("condition")] string Condition,
    [property: JsonPropertyName("predicate_name")] string PredicateName,
    [property: JsonPropertyName("negated")] bool Negated,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("native_result")] bool? NativeResult,
    [property: JsonPropertyName("condition_matches")] bool? ConditionMatches,
    [property: JsonPropertyName("evidence_paths")] string[] EvidencePaths,
    [property: JsonPropertyName("blocking_reason")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BlockingReason);
