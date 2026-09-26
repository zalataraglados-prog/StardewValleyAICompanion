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

    [JsonPropertyName("uses_current_community_center_denominator")]
    public bool UsesCurrentCommunityCenterDenominator { get; set; }

    [JsonPropertyName("community_center_bundle_mode")]
    public string CommunityCenterBundleMode { get; set; } = string.Empty;

    [JsonPropertyName("community_center_denominator_sha256")]
    public string CommunityCenterDenominatorSha256 { get; set; } = string.Empty;

    [JsonPropertyName("community_center_source_state_hash")]
    public string CommunityCenterSourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("community_center_snapshot_sha256")]
    public string CommunityCenterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("master_angler_window_index_sha256")]
    public string MasterAnglerWindowIndexSha256 { get; set; } = string.Empty;

    [JsonPropertyName("master_angler_opportunity_catalog_sha256")]
    public string MasterAnglerOpportunityCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("location_data_sha256")]
    public string LocationDataSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_data_sha256")]
    public string CropDataSha256 { get; set; } = string.Empty;

    [JsonPropertyName("shop_data_sha256")]
    public string ShopDataSha256 { get; set; } = string.Empty;

    [JsonPropertyName("machine_data_sha256")]
    public string MachineDataSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_machine_output_selection_source_sha256")]
    public string NativeMachineOutputSelectionSourceSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("access_constraint_index_sha256")]
    public string AccessConstraintIndexSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_crop_growth_source_sha256")]
    public string NativeCropGrowthSourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_crop_planting_source_sha256")]
    public string NativeCropPlantingSourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_shop_stock_source_sha256")]
    public string NativeShopStockSourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_shop_open_source_sha256")]
    public string NativeShopOpenSourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_shop_purchase_source_sha256")]
    public string NativeShopPurchaseSourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_game_state_query_source_sha256")]
    public string NativeGameStateQuerySourceSha256 { get; set; } = string.Empty;

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
        "A route is only statically resolved when its exact requirement occurrence and source identity bind to an authoritative source row and calendar projection. A current-save build replaces the static Community Center set with the hash-bound standard/remixed denominator; category slots retain one alternative identity while each concrete accepted target receives its own route occurrence. Crop source resolution preserves native growth facts. Shop source resolution preserves native stock, price, trade, condition, owner, endpoint and door facts. Neither projection proves target-date availability, location access, live stock, resource affordability, or any other downstream dependency axis.";
}

