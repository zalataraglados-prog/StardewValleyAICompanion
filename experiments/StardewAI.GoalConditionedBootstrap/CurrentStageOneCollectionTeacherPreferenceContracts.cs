using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentStageOneCollectionTeacherPreferenceLabel
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "current_stage_one_collection_teacher_preference.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("teacher_preference_label_eligible")]
    public bool TeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("uses_learner_rank_or_score")]
    public bool UsesLearnerRankOrScore { get; set; }

    [JsonPropertyName("emits_negative_labels_for_unavailable_routes")]
    public bool EmitsNegativeLabelsForUnavailableRoutes { get; set; }

    [JsonPropertyName("preference_provenance_class")]
    public string PreferenceProvenanceClass { get; set; } =
        "independent_deterministic_teacher_preference";

    [JsonPropertyName("selection_policy_id")]
    public string SelectionPolicyId { get; set; } =
        "stage_one_collection_current_choice_lexicographic.v1";

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("ranking_sha256")]
    public string RankingSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("master_angler_target_date_intents_sha256")]
    public string MasterAnglerTargetDateIntentsSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("candidate_membership")]
    public CurrentStageOneCollectionTeacherFrontier CandidateMembership { get; set; } =
        new();

    [JsonPropertyName("ordered_candidate_evaluations")]
    public CurrentCollectionTeacherCandidateEvaluation[] OrderedCandidateEvaluations
        { get; set; } = Array.Empty<CurrentCollectionTeacherCandidateEvaluation>();

    [JsonPropertyName("selected_candidate")]
    public CurrentCollectionTeacherSelectedCandidate? SelectedCandidate { get; set; }

    [JsonPropertyName("pairwise_preferences")]
    public CurrentCollectionTeacherPairwisePreference[] PairwisePreferences { get; set; } =
        Array.Empty<CurrentCollectionTeacherPairwisePreference>();

    [JsonPropertyName("compiled_plan")]
    public SmallModelPlanEnvelope? CompiledPlan { get; set; }

    [JsonPropertyName("compiled_queue")]
    public ActionQueueEnvelope? CompiledQueue { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("non_selected_candidate_semantics")]
    public string NonSelectedCandidateSemantics { get; set; } =
        "eligible_counterfactual_not_binary_negative";

    [JsonPropertyName("scope")]
    public string Scope { get; set; } =
        "one_current_state_four_collection_set_teacher_preference";
}

public sealed class CurrentCollectionTeacherCandidateEvaluation
{
    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("has_authoritative_current_day_deadline")]
    public bool HasAuthoritativeCurrentDayDeadline { get; set; }

    [JsonPropertyName("minimum_deadline_slack_days")]
    public int? MinimumDeadlineSlackDays { get; set; }

    [JsonPropertyName("earliest_action_deadline_time_exclusive")]
    public int? EarliestActionDeadlineTimeExclusive { get; set; }

    [JsonPropertyName("terminal_transition_credit_count")]
    public int TerminalTransitionCreditCount { get; set; }

    [JsonPropertyName("distinct_requirement_set_credit_count")]
    public int DistinctRequirementSetCreditCount { get; set; }

    [JsonPropertyName("exact_requirement_credit_count")]
    public int ExactRequirementCreditCount { get; set; }

    [JsonPropertyName("rarest_credited_selection_group_candidate_count")]
    public int RarestCreditedSelectionGroupCandidateCount { get; set; }

    [JsonPropertyName("estimated_ticks_known")]
    public bool EstimatedTicksKnown { get; set; }

    [JsonPropertyName("estimated_ticks")]
    public int? EstimatedTicks { get; set; }

    [JsonPropertyName("energy_cost")]
    public int EnergyCost { get; set; }

    [JsonPropertyName("selection_order")]
    public int SelectionOrder { get; set; }

    [JsonPropertyName("reasons")]
    public string[] Reasons { get; set; } = Array.Empty<string>();
}

public sealed record CurrentCollectionTeacherSelectedCandidate(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("location_id")] string LocationId,
    [property: JsonPropertyName("tile_x")] int? TileX,
    [property: JsonPropertyName("tile_y")] int? TileY,
    [property: JsonPropertyName("requirement_credits")] CurrentCollectionRequirementCredit[] RequirementCredits,
    [property: JsonPropertyName("selection_reason")] string SelectionReason);

public sealed record CurrentCollectionTeacherPairwisePreference(
    [property: JsonPropertyName("preferred_candidate_id")] string PreferredCandidateId,
    [property: JsonPropertyName("alternative_candidate_id")] string AlternativeCandidateId,
    [property: JsonPropertyName("first_differing_authoritative_criterion")] string FirstDifferingAuthoritativeCriterion,
    [property: JsonPropertyName("label_semantics")] string LabelSemantics);
