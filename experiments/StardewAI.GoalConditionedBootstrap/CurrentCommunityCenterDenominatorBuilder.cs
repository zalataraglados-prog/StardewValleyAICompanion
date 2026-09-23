using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentCommunityCenterDenominatorBuilder
{
    private const string StandardSetId = "community_center_standard";

    public static CurrentCommunityCenterDenominatorReport Build(
        string requirementInventoryPath,
        string snapshotPath)
    {
        var inventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            inventoryFullPath,
            "Authoritative requirement inventory");
        ValidateCatalog(inventory);

        using var snapshot = JsonDocument.Parse(File.ReadAllText(snapshotFullPath));
        var root = snapshot.RootElement;
        var stateHash = CurrentTeacherFrontierSupport.RequiredString(
            root,
            "state_hash");
        var snapshotGameVersion = CurrentTeacherFrontierSupport.RequiredString(
            root,
            "game_version");
        if (!string.Equals(snapshotGameVersion, inventory.GameVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Live snapshot game_version does not match the requirement inventory.");
        }

        var catalog = inventory.CommunityCenterDenominatorCatalog;
        var progress = ReadProgressValue(root);
        var rows = ReadRows(progress, catalog);
        var activeRows = catalog.StandardActiveBundleKeys
            .Select(key => rows[key])
            .ToArray();
        var supplementalRows = catalog.SupplementalBundleKeys
            .Select(key => rows[key])
            .ToArray();

        string mode;
        CurrentCommunityCenterBundle[] active;
        if (TryBindStandard(activeRows, catalog, out active))
        {
            mode = "standard";
        }
        else if (TryBindRemixed(activeRows, catalog, out active))
        {
            mode = "remixed";
        }
        else
        {
            throw new InvalidDataException(
                "Live Community Center BundleData is neither the locked standard denominator nor a native remixed realization.");
        }

        var supplementalTemplates = catalog.SupplementalTemplates.ToDictionary(
            value => value.BundleDataKey,
            StringComparer.Ordinal);
        var supplemental = supplementalRows
            .Select(row =>
            {
                if (!supplementalTemplates.TryGetValue(row.BundleDataKey,
                        out var template) ||
                    !MatchesNativeTemplate(row, template))
                {
                    throw new InvalidDataException(
                        "A supplemental Community Center bundle differs from Data/Bundles.");
                }
                return Normalize(
                    row,
                    "community_center:supplemental_bundle:" + row.BundleDataKey,
                    "standard-supplemental:" + row.BundleDataKey,
                    string.Empty,
                    catalog);
            })
            .OrderBy(value => value.BundleDataKey, StringComparer.Ordinal)
            .ToArray();
        active = active
            .OrderBy(value => value.BundleDataKey, StringComparer.Ordinal)
            .ToArray();

        return new CurrentCommunityCenterDenominatorReport
        {
            Status = "ready",
            GoalId = inventory.GoalId,
            GameVersion = inventory.GameVersion,
            SourceStateHash = stateHash,
            RequirementInventorySha256 = CurrentTeacherFrontierSupport.HashFile(
                inventoryFullPath),
            SnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            DenominatorSha256 = ComputeDenominatorSha256(mode, active),
            IngredientAcquisitionCatalogComplete =
                active.SelectMany(value => value.Ingredients)
                    .All(value => value.AcquisitionTargets.Any(target =>
                        target.RouteCovered)),
            BundleMode = mode,
            ActiveBundleCount = active.Length,
            CompletedActiveBundleCount = active.Count(value => value.Complete),
            SupplementalBundleCount = supplemental.Length,
            CompletedSupplementalBundleCount = supplemental.Count(value => value.Complete),
            ActiveBundles = active,
            SupplementalBundles = supplemental
        };
    }

    private static void ValidateCatalog(
        AuthoritativeRequirementInventoryReport inventory)
    {
        var catalog = inventory.CommunityCenterDenominatorCatalog;
        if (!inventory.DenominatorComplete ||
            !string.Equals(catalog.Status,
                "complete_standard_and_remixed_catalog",
                StringComparison.Ordinal) ||
            !catalog.StandardSupported ||
            !catalog.RemixedSupported ||
            !catalog.ActiveKeyTopologyComplete ||
            !catalog.IngredientAcquisitionCatalogComplete ||
            catalog.StandardActiveBundleCount != 30 ||
            catalog.StandardActiveBundleKeys.Length !=
                catalog.StandardActiveBundleCount ||
            catalog.StandardActiveTemplates.Length !=
                catalog.StandardActiveBundleCount ||
            catalog.SupplementalBundleKeys.Length !=
                catalog.SupplementalBundleCount ||
            catalog.SupplementalTemplates.Length !=
                catalog.SupplementalBundleCount ||
            catalog.RetainedStandardBundleKeys.Length !=
                catalog.RetainedStandardKeyCount ||
            catalog.IngredientAcquisitionCatalog.Length !=
                catalog.IngredientAcquisitionIdentityCount ||
            catalog.IngredientAcquisitionCatalog.Sum(value =>
                value.Targets.Length) != catalog.IngredientAcquisitionTargetCount ||
            catalog.RemixedAreas.Length != catalog.RemixedAreaCount ||
            catalog.RemixedAreas.Sum(area => area.KeyIds.Length) !=
                catalog.RemixedKeyCount)
        {
            throw new InvalidDataException(
                "Community Center denominator catalog is incomplete.");
        }
        RequireUnique(catalog.StandardActiveBundleKeys, "standard active bundle keys");
        RequireUnique(catalog.SupplementalBundleKeys, "supplemental bundle keys");
        RequireUnique(catalog.RetainedStandardBundleKeys,
            "retained standard bundle keys");
        RequireUnique(
            catalog.IngredientAcquisitionCatalog.Select(value =>
                value.MatchKind + ":" + value.ItemIdOrCategory),
            "ingredient acquisition identities");
        foreach (var row in catalog.IngredientAcquisitionCatalog)
        {
            if (!row.AcquisitionRouteComplete || row.Targets.Length == 0 ||
                !row.Targets.Any(value => value.RouteCovered) ||
                row.Targets.Any(value => string.IsNullOrWhiteSpace(value.ItemId) ||
                    string.IsNullOrWhiteSpace(value.QualifiedItemId) &&
                    row.MatchKind != "money_payment" ||
                    value.RouteCovered != (value.AcquisitionRoutes.Length > 0)))
            {
                throw new InvalidDataException(
                    "A Community Center ingredient acquisition row is incomplete: " +
                    row.ItemIdOrCategory);
            }
        }

        var standard = CurrentTeacherFrontierSupport.SingleSet(
            inventory.RequirementSets,
            value => value.RequirementSetId,
            StandardSetId,
            "requirement inventory");
        var groupByKey = standard.Groups.ToDictionary(
            value => RequirementBundleKey(value.RequirementId),
            StringComparer.Ordinal);
        var templateByKey = catalog.StandardActiveTemplates.ToDictionary(
            value => value.BundleDataKey,
            StringComparer.Ordinal);
        if (standard.RequiredGroupCount != catalog.StandardActiveBundleCount ||
            standard.Groups.Length != catalog.StandardActiveBundleCount ||
            !groupByKey.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(catalog.StandardActiveBundleKeys) ||
            !templateByKey.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(catalog.StandardActiveBundleKeys))
        {
            throw new InvalidDataException(
                "Standard Community Center requirements and denominator catalog diverge.");
        }
        foreach (var key in catalog.StandardActiveBundleKeys)
        {
            var group = groupByKey[key];
            var template = templateByKey[key];
            if (group.RequiredAlternativeCount != template.RequiredItemCount ||
                group.Alternatives.Length != template.Ingredients.Length ||
                group.Alternatives.Zip(template.Ingredients)
                    .Any(pair => pair.First.ItemId != pair.Second.ItemIdOrCategory ||
                        pair.First.Amount != pair.Second.Amount ||
                        pair.First.MinimumQuality != pair.Second.MinimumQuality ||
                        pair.First.MatchKind != pair.Second.MatchKind))
            {
                throw new InvalidDataException(
                    "A standard Community Center template differs from its requirement group: " +
                    key);
            }
        }
    }

    private static JsonElement ReadProgressValue(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("state", out var state) ||
            state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("world_progress", out var world) ||
            world.ValueKind != JsonValueKind.Object ||
            !world.TryGetProperty("community_center", out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not ("available" or "derived") ||
            !field.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Live Community Center transparent state is unavailable.");
        }
        return value;
    }

    private static Dictionary<string, LiveBundleRow> ReadRows(
        JsonElement progress,
        CommunityCenterDenominatorCatalog catalog)
    {
        if (!progress.TryGetProperty("bundle_rows", out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Live Community Center bundle_rows is unavailable.");
        }
        var expectedKeys = catalog.StandardActiveBundleKeys
            .Concat(catalog.SupplementalBundleKeys)
            .ToHashSet(StringComparer.Ordinal);
        var dataCount = RequiredInt(progress, "bundle_data_row_count");
        var projectedCount = RequiredInt(progress, "projected_bundle_row_count");
        var unavailableCount = RequiredInt(progress, "unavailable_bundle_row_count");
        var reportedComplete = RequiredInt(progress, "complete_bundle_count");
        if (dataCount != expectedKeys.Count ||
            projectedCount != dataCount ||
            values.GetArrayLength() != dataCount ||
            unavailableCount != 0)
        {
            throw new InvalidDataException(
                "Live Community Center bundle projection is incomplete.");
        }

        var rows = new Dictionary<string, LiveBundleRow>(StringComparer.Ordinal);
        foreach (var value in values.EnumerateArray())
        {
            var row = ParseRow(value);
            if (!expectedKeys.Contains(row.BundleDataKey) ||
                !rows.TryAdd(row.BundleDataKey, row))
            {
                throw new InvalidDataException(
                    "Live Community Center rows contain an unknown or duplicate key.");
            }
        }
        if (!rows.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedKeys) ||
            rows.Values.Count(value => value.Complete) != reportedComplete)
        {
            throw new InvalidDataException(
                "Live Community Center aggregate counts disagree with exact bundle rows.");
        }
        return rows;
    }

    private static LiveBundleRow ParseRow(JsonElement value)
    {
        if (!string.Equals(CurrentTeacherFrontierSupport.RequiredString(
                value,
                "projection_status"),
                "exact",
                StringComparison.Ordinal) ||
            !value.TryGetProperty("ingredients", out var ingredientValues) ||
            ingredientValues.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "A live Community Center bundle is not an exact projection.");
        }
        var key = CurrentTeacherFrontierSupport.RequiredString(
            value,
            "bundle_data_key");
        var separator = key.LastIndexOf('/');
        var areaName = CurrentTeacherFrontierSupport.RequiredString(
            value,
            "area_name");
        var bundleId = RequiredInt(value, "bundle_id");
        if (separator <= 0 ||
            !string.Equals(key[..separator], areaName, StringComparison.Ordinal) ||
            !int.TryParse(key[(separator + 1)..], out var keyBundleId) ||
            keyBundleId != bundleId)
        {
            throw new InvalidDataException(
                "A live Community Center bundle key, area, and ID disagree.");
        }

        var ingredients = ingredientValues.EnumerateArray()
            .Select((ingredient, index) =>
            {
                if (RequiredInt(ingredient, "ingredient_index") != index)
                {
                    throw new InvalidDataException(
                        "A live Community Center ingredient index is non-contiguous.");
                }
                var stack = RequiredInt(ingredient, "required_stack");
                var quality = RequiredInt(ingredient, "minimum_quality");
                if (stack <= 0 || quality < 0)
                {
                    throw new InvalidDataException(
                        "A live Community Center ingredient quantity or quality is invalid.");
                }
                return new CurrentCommunityCenterIngredient(
                    index,
                    CurrentTeacherFrontierSupport.RequiredString(
                        ingredient,
                        "item_id_or_category"),
                    string.Empty,
                    string.Empty,
                    stack,
                    quality,
                    RequiredBool(ingredient, "completed"),
                    Array.Empty<CommunityCenterIngredientAcquisitionTarget>());
            })
            .ToArray();
        var required = RequiredInt(value, "required_slot_count");
        var reportedCompleted = RequiredInt(value, "completed_ingredient_count");
        var complete = RequiredBool(value, "complete");
        var actualCompleted = ingredients.Count(ingredient => ingredient.Completed);
        if (ingredients.Length == 0 || required <= 0 ||
            required > ingredients.Length || reportedCompleted != actualCompleted ||
            complete != (actualCompleted >= required))
        {
            throw new InvalidDataException(
                "A live Community Center bundle aggregate disagrees with its ingredients.");
        }
        return new LiveBundleRow(
            key,
            areaName,
            bundleId,
            CurrentTeacherFrontierSupport.RequiredString(value, "internal_name"),
            required,
            actualCompleted,
            complete,
            ingredients);
    }

    private static bool TryBindStandard(
        LiveBundleRow[] rows,
        CommunityCenterDenominatorCatalog catalog,
        out CurrentCommunityCenterBundle[] bundles)
    {
        var templates = catalog.StandardActiveTemplates.ToDictionary(
            value => value.BundleDataKey,
            StringComparer.Ordinal);
        if (rows.Any(row => !templates.TryGetValue(row.BundleDataKey,
                out var template) || !MatchesNativeTemplate(row, template)))
        {
            bundles = Array.Empty<CurrentCommunityCenterBundle>();
            return false;
        }
        bundles = rows.Select(row => Normalize(
                row,
                "community_center:bundle:" + row.BundleDataKey,
                "standard:" + row.BundleDataKey,
                string.Empty,
                catalog))
            .ToArray();
        return true;
    }

    private static bool MatchesNativeTemplate(
        LiveBundleRow row,
        CommunityCenterNativeBundleTemplate template) =>
        string.Equals(row.BundleDataKey, template.BundleDataKey,
            StringComparison.Ordinal) &&
        string.Equals(row.InternalName, template.InternalName,
            StringComparison.Ordinal) &&
        row.RequiredSlotCount == template.RequiredItemCount &&
        row.Ingredients.Length == template.Ingredients.Length &&
        row.Ingredients.Zip(template.Ingredients).All(pair =>
            MatchesIngredient(pair.First, pair.Second));

    private static bool MatchesIngredient(
        CurrentCommunityCenterIngredient actual,
        CommunityCenterTemplateIngredient expected) =>
        string.Equals(actual.ItemIdOrCategory, expected.ItemIdOrCategory,
            StringComparison.Ordinal) &&
        actual.RequiredStack == expected.Amount &&
        actual.MinimumQuality == expected.MinimumQuality;

    private static CurrentCommunityCenterBundle Normalize(
        LiveBundleRow row,
        string requirementId,
        string templateId,
        string bundleSetId,
        CommunityCenterDenominatorCatalog catalog)
    {
        var acquisition = catalog.IngredientAcquisitionCatalog.ToDictionary(
            value => value.MatchKind + ":" + value.ItemIdOrCategory,
            StringComparer.Ordinal);
        var ingredients = row.Ingredients.Select(value =>
        {
            var matchKind = value.ItemIdOrCategory == "-1"
                ? "money_payment"
                : value.ItemIdOrCategory.StartsWith("-", StringComparison.Ordinal)
                    ? "category"
                    : "item_id";
            if (!acquisition.TryGetValue(
                    matchKind + ":" + value.ItemIdOrCategory,
                    out var binding) ||
                !binding.AcquisitionRouteComplete ||
                !binding.Targets.Any(target => target.RouteCovered))
            {
                throw new InvalidDataException(
                    "A live Community Center ingredient has no authoritative acquisition binding: " +
                    value.ItemIdOrCategory);
            }
            return new CurrentCommunityCenterIngredient(
                value.IngredientIndex,
                value.ItemIdOrCategory,
                binding.QualifiedItemId,
                binding.MatchKind,
                value.RequiredStack,
                value.MinimumQuality,
                value.Completed,
                binding.Targets);
        }).ToArray();
        return new CurrentCommunityCenterBundle
        {
            RequirementId = requirementId,
            BundleDataKey = row.BundleDataKey,
            AreaName = row.AreaName,
            BundleId = row.BundleId,
            InternalName = row.InternalName,
            SourceTemplateId = templateId,
            SourceBundleSetId = bundleSetId,
            RequiredSlotCount = row.RequiredSlotCount,
            CompletedIngredientCount = row.CompletedIngredientCount,
            Complete = row.Complete,
            Ingredients = ingredients
        };
    }

    internal static string ComputeDenominatorSha256(
        string mode,
        CurrentCommunityCenterBundle[] bundles)
    {
        var value = JsonSerializer.Serialize(new
        {
            bundle_mode = mode,
            bundles = bundles.Select(bundle => new
            {
                bundle_data_key = bundle.BundleDataKey,
                area_name = bundle.AreaName,
                bundle_id = bundle.BundleId,
                internal_name = bundle.InternalName,
                source_template_id = bundle.SourceTemplateId,
                source_bundle_set_id = bundle.SourceBundleSetId,
                required_slot_count = bundle.RequiredSlotCount,
                ingredients = bundle.Ingredients.Select(ingredient => new
                {
                    ingredient_index = ingredient.IngredientIndex,
                    item_id_or_category = ingredient.ItemIdOrCategory,
                    qualified_item_id = ingredient.QualifiedItemId,
                    match_kind = ingredient.MatchKind,
                    required_stack = ingredient.RequiredStack,
                    minimum_quality = ingredient.MinimumQuality,
                    acquisition_targets = ingredient.AcquisitionTargets.Select(
                        target => new
                        {
                            item_id = target.ItemId,
                            qualified_item_id = target.QualifiedItemId,
                            display_name = target.DisplayName,
                            route_covered = target.RouteCovered,
                            acquisition_routes = target.AcquisitionRoutes
                        })
                })
            })
        }, JsonDefaults.Compact);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static int RequiredInt(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) ||
            result.ValueKind != JsonValueKind.Number ||
            !result.TryGetInt32(out var number))
        {
            throw new InvalidDataException(
                "Snapshot integer property is missing: " + property);
        }
        return number;
    }

    private static bool RequiredBool(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) ||
            result.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException(
                "Snapshot boolean property is missing: " + property);
        }
        return result.GetBoolean();
    }

    private static string RequirementBundleKey(string requirementId)
    {
        const string prefix = "community_center:bundle:";
        if (!requirementId.StartsWith(prefix, StringComparison.Ordinal) ||
            requirementId.Length == prefix.Length)
        {
            throw new InvalidDataException(
                "A Community Center requirement ID has no bundle key.");
        }
        return requirementId[prefix.Length..];
    }

    private static void RequireUnique(IEnumerable<string> values, string label)
    {
        var array = values.ToArray();
        if (array.Any(string.IsNullOrWhiteSpace) ||
            array.Distinct(StringComparer.Ordinal).Count() != array.Length)
        {
            throw new InvalidDataException(
                "Community Center catalog contains invalid or duplicate " + label + ".");
        }
    }

    private sealed record LiveBundleRow(
        string BundleDataKey,
        string AreaName,
        int BundleId,
        string InternalName,
        int RequiredSlotCount,
        int CompletedIngredientCount,
        bool Complete,
        CurrentCommunityCenterIngredient[] Ingredients);
}
