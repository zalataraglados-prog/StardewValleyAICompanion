using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MasterAnglerOpportunityCatalogReport
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = "master_angler_opportunity_catalog.v1";
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("goal_id")] public string GoalId { get; set; } = string.Empty;
    [JsonPropertyName("game_version")] public string GameVersion { get; set; } = string.Empty;
    [JsonPropertyName("native_denominator_count")] public int NativeDenominatorCount { get; set; }
    [JsonPropertyName("rod_location_species_count")] public int RodLocationSpeciesCount { get; set; }
    [JsonPropertyName("mine_override_only_species_count")] public int MineOverrideOnlySpeciesCount { get; set; }
    [JsonPropertyName("mine_override_source_species_count")] public int MineOverrideSourceSpeciesCount { get; set; }
    [JsonPropertyName("mine_override_area_count")] public int MineOverrideAreaCount { get; set; }
    [JsonPropertyName("trap_species_count")] public int TrapSpeciesCount { get; set; }
    [JsonPropertyName("source_inventory_complete")] public bool SourceInventoryComplete { get; set; }
    [JsonPropertyName("static_calendar_constraint_complete")] public bool StaticCalendarConstraintComplete { get; set; }
    [JsonPropertyName("location_rule_spawn_chance_input_count")] public int LocationRuleSpawnChanceInputCount { get; set; }
    [JsonPropertyName("location_rule_spawn_chance_input_inventory_complete")] public bool LocationRuleSpawnChanceInputInventoryComplete { get; set; }
    [JsonPropertyName("native_get_fish_override_file_count")] public int NativeGetFishOverrideFileCount { get; set; }
    [JsonPropertyName("requirement_inventory_path")] public string RequirementInventoryPath { get; set; } = string.Empty;
    [JsonPropertyName("requirement_inventory_sha256")] public string RequirementInventorySha256 { get; set; } = string.Empty;
    [JsonPropertyName("source_evidence")] public RequirementSourceEvidence[] SourceEvidence { get; set; } = Array.Empty<RequirementSourceEvidence>();
    [JsonPropertyName("species")] public MasterAnglerSpeciesOpportunity[] Species { get; set; } = Array.Empty<MasterAnglerSpeciesOpportunity>();
    [JsonPropertyName("unresolved_species_ids")] public string[] UnresolvedSpeciesIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("unresolved_calendar_rule_ids")] public string[] UnresolvedCalendarRuleIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("unresolved_spawn_chance_input_rule_ids")] public string[] UnresolvedSpawnChanceInputRuleIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("admission_policy")] public string AdmissionPolicy { get; set; } =
        "This proves exact source enumeration and lossless matching-rule spawn-chance inputs only. It does not prove terminal catch probability. A species becomes a future teacher opportunity only after date, weather, clock, location access, route, tile, equipment, competing-rule precedence, item resolution and native condition evidence all resolve.";
}

public sealed class MasterAnglerSpeciesOpportunity
{
    [JsonPropertyName("item_id")] public string ItemId { get; set; } = string.Empty;
    [JsonPropertyName("qualified_item_id")] public string QualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("acquisition_class")] public string AcquisitionClass { get; set; } = string.Empty;
    [JsonPropertyName("route_status")] public string RouteStatus { get; set; } = string.Empty;
    [JsonPropertyName("fish_data")] public MasterAnglerFishDataConstraint FishData { get; set; } = new();
    [JsonPropertyName("location_rules")] public MasterAnglerLocationRule[] LocationRules { get; set; } = Array.Empty<MasterAnglerLocationRule>();
    [JsonPropertyName("mine_overrides")] public MasterAnglerMineOverrideRule[] MineOverrides { get; set; } = Array.Empty<MasterAnglerMineOverrideRule>();
    [JsonPropertyName("trap_water_type")] public string TrapWaterType { get; set; } = string.Empty;
    [JsonPropertyName("trap_daily_output_chance")] public double? TrapDailyOutputChance { get; set; }
}

public sealed class MasterAnglerMineOverrideRule
{
    [JsonPropertyName("mine_area")] public int MineArea { get; set; }
    [JsonPropertyName("base_chance")] public double BaseChance { get; set; }
    [JsonPropertyName("score_multiplier")] public double ScoreMultiplier { get; set; }
    [JsonPropertyName("base_score")] public double BaseScore { get; set; }
    [JsonPropertyName("fishing_level_score_multiplier")] public double FishingLevelScoreMultiplier { get; set; }
    [JsonPropertyName("water_depth_score_multiplier")] public double WaterDepthScoreMultiplier { get; set; }
    [JsonPropertyName("curiosity_lure_score_bonus")] public double CuriosityLureScoreBonus { get; set; }
    [JsonPropertyName("targeted_bait_score_bonus")] public double TargetedBaitScoreBonus { get; set; }
    [JsonPropertyName("targeted_bait_name")] public string TargetedBaitName { get; set; } = string.Empty;
    [JsonPropertyName("training_rod_allowed")] public bool TrainingRodAllowed { get; set; }
}

