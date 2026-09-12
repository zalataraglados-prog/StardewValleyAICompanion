using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteCalendarResolutionReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "acquisition_route_calendar_resolution.v1";

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

    [JsonPropertyName("master_angler_window_index_sha256")]
    public string MasterAnglerWindowIndexSha256 { get; set; } = string.Empty;

    [JsonPropertyName("master_angler_opportunity_catalog_sha256")]
    public string MasterAnglerOpportunityCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("location_data_sha256")]
    public string LocationDataSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_data_sha256")]
    public string CropDataSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_crop_growth_source_sha256")]
    public string NativeCropGrowthSourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_crop_planting_source_sha256")]
    public string NativeCropPlantingSourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("deadline_total_day_exclusive")]
    public int DeadlineTotalDayExclusive { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("resolved_static_source_count")]
    public int ResolvedStaticSourceCount { get; set; }

    [JsonPropertyName("blocked_static_source_count")]
    public int BlockedStaticSourceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("static_calendar_source_resolution_complete")]
    public bool StaticCalendarSourceResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("supported_route_kinds")]
    public string[] SupportedRouteKinds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("unresolved_route_kinds")]
    public string[] UnresolvedRouteKinds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("routes")]
    public AcquisitionRouteCalendarResolution[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteCalendarResolution>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A route is only statically resolved when its exact requirement occurrence and source identity bind to an authoritative calendar window. Crop source resolution preserves native growth, watering, paddy, location-rule and possible-output facts but does not resolve their target-date dependencies. Static source resolution does not satisfy target-date dynamic predicates or any other dependency axis.";
}

public sealed record AcquisitionRouteCalendarResolution(
    [property: JsonPropertyName("route_occurrence_id")] string RouteOccurrenceId,
    [property: JsonPropertyName("requirement_set_id")] string RequirementSetId,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("alternative_index")] int AlternativeIndex,
    [property: JsonPropertyName("route_index")] int RouteIndex,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("route_kind")] string RouteKind,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_asset")] string SourceAsset,
    [property: JsonPropertyName("source_path")] string SourcePath,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("evidence_class")] string EvidenceClass,
    [property: JsonPropertyName("calendar_windows")] AuthoritativeCalendarSourceWindow[] CalendarWindows,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons,
    [property: JsonPropertyName("crop_source")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AcquisitionCropSourceEvidence? CropSource = null);

public sealed record AcquisitionCropSourceEvidence(
    [property: JsonPropertyName("seed_item_id")] string SeedItemId,
    [property: JsonPropertyName("data_harvest_qualified_item_id")] string DataHarvestQualifiedItemId,
    [property: JsonPropertyName("possible_harvest_qualified_item_ids")] string[] PossibleHarvestQualifiedItemIds,
    [property: JsonPropertyName("native_seasons")] string[] NativeSeasons,
    [property: JsonPropertyName("days_in_phase")] int[] DaysInPhase,
    [property: JsonPropertyName("base_growth_days")] int BaseGrowthDays,
    [property: JsonPropertyName("regrow_days")] int RegrowDays,
    [property: JsonPropertyName("needs_watering")] bool NeedsWatering,
    [property: JsonPropertyName("is_paddy_crop")] bool IsPaddyCrop,
    [property: JsonPropertyName("stochastic_outcome")] bool StochasticOutcome,
    [property: JsonPropertyName("texture")] string Texture,
    [property: JsonPropertyName("sprite_index")] int SpriteIndex,
    [property: JsonPropertyName("plantable_location_rules")] AcquisitionCropPlantableLocationRule[] PlantableLocationRules,
    [property: JsonPropertyName("requires_growth_modifier_resolution")] bool RequiresGrowthModifierResolution,
    [property: JsonPropertyName("requires_watering_schedule_evidence")] bool RequiresWateringScheduleEvidence,
    [property: JsonPropertyName("requires_paddy_adjacency_evidence")] bool RequiresPaddyAdjacencyEvidence,
    [property: JsonPropertyName("requires_location_rule_resolution")] bool RequiresLocationRuleResolution);

public sealed record AcquisitionCropPlantableLocationRule(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("condition")] string? Condition,
    [property: JsonPropertyName("planted_in")] int PlantedIn,
    [property: JsonPropertyName("result")] int Result,
    [property: JsonPropertyName("denied_message")] string? DeniedMessage);
