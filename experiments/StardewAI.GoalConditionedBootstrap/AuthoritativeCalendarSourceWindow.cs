using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AuthoritativeCalendarSourceWindow
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
    [JsonPropertyName("time_windows")] public MasterAnglerTimeWindow[] TimeWindows { get; set; } =
        Array.Empty<MasterAnglerTimeWindow>();
    [JsonPropertyName("weather_modes")] public string[] WeatherModes { get; set; } = Array.Empty<string>();
    [JsonPropertyName("dynamic_conditions")] public string[] DynamicConditions { get; set; } =
        Array.Empty<string>();
    [JsonPropertyName("minimum_fishing_level")] public int MinimumFishingLevel { get; set; }
    [JsonPropertyName("require_magic_bait")] public bool RequireMagicBait { get; set; }
    [JsonPropertyName("training_rod_allowed")] public bool? TrainingRodAllowed { get; set; }
    [JsonPropertyName("requires_trap_infrastructure")] public bool RequiresTrapInfrastructure { get; set; }
    [JsonPropertyName("requires_location_access_evidence")] public bool RequiresLocationAccessEvidence { get; set; }
    [JsonPropertyName("requires_route_and_fishable_tile_evidence")]
    public bool RequiresRouteAndFishableTileEvidence { get; set; }
    [JsonPropertyName("requires_existing_live_candidate_match")]
    public bool RequiresExistingLiveCandidateMatch { get; set; }
    [JsonPropertyName("stochastic_outcome")] public bool StochasticOutcome { get; set; }

    [JsonPropertyName("required_location_capability")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiredLocationCapability { get; set; }
}
