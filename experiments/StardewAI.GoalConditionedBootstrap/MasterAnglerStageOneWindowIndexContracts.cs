using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MasterAnglerStageOneWindowIndex
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = "master_angler_stage_one_window_index.v1";
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("goal_id")] public string GoalId { get; set; } = string.Empty;
    [JsonPropertyName("game_version")] public string GameVersion { get; set; } = string.Empty;
    [JsonPropertyName("catalog_path")] public string CatalogPath { get; set; } = string.Empty;
    [JsonPropertyName("catalog_sha256")] public string CatalogSha256 { get; set; } = string.Empty;
    [JsonPropertyName("deadline_year")] public int DeadlineYear { get; set; }
    [JsonPropertyName("deadline_season")] public string DeadlineSeason { get; set; } = string.Empty;
    [JsonPropertyName("deadline_day_of_month")] public int DeadlineDayOfMonth { get; set; }
    [JsonPropertyName("deadline_total_day_exclusive")] public int DeadlineTotalDayExclusive { get; set; }
    [JsonPropertyName("native_denominator_count")] public int NativeDenominatorCount { get; set; }
    [JsonPropertyName("static_window_coverage_complete")] public bool StaticWindowCoverageComplete { get; set; }
    [JsonPropertyName("training_label_eligible")] public bool TrainingLabelEligible { get; set; }
    [JsonPropertyName("species")] public MasterAnglerStageOneSpeciesWindow[] Species { get; set; } = Array.Empty<MasterAnglerStageOneSpeciesWindow>();
    [JsonPropertyName("unresolved_species_ids")] public string[] UnresolvedSpeciesIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("remaining_admission")]
    public string RemainingAdmission { get; set; } =
        "Resolve each target date's dynamic native predicates, location unlock, access route, fishable tile, equipment, existing live candidate, stochastic retry reserve and fresh native receipt.";
}

public sealed class MasterAnglerStageOneSpeciesWindow
{
    [JsonPropertyName("qualified_item_id")] public string QualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("acquisition_class")] public string AcquisitionClass { get; set; } = string.Empty;
    [JsonPropertyName("earliest_static_total_day")] public int? EarliestStaticTotalDay { get; set; }
    [JsonPropertyName("latest_static_total_day")] public int? LatestStaticTotalDay { get; set; }
    [JsonPropertyName("has_dynamic_conditions")] public bool HasDynamicConditions { get; set; }
    [JsonPropertyName("windows")] public MasterAnglerStageOneSourceWindow[] Windows { get; set; } = Array.Empty<MasterAnglerStageOneSourceWindow>();
}

public sealed class MasterAnglerStageOneSourceWindow
{
    [JsonPropertyName("source_kind")] public string SourceKind { get; set; } = string.Empty;
    [JsonPropertyName("source_key")] public string SourceKey { get; set; } = string.Empty;
    [JsonPropertyName("location_id")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("rule_id")] public string RuleId { get; set; } = string.Empty;
    [JsonPropertyName("mine_area")] public int? MineArea { get; set; }
    [JsonPropertyName("water_type")] public string WaterType { get; set; } = string.Empty;
    [JsonPropertyName("year")] public int? Year { get; set; }
    [JsonPropertyName("season")] public string Season { get; set; } = string.Empty;
    [JsonPropertyName("first_total_day")] public int FirstTotalDay { get; set; }
    [JsonPropertyName("last_total_day")] public int LastTotalDay { get; set; }
    [JsonPropertyName("time_windows")] public MasterAnglerTimeWindow[] TimeWindows { get; set; } = Array.Empty<MasterAnglerTimeWindow>();
    [JsonPropertyName("weather_modes")] public string[] WeatherModes { get; set; } = Array.Empty<string>();
    [JsonPropertyName("dynamic_conditions")] public string[] DynamicConditions { get; set; } = Array.Empty<string>();
    [JsonPropertyName("minimum_fishing_level")] public int MinimumFishingLevel { get; set; }
    [JsonPropertyName("require_magic_bait")] public bool RequireMagicBait { get; set; }
    [JsonPropertyName("training_rod_allowed")] public bool? TrainingRodAllowed { get; set; }
    [JsonPropertyName("requires_trap_infrastructure")] public bool RequiresTrapInfrastructure { get; set; }
    [JsonPropertyName("requires_location_access_evidence")] public bool RequiresLocationAccessEvidence { get; set; }
    [JsonPropertyName("requires_route_and_fishable_tile_evidence")] public bool RequiresRouteAndFishableTileEvidence { get; set; }
    [JsonPropertyName("requires_existing_live_candidate_match")] public bool RequiresExistingLiveCandidateMatch { get; set; }
    [JsonPropertyName("stochastic_outcome")] public bool StochasticOutcome { get; set; }
}
