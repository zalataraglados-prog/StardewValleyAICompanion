using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CurrentCommunityCenterDenominatorReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "current_community_center_denominator.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("denominator_sha256")]
    public string DenominatorSha256 { get; set; } = string.Empty;

    [JsonPropertyName("ingredient_acquisition_catalog_complete")]
    public bool IngredientAcquisitionCatalogComplete { get; set; }

    [JsonPropertyName("bundle_mode")]
    public string BundleMode { get; set; } = string.Empty;

    [JsonPropertyName("active_bundle_count")]
    public int ActiveBundleCount { get; set; }

    [JsonPropertyName("completed_active_bundle_count")]
    public int CompletedActiveBundleCount { get; set; }

    [JsonPropertyName("supplemental_bundle_count")]
    public int SupplementalBundleCount { get; set; }

    [JsonPropertyName("completed_supplemental_bundle_count")]
    public int CompletedSupplementalBundleCount { get; set; }

    [JsonPropertyName("active_bundles")]
    public CurrentCommunityCenterBundle[] ActiveBundles { get; set; } =
        Array.Empty<CurrentCommunityCenterBundle>();

    [JsonPropertyName("supplemental_bundles")]
    public CurrentCommunityCenterBundle[] SupplementalBundles { get; set; } =
        Array.Empty<CurrentCommunityCenterBundle>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "Only an exact live 1.6.15 standard denominator or a whole-area native remixed BundleGenerator realization is admitted. Every active ingredient binds the authoritative acquisition targets for its exact item or native category. Supplemental bundles are reported separately and never contribute to Community Center completion.";
}

public sealed class CurrentCommunityCenterBundle
{
    [JsonPropertyName("requirement_id")]
    public string RequirementId { get; set; } = string.Empty;

    [JsonPropertyName("bundle_data_key")]
    public string BundleDataKey { get; set; } = string.Empty;

    [JsonPropertyName("area_name")]
    public string AreaName { get; set; } = string.Empty;

    [JsonPropertyName("bundle_id")]
    public int BundleId { get; set; }

    [JsonPropertyName("internal_name")]
    public string InternalName { get; set; } = string.Empty;

    [JsonPropertyName("source_template_id")]
    public string SourceTemplateId { get; set; } = string.Empty;

    [JsonPropertyName("source_bundle_set_id")]
    public string SourceBundleSetId { get; set; } = string.Empty;

    [JsonPropertyName("required_slot_count")]
    public int RequiredSlotCount { get; set; }

    [JsonPropertyName("completed_ingredient_count")]
    public int CompletedIngredientCount { get; set; }

    [JsonPropertyName("complete")]
    public bool Complete { get; set; }

    [JsonPropertyName("ingredients")]
    public CurrentCommunityCenterIngredient[] Ingredients { get; set; } =
        Array.Empty<CurrentCommunityCenterIngredient>();
}

public sealed record CurrentCommunityCenterIngredient(
    [property: JsonPropertyName("ingredient_index")] int IngredientIndex,
    [property: JsonPropertyName("item_id_or_category")] string ItemIdOrCategory,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("match_kind")] string MatchKind,
    [property: JsonPropertyName("required_stack")] int RequiredStack,
    [property: JsonPropertyName("minimum_quality")] int MinimumQuality,
    [property: JsonPropertyName("completed")] bool Completed,
    [property: JsonPropertyName("acquisition_targets")]
        CommunityCenterIngredientAcquisitionTarget[] AcquisitionTargets);
