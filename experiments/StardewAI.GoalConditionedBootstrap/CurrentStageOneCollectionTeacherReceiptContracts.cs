using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentStageOneCollectionTeacherReceiptAdmission
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "current_stage_one_collection_teacher_receipt_admission.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("teacher_training_row_eligible")]
    public bool TeacherTrainingRowEligible { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("preference_artifact_sha256")]
    public string PreferenceArtifactSha256 { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_sha256")]
    public string ExecutionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("preference")]
    public CurrentStageOneCollectionTeacherPreferenceLabel? Preference { get; set; }

    [JsonPropertyName("verified_requirement_transitions")]
    public PolicyTeacherRequirementTransition[] VerifiedRequirementTransitions { get; set; } =
        Array.Empty<PolicyTeacherRequirementTransition>();

    [JsonPropertyName("training_row")]
    public PolicyDecisionTrajectoryEnvelope? TrainingRow { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("scope")]
    public string Scope { get; set; } =
        "bounded_ordered_candidate_queue_with_fresh_exact_collection_transition";
}
