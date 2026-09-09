using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentStageOneCollectionTeacherFrontier
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "current_stage_one_collection_teacher_frontier.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("requirement_set_count")]
    public int RequirementSetCount { get; set; }

    [JsonPropertyName("current_candidate_membership_eligible")]
    public bool CurrentCandidateMembershipEligible { get; set; }

    [JsonPropertyName("teacher_preference_label_eligible")]
    public bool TeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("uses_learner_rank_or_score")]
    public bool UsesLearnerRankOrScore { get; set; }

    [JsonPropertyName("emits_negative_labels_for_unavailable_routes")]
    public bool EmitsNegativeLabelsForUnavailableRoutes { get; set; }

    [JsonPropertyName("selection_contract")]
    public CurrentCollectionCandidateSelectionContract SelectionContract { get; set; } = new();

    [JsonPropertyName("full_shipment")]
    public CurrentFullShipmentTeacherFrontier FullShipment { get; set; } = new();

    [JsonPropertyName("museum_and_community_center")]
    public CurrentCollectionTeacherFrontier MuseumAndCommunityCenter { get; set; } = new();

    [JsonPropertyName("master_angler")]
    public CurrentMasterAnglerTeacherFrontier MasterAngler { get; set; } = new();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The four Stage 1 collection sets share one exact current-candidate membership contract. One candidate executes once and receives every exact requirement credit it advances. Bundle alternatives remain mutually exclusive within their remaining slots. Learner rank, score, model score and expected reward are excluded from this contract; a separate deterministic Teacher preference step is still required before preference labels may be emitted.";
}

public sealed class CurrentCollectionCandidateSelectionContract
{
    [JsonPropertyName("contract_id")]
    public string ContractId { get; set; } =
        "stage_one_collection_exact_current_candidate_union.v1";

    [JsonPropertyName("required_requirement_set_ids")]
    public string[] RequiredRequirementSetIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("maximum_selected_candidate_count_per_decision")]
    public int MaximumSelectedCandidateCountPerDecision { get; set; } = 1;

    [JsonPropertyName("shared_candidate_execution_semantics")]
    public string SharedCandidateExecutionSemantics { get; set; } =
        "execute_once_and_credit_all_exact_bindings";

    [JsonPropertyName("unavailable_candidate_semantics")]
    public string UnavailableCandidateSemantics { get; set; } =
        "defer_without_negative_label";

    [JsonPropertyName("selection_groups")]
    public CurrentCollectionSelectionGroup[] SelectionGroups { get; set; } =
        Array.Empty<CurrentCollectionSelectionGroup>();

    [JsonPropertyName("candidate_choices")]
    public CurrentCollectionCandidateChoice[] CandidateChoices { get; set; } =
        Array.Empty<CurrentCollectionCandidateChoice>();
}

public sealed record CurrentCollectionSelectionGroup(
    [property: JsonPropertyName("selection_group_id")] string SelectionGroupId,
    [property: JsonPropertyName("requirement_set_id")] string RequirementSetId,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("selection_rule")] string SelectionRule,
    [property: JsonPropertyName("current_status")] string CurrentStatus,
    [property: JsonPropertyName("remaining_completion_slot_count")] int? RemainingCompletionSlotCount,
    [property: JsonPropertyName("maximum_selected_candidate_count_this_decision")] int MaximumSelectedCandidateCountThisDecision,
    [property: JsonPropertyName("candidate_ids")] string[] CandidateIds);

public sealed record CurrentCollectionCandidateChoice(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("execution_semantics")] string ExecutionSemantics,
    [property: JsonPropertyName("requirement_credits")] CurrentCollectionRequirementCredit[] RequirementCredits);

public sealed record CurrentCollectionRequirementCredit(
    [property: JsonPropertyName("requirement_set_id")] string RequirementSetId,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("alternative_index")] int AlternativeIndex,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("binding_kind")] string BindingKind,
    [property: JsonPropertyName("required_quantity")] int RequiredQuantity,
    [property: JsonPropertyName("minimum_quality")] int MinimumQuality,
    [property: JsonPropertyName("reservation_semantics")] string ReservationSemantics);
