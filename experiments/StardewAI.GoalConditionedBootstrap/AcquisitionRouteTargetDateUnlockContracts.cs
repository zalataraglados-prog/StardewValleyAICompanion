using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateUnlockReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_unlock_state.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_calendar_sha256")]
    public string TargetDateCalendarSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("unlock_axis_resolved_count")]
    public int UnlockAxisResolvedCount { get; set; }

    [JsonPropertyName("unlock_state_match_count")]
    public int UnlockStateMatchCount { get; set; }

    [JsonPropertyName("unlock_state_miss_count")]
    public int UnlockStateMissCount { get; set; }

    [JsonPropertyName("static_window_miss_count")]
    public int StaticWindowMissCount { get; set; }

    [JsonPropertyName("blocked_upstream_calendar_count")]
    public int BlockedUpstreamCalendarCount { get; set; }

    [JsonPropertyName("blocked_unlock_evidence_count")]
    public int BlockedUnlockEvidenceCount { get; set; }

    [JsonPropertyName("pending_calendar_condition_count")]
    public int PendingCalendarConditionCount { get; set; }

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

    [JsonPropertyName("unlock_axis_resolution_complete")]
    public bool UnlockAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateUnlock[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateUnlock>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "Unlock predicates are evaluated only from a same-day transparent snapshot and native 1.6.15 semantics. Calendar/festival, stochastic, inventory/resource and location predicates remain owned by their downstream axes. Unknown syntax, unsupported Target-player context, missing player identities and unavailable bridge state fail closed. This report cannot authorize a training label.";
}

public sealed record AcquisitionRouteTargetDateUnlock(
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
    [property: JsonPropertyName("source_resolution_status")]
    string SourceResolutionStatus,
    [property: JsonPropertyName("calendar_axis_status")] string CalendarAxisStatus,
    [property: JsonPropertyName("static_window_matches_target_date")]
    bool StaticWindowMatchesTargetDate,
    [property: JsonPropertyName("matching_windows")]
    AuthoritativeCalendarSourceWindow[] MatchingWindows,
    [property: JsonPropertyName("unlock_axis_status")] string UnlockAxisStatus,
    [property: JsonPropertyName("unlock_axis_resolved")] bool UnlockAxisResolved,
    [property: JsonPropertyName("unlock_state_matches_target_date")]
    bool? UnlockStateMatchesTargetDate,
    [property: JsonPropertyName("unlock_conditions")]
    AcquisitionUnlockConditionEvaluation[] UnlockConditions,
    [property: JsonPropertyName("pending_calendar_conditions")]
    string[] PendingCalendarConditions,
    [property: JsonPropertyName("pending_stochastic_conditions")]
    string[] PendingStochasticConditions,
    [property: JsonPropertyName("pending_resource_conditions")]
    string[] PendingResourceConditions,
    [property: JsonPropertyName("pending_location_conditions")]
    string[] PendingLocationConditions,
    [property: JsonPropertyName("unsupported_conditions")]
    string[] UnsupportedConditions,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);

public sealed record AcquisitionUnlockConditionEvaluation(
    [property: JsonPropertyName("condition")] string Condition,
    [property: JsonPropertyName("predicate_name")] string PredicateName,
    [property: JsonPropertyName("negated")] bool Negated,
    [property: JsonPropertyName("player_selector")] string PlayerSelector,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("native_result")] bool? NativeResult,
    [property: JsonPropertyName("condition_matches")] bool? ConditionMatches,
    [property: JsonPropertyName("evidence_paths")] string[] EvidencePaths,
    [property: JsonPropertyName("blocking_reason")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BlockingReason);
