using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateProcessingReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_processing_lead_time.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_inventory_reservation_sha256")]
    public string TargetDateInventoryReservationSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("static_calendar_resolution_sha256")]
    public string StaticCalendarResolutionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("processing_lead_time_axis_resolved_count")]
    public int ProcessingLeadTimeAxisResolvedCount { get; set; }

    [JsonPropertyName("processing_lead_time_match_count")]
    public int ProcessingLeadTimeMatchCount { get; set; }

    [JsonPropertyName("processing_lead_time_miss_count")]
    public int ProcessingLeadTimeMissCount { get; set; }

    [JsonPropertyName("processing_lead_time_not_required_count")]
    public int ProcessingLeadTimeNotRequiredCount { get; set; }

    [JsonPropertyName("not_applicable_upstream_count")]
    public int NotApplicableUpstreamCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_processing_evidence_count")]
    public int BlockedProcessingEvidenceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("processing_lead_time_axis_resolution_complete")]
    public bool ProcessingLeadTimeAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateProcessing[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateProcessing>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The processing_lead_time axis runs only after an exact inventory-reservation match. Immediate interactions carry zero deterministic production delay; action duration remains owned by daily_time_energy_budget and random attempts remain owned by stochastic_retry_budget. Crop routes bind authoritative native growth data and exact live per-tile crop state. A new planting cannot satisfy the same target day; an existing crop must be harvest-ready on that date. Crab pots distinguish a ready matching output from a next-morning production attempt. Production route kinds without a locked evaluator fail closed instead of defaulting to zero time. This report neither selects routes nor mutates reservations, and training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateProcessing(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateReservation UpstreamRoute,
    [property: JsonPropertyName("processing_lead_time_axis_status")]
    string ProcessingLeadTimeAxisStatus,
    [property: JsonPropertyName("processing_lead_time_axis_resolved")]
    bool ProcessingLeadTimeAxisResolved,
    [property: JsonPropertyName("processing_lead_time_matches_target_date")]
    bool? ProcessingLeadTimeMatchesTargetDate,
    [property: JsonPropertyName("processing_lead_time_requirement_kind")]
    string ProcessingLeadTimeRequirementKind,
    [property: JsonPropertyName("evaluations")]
    AcquisitionProcessingLeadTimeEvaluation[] Evaluations,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionProcessingLeadTimeEvaluation(
    [property: JsonPropertyName("target_location_id")]
    string TargetLocationId,
    [property: JsonPropertyName("production_state_kind")]
    string ProductionStateKind,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("lead_time_bound_kind")]
    string LeadTimeBoundKind,
    [property: JsonPropertyName("authoritative_base_growth_days")]
    int? AuthoritativeBaseGrowthDays,
    [property: JsonPropertyName("proven_lead_time_days_lower_bound")]
    int? ProvenLeadTimeDaysLowerBound,
    [property: JsonPropertyName("proven_not_before_total_day")]
    int? ProvenNotBeforeTotalDay,
    [property: JsonPropertyName("output_ready_on_target_date")]
    bool? OutputReadyOnTargetDate,
    [property: JsonPropertyName("evidence_paths")]
    string[] EvidencePaths,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);
