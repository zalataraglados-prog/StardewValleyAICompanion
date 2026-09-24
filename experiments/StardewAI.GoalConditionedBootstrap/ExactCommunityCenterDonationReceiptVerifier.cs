using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class ExactCommunityCenterDonationReceiptEvidence
{
    public bool Verified { get; init; }
    public string BeforeSummary { get; init; } = string.Empty;
    public string AfterSummary { get; init; } = string.Empty;
    public string[] BlockingReasons { get; init; } = Array.Empty<string>();
}

internal static class ExactCommunityCenterDonationReceiptVerifier
{
    private const string RequirementPrefix = "community_center:bundle:";

    public static ExactCommunityCenterDonationReceiptEvidence Verify(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var reasons = new List<string>();
        var bundleKey = credit.RequirementId.StartsWith(
                RequirementPrefix,
                StringComparison.Ordinal)
            ? credit.RequirementId[RequirementPrefix.Length..]
            : string.Empty;
        if (queueItem is null ||
            !string.Equals(
                queueItem.OptionId,
                "executor.donate_community_center_item",
                StringComparison.Ordinal))
        {
            reasons.Add("community_center_donation_queue_item_missing");
        }
        var ingredientIndex = ReadIntParameter(
            queueItem,
            "bundle_ingredient_index");
        var bundleId = ReadIntParameter(queueItem, "bundle_id");
        var areaId = ReadIntParameter(queueItem, "bundle_area_id");
        var requiredStack = ReadIntParameter(queueItem, "required_stack");
        var qualifiedItemId = ReadParameter(queueItem, "qualified_item_id");
        if (string.IsNullOrWhiteSpace(bundleKey) ||
            ingredientIndex != credit.AlternativeIndex ||
            !string.Equals(
                ReadParameter(queueItem, "bundle_data_key"),
                bundleKey,
                StringComparison.Ordinal) ||
            !string.Equals(
                qualifiedItemId,
                credit.QualifiedItemId,
                StringComparison.Ordinal) ||
            !bundleId.HasValue ||
            !areaId.HasValue ||
            !requiredStack.HasValue ||
            requiredStack.Value <= 0)
        {
            reasons.Add("community_center_donation_queue_binding_invalid");
        }

        var beforeProgress = ReadProgress(before);
        var afterProgress = ReadProgress(after);
        if (!beforeProgress.HasValue || !afterProgress.HasValue)
            reasons.Add("community_center_donation_progress_unavailable");
        var beforeBundle = beforeProgress.HasValue
            ? FindBundle(beforeProgress.Value, bundleKey, bundleId)
            : null;
        var afterBundle = afterProgress.HasValue
            ? FindBundle(afterProgress.Value, bundleKey, bundleId)
            : null;
        if (!beforeBundle.HasValue || !afterBundle.HasValue ||
            ReadString(beforeBundle, "projection_status") != "exact" ||
            ReadString(afterBundle, "projection_status") != "exact" ||
            ReadInt(beforeBundle, "area_id") != areaId ||
            ReadInt(afterBundle, "area_id") != areaId)
        {
            reasons.Add("community_center_donation_bundle_projection_unavailable");
        }

        var beforeIngredient = beforeBundle.HasValue
            ? ReadIngredientCompleted(
                beforeBundle.Value,
                ingredientIndex,
                qualifiedItemId)
            : null;
        var afterIngredient = afterBundle.HasValue
            ? ReadIngredientCompleted(
                afterBundle.Value,
                ingredientIndex,
                qualifiedItemId)
            : null;
        if (beforeIngredient != false || afterIngredient != true)
            reasons.Add("community_center_donation_ingredient_transition_missing");

        var beforeInventory = ExactInventoryReceiptVerifier.InventoryCount(
            before,
            qualifiedItemId,
            0);
        var afterInventory = ExactInventoryReceiptVerifier.InventoryCount(
            after,
            qualifiedItemId,
            0);
        var expectedInventoryBefore = ReadIntParameter(
            queueItem,
            "inventory_item_total_before");
        var expectedInventoryAfter = ReadIntParameter(
            queueItem,
            "inventory_item_total_after");
        if (!beforeInventory.HasValue || !afterInventory.HasValue ||
            beforeInventory != expectedInventoryBefore ||
            afterInventory != expectedInventoryAfter ||
            beforeInventory.Value - afterInventory.Value != requiredStack)
        {
            reasons.Add("community_center_donation_inventory_delta_mismatch");
        }

        VerifyInt(
            afterBundle,
            "completed_ingredient_count",
            ReadIntParameter(queueItem, "expected_bundle_completed_count_after"),
            "community_center_donation_completed_count_mismatch",
            reasons);
        VerifyBool(
            afterBundle,
            "complete",
            ReadBoolParameter(queueItem, "expected_bundle_complete_after"),
            "community_center_donation_bundle_completion_mismatch",
            reasons);
        VerifyBool(
            afterBundle,
            "reward_available",
            ReadBoolParameter(
                queueItem,
                "expected_bundle_reward_available_after"),
            "community_center_donation_reward_state_mismatch",
            reasons);
        VerifyInt(
            afterProgress,
            "complete_bundle_count",
            ReadIntParameter(queueItem, "expected_complete_bundle_count_after"),
            "community_center_donation_complete_bundle_count_mismatch",
            reasons);
        VerifyBool(
            afterBundle,
            "area_complete",
            ReadBoolParameter(queueItem, "expected_area_complete_after"),
            "community_center_donation_area_completion_mismatch",
            reasons);
        VerifyBool(
            afterBundle,
            "area_completion_mail_pending",
            ReadBoolParameter(
                queueItem,
                "expected_area_completion_mail_pending_after"),
            "community_center_donation_area_mail_mismatch",
            reasons);
        VerifyBool(
            afterBundle,
            "bulletin_thank_you_pending",
            ReadBoolParameter(
                queueItem,
                "expected_bulletin_thank_you_pending_after"),
            "community_center_donation_bulletin_mail_mismatch",
            reasons);

        var expectedAllAreas = ReadBoolParameter(
            queueItem,
            "expected_all_areas_complete_after");
        var afterAllAreas = ReadAllAreasComplete(afterProgress);
        if (!expectedAllAreas.HasValue || afterAllAreas != expectedAllAreas)
            reasons.Add("community_center_donation_all_areas_mismatch");
        if (expectedAllAreas == true &&
            ReadLifecycleBool(
                afterProgress,
                "community_center_complete_flag_received") != true)
        {
            reasons.Add("community_center_final_star_flag_not_received");
        }

        var expectedNewNotes = ReadIntArrayParameter(
            queueItem,
            "newly_appearing_note_area_ids_json");
        var observedNewNotes = beforeProgress.HasValue && afterProgress.HasValue
            ? ReadNoteAreas(afterProgress.Value)
                .Except(ReadNoteAreas(beforeProgress.Value))
                .OrderBy(value => value)
                .ToArray()
            : null;
        if (expectedNewNotes is null ||
            observedNewNotes is null ||
            !observedNewNotes.SequenceEqual(expectedNewNotes))
        {
            reasons.Add("community_center_donation_new_note_transition_mismatch");
        }

        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new ExactCommunityCenterDonationReceiptEvidence
        {
            Verified = distinct.Length == 0,
            BeforeSummary =
                $"ingredient={ValueText(beforeIngredient)};inventory={ValueText(beforeInventory)};all_areas={ValueText(ReadAllAreasComplete(beforeProgress))}",
            AfterSummary =
                $"ingredient={ValueText(afterIngredient)};inventory={ValueText(afterInventory)};all_areas={ValueText(afterAllAreas)};new_notes={JsonSerializer.Serialize(observedNewNotes ?? Array.Empty<int>())}",
            BlockingReasons = distinct
        };
    }

