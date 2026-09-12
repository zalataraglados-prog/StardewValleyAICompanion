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
    [JsonPropertyName("windows")] public AuthoritativeCalendarSourceWindow[] Windows { get; set; } = Array.Empty<AuthoritativeCalendarSourceWindow>();
}
