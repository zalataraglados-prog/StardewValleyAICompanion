using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateCalendarReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_calendar.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("static_calendar_resolution_sha256")]
    public string StaticCalendarResolutionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("deadline_total_day_exclusive")]
    public int DeadlineTotalDayExclusive { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("calendar_axis_resolved_count")]
    public int CalendarAxisResolvedCount { get; set; }

    [JsonPropertyName("static_window_match_count")]
    public int StaticWindowMatchCount { get; set; }

    [JsonPropertyName("static_window_miss_count")]
    public int StaticWindowMissCount { get; set; }

    [JsonPropertyName("blocked_static_source_count")]
    public int BlockedStaticSourceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("calendar_axis_resolution_complete")]
    public bool CalendarAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateCalendar[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateCalendar>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A target-date match resolves only the calendar_window axis. Matching time/weather ranges and exact dynamic predicates remain attached; unlock, route, capacity, resources, currency, reservations, lead time, retries, daily budget, opportunity cost and fresh receipt remain independent. This report cannot authorize a training label.";
}

public sealed record AcquisitionRouteTargetDateCalendar(
    [property: JsonPropertyName("route_occurrence_id")] string RouteOccurrenceId,
    [property: JsonPropertyName("requirement_set_id")] string RequirementSetId,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("alternative_index")] int AlternativeIndex,
    [property: JsonPropertyName("route_index")] int RouteIndex,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("match_kind")] string MatchKind,
    [property: JsonPropertyName("required_amount")] int RequiredAmount,
    [property: JsonPropertyName("minimum_quality")] int MinimumQuality,
    [property: JsonPropertyName("route_kind")] string RouteKind,
    [property: JsonPropertyName("uncertainty_mode")] string UncertaintyMode,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_resolution_status")] string SourceResolutionStatus,
    [property: JsonPropertyName("calendar_axis_status")] string CalendarAxisStatus,
    [property: JsonPropertyName("calendar_axis_resolved")] bool CalendarAxisResolved,
    [property: JsonPropertyName("static_window_matches_target_date")]
    bool StaticWindowMatchesTargetDate,
    [property: JsonPropertyName("matching_windows")] AuthoritativeCalendarSourceWindow[] MatchingWindows,
    [property: JsonPropertyName("pending_dynamic_conditions")] string[] PendingDynamicConditions,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);
