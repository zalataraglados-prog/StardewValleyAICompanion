using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MasterAnglerFreshSavePrerequisiteCompletionProof(
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("goal_id")] string GoalId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("audit_path")] string AuditPath,
    [property: JsonPropertyName("audit_sha256")] string AuditSha256,
    [property: JsonPropertyName("dependency_expansion_path")] string DependencyExpansionPath,
    [property: JsonPropertyName("dependency_expansion_sha256")] string DependencyExpansionSha256,
    [property: JsonPropertyName("native_denominator_count")] int NativeDenominatorCount,
    [property: JsonPropertyName("prerequisite_family_count")] int PrerequisiteFamilyCount,
    [property: JsonPropertyName("shared_capability_certificate_ids")] string[] SharedCapabilityCertificateIds,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons)
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion => "master_angler_fresh_save_prerequisite_completion_proof.v2";

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible => false;
}
