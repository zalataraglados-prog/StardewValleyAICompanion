using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record GoldenWalnutManagedReadbackSourceBindingRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("native_after_snapshot_path")] string NativeAfterSnapshotPath,
    [property: JsonPropertyName("continuation_episode_path")] string ContinuationEpisodePath,
    [property: JsonPropertyName("accepted_saved_readbacks")] MoneyRevenueCropAcceptedSavedReadbackReference[] AcceptedSavedReadbacks);

public sealed record GoldenWalnutManagedReadbackSourceBinding(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("source_run_id")] string SourceRunId,
    [property: JsonPropertyName("source_episode_id")] string SourceEpisodeId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("loaded_snapshot_path")] string LoadedSnapshotPath,
    [property: JsonPropertyName("continuation_before_snapshot_path")] string ContinuationBeforeSnapshotPath,
    [property: JsonPropertyName("loaded_state_hash")] string LoadedStateHash,
    [property: JsonPropertyName("source_state_hash")] string SourceStateHash,
    [property: JsonPropertyName("loaded_game_tick")] long LoadedGameTick,
    [property: JsonPropertyName("source_game_tick")] long SourceGameTick,
    [property: JsonPropertyName("persistent_state_sha256")] string PersistentStateSha256,
    [property: JsonPropertyName("readback_acceptance_sha256s")] string[] ReadbackAcceptanceSha256s,
    [property: JsonPropertyName("input_artifact_hashes")] ReadOnlyDictionary<string, string> InputArtifactHashes)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "golden_walnut_managed_readback_source_binding.v1";
    [JsonPropertyName("source_binding_verified")] public bool SourceBindingVerified => true;
    [JsonPropertyName("source_schedule_verified")] public bool SourceScheduleVerified => false;
    [JsonPropertyName("global_route_verified")] public bool GlobalRouteVerified => false;
    [JsonPropertyName("native_dispatch_performed")] public bool NativeDispatchPerformed => false;
    [JsonPropertyName("teacher_preference_label_eligible")] public bool TeacherPreferenceLabelEligible => false;
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;
}
