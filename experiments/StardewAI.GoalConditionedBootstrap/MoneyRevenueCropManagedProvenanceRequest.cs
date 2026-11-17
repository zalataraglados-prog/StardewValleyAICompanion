using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MoneyRevenueCropManagedProvenanceRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("accepted_managed_closeout")] MoneyRevenueArtifactBinding AcceptedManagedCloseout,
    [property: JsonPropertyName("initial_boundary")] MoneyRevenueArtifactBinding InitialBoundary,
    [property: JsonPropertyName("opening_episode_path")] string OpeningEpisodePath,
    [property: JsonPropertyName("accepted_saved_readbacks")] MoneyRevenueCropAcceptedSavedReadbackReference[] AcceptedSavedReadbacks,
    [property: JsonPropertyName("prior_episode_directories")] string[] PriorEpisodeDirectories);
