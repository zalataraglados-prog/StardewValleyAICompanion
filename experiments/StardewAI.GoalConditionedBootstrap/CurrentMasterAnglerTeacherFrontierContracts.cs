using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentMasterAnglerTeacherFrontier
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "current_master_angler_teacher_frontier.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("requirement_set_id")]
    public string RequirementSetId { get; set; } = "master_angler";

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

    [JsonPropertyName("target_date_intents_sha256")]
    public string TargetDateIntentsSha256 { get; set; } = string.Empty;

    [JsonPropertyName("window_index_sha256")]
    public string WindowIndexSha256 { get; set; } = string.Empty;

    [JsonPropertyName("opportunity_catalog_sha256")]
    public string OpportunityCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("route_timing_calibration_sha256")]
    public string RouteTimingCalibrationSha256 { get; set; } = string.Empty;

    [JsonPropertyName("required_group_count")]
    public int RequiredGroupCount { get; set; }

    [JsonPropertyName("observed_group_count")]
    public int ObservedGroupCount { get; set; }

    [JsonPropertyName("completed_group_count")]
    public int CompletedGroupCount { get; set; }

    [JsonPropertyName("missing_group_count")]
    public int MissingGroupCount { get; set; }

    [JsonPropertyName("current_intent_count")]
    public int CurrentIntentCount { get; set; }

    [JsonPropertyName("matched_missing_group_count")]
    public int MatchedMissingGroupCount { get; set; }

    [JsonPropertyName("current_candidate_binding_count")]
    public int CurrentCandidateBindingCount { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("emits_negative_labels_for_unavailable_routes")]
    public bool EmitsNegativeLabelsForUnavailableRoutes { get; set; }

    [JsonPropertyName("requirements")]
    public CurrentMasterAnglerRequirement[] Requirements { get; set; } =
        Array.Empty<CurrentMasterAnglerRequirement>();

    [JsonPropertyName("candidate_bindings")]
    public CurrentMasterAnglerCandidateBinding[] CandidateBindings { get; set; } =
        Array.Empty<CurrentMasterAnglerCandidateBinding>();

    [JsonPropertyName("rejected_intent_candidates")]
    public CurrentMasterAnglerCandidateRejection[] RejectedIntentCandidates { get; set; } =
        Array.Empty<CurrentMasterAnglerCandidateRejection>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A positive Master Angler candidate must target one exact missing species in the authoritative 72-species denominator, carry a hash-locked current-date window intent, pass the shared rolling route/time validator, and expose either a complete rod-fishing outcome domain, an exact collection-eligible ready crab-pot output, or the next deterministic route step. Missing or unavailable opportunities are deferred and never become negative labels.";
}

public sealed record CurrentMasterAnglerRequirement(
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("completed")] bool Completed,
    [property: JsonPropertyName("current_status")] string CurrentStatus,
    [property: JsonPropertyName("current_intent_ids")] string[] CurrentIntentIds,
    [property: JsonPropertyName("matched_candidate_ids")] string[] MatchedCandidateIds,
    [property: JsonPropertyName("admitted_endpoint_option_ids")] string[] AdmittedEndpointOptionIds,
    [property: JsonPropertyName("admitted_supporting_option_ids")] string[] AdmittedSupportingOptionIds,
    [property: JsonPropertyName("defer_reasons")] string[] DeferReasons);

public sealed record CurrentMasterAnglerCandidateBinding(
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("intent_id")] string IntentId,
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("binding_kind")] string BindingKind,
    [property: JsonPropertyName("target_location")] string TargetLocation,
    [property: JsonPropertyName("source_kind")] string SourceKind,
    [property: JsonPropertyName("source_key")] string SourceKey,
    [property: JsonPropertyName("effective_start_time")] int EffectiveStartTime,
    [property: JsonPropertyName("last_cast_time_exclusive")] int LastCastTimeExclusive,
    [property: JsonPropertyName("deadline_slack_days")] int DeadlineSlackDays,
    [property: JsonPropertyName("identity_evidence")] string IdentityEvidence);

public sealed record CurrentMasterAnglerCandidateRejection(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("reason")] string Reason);