    private static JsonElement? ReadProgress(SnapshotEnvelope snapshot) =>
        TryStateValue(snapshot, "world_progress", "community_center", out var value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static JsonElement? FindBundle(
        JsonElement progress,
        string bundleKey,
        int? bundleId)
    {
        if (!bundleId.HasValue ||
            !progress.TryGetProperty("bundle_rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
            return null;
        var matches = rows.EnumerateArray()
            .Where(row => ReadString(row, "bundle_data_key") == bundleKey &&
                ReadInt(row, "bundle_id") == bundleId)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool? ReadIngredientCompleted(
        JsonElement bundle,
        int? ingredientIndex,
        string qualifiedItemId)
    {
        if (!ingredientIndex.HasValue ||
            !bundle.TryGetProperty("ingredients", out var ingredients) ||
            ingredients.ValueKind != JsonValueKind.Array)
            return null;
        var matches = ingredients.EnumerateArray()
            .Where(row => ReadInt(row, "ingredient_index") == ingredientIndex)
            .Where(row =>
            {
                var identity = ReadString(row, "item_id_or_category");
                return identity.StartsWith("-", StringComparison.Ordinal) ||
                    qualifiedItemId == "(O)" + identity;
            })
            .ToArray();
        return matches.Length == 1 &&
            matches[0].TryGetProperty("completed", out var completed) &&
            completed.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? completed.GetBoolean()
                : null;
    }

    private static int[] ReadNoteAreas(JsonElement progress)
    {
        if (!progress.TryGetProperty("bundle_rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();
        return rows.EnumerateArray()
            .Where(row => ReadBool(row, "note_appears") == true)
            .Select(row => ReadInt(row, "area_id"))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
    }

    private static bool? ReadAllAreasComplete(JsonElement? progress)
    {
        var lifecycle = ReadLifecycleBool(progress, "all_areas_complete");
        if (lifecycle.HasValue)
            return lifecycle;
        if (!progress.HasValue ||
            !progress.Value.TryGetProperty("areas_complete", out var areas) ||
            areas.ValueKind != JsonValueKind.Array)
            return null;
        var rows = areas.EnumerateArray().ToArray();
        return rows.Length > 0 && rows.All(row => row.ValueKind == JsonValueKind.True);
    }

    private static bool? ReadLifecycleBool(
        JsonElement? progress,
        string property)
    {
        if (!progress.HasValue ||
            !progress.Value.TryGetProperty("lifecycle", out var lifecycle) ||
            lifecycle.ValueKind != JsonValueKind.Object)
            return null;
        return ReadBool(lifecycle, property);
    }

    private static void VerifyInt(
        JsonElement? row,
        string property,
        int? expected,
        string failure,
        ICollection<string> reasons)
    {
        if (!expected.HasValue || ReadInt(row, property) != expected)
            reasons.Add(failure);
    }

    private static void VerifyBool(
        JsonElement? row,
        string property,
        bool? expected,
        string failure,
        ICollection<string> reasons)
    {
        if (!expected.HasValue || ReadBool(row, property) != expected)
            reasons.Add(failure);
    }

    private static string ReadParameter(ActionQueueItem? item, string name) =>
        (item?.NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>())
        .SingleOrDefault(value => string.Equals(
            value.Name,
            name,
            StringComparison.Ordinal))?.Value ?? string.Empty;

    private static int? ReadIntParameter(ActionQueueItem? item, string name) =>
        int.TryParse(ReadParameter(item, name), out var value) ? value : null;

    private static bool? ReadBoolParameter(ActionQueueItem? item, string name) =>
        bool.TryParse(ReadParameter(item, name), out var value) ? value : null;

    private static int[]? ReadIntArrayParameter(
        ActionQueueItem? item,
        string name)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(ReadParameter(item, name))?
                .OrderBy(value => value)
                .ToArray();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ReadInt(JsonElement? row, string property) =>
        row.HasValue &&
        row.Value.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var result)
            ? result
            : null;

    private static string ReadString(JsonElement? row, string property) =>
        row.HasValue &&
        row.Value.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool? ReadBool(JsonElement? row, string property) =>
        row.HasValue &&
        row.Value.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static bool TryStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out value);
    }

    private static string ValueText<T>(T? value) where T : struct =>
        value?.ToString() ?? "unavailable";
}
