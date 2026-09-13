using System.Text.Json.Serialization;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class FishingForecastSnapshotManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "fishing_forecast_snapshot_manifest.v1";

    [JsonPropertyName("snapshots")]
    public FishingForecastSnapshotReference[] Snapshots { get; set; } =
        Array.Empty<FishingForecastSnapshotReference>();
}

public sealed record FishingForecastSnapshotReference(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("target_location_id")] string TargetLocationId,
    [property: JsonPropertyName("rod_slot_index")] int RodSlotIndex,
    [property: JsonPropertyName("snapshot_path")] string SnapshotPath,
    [property: JsonPropertyName("snapshot_sha256")] string SnapshotSha256);

public sealed class AcquisitionRouteTargetDateFishingProbabilityReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_fishing_probability.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_processing_lead_time_sha256")]
    public string TargetDateProcessingLeadTimeSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("base_snapshot_sha256")]
    public string BaseSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("base_snapshot_state_hash")]
    public string BaseSnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("forecast_manifest_sha256")]
    public string ForecastManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("fishing_route_count")]
    public int FishingRouteCount { get; set; }

    [JsonPropertyName("positive_probability_count")]
    public int PositiveProbabilityCount { get; set; }

    [JsonPropertyName("zero_probability_count")]
    public int ZeroProbabilityCount { get; set; }

    [JsonPropertyName("blocked_probability_count")]
    public int BlockedProbabilityCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateFishingProbability[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateFishingProbability>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "This artifact preserves every route occurrence but evaluates only active native_location_fish_spawn routes. Every usable probability is a conservative first-pass lower bound built from one demand-only target-location/rod snapshot, an explicit bobber tile and a mechanically legal stand tile. Equal-precedence competitors are pessimistically ordered first. Unseeded conditions, custom query resolvers, random chance modifiers, unresolved item queries, stale or cross-save forecasts, minimum quality above zero and missing target-location forecasts fail closed. Stand reachability, the second targeted-bait pass, exact multi-success retry math, retry-expanded reservations, daily time/energy and fresh receipts remain downstream. This artifact cannot authorize training.";
}

public sealed record AcquisitionRouteTargetDateFishingProbability(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("route_kind")]
    string RouteKind,
    [property: JsonPropertyName("target_qualified_item_id")]
    string TargetQualifiedItemId,
    [property: JsonPropertyName("required_amount")]
    int RequiredAmount,
    [property: JsonPropertyName("minimum_quality")]
    int MinimumQuality,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("probability_axis_resolved")]
    bool ProbabilityAxisResolved,
    [property: JsonPropertyName("positive_probability_available")]
    bool? PositiveProbabilityAvailable,
    [property: JsonPropertyName("single_attempt_probability_lower_bound")]
    double? SingleAttemptProbabilityLowerBound,
    [property: JsonPropertyName("independent_retry_lower_bound_proven")]
    bool? IndependentRetryLowerBoundProven,
    [property: JsonPropertyName("retry_blocking_reasons")]
    string[] RetryBlockingReasons,
    [property: JsonPropertyName("selected_projection")]
    FishingTerminalProbabilityProjectionResult? SelectedProjection,
    [property: JsonPropertyName("forecast_request_urls")]
    string[] ForecastRequestUrls,
    [property: JsonPropertyName("forecast_snapshot_sha256")]
    string[] ForecastSnapshotSha256,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);
