using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentCollectionTeacherFrontierBuilder
{
    private static CurrentCollectionProgress ReadMuseumProgress(
        JsonElement snapshot,
        GoalRequirementSet inventory)
    {
        if (!TryReadProgressValue(snapshot, "museum", out var value))
        {
            return CurrentCollectionProgress.Unavailable(
                "museum_transparent_state_unavailable");
        }
        if (!value.TryGetProperty("donatable_items", out var rows))
        {
            return CurrentCollectionProgress.Unavailable(
                "museum_per_item_denominator_unavailable");
        }
        if (rows.ValueKind != JsonValueKind.Array ||
            !TryReadInt(value, "total_donatable_items", out var total) ||
            total != inventory.RequiredGroupCount ||
            rows.GetArrayLength() != total ||
            !TryReadInt(value, "donated_count", out var donatedCount) ||
            !TryReadInt(value, "missing_item_count", out var missingCount) ||
            !TryReadBool(value, "collection_complete", out var complete) ||
            !value.TryGetProperty("missing_item_ids", out var missingIds) ||
            missingIds.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Live museum progress has an invalid per-item denominator shape.");
        }

        var expectedByQualifiedItemId = inventory.Groups.ToDictionary(
            group => group.Alternatives.Single().QualifiedItemId,
            StringComparer.Ordinal);
        var donatedByQualifiedItemId = new Dictionary<string, bool>(
            StringComparer.Ordinal);
        var actualMissingItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            var itemId = CurrentTeacherFrontierSupport.RequiredString(
                row,
                "item_id");
            var qualifiedItemId = CurrentTeacherFrontierSupport.RequiredString(
                row,
                "qualified_item_id");
            if (!TryReadBool(row, "donated", out var donated) ||
                !string.Equals(
                    qualifiedItemId,
                    "(O)" + itemId,
                    StringComparison.Ordinal) ||
                !expectedByQualifiedItemId.ContainsKey(qualifiedItemId) ||
                !donatedByQualifiedItemId.TryAdd(qualifiedItemId, donated))
            {
                throw new InvalidDataException(
                    "Live museum progress contains an invalid, duplicate, or unknown item row.");
            }
            if (!donated)
                actualMissingItemIds.Add(itemId);
        }

        var reportedMissingItemIds = ReadUniqueStringSet(
            missingIds,
            "museum missing_item_ids");
        var actualDonatedCount = donatedByQualifiedItemId.Count(value => value.Value);
        if (actualDonatedCount != donatedCount ||
            actualMissingItemIds.Count != missingCount ||
            complete != (missingCount == 0) ||
            !actualMissingItemIds.SetEquals(reportedMissingItemIds) ||
            !expectedByQualifiedItemId.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(donatedByQualifiedItemId.Keys))
        {
            throw new InvalidDataException(
                "Live museum aggregate progress disagrees with its per-item rows.");
        }

        var requirements = inventory.Groups.ToDictionary(
            group => group.RequirementId,
            group =>
            {
                var donated = donatedByQualifiedItemId[
                    group.Alternatives.Single().QualifiedItemId];
                return new CurrentCollectionRequirementProgress(
                    donated,
                    donated ? 1 : 0,
                    donated ? 0 : 1,
                    new[] { donated },
                    group.RequirementId);
            },
            StringComparer.Ordinal);
        return CurrentCollectionProgress.Ready(
            requirements,
            aggregateCompletedCount: donatedCount);
    }

    private static CurrentCollectionProgress ReadCommunityCenterProgress(
        JsonElement snapshot,
        GoalRequirementSet inventory)
    {
        if (!TryReadProgressValue(snapshot, "community_center", out var value))
        {
            return CurrentCollectionProgress.Unavailable(
                "community_center_transparent_state_unavailable");
        }
        if (!value.TryGetProperty("bundle_rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array ||
            !TryReadInt(value, "bundle_data_row_count", out var dataRowCount) ||
            !TryReadInt(value, "projected_bundle_row_count", out var projectedCount) ||
            !TryReadInt(value, "unavailable_bundle_row_count", out var unavailableCount) ||
            !TryReadInt(value, "complete_bundle_count", out var reportedCompleteCount) ||
            dataRowCount <= 0 ||
            projectedCount != dataRowCount ||
            rows.GetArrayLength() != dataRowCount ||
            unavailableCount != 0)
        {
            throw new InvalidDataException(
                "Live Community Center bundle projection is incomplete.");
        }

        var liveRows = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var actualCompleteCount = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var key = CurrentTeacherFrontierSupport.RequiredString(
                row,
                "bundle_data_key");
            if (!string.Equals(
                    CurrentTeacherFrontierSupport.RequiredString(
                        row,
                        "projection_status"),
                    "exact",
                    StringComparison.Ordinal) ||
                !liveRows.TryAdd(key, row) ||
                !TryReadBool(row, "complete", out var rowComplete))
            {
                throw new InvalidDataException(
                    "Live Community Center bundle rows are not unique exact projections.");
            }
            if (rowComplete)
                actualCompleteCount++;
        }
        if (actualCompleteCount != reportedCompleteCount)
        {
            throw new InvalidDataException(
                "Live Community Center complete_bundle_count disagrees with bundle rows.");
        }

        var requirements = new Dictionary<
            string,
            CurrentCollectionRequirementProgress>(StringComparer.Ordinal);
        foreach (var group in inventory.Groups)
        {
            var key = CommunityCenterBundleKey(group.RequirementId);
            if (!liveRows.TryGetValue(key, out var row) ||
                !TryReadInt(row, "required_slot_count", out var requiredSlots) ||
                requiredSlots != group.RequiredAlternativeCount ||
                !TryReadInt(
                    row,
                    "completed_ingredient_count",
                    out var reportedCompleted) ||
                !TryReadBool(row, "complete", out var complete) ||
                !row.TryGetProperty("ingredients", out var ingredients) ||
                ingredients.ValueKind != JsonValueKind.Array ||
                ingredients.GetArrayLength() != group.Alternatives.Length)
            {
                throw new InvalidDataException(
                    "A standard Community Center bundle does not match its authoritative requirement group.");
            }

            var completedAlternatives = new bool[group.Alternatives.Length];
            var ingredientIndex = 0;
            foreach (var ingredient in ingredients.EnumerateArray())
            {
                var expected = group.Alternatives[ingredientIndex];
                var itemId = CurrentTeacherFrontierSupport.RequiredString(
                    ingredient,
                    "item_id_or_category");
                if (!TryReadInt(ingredient, "ingredient_index", out var index) ||
                    index != ingredientIndex ||
                    !TryReadInt(ingredient, "required_stack", out var stack) ||
                    !TryReadInt(ingredient, "minimum_quality", out var quality) ||
                    !TryReadBool(ingredient, "completed", out var completed) ||
                    !string.Equals(itemId, expected.ItemId, StringComparison.Ordinal) ||
                    stack != expected.Amount ||
                    quality != expected.MinimumQuality)
                {
                    throw new InvalidDataException(
                        "A live Community Center ingredient differs from the authoritative identity, quantity, or quality.");
                }
                completedAlternatives[ingredientIndex] = completed;
                ingredientIndex++;
            }

            var actualCompleted = completedAlternatives.Count(value => value);
            if (reportedCompleted != actualCompleted ||
                complete != (actualCompleted >= requiredSlots))
            {
                throw new InvalidDataException(
                    "A live Community Center bundle aggregate disagrees with its ingredient rows.");
            }
            requirements.Add(
                group.RequirementId,
                new CurrentCollectionRequirementProgress(
                    complete,
                    actualCompleted,
                    Math.Max(0, requiredSlots - actualCompleted),
                    completedAlternatives,
                    key));
        }

        var routeState = CurrentTeacherFrontierSupport.RequiredString(
            value,
            "route_state");
        var blockingReasons = routeState switch
        {
            "undecided" or "community_center_locked" => Array.Empty<string>(),
            "joja_locked" => new[]
            {
                "community_center_route_locked_out_by_joja"
            },
            "conflicting_irreversible_flags" => new[]
            {
                "community_center_route_state_conflict"
            },
            _ => throw new InvalidDataException(
                "Live Community Center route_state is unknown.")
        };
        return CurrentCollectionProgress.Ready(
            requirements,
            reportedCompleteCount,
            blockingReasons);
    }

    private static void ValidateMuseumSets(
        GoalRequirementSet inventory,
        AcquisitionRequirementSetLowering lowering)
    {
        ValidateSetShape(inventory, lowering);
        foreach (var group in inventory.Groups)
        {
            if (group.SelectionRule != "all_required" ||
                group.RequiredAlternativeCount != 1 ||
                group.Alternatives.Length != 1)
            {
                throw new InvalidDataException(
                    "Museum requirements must be exact one-item groups.");
            }
            var alternative = group.Alternatives[0];
            if (alternative.MatchKind != "item_id" ||
                alternative.Amount != 1 ||
                alternative.MinimumQuality != 0 ||
                alternative.QualifiedItemId != "(O)" + alternative.ItemId)
            {
                throw new InvalidDataException(
                    "Museum requirement identity, quantity, or quality drifted.");
            }
        }
        ValidateAlternativeParity(inventory, lowering);
    }

    private static void ValidateCommunityCenterSets(
        GoalRequirementSet inventory,
        AcquisitionRequirementSetLowering lowering)
    {
        ValidateSetShape(inventory, lowering);
        foreach (var group in inventory.Groups)
        {
            if (group.SelectionRule != "choose_at_least_required_slots" ||
                group.RequiredAlternativeCount < 1 ||
                group.RequiredAlternativeCount > group.Alternatives.Length)
            {
                throw new InvalidDataException(
                    "Community Center requirement selection semantics drifted.");
            }
            foreach (var alternative in group.Alternatives)
            {
                var validItem = alternative.MatchKind == "item_id" &&
                    alternative.QualifiedItemId == "(O)" + alternative.ItemId;
                var validMoney = alternative.MatchKind == "money_payment" &&
                    alternative.ItemId == "-1" &&
                    alternative.QualifiedItemId.Length == 0;
                if ((!validItem && !validMoney) ||
                    alternative.Amount < 1 ||
                    alternative.MinimumQuality < 0)
                {
                    throw new InvalidDataException(
                        "Community Center requirement identity, quantity, or quality drifted.");
                }
            }
        }
        ValidateAlternativeParity(inventory, lowering);
    }

    private static void ValidateSetShape(
        GoalRequirementSet inventory,
        AcquisitionRequirementSetLowering lowering)
    {
        if (!inventory.AcquisitionRoutesComplete ||
            inventory.RequiredGroupCount != inventory.Groups.Length ||
            lowering.RequiredGroupCount != inventory.RequiredGroupCount ||
            lowering.TeacherAdmittedGroupCount != lowering.RequiredGroupCount ||
            lowering.Groups.Length != inventory.Groups.Length)
        {
            throw new InvalidDataException(
                $"{inventory.RequirementSetId} inventory or acquisition lowering is incomplete.");
        }
    }

    private static void ValidateAlternativeParity(
        GoalRequirementSet inventory,
        AcquisitionRequirementSetLowering lowering)
    {
        var loweredGroups = lowering.Groups.ToDictionary(
            value => value.RequirementId,
            StringComparer.Ordinal);
        foreach (var group in inventory.Groups)
        {
            if (!loweredGroups.TryGetValue(group.RequirementId, out var lowered) ||
                !lowered.TeacherAdmissionReady ||
                lowered.RequiredAlternativeCount != group.RequiredAlternativeCount ||
                lowered.Alternatives.Length != group.Alternatives.Length)
            {
                throw new InvalidDataException(
                    $"{inventory.RequirementSetId} lowering group shape drifted.");
            }
            for (var index = 0; index < group.Alternatives.Length; index++)
            {
                var expected = group.Alternatives[index];
                var actual = lowered.Alternatives[index];
                if (!actual.TeacherAdmissionReady ||
                    actual.Routes.Length == 0 ||
                    expected.ItemId != actual.ItemId ||
                    expected.QualifiedItemId != actual.QualifiedItemId ||
                    expected.MatchKind != actual.MatchKind ||
                    expected.Amount != actual.Amount ||
                    expected.MinimumQuality != actual.MinimumQuality)
                {
                    throw new InvalidDataException(
                        $"{inventory.RequirementSetId} lowered alternative drifted.");
                }
            }
        }
    }

    private static bool TryReadProgressValue(
        JsonElement snapshot,
        string fieldName,
        out JsonElement value)
    {
        value = default;
        return snapshot.TryGetProperty("state", out var state) &&
            state.ValueKind == JsonValueKind.Object &&
            state.TryGetProperty("world_progress", out var world) &&
            world.ValueKind == JsonValueKind.Object &&
            world.TryGetProperty(fieldName, out var field) &&
            field.ValueKind == JsonValueKind.Object &&
            field.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            field.TryGetProperty("value", out value) &&
            value.ValueKind == JsonValueKind.Object;
    }

    private static HashSet<string> ReadUniqueStringSet(
        JsonElement values,
        string label)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()) ||
                !result.Add(value.GetString()!))
            {
                throw new InvalidDataException(label + " is invalid.");
            }
        }
        return result;
    }

    private static string CommunityCenterBundleKey(string requirementId)
    {
        const string prefix = "community_center:bundle:";
        if (!requirementId.StartsWith(prefix, StringComparison.Ordinal) ||
            requirementId.Length == prefix.Length)
        {
            throw new InvalidDataException(
                "Community Center requirement ID has no exact bundle key.");
        }
        return requirementId[prefix.Length..];
    }

    private static bool TryReadInt(
        JsonElement value,
        string property,
        out int result)
    {
        result = 0;
        return value.TryGetProperty(property, out var field) &&
            field.TryGetInt32(out result);
    }

    private static bool TryReadBool(
        JsonElement value,
        string property,
        out bool result)
    {
        result = false;
        if (!value.TryGetProperty(property, out var field) ||
            field.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        result = field.GetBoolean();
        return true;
    }
}

internal sealed class CurrentCollectionProgress
{
    public bool Available { get; private init; }

    public IReadOnlyDictionary<string, CurrentCollectionRequirementProgress>
        Requirements { get; private init; } =
            new Dictionary<string, CurrentCollectionRequirementProgress>();

    public int AggregateCompletedCount { get; private init; }

    public string[] BlockingReasons { get; private init; } = Array.Empty<string>();

    public static CurrentCollectionProgress Ready(
        IReadOnlyDictionary<string, CurrentCollectionRequirementProgress> requirements,
        int aggregateCompletedCount,
        string[]? blockingReasons = null) => new()
    {
        Available = true,
        Requirements = requirements,
        AggregateCompletedCount = aggregateCompletedCount,
        BlockingReasons = blockingReasons ?? Array.Empty<string>()
    };

    public static CurrentCollectionProgress Unavailable(string reason) => new()
    {
        Available = false,
        BlockingReasons = new[] { reason }
    };
}

internal sealed record CurrentCollectionRequirementProgress(
    bool Completed,
    int CompletedAlternativeCount,
    int RemainingSlotCount,
    bool[] CompletedAlternatives,
    string RuntimeKey);
