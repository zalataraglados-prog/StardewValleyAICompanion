using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentCollectionTeacherFrontier
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "current_collection_teacher_frontier.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("ranking_sha256")]
    public string RankingSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("emits_negative_labels_for_unavailable_routes")]
    public bool EmitsNegativeLabelsForUnavailableRoutes { get; set; }

    [JsonPropertyName("requirement_sets")]
    public CurrentCollectionRequirementSet[] RequirementSets { get; set; } =
        Array.Empty<CurrentCollectionRequirementSet>();

    [JsonPropertyName("candidate_bindings")]
    public CurrentCollectionCandidateBinding[] CandidateBindings { get; set; } =
        Array.Empty<CurrentCollectionCandidateBinding>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "Museum and standard Community Center labels require an exact live completion denominator. Direct completion requires an exact native projection. Acquisition labels require an exact qualified output, positive quantity, admitted authoritative endpoint, and verified quality whenever the requirement quality is above zero. OR bundle alternatives expose remaining-slot and reservation semantics; unavailable alternatives are deferred, never negative labels.";
}

public sealed class CurrentCollectionRequirementSet
{
    [JsonPropertyName("requirement_set_id")]
    public string RequirementSetId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("transparent_state_ready")]
    public bool TransparentStateReady { get; set; }

    [JsonPropertyName("required_group_count")]
    public int RequiredGroupCount { get; set; }

    [JsonPropertyName("observed_group_count")]
    public int ObservedGroupCount { get; set; }

    [JsonPropertyName("completed_group_count")]
    public int CompletedGroupCount { get; set; }

    [JsonPropertyName("missing_group_count")]
    public int MissingGroupCount { get; set; }

    [JsonPropertyName("matched_missing_group_count")]
    public int MatchedMissingGroupCount { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("requirements")]
    public CurrentCollectionRequirement[] Requirements { get; set; } =
        Array.Empty<CurrentCollectionRequirement>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class CurrentCollectionRequirement
{
    [JsonPropertyName("requirement_id")]
    public string RequirementId { get; set; } = string.Empty;

    [JsonPropertyName("selection_rule")]
    public string SelectionRule { get; set; } = string.Empty;

    [JsonPropertyName("required_alternative_count")]
    public int RequiredAlternativeCount { get; set; }

    [JsonPropertyName("completed_alternative_count")]
    public int? CompletedAlternativeCount { get; set; }

    [JsonPropertyName("remaining_slot_count")]
    public int? RemainingSlotCount { get; set; }

    [JsonPropertyName("completed")]
    public bool? Completed { get; set; }

    [JsonPropertyName("current_status")]
    public string CurrentStatus { get; set; } = string.Empty;

    [JsonPropertyName("alternatives")]
    public CurrentCollectionAlternative[] Alternatives { get; set; } =
        Array.Empty<CurrentCollectionAlternative>();

    [JsonPropertyName("defer_reasons")]
    public string[] DeferReasons { get; set; } = Array.Empty<string>();
}

public sealed record CurrentCollectionAlternative(
    [property: JsonPropertyName("alternative_index")] int AlternativeIndex,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("match_kind")] string MatchKind,
    [property: JsonPropertyName("required_quantity")] int RequiredQuantity,
    [property: JsonPropertyName("minimum_quality")] int MinimumQuality,
    [property: JsonPropertyName("completed")] bool? Completed,
    [property: JsonPropertyName("reservation_semantics")] string ReservationSemantics,
    [property: JsonPropertyName("matched_candidate_ids")] string[] MatchedCandidateIds,
    [property: JsonPropertyName("admitted_endpoint_option_ids")] string[] AdmittedEndpointOptionIds,
    [property: JsonPropertyName("admitted_supporting_option_ids")] string[] AdmittedSupportingOptionIds);

public sealed record CurrentCollectionCandidateBinding(
    [property: JsonPropertyName("requirement_set_id")] string RequirementSetId,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("alternative_index")] int AlternativeIndex,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("binding_kind")] string BindingKind,
    [property: JsonPropertyName("required_quantity")] int RequiredQuantity,
    [property: JsonPropertyName("minimum_quality")] int MinimumQuality,
    [property: JsonPropertyName("candidate_quantity")] int CandidateQuantity,
    [property: JsonPropertyName("candidate_quality")] int? CandidateQuality,
    [property: JsonPropertyName("remaining_slot_count")] int RemainingSlotCount,
    [property: JsonPropertyName("reservation_status")] string ReservationStatus,
    [property: JsonPropertyName("identity_evidence")] string IdentityEvidence,
    [property: JsonPropertyName("matched_routes")] CurrentRequirementRouteEvidence[] MatchedRoutes);
