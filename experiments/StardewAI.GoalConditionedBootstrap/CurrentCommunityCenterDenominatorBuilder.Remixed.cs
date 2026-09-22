namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentCommunityCenterDenominatorBuilder
{
    private static bool TryBindRemixed(
        LiveBundleRow[] rows,
        CommunityCenterDenominatorCatalog catalog,
        out CurrentCommunityCenterBundle[] bundles)
    {
        var byKey = rows.ToDictionary(
            value => value.BundleDataKey,
            StringComparer.Ordinal);
        var standardTemplates = catalog.StandardActiveTemplates.ToDictionary(
            value => value.BundleDataKey,
            StringComparer.Ordinal);
        var result = new List<CurrentCommunityCenterBundle>();
        foreach (var key in catalog.RetainedStandardBundleKeys)
        {
            if (!byKey.TryGetValue(key, out var row) ||
                !standardTemplates.TryGetValue(key, out var template) ||
                !MatchesNativeTemplate(row, template))
            {
                bundles = Array.Empty<CurrentCommunityCenterBundle>();
                return false;
            }
            result.Add(Normalize(
                row,
                "community_center:bundle:" + key,
                "standard-retained:" + key,
                string.Empty));
        }

        foreach (var area in catalog.RemixedAreas
                     .OrderBy(value => value.AreaName, StringComparer.Ordinal))
        {
            var areaRows = area.KeyIds
                .Select(keyId => byKey.GetValueOrDefault(
                    area.AreaName + "/" + keyId))
                .ToArray();
            if (areaRows.Any(row => row is null))
            {
                bundles = Array.Empty<CurrentCommunityCenterBundle>();
                return false;
            }
            var exactAreaRows = areaRows.Select(row => row!).ToArray();
            if (!TryBindRemixedArea(
                    exactAreaRows,
                    area,
                    out var areaBindings,
                    out var bundleSetId))
            {
                bundles = Array.Empty<CurrentCommunityCenterBundle>();
                return false;
            }
            result.AddRange(areaBindings.Select(binding => Normalize(
                binding.Row,
                "community_center:bundle:" + binding.Row.BundleDataKey,
                binding.Template.TemplateId,
                bundleSetId)));
        }

        if (result.Count != catalog.StandardActiveBundleCount ||
            !result.Select(value => value.BundleDataKey)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(catalog.StandardActiveBundleKeys))
        {
            bundles = Array.Empty<CurrentCommunityCenterBundle>();
            return false;
        }
        bundles = result.ToArray();
        return true;
    }

    private static bool TryBindRemixedArea(
        LiveBundleRow[] rows,
        CommunityCenterRemixedAreaCatalog area,
        out RemixedRowBinding[] bindings,
        out string bundleSetId)
    {
        var setCandidates = area.BundleSets.Length == 0
            ? new CommunityCenterRemixedBundleSet?[] { null }
            : area.BundleSets.Cast<CommunityCenterRemixedBundleSet?>().ToArray();
        foreach (var set in setCandidates)
        {
            var assigned = new CommunityCenterBundleTemplate?[rows.Length];
            var setValid = true;
            foreach (var template in set?.Templates ??
                         Array.Empty<CommunityCenterBundleTemplate>())
            {
                if (template.Index < 0 || template.Index >= rows.Length ||
                    assigned[template.Index] is not null ||
                    !MatchesRemixedTemplate(rows[template.Index], template))
                {
                    setValid = false;
                    break;
                }
                assigned[template.Index] = template;
            }
            if (!setValid || !AssignPoolTemplates(
                    rows,
                    area.PoolTemplates,
                    assigned,
                    new HashSet<string>(StringComparer.Ordinal),
                    0))
            {
                continue;
            }
            bindings = rows.Select((row, index) =>
                    new RemixedRowBinding(row, assigned[index]!))
                .ToArray();
            bundleSetId = set?.SetId ?? "none";
            return true;
        }
        bindings = Array.Empty<RemixedRowBinding>();
        bundleSetId = string.Empty;
        return false;
    }

    private static bool AssignPoolTemplates(
        LiveBundleRow[] rows,
        CommunityCenterBundleTemplate[] pool,
        CommunityCenterBundleTemplate?[] assigned,
        HashSet<string> usedTemplateIds,
        int position)
    {
        while (position < rows.Length && assigned[position] is not null)
            position++;
        if (position == rows.Length)
            return true;

        var indexed = pool
            .Where(template => template.Index == position &&
                !usedTemplateIds.Contains(template.TemplateId))
            .ToArray();
        var candidates = indexed.Length > 0
            ? indexed
            : pool.Where(template => template.Index == -1 &&
                    !usedTemplateIds.Contains(template.TemplateId))
                .ToArray();
        foreach (var template in candidates
                     .OrderBy(value => value.TemplateId, StringComparer.Ordinal))
        {
            if (!MatchesRemixedTemplate(rows[position], template))
                continue;
            assigned[position] = template;
            usedTemplateIds.Add(template.TemplateId);
            if (AssignPoolTemplates(
                    rows,
                    pool,
                    assigned,
                    usedTemplateIds,
                    position + 1))
            {
                return true;
            }
            usedTemplateIds.Remove(template.TemplateId);
            assigned[position] = null;
        }
        return false;
    }

    private static bool MatchesRemixedTemplate(
        LiveBundleRow row,
        CommunityCenterBundleTemplate template)
    {
        if (!string.Equals(row.InternalName, template.InternalName,
                StringComparison.Ordinal) ||
            row.RequiredSlotCount != template.RequiredItemCount ||
            row.Ingredients.Length != template.PickCount)
        {
            return false;
        }
        return MatchesIngredientSubsequence(
            row.Ingredients,
            template.IngredientSlots,
            0,
            0);
    }

    private static bool MatchesIngredientSubsequence(
        CurrentCommunityCenterIngredient[] actual,
        CommunityCenterTemplateIngredientSlot[] slots,
        int actualIndex,
        int slotIndex)
    {
        if (actualIndex == actual.Length)
            return true;
        if (slots.Length - slotIndex < actual.Length - actualIndex)
            return false;
        for (var index = slotIndex; index < slots.Length; index++)
        {
            if (!slots[index].Options.Any(option =>
                    MatchesIngredient(actual[actualIndex], option)))
            {
                continue;
            }
            if (MatchesIngredientSubsequence(
                    actual,
                    slots,
                    actualIndex + 1,
                    index + 1))
            {
                return true;
            }
        }
        return false;
    }

    private sealed record RemixedRowBinding(
        LiveBundleRow Row,
        CommunityCenterBundleTemplate Template);
}
