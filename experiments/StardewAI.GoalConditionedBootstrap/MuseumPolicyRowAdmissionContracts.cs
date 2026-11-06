using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MuseumPolicyCandidateAdmission(
    [property: JsonPropertyName("candidate")] PolicyEventCandidatePrediction Candidate,
    [property: JsonPropertyName("original_runtime_candidate")] bool OriginalRuntimeCandidate,
    [property: JsonPropertyName("independent_comparison_admitted")] bool IndependentComparisonAdmitted,
    [property: JsonPropertyName("policy_option_admitted")] bool PolicyOptionAdmitted,
    [property: JsonPropertyName("policy_exclusion_reasons")] string[] PolicyExclusionReasons);

public sealed record MuseumPolicyRowAdmission(
    [property: JsonPropertyName("join_path")] string JoinPath,
    [property: JsonPropertyName("join_sha256")] string JoinSha256,
    [property: JsonPropertyName("policy_allowlist_sha256")] string PolicyAllowlistSha256,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("source_state_hash")] string SourceStateHash,
    [property: JsonPropertyName("native_split_key")] string NativeSplitKey,
    [property: JsonPropertyName("native_dataset_partition")] string NativeDatasetPartition,
    [property: JsonPropertyName("prospective_policy_context")] PolicyTrajectoryContext ProspectivePolicyContext,
    [property: JsonPropertyName("state_features")] FeatureVector StateFeatures,
    [property: JsonPropertyName("candidates")] MuseumPolicyCandidateAdmission[] Candidates,
    [property: JsonPropertyName("selected_candidate_id")] string SelectedCandidateId,
    [property: JsonPropertyName("pairwise_preferences")] CurrentCollectionTeacherPairwisePreference[] PairwisePreferences,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "museum_policy_row_admission.v1";
    [JsonPropertyName("status")] public string Status => BlockingReasons.Length == 0
        ? "verified_evidence_ready_for_original_policy_builder" : "verified_evidence_only_policy_option_not_admitted";
    [JsonPropertyName("native_terminal_outcome_verified")] public bool NativeTerminalOutcomeVerified => true;
    [JsonPropertyName("original_candidate_count")] public int OriginalCandidateCount => Candidates.Count(row => row.OriginalRuntimeCandidate);
    [JsonPropertyName("comparison_reference_count")] public int ComparisonReferenceCount => Candidates.Count(row => !row.OriginalRuntimeCandidate);
    [JsonPropertyName("policy_row_built")] public bool PolicyRowBuilt => false;
    [JsonPropertyName("formal_training_authorized")] public bool FormalTrainingAuthorized => false;
}
