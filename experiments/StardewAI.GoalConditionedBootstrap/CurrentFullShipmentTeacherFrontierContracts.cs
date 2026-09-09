using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentFullShipmentTeacherFrontier
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "current_full_shipment_teacher_frontier.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("requirement_set_id")]
    public string RequirementSetId { get; set; } = "full_shipment";

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

    [JsonPropertyName("required_group_count")]
    public int RequiredGroupCount { get; set; }

    [JsonPropertyName("completed_group_count")]
    public int CompletedGroupCount { get; set; }

    [JsonPropertyName("missing_group_count")]
    public int MissingGroupCount { get; set; }

    [JsonPropertyName("matched_missing_group_count")]
    public int MatchedMissingGroupCount { get; set; }

    [JsonPropertyName("current_candidate_binding_count")]
    public int CurrentCandidateBindingCount { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("emits_negative_labels_for_unavailable_routes")]
    public bool EmitsNegativeLabelsForUnavailableRoutes { get; set; }

    [JsonPropertyName("requirements")]
    public CurrentFullShipmentRequirement[] Requirements { get; set; } =
        Array.Empty<CurrentFullShipmentRequirement>();

    [JsonPropertyName("candidate_bindings")]
    public CurrentRequirementCandidateBinding[] CandidateBindings { get; set; } =
        Array.Empty<CurrentRequirementCandidateBinding>();

    [JsonPropertyName("limitations")]
    public string[] Limitations { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A positive Teacher label requires an exact live missing-item identity and either native Full Shipment contribution evidence or an exact item-producing candidate on an authoritative admitted acquisition endpoint. Absent current candidates are deferred, never negative labels.";
}

public sealed record CurrentFullShipmentRequirement(
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("completed")] bool Completed,
    [property: JsonPropertyName("current_status")] string CurrentStatus,
    [property: JsonPropertyName("matched_candidate_ids")] string[] MatchedCandidateIds,
    [property: JsonPropertyName("admitted_endpoint_option_ids")] string[] AdmittedEndpointOptionIds,
    [property: JsonPropertyName("admitted_supporting_option_ids")] string[] AdmittedSupportingOptionIds,
    [property: JsonPropertyName("defer_reasons")] string[] DeferReasons);

public sealed record CurrentRequirementCandidateBinding(
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("binding_kind")] string BindingKind,
    [property: JsonPropertyName("identity_evidence")] string IdentityEvidence,
    [property: JsonPropertyName("matched_routes")] CurrentRequirementRouteEvidence[] MatchedRoutes);

public sealed record CurrentRequirementRouteEvidence(
    [property: JsonPropertyName("route_kind")] string RouteKind,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_asset")] string SourceAsset,
    [property: JsonPropertyName("source_path")] string SourcePath);
