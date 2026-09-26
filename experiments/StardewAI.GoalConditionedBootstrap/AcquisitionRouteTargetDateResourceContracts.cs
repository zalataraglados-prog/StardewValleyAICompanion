using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateResourceReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_resource_inputs.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_facility_sha256")]
    public string TargetDateFacilitySha256 { get; set; } = string.Empty;

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

    [JsonPropertyName("resource_input_axis_resolved_count")]
    public int ResourceInputAxisResolvedCount { get; set; }

    [JsonPropertyName("resource_input_match_count")]
    public int ResourceInputMatchCount { get; set; }

    [JsonPropertyName("resource_input_miss_count")]
    public int ResourceInputMissCount { get; set; }

    [JsonPropertyName("resource_input_not_required_count")]
    public int ResourceInputNotRequiredCount { get; set; }

    [JsonPropertyName("not_applicable_upstream_count")]
    public int NotApplicableUpstreamCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_resource_evidence_count")]
    public int BlockedResourceEvidenceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("resource_input_axis_resolution_complete")]
    public bool ResourceInputAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateResource[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateResource>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The resource_inputs axis runs only after an exact facility-capacity match. It reads canonical actor-authorized immediately available material nodes, not a player-inventory-only approximation. Non-consuming PLAYER_HAS_ITEM conditions use only the exact current-player inventory node and preserve native negation and count ranges. Crop seed demand is the conservative remainder after guaranteed existing-crop output, using authoritative minimum harvest stack. Shop barter and mandatory Magic Bait quantities scale with the route requirement amount; money and other native currencies remain owned by currency_budget. Attached and loose Magic Bait can jointly satisfy the deterministic catch count. Existing crab-pot output, loaded bait or owner Luremaster satisfies current input service; an unserviced pot fails closed until its exact native-accepted bait candidate domain is bound. Machine item-placement routes bind one native trigger witness, exact item IDs or all required context tags, input edibility conditions, guaranteed-output attempt count, and every additional consumed item; automatic day/update/collection/placement triggers consume no load inventory. Machine reservations remain restricted to the slots that proved the selected witness. Reusable tools, facility construction, processing time, stochastic retry increments, reservations, daily budget and terminal receipts remain independently owned. Unknown or deferred route kinds cannot default to no input, and training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateResource(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateFacility UpstreamRoute,
    [property: JsonPropertyName("resource_input_axis_status")]
    string ResourceInputAxisStatus,
    [property: JsonPropertyName("resource_input_axis_resolved")]
    bool ResourceInputAxisResolved,
    [property: JsonPropertyName("resource_inputs_match_target_date")]
    bool? ResourceInputsMatchTargetDate,
    [property: JsonPropertyName("resource_requirement_kind")]
    string ResourceRequirementKind,
    [property: JsonPropertyName("input_evaluations")]
    AcquisitionResourceInputEvaluation[] InputEvaluations,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionResourceInputEvaluation(
    [property: JsonPropertyName("input_kind")]
    string InputKind,
    [property: JsonPropertyName("qualified_item_id")]
    string QualifiedItemId,
    [property: JsonPropertyName("required_quantity")]
    int RequiredQuantity,
    [property: JsonPropertyName("available_quantity")]
    int? AvailableQuantity,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("evidence_paths")]
    string[] EvidencePaths,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons,
    [property: JsonPropertyName("machine_binding")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AcquisitionMachineResourceBinding? MachineBinding = null,
    [property: JsonPropertyName("resource_condition_binding")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AcquisitionResourceConditionBinding? ResourceConditionBinding = null);

public sealed record AcquisitionResourceConditionBinding(
    [property: JsonPropertyName("condition")] string Condition,
    [property: JsonPropertyName("predicate_name")] string PredicateName,
    [property: JsonPropertyName("negated")] bool Negated,
    [property: JsonPropertyName("player_selector")] string PlayerSelector,
    [property: JsonPropertyName("minimum_count")] int MinimumCount,
    [property: JsonPropertyName("maximum_count")] int MaximumCount,
    [property: JsonPropertyName("native_result")] bool NativeResult);

public sealed record AcquisitionMachineResourceBinding(
    [property: JsonPropertyName("source_trigger_id")]
    string SourceTriggerId,
    [property: JsonPropertyName("trigger_condition")]
    string TriggerCondition,
    [property: JsonPropertyName("required_context_tags")]
    string[] RequiredContextTags,
    [property: JsonPropertyName("minimum_edibility")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? MinimumEdibility,
    [property: JsonPropertyName("maximum_edibility")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? MaximumEdibility,
    [property: JsonPropertyName("per_attempt_required_quantity")]
    int PerAttemptRequiredQuantity,
    [property: JsonPropertyName("required_attempt_count")]
    int RequiredAttemptCount,
    [property: JsonPropertyName("eligible_slots")]
    AcquisitionMachineResourceSlot[] EligibleSlots);

public sealed record AcquisitionMachineResourceSlot(
    [property: JsonPropertyName("node_id")] string NodeId,
    [property: JsonPropertyName("slot_index")] int SlotIndex,
    [property: JsonPropertyName("qualified_item_id")]
    string QualifiedItemId,
    [property: JsonPropertyName("available_quantity")]
    int AvailableQuantity);