public sealed record AcquisitionRouteCalendarResolution(
    [property: JsonPropertyName("route_occurrence_id")] string RouteOccurrenceId,
    [property: JsonPropertyName("requirement_set_id")] string RequirementSetId,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("alternative_index")] int AlternativeIndex,
    [property: JsonPropertyName("route_index")] int RouteIndex,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("match_kind")] string MatchKind,
    [property: JsonPropertyName("required_amount")] int RequiredAmount,
    [property: JsonPropertyName("minimum_quality")] int MinimumQuality,
    [property: JsonPropertyName("route_kind")] string RouteKind,
    [property: JsonPropertyName("uncertainty_mode")] string UncertaintyMode,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_asset")] string SourceAsset,
    [property: JsonPropertyName("source_path")] string SourcePath,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("evidence_class")] string EvidenceClass,
    [property: JsonPropertyName("calendar_windows")] AuthoritativeCalendarSourceWindow[] CalendarWindows,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons,
    [property: JsonPropertyName("crop_source")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AcquisitionCropSourceEvidence? CropSource = null,
    [property: JsonPropertyName("shop_source")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AcquisitionShopSourceEvidence? ShopSource = null,
    [property: JsonPropertyName("machine_source")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AcquisitionMachineSourceEvidence? MachineSource = null);

public sealed record AcquisitionMachineSourceEvidence(
    [property: JsonPropertyName("machine_qualified_item_id")]
    string MachineQualifiedItemId,
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("rule_index")] int RuleIndex,
    [property: JsonPropertyName("output_index")] int OutputIndex,
    [property: JsonPropertyName("rule_condition")] string RuleCondition,
    [property: JsonPropertyName("use_first_valid_output")]
    bool UseFirstValidOutput,
    [property: JsonPropertyName("minutes_until_ready")]
    int MinutesUntilReady,
    [property: JsonPropertyName("days_until_ready")] int DaysUntilReady,
    [property: JsonPropertyName("only_complete_overnight")]
    bool OnlyCompleteOvernight,
    [property: JsonPropertyName("recalculate_on_collect")]
    bool RecalculateOnCollect,
    [property: JsonPropertyName("ready_time_modifier_mode")]
    int ReadyTimeModifierMode,
    [property: JsonPropertyName("ready_time_modifiers")]
    AcquisitionMachineNumericModifierEvidence[] ReadyTimeModifiers,
    [property: JsonPropertyName("triggers")]
    AcquisitionMachineTriggerEvidence[] Triggers,
    [property: JsonPropertyName("additional_consumed_items")]
    AcquisitionMachineConsumedItemEvidence[] AdditionalConsumedItems,
    [property: JsonPropertyName("output_item_query")]
    string OutputItemQuery,
    [property: JsonPropertyName("output_method")] string OutputMethod,
    [property: JsonPropertyName("output_condition")]
    string OutputCondition,
    [property: JsonPropertyName("per_item_condition")]
    string PerItemCondition,
    [property: JsonPropertyName("random_item_id")] string RandomItemId,
    [property: JsonPropertyName("minimum_stack")] int MinimumStack,
    [property: JsonPropertyName("maximum_stack")] int MaximumStack,
    [property: JsonPropertyName("quality")] int Quality,
    [property: JsonPropertyName("copy_quality")] bool CopyQuality,
    [property: JsonPropertyName("stack_modifier_mode")]
    int StackModifierMode,
    [property: JsonPropertyName("stack_modifiers")]
    AcquisitionMachineNumericModifierEvidence[] StackModifiers,
    [property: JsonPropertyName("quality_modifier_mode")]
    int QualityModifierMode,
    [property: JsonPropertyName("quality_modifiers")]
    AcquisitionMachineNumericModifierEvidence[] QualityModifiers,
    [property: JsonPropertyName("output_selection_count")]
    int OutputSelectionCount,
    [property: JsonPropertyName("stochastic_outcome")]
    bool StochasticOutcome);

public sealed record AcquisitionMachineTriggerEvidence(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("trigger")] int Trigger,
    [property: JsonPropertyName("required_item_id")] string RequiredItemId,
    [property: JsonPropertyName("required_tags")] string[] RequiredTags,
    [property: JsonPropertyName("required_count")] int RequiredCount,
    [property: JsonPropertyName("condition")] string Condition);

public sealed record AcquisitionMachineConsumedItemEvidence(
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("required_count")] int RequiredCount);

public sealed record AcquisitionMachineNumericModifierEvidence(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("condition")] string Condition,
    [property: JsonPropertyName("modification")] int Modification,
    [property: JsonPropertyName("amount")] double Amount,
    [property: JsonPropertyName("random_amount")] double? RandomAmount);

public sealed record AcquisitionCropSourceEvidence(
    [property: JsonPropertyName("seed_item_id")] string SeedItemId,
    [property: JsonPropertyName("data_harvest_qualified_item_id")] string DataHarvestQualifiedItemId,
    [property: JsonPropertyName("possible_harvest_qualified_item_ids")] string[] PossibleHarvestQualifiedItemIds,
    [property: JsonPropertyName("harvest_min_stack")] int HarvestMinStack,
    [property: JsonPropertyName("harvest_max_stack")] int HarvestMaxStack,
    [property: JsonPropertyName("extra_harvest_chance")] double ExtraHarvestChance,
    [property: JsonPropertyName("harvest_min_quality")] int HarvestMinQuality,
    [property: JsonPropertyName("harvest_max_quality")] int? HarvestMaxQuality,
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