public sealed class MasterAnglerFishDataConstraint
{
    [JsonPropertyName("parse_status")] public string ParseStatus { get; set; } = string.Empty;
    [JsonPropertyName("raw")] public string Raw { get; set; } = string.Empty;
    [JsonPropertyName("time_windows")] public MasterAnglerTimeWindow[] TimeWindows { get; set; } = Array.Empty<MasterAnglerTimeWindow>();
    [JsonPropertyName("seasons")] public string[] Seasons { get; set; } = Array.Empty<string>();
    [JsonPropertyName("weather")] public string Weather { get; set; } = string.Empty;
    [JsonPropertyName("minimum_fishing_level")] public int? MinimumFishingLevel { get; set; }
}

public sealed class MasterAnglerTimeWindow
{
    [JsonPropertyName("start_time")] public int StartTime { get; set; }
    [JsonPropertyName("end_time")] public int EndTime { get; set; }
}

public sealed class MasterAnglerLocationRule
{
    [JsonPropertyName("location_id")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("rule_index")] public int RuleIndex { get; set; }
    [JsonPropertyName("rule_id")] public string RuleId { get; set; } = string.Empty;
    [JsonPropertyName("spawn_season")] public string SpawnSeason { get; set; } = string.Empty;
    [JsonPropertyName("condition")] public string Condition { get; set; } = string.Empty;
    [JsonPropertyName("per_item_condition")] public string PerItemCondition { get; set; } = string.Empty;
    [JsonPropertyName("item_id")] public string ItemId { get; set; } = string.Empty;
    [JsonPropertyName("random_item_ids")] public string[] RandomItemIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("item_selection_mode")] public string ItemSelectionMode { get; set; } = string.Empty;
    [JsonPropertyName("spawn_chance_input_status")] public string SpawnChanceInputStatus { get; set; } = string.Empty;
    [JsonPropertyName("base_chance")] public double BaseChance { get; set; }
    [JsonPropertyName("apply_daily_luck")] public bool ApplyDailyLuck { get; set; }
    [JsonPropertyName("curiosity_lure_buff")] public double CuriosityLureBuff { get; set; }
    [JsonPropertyName("specific_bait_buff")] public double SpecificBaitBuff { get; set; }
    [JsonPropertyName("specific_bait_multiplier")] public double SpecificBaitMultiplier { get; set; }
    [JsonPropertyName("chance_boost_per_luck_level")] public double ChanceBoostPerLuckLevel { get; set; }
    [JsonPropertyName("chance_modifier_mode")] public int ChanceModifierMode { get; set; }
    [JsonPropertyName("chance_modifiers")] public MasterAnglerChanceModifier[] ChanceModifiers { get; set; } = Array.Empty<MasterAnglerChanceModifier>();
    [JsonPropertyName("use_fish_caught_seeded_random")] public bool UseFishCaughtSeededRandom { get; set; }
    [JsonPropertyName("fish_area_id")] public string FishAreaId { get; set; } = string.Empty;
    [JsonPropertyName("minimum_fishing_level")] public int MinimumFishingLevel { get; set; }
    [JsonPropertyName("minimum_distance_from_shore")] public int MinimumDistanceFromShore { get; set; }
    [JsonPropertyName("maximum_distance_from_shore")] public int MaximumDistanceFromShore { get; set; }
    [JsonPropertyName("require_magic_bait")] public bool RequireMagicBait { get; set; }
    [JsonPropertyName("ignore_fish_data_requirements")] public bool IgnoreFishDataRequirements { get; set; }
    [JsonPropertyName("can_use_training_rod")] public bool? CanUseTrainingRod { get; set; }
    [JsonPropertyName("catch_limit")] public int CatchLimit { get; set; }
    [JsonPropertyName("precedence")] public int Precedence { get; set; }
    [JsonPropertyName("calendar")] public MasterAnglerCalendarConstraint Calendar { get; set; } = new();
}

public sealed class MasterAnglerChanceModifier
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("condition")] public string Condition { get; set; } = string.Empty;
    [JsonPropertyName("modification")] public int Modification { get; set; }
    [JsonPropertyName("amount")] public double Amount { get; set; }
    [JsonPropertyName("random_amount")] public double[] RandomAmount { get; set; } = Array.Empty<double>();
}

public sealed class MasterAnglerCalendarConstraint
{
    [JsonPropertyName("parse_status")] public string ParseStatus { get; set; } = string.Empty;
    [JsonPropertyName("minimum_year")] public int MinimumYear { get; set; } = 1;
    [JsonPropertyName("maximum_year")] public int? MaximumYear { get; set; }
    [JsonPropertyName("seasons")] public string[] Seasons { get; set; } = Array.Empty<string>();
    [JsonPropertyName("time_windows")] public MasterAnglerTimeWindow[] TimeWindows { get; set; } = Array.Empty<MasterAnglerTimeWindow>();
    [JsonPropertyName("weather_modes")] public string[] WeatherModes { get; set; } = Array.Empty<string>();
    [JsonPropertyName("dynamic_conditions")] public string[] DynamicConditions { get; set; } = Array.Empty<string>();
    [JsonPropertyName("unparsed_conditions")] public string[] UnparsedConditions { get; set; } = Array.Empty<string>();
    [JsonPropertyName("static_calendar_possible")] public bool StaticCalendarPossible { get; set; }
}
