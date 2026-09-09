using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteOptionLoweringCatalog
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("routes")]
    public AcquisitionRouteOptionLoweringCatalogRow[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteOptionLoweringCatalogRow>();
}

public sealed class AcquisitionRouteOptionLoweringCatalogRow
{
    [JsonPropertyName("route_kind")]
    public string RouteKind { get; set; } = string.Empty;

    [JsonPropertyName("lowering_class")]
    public string LoweringClass { get; set; } = string.Empty;

    [JsonPropertyName("supervision_mode")]
    public string SupervisionMode { get; set; } = string.Empty;

    [JsonPropertyName("endpoint_option_ids")]
    public string[] EndpointOptionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("supporting_option_ids")]
    public string[] SupportingOptionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("uncertainty_mode")]
    public string UncertaintyMode { get; set; } = string.Empty;

    [JsonPropertyName("gap_id")]
    public string GapId { get; set; } = string.Empty;
}

public sealed class AcquisitionRouteOptionLoweringReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "acquisition_route_option_lowering.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("lowering_catalog_sha256")]
    public string LoweringCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("option_matrix_sha256")]
    public string OptionMatrixSha256 { get; set; } = string.Empty;

    [JsonPropertyName("isolated_training_authorization_sha256")]
    public string IsolatedTrainingAuthorizationSha256 { get; set; } = string.Empty;

    [JsonPropertyName("requirement_set_count")]
    public int RequirementSetCount { get; set; }

    [JsonPropertyName("requirement_group_count")]
    public int RequirementGroupCount { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("observed_route_kind_count")]
    public int ObservedRouteKindCount { get; set; }

    [JsonPropertyName("classified_route_kind_count")]
    public int ClassifiedRouteKindCount { get; set; }

    [JsonPropertyName("admitted_route_kind_count")]
    public int AdmittedRouteKindCount { get; set; }

    [JsonPropertyName("blocked_route_kind_count")]
    public int BlockedRouteKindCount { get; set; }

    [JsonPropertyName("unknown_route_kinds")]
    public string[] UnknownRouteKinds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("unobserved_catalog_route_kinds")]
    public string[] UnobservedCatalogRouteKinds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("route_kinds")]
    public AcquisitionRouteKindLowering[] RouteKinds { get; set; } =
        Array.Empty<AcquisitionRouteKindLowering>();

    [JsonPropertyName("requirement_sets")]
    public AcquisitionRequirementSetLowering[] RequirementSets { get; set; } =
        Array.Empty<AcquisitionRequirementSetLowering>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } = string.Empty;
}

public sealed record AcquisitionRouteKindLowering(
    [property: JsonPropertyName("route_kind")] string RouteKind,
    [property: JsonPropertyName("lowering_class")] string LoweringClass,
    [property: JsonPropertyName("supervision_mode")] string SupervisionMode,
    [property: JsonPropertyName("uncertainty_mode")] string UncertaintyMode,
    [property: JsonPropertyName("route_occurrence_count")] int RouteOccurrenceCount,
    [property: JsonPropertyName("requirement_group_count")] int RequirementGroupCount,
    [property: JsonPropertyName("requirement_set_ids")] string[] RequirementSetIds,
    [property: JsonPropertyName("endpoint_options")] AcquisitionLoweringOption[] EndpointOptions,
    [property: JsonPropertyName("supporting_options")] AcquisitionLoweringOption[] SupportingOptions,
    [property: JsonPropertyName("runtime_admission_ready")] bool RuntimeAdmissionReady,
    [property: JsonPropertyName("teacher_admission_ready")] bool TeacherAdmissionReady,
    [property: JsonPropertyName("emits_policy_label")] bool EmitsPolicyLabel,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);

public sealed record AcquisitionLoweringOption(
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("layer")] string Layer,
    [property: JsonPropertyName("training_eligibility")] string TrainingEligibility,
    [property: JsonPropertyName("runtime_status")] string RuntimeStatus,
    [property: JsonPropertyName("policy_training_candidate")] bool PolicyTrainingCandidate,
    [property: JsonPropertyName("isolated_teacher_authorized")] bool IsolatedTeacherAuthorized,
    [property: JsonPropertyName("training_exclusion_reasons")] string[] TrainingExclusionReasons);

public sealed record AcquisitionRequirementSetLowering(
    [property: JsonPropertyName("requirement_set_id")] string RequirementSetId,
    [property: JsonPropertyName("required_group_count")] int RequiredGroupCount,
    [property: JsonPropertyName("runtime_admitted_group_count")] int RuntimeAdmittedGroupCount,
    [property: JsonPropertyName("teacher_admitted_group_count")] int TeacherAdmittedGroupCount,
    [property: JsonPropertyName("groups")] AcquisitionRequirementGroupLowering[] Groups);

public sealed record AcquisitionRequirementGroupLowering(
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("required_alternative_count")] int RequiredAlternativeCount,
    [property: JsonPropertyName("runtime_admitted_alternative_count")] int RuntimeAdmittedAlternativeCount,
    [property: JsonPropertyName("teacher_admitted_alternative_count")] int TeacherAdmittedAlternativeCount,
    [property: JsonPropertyName("runtime_admission_ready")] bool RuntimeAdmissionReady,
    [property: JsonPropertyName("teacher_admission_ready")] bool TeacherAdmissionReady,
    [property: JsonPropertyName("blocked_route_kinds")] string[] BlockedRouteKinds);
