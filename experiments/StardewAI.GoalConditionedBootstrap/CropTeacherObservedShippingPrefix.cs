using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

// Original accepted transitions after the retained wave, not synthetic terminal actions or settled revenue.
public sealed record CropTeacherObservedShippingPrefix(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("output_retention_proof_path")] string OutputRetentionProofPath,
    [property: JsonPropertyName("retention_snapshot_path")] string RetentionSnapshotPath,
    [property: JsonPropertyName("transitions")] MoneyRevenueCropNativeLifecycleTransitionRequest[] Transitions);
