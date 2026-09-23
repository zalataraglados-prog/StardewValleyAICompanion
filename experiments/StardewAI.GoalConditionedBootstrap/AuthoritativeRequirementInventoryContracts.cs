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

    [JsonPropertyName("community_center_denominator_catalog")]
    public CommunityCenterDenominatorCatalog CommunityCenterDenominatorCatalog { get; set; } = new();

    [JsonPropertyName("unresolved_acquisition_requirement_ids")]
    public string[] UnresolvedAcquisitionRequirementIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "Denominators are compiled from exact-version runtime exports and guarded decompiled native methods. Missing acquisition routes fail closed and cannot supervise the model.";
}

public sealed class CommunityCenterDenominatorCatalog
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "community_center_denominator_catalog.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("standard_active_bundle_count")]
    public int StandardActiveBundleCount { get; set; }

    [JsonPropertyName("supplemental_bundle_count")]
    public int SupplementalBundleCount { get; set; }

    [JsonPropertyName("remixed_area_count")]
    public int RemixedAreaCount { get; set; }

    [JsonPropertyName("remixed_key_count")]
    public int RemixedKeyCount { get; set; }

    [JsonPropertyName("remixed_template_count")]
    public int RemixedTemplateCount { get; set; }

    [JsonPropertyName("retained_standard_key_count")]
    public int RetainedStandardKeyCount { get; set; }

    [JsonPropertyName("ingredient_acquisition_identity_count")]
    public int IngredientAcquisitionIdentityCount { get; set; }

    [JsonPropertyName("ingredient_acquisition_target_count")]
    public int IngredientAcquisitionTargetCount { get; set; }

    [JsonPropertyName("standard_supported")]
    public bool StandardSupported { get; set; }

    [JsonPropertyName("remixed_supported")]
    public bool RemixedSupported { get; set; }

    [JsonPropertyName("active_key_topology_complete")]
    public bool ActiveKeyTopologyComplete { get; set; }

    [JsonPropertyName("ingredient_acquisition_catalog_complete")]
    public bool IngredientAcquisitionCatalogComplete { get; set; }

    [JsonPropertyName("live_denominator_authority")]
    public string LiveDenominatorAuthority { get; set; } =
        "NetWorldState.BundleData persisted by the active save";

    [JsonPropertyName("standard_active_bundle_keys")]
    public string[] StandardActiveBundleKeys { get; set; } = Array.Empty<string>();

    [JsonPropertyName("supplemental_bundle_keys")]
    public string[] SupplementalBundleKeys { get; set; } = Array.Empty<string>();

    [JsonPropertyName("retained_standard_bundle_keys")]
    public string[] RetainedStandardBundleKeys { get; set; } = Array.Empty<string>();

    [JsonPropertyName("standard_active_templates")]
    public CommunityCenterNativeBundleTemplate[] StandardActiveTemplates { get; set; } =
        Array.Empty<CommunityCenterNativeBundleTemplate>();

    [JsonPropertyName("supplemental_templates")]
    public CommunityCenterNativeBundleTemplate[] SupplementalTemplates { get; set; } =
        Array.Empty<CommunityCenterNativeBundleTemplate>();

    [JsonPropertyName("supplemental_area_names")]
    public string[] SupplementalAreaNames { get; set; } = Array.Empty<string>();

    [JsonPropertyName("remixed_areas")]
    public CommunityCenterRemixedAreaCatalog[] RemixedAreas { get; set; } =
        Array.Empty<CommunityCenterRemixedAreaCatalog>();

    [JsonPropertyName("ingredient_acquisition_catalog")]
    public CommunityCenterIngredientAcquisitionCatalogRow[]
        IngredientAcquisitionCatalog { get; set; } =
            Array.Empty<CommunityCenterIngredientAcquisitionCatalogRow>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A live standard denominator must exactly match the locked standard requirement set. A live remixed denominator must preserve the complete 30-key topology and match one whole-area native BundleGenerator configuration without reusing a pool template. Every possible standard/remixed ingredient identity binds at least one authoritative acquisition target; category ingredients retain every native accepted object and identify which targets have routes. The Abandoned Joja Mart bundle is supplemental and never contributes to Community Center completion.";
}

public sealed class CommunityCenterIngredientAcquisitionCatalogRow
{
    [JsonPropertyName("item_id_or_category")]
    public string ItemIdOrCategory { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("match_kind")]
    public string MatchKind { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_route_complete")]
    public bool AcquisitionRouteComplete { get; set; }

    [JsonPropertyName("targets")]
    public CommunityCenterIngredientAcquisitionTarget[] Targets { get; set; } =
        Array.Empty<CommunityCenterIngredientAcquisitionTarget>();
}

public sealed record CommunityCenterIngredientAcquisitionTarget(
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("route_covered")] bool RouteCovered,
    [property: JsonPropertyName("acquisition_routes")] RequirementAcquisitionRoute[]
        AcquisitionRoutes);

public sealed class CommunityCenterRemixedAreaCatalog
{
    [JsonPropertyName("area_name")]
    public string AreaName { get; set; } = string.Empty;

    [JsonPropertyName("key_ids")]
    public int[] KeyIds { get; set; } = Array.Empty<int>();

    [JsonPropertyName("bundle_sets")]
    public CommunityCenterRemixedBundleSet[] BundleSets { get; set; } =
        Array.Empty<CommunityCenterRemixedBundleSet>();

    [JsonPropertyName("pool_templates")]
    public CommunityCenterBundleTemplate[] PoolTemplates { get; set; } =
        Array.Empty<CommunityCenterBundleTemplate>();
}

public sealed class CommunityCenterNativeBundleTemplate
{
    [JsonPropertyName("bundle_data_key")]
    public string BundleDataKey { get; set; } = string.Empty;

    [JsonPropertyName("internal_name")]
    public string InternalName { get; set; } = string.Empty;

    [JsonPropertyName("required_item_count")]
    public int RequiredItemCount { get; set; }

    [JsonPropertyName("ingredients")]
    public CommunityCenterTemplateIngredient[] Ingredients { get; set; } =
        Array.Empty<CommunityCenterTemplateIngredient>();
}

public sealed class CommunityCenterRemixedBundleSet
{
    [JsonPropertyName("set_id")]
    public string SetId { get; set; } = string.Empty;

    [JsonPropertyName("templates")]
    public CommunityCenterBundleTemplate[] Templates { get; set; } =
        Array.Empty<CommunityCenterBundleTemplate>();
}

public sealed class CommunityCenterBundleTemplate
{
    [JsonPropertyName("template_id")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("internal_name")]
    public string InternalName { get; set; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("pick_count")]
    public int PickCount { get; set; }

    [JsonPropertyName("required_item_count")]
    public int RequiredItemCount { get; set; }

    [JsonPropertyName("ingredient_slots")]
    public CommunityCenterTemplateIngredientSlot[] IngredientSlots { get; set; } =
        Array.Empty<CommunityCenterTemplateIngredientSlot>();
}

public sealed class CommunityCenterTemplateIngredientSlot
{
    [JsonPropertyName("options")]
    public CommunityCenterTemplateIngredient[] Options { get; set; } =
        Array.Empty<CommunityCenterTemplateIngredient>();
}

public sealed record CommunityCenterTemplateIngredient(
    [property: JsonPropertyName("item_id_or_category")] string ItemIdOrCategory,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("match_kind")] string MatchKind,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("minimum_quality")] int MinimumQuality);

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
