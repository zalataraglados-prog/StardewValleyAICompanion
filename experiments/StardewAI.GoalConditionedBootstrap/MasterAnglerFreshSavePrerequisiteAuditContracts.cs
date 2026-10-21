using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MasterAnglerFreshSavePrerequisiteAuditReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "master_angler_fresh_save_prerequisite_audit.v1";

    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("goal_id")] public string GoalId { get; set; } = string.Empty;
    [JsonPropertyName("game_version")] public string GameVersion { get; set; } = string.Empty;
    [JsonPropertyName("training_label_eligible")] public bool TrainingLabelEligible { get; set; }
    [JsonPropertyName("catalog_path")] public string CatalogPath { get; set; } = string.Empty;
    [JsonPropertyName("catalog_sha256")] public string CatalogSha256 { get; set; } = string.Empty;
    [JsonPropertyName("window_index_path")] public string WindowIndexPath { get; set; } = string.Empty;
    [JsonPropertyName("window_index_sha256")] public string WindowIndexSha256 { get; set; } = string.Empty;
    [JsonPropertyName("dependency_expansion_path")] public string DependencyExpansionPath { get; set; } = string.Empty;
    [JsonPropertyName("dependency_expansion_sha256")] public string DependencyExpansionSha256 { get; set; } = string.Empty;
    [JsonPropertyName("deadline_total_day_exclusive")] public int DeadlineTotalDayExclusive { get; set; }
    [JsonPropertyName("native_denominator_count")] public int NativeDenominatorCount { get; set; }
    [JsonPropertyName("rod_location_species_count")] public int RodLocationSpeciesCount { get; set; }
    [JsonPropertyName("mine_override_only_species_count")] public int MineOverrideOnlySpeciesCount { get; set; }
    [JsonPropertyName("trap_species_count")] public int TrapSpeciesCount { get; set; }
    [JsonPropertyName("species_with_two_or_more_static_opportunity_days")]
    public int SpeciesWithTwoOrMoreStaticOpportunityDays { get; set; }
    [JsonPropertyName("rain_or_storm_required_species_count")]
    public int RainOrStormRequiredSpeciesCount { get; set; }
    [JsonPropertyName("dynamic_condition_species_count")]
    public int DynamicConditionSpeciesCount { get; set; }
    [JsonPropertyName("maximum_required_fishing_level")]
    public int MaximumRequiredFishingLevel { get; set; }
    [JsonPropertyName("static_opportunity_inventory_complete")]
    public bool StaticOpportunityInventoryComplete { get; set; }
    [JsonPropertyName("prerequisite_family_inventory_complete")]
    public bool PrerequisiteFamilyInventoryComplete { get; set; }
    [JsonPropertyName("species")]
    public MasterAnglerFreshSaveSpeciesPrerequisite[] Species { get; set; } =
        Array.Empty<MasterAnglerFreshSaveSpeciesPrerequisite>();
    [JsonPropertyName("prerequisite_families")]
    public MasterAnglerFreshSavePrerequisiteFamily[] PrerequisiteFamilies { get; set; } =
        Array.Empty<MasterAnglerFreshSavePrerequisiteFamily>();
    [JsonPropertyName("missing_or_unbound_producer_families")]
    public string[] MissingOrUnboundProducerFamilies { get; set; } = Array.Empty<string>();
    [JsonPropertyName("unknown_location_ids")]
    public string[] UnknownLocationIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "This inventory distinguishes static calendar opportunities from fresh-save reachability. " +
        "It never treats a date span as a dynamically legal catch, never predicts a stochastic rod " +
        "or Crab Pot result, and never authorizes training. Every missing or merely reusable-but-unbound " +
        "producer family must be connected to the Master Angler recurrence and proven before the initial " +
        "Year 3 evaluation.";
}

public sealed class MasterAnglerFreshSaveSpeciesPrerequisite
{
    [JsonPropertyName("qualified_item_id")] public string QualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("acquisition_class")] public string AcquisitionClass { get; set; } = string.Empty;
    [JsonPropertyName("route_prerequisite_expression")]
    public string RoutePrerequisiteExpression { get; set; } = string.Empty;
    [JsonPropertyName("route_prerequisite_alternatives")]
    public string[] RoutePrerequisiteAlternatives { get; set; } = Array.Empty<string>();
    [JsonPropertyName("source_alternatives")]
    public MasterAnglerFreshSaveSourceAlternative[] SourceAlternatives { get; set; } =
        Array.Empty<MasterAnglerFreshSaveSourceAlternative>();
    [JsonPropertyName("minimum_required_fishing_level")]
    public int MinimumRequiredFishingLevel { get; set; }
    [JsonPropertyName("maximum_source_required_fishing_level")]
    public int MaximumSourceRequiredFishingLevel { get; set; }
    [JsonPropertyName("weather_requirement")]
    public string WeatherRequirement { get; set; } = string.Empty;
    [JsonPropertyName("weather_modes")]
    public string[] WeatherModes { get; set; } = Array.Empty<string>();
    [JsonPropertyName("dynamic_conditions")]
    public string[] DynamicConditions { get; set; } = Array.Empty<string>();
    [JsonPropertyName("static_opportunity_day_count")]
    public int StaticOpportunityDayCount { get; set; }
    [JsonPropertyName("first_static_opportunity_total_day")]
    public int FirstStaticOpportunityTotalDay { get; set; }
    [JsonPropertyName("final_static_reserve_total_day")]
    public int FinalStaticReserveTotalDay { get; set; }
    [JsonPropertyName("trap_daily_output_chance")]
    public double? TrapDailyOutputChance { get; set; }
    [JsonPropertyName("trap_required_independent_pot_days_at_95_percent")]
    public int? TrapRequiredIndependentPotDaysAt95Percent { get; set; }
    [JsonPropertyName("trap_static_maximum_serviced_cycle_count_from_day_one")]
    public int? TrapStaticMaximumServicedCycleCountFromDayOne { get; set; }
    [JsonPropertyName("trap_theoretical_minimum_parallel_pot_capacity_from_day_one")]
    public int? TrapTheoreticalMinimumParallelPotCapacityFromDayOne { get; set; }
    [JsonPropertyName("rod_retry_budget_owner")]
    public string RodRetryBudgetOwner { get; set; } = string.Empty;
}

public sealed class MasterAnglerFreshSaveSourceAlternative
{
    [JsonPropertyName("source_kind")] public string SourceKind { get; set; } = string.Empty;
    [JsonPropertyName("source_key")] public string SourceKey { get; set; } = string.Empty;
    [JsonPropertyName("location_id")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("mine_area")] public int? MineArea { get; set; }
    [JsonPropertyName("water_type")] public string WaterType { get; set; } = string.Empty;
    [JsonPropertyName("prerequisite_family")]
    public string PrerequisiteFamily { get; set; } = string.Empty;
}

public sealed class MasterAnglerFreshSavePrerequisiteFamily
{
    [JsonPropertyName("family_id")] public string FamilyId { get; set; } = string.Empty;
    [JsonPropertyName("producer_status")]
    public string ProducerStatus { get; set; } = string.Empty;
    [JsonPropertyName("required_by_species_ids")]
    public string[] RequiredBySpeciesIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("existing_evidence")]
    public string[] ExistingEvidence { get; set; } = Array.Empty<string>();
    [JsonPropertyName("remaining_work")]
    public string RemainingWork { get; set; } = string.Empty;
}
