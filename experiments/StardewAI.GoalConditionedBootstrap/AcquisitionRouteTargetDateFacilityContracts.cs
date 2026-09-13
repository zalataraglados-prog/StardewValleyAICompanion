using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateFacilityReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_facility_capacity.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_location_sha256")]
    public string TargetDateLocationSha256 { get; set; } = string.Empty;

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

    [JsonPropertyName("facility_capacity_axis_resolved_count")]
    public int FacilityCapacityAxisResolvedCount { get; set; }

    [JsonPropertyName("facility_capacity_match_count")]
    public int FacilityCapacityMatchCount { get; set; }

    [JsonPropertyName("facility_capacity_miss_count")]
    public int FacilityCapacityMissCount { get; set; }

    [JsonPropertyName("facility_capacity_not_required_count")]
    public int FacilityCapacityNotRequiredCount { get; set; }

    [JsonPropertyName("not_applicable_upstream_count")]
    public int NotApplicableUpstreamCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_facility_evidence_count")]
    public int BlockedFacilityEvidenceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("facility_capacity_axis_resolution_complete")]
    public bool FacilityCapacityAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateFacility[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateFacility>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The facility_capacity axis is evaluated only after an exact target-date source-location match. Routes without a capacity-bearing production facility are explicitly not-required. Crop routes bind authoritative minimum harvest stack and require enough exact existing-target-crop plus open prepared-soil slots across matched locations to cover the full route amount. Unresolved crop identities block only when they can change that conclusion. Crab-pot routes reuse the exact placed-pot source already proven by location_route. Capacity-bearing route kinds that cannot yet reach this axis remain inherited upstream blocks, and any such kind reaching it without a locked evaluator fails closed. Inputs, construction, growth or processing lead time, stochastic output, daily budget, inventory receipt and final interaction remain independently owned; training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateFacility(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateLocation UpstreamRoute,
    [property: JsonPropertyName("facility_capacity_axis_status")]
    string FacilityCapacityAxisStatus,
    [property: JsonPropertyName("facility_capacity_axis_resolved")]
    bool FacilityCapacityAxisResolved,
    [property: JsonPropertyName("facility_capacity_matches_target_date")]
    bool? FacilityCapacityMatchesTargetDate,
    [property: JsonPropertyName("facility_requirement_kind")]
    string FacilityRequirementKind,
    [property: JsonPropertyName("target_evaluations")]
    AcquisitionFacilityTargetEvaluation[] TargetEvaluations,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionFacilityTargetEvaluation(
    [property: JsonPropertyName("target_location_id")]
    string TargetLocationId,
    [property: JsonPropertyName("required_crop_slot_count")]
    int? RequiredCropSlotCount,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("total_prepared_soil_slot_count")]
    int? TotalPreparedSoilSlotCount,
    [property: JsonPropertyName("open_prepared_soil_slot_count")]
    int? OpenPreparedSoilSlotCount,
    [property: JsonPropertyName("matching_existing_crop_slot_count")]
    int? MatchingExistingCropSlotCount,
    [property: JsonPropertyName("unresolved_harvest_item_slot_count")]
    int? UnresolvedHarvestItemSlotCount,
    [property: JsonPropertyName("evidence_paths")]
    string[] EvidencePaths,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);
