using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AuthoritativeRequirementInventoryReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "authoritative_goal_requirement_inventory.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("denominator_complete")]
    public bool DenominatorComplete { get; set; }

    [JsonPropertyName("acquisition_routes_complete")]
    public bool AcquisitionRoutesComplete { get; set; }

    [JsonPropertyName("source_evidence")]
    public RequirementSourceEvidence[] SourceEvidence { get; set; } = Array.Empty<RequirementSourceEvidence>();

    [JsonPropertyName("requirement_sets")]
    public GoalRequirementSet[] RequirementSets { get; set; } = Array.Empty<GoalRequirementSet>();

    [JsonPropertyName("unresolved_acquisition_requirement_ids")]
    public string[] UnresolvedAcquisitionRequirementIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "Denominators are compiled from exact-version runtime exports and guarded decompiled native methods. Missing acquisition routes fail closed and cannot supervise the model.";
}

public sealed record RequirementSourceEvidence(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("authority")] string Authority);

public sealed class GoalRequirementSet
{
    [JsonPropertyName("requirement_set_id")]
    public string RequirementSetId { get; set; } = string.Empty;

    [JsonPropertyName("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [JsonPropertyName("native_completion_rule")]
    public string NativeCompletionRule { get; set; } = string.Empty;

    [JsonPropertyName("transparent_state_path")]
    public string TransparentStatePath { get; set; } = string.Empty;

    [JsonPropertyName("denominator_status")]
    public string DenominatorStatus { get; set; } = string.Empty;

    [JsonPropertyName("required_group_count")]
    public int RequiredGroupCount { get; set; }

    [JsonPropertyName("route_covered_group_count")]
    public int RouteCoveredGroupCount { get; set; }

    [JsonPropertyName("acquisition_routes_complete")]
    public bool AcquisitionRoutesComplete { get; set; }

    [JsonPropertyName("groups")]
    public GoalRequirementGroup[] Groups { get; set; } = Array.Empty<GoalRequirementGroup>();
}

public sealed class GoalRequirementGroup
{
    [JsonPropertyName("requirement_id")]
    public string RequirementId { get; set; } = string.Empty;

    [JsonPropertyName("selection_rule")]
    public string SelectionRule { get; set; } = string.Empty;

    [JsonPropertyName("required_alternative_count")]
    public int RequiredAlternativeCount { get; set; }

    [JsonPropertyName("transparent_completion_path")]
    public string TransparentCompletionPath { get; set; } = string.Empty;

    [JsonPropertyName("route_covered")]
    public bool RouteCovered { get; set; }

    [JsonPropertyName("alternatives")]
    public GoalRequirementAlternative[] Alternatives { get; set; } = Array.Empty<GoalRequirementAlternative>();
}

public sealed class GoalRequirementAlternative
{
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("match_kind")]
    public string MatchKind { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("minimum_quality")]
    public int MinimumQuality { get; set; }

    [JsonPropertyName("acquisition_routes")]
    public RequirementAcquisitionRoute[] AcquisitionRoutes { get; set; } = Array.Empty<RequirementAcquisitionRoute>();
}

public sealed record RequirementAcquisitionRoute(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_asset")] string SourceAsset,
    [property: JsonPropertyName("source_path")] string SourcePath);
