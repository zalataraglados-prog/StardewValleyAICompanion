using System.Security.Cryptography;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentFullShipmentTeacherFrontierBuilder
{
    private static Dictionary<string, bool> ReadFullShipmentProgress(
        JsonElement snapshot,
        GoalRequirementSet inventorySet)
    {
        if (!snapshot.TryGetProperty("state", out var state) ||
            state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("world_progress", out var world) ||
            world.ValueKind != JsonValueKind.Object ||
            !world.TryGetProperty("full_shipment_progress", out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("status", out var statusValue) ||
            statusValue.ValueKind != JsonValueKind.String ||
            statusValue.GetString() is not ("available" or "derived") ||
            !field.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("eligible_item_count", out var countValue) ||
            !countValue.TryGetInt32(out var eligibleCount) ||
            eligibleCount != inventorySet.RequiredGroupCount ||
            !value.TryGetProperty(
                "shipped_eligible_item_count",
                out var shippedCountValue) ||
            !shippedCountValue.TryGetInt32(out var reportedShippedCount) ||
            !value.TryGetProperty("missing_item_count", out var missingCountValue) ||
            !missingCountValue.TryGetInt32(out var reportedMissingCount) ||
            !value.TryGetProperty("completion_ratio", out var ratioValue) ||
            !ratioValue.TryGetDouble(out var reportedRatio) ||
            !value.TryGetProperty("complete", out var completeValue) ||
            completeValue.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False) ||
            !value.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array ||
            items.GetArrayLength() != eligibleCount ||
            !value.TryGetProperty("missing_item_ids", out var missingIds) ||
            missingIds.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Live full_shipment_progress is unavailable or its denominator does not match the authoritative requirement inventory.");
        }

        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        var actualMissingItemIds = new HashSet<string>(StringComparer.Ordinal);
        var reportedMissingItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var missingId in missingIds.EnumerateArray())
        {
            if (missingId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(missingId.GetString()) ||
                !reportedMissingItemIds.Add(missingId.GetString()!))
            {
                throw new InvalidDataException(
                    "Live full_shipment_progress contains invalid missing_item_ids.");
            }
        }
        foreach (var item in items.EnumerateArray())
        {
            var itemId = RequiredString(item, "item_id");
            var qualifiedItemId = RequiredString(item, "qualified_item_id");
            if (!item.TryGetProperty("current_shipped_count", out var count) ||
                !count.TryGetInt32(out var shippedCount) || shippedCount < 0 ||
                !item.TryGetProperty("shipped", out var shippedValue) ||
                shippedValue.ValueKind is not (
                    JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException(
                    "Live full_shipment_progress contains an invalid item row.");
            }
            var shipped = shippedValue.GetBoolean();
            if (shipped != (shippedCount > 0) ||
                !result.TryAdd(qualifiedItemId, shipped))
            {
                throw new InvalidDataException(
                    "Live full_shipment_progress contains inconsistent or duplicate item rows.");
            }
            var expectedItemId = qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal)
                ? qualifiedItemId[3..]
                : qualifiedItemId;
            if (!string.Equals(itemId, expectedItemId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Live full_shipment_progress item and qualified-item identities disagree.");
            }
            if (!shipped)
                actualMissingItemIds.Add(itemId);
        }

        var actualShippedCount = result.Count(value => value.Value);
        var actualMissingCount = result.Count - actualShippedCount;
        var expectedRatio = result.Count == 0
            ? 0
            : actualShippedCount / (double)result.Count;
        if (reportedShippedCount != actualShippedCount ||
            reportedMissingCount != actualMissingCount ||
            completeValue.GetBoolean() != (actualMissingCount == 0) ||
            Math.Abs(reportedRatio - expectedRatio) > 0.0000001 ||
            !reportedMissingItemIds.SetEquals(actualMissingItemIds))
        {
            throw new InvalidDataException(
                "Live full_shipment_progress aggregate fields are inconsistent with its item rows.");
        }

        var expected = inventorySet.Groups
            .Select(value => value.Alternatives.Single().QualifiedItemId)
            .ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(result.Keys))
        {
            throw new InvalidDataException(
                "Live full_shipment_progress identities do not match the authoritative requirement inventory.");
        }
        return result;
    }

    private static void ValidateAuthority(
        string inventoryPath,
        AuthoritativeRequirementInventoryReport inventory,
        AcquisitionRouteOptionLoweringReport lowering)
    {
        if (!inventory.DenominatorComplete ||
            !inventory.AcquisitionRoutesComplete ||
            !string.Equals(inventory.Status, "complete", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Authoritative requirement inventory is not complete.");
        }
        if (!string.Equals(lowering.Status, "complete", StringComparison.Ordinal) ||
            !string.Equals(lowering.GoalId, inventory.GoalId, StringComparison.Ordinal) ||
            !string.Equals(
                lowering.RequirementInventorySha256,
                HashFile(inventoryPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Acquisition lowering is incomplete or does not bind the supplied requirement inventory.");
        }
    }

    private static void ValidateSets(
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
                "Full Shipment inventory and acquisition lowering are incomplete.");
        }

        var loweredGroups = lowering.Groups.ToDictionary(
            value => value.RequirementId,
            StringComparer.Ordinal);
        foreach (var group in inventory.Groups)
        {
            if (group.RequiredAlternativeCount != 1 ||
                group.Alternatives.Length != 1 ||
                !loweredGroups.TryGetValue(group.RequirementId, out var lowered) ||
                !lowered.TeacherAdmissionReady ||
                lowered.Alternatives.Length != 1)
            {
                throw new InvalidDataException(
                    "Full Shipment requirement groups must have one admitted exact-item alternative.");
            }
            var expected = group.Alternatives[0];
            var actual = lowered.Alternatives[0];
            if (!string.Equals(expected.MatchKind, "item_id", StringComparison.Ordinal) ||
                expected.Amount != 1 ||
                expected.MinimumQuality != 0 ||
                !string.Equals(
                    expected.QualifiedItemId,
                    "(O)" + expected.ItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(expected.ItemId, actual.ItemId, StringComparison.Ordinal) ||
                !string.Equals(
                    expected.QualifiedItemId,
                    actual.QualifiedItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(expected.MatchKind, actual.MatchKind, StringComparison.Ordinal) ||
                expected.Amount != actual.Amount ||
                expected.MinimumQuality != actual.MinimumQuality ||
                !actual.TeacherAdmissionReady ||
                actual.Routes.Length == 0)
            {
                throw new InvalidDataException(
                    "Full Shipment requirement identity or admitted route lowering drifted.");
            }
        }
    }

    private static TSet SingleSet<TSet>(
        IEnumerable<TSet> sets,
        Func<TSet, string> id,
        string source)
    {
        var matches = sets.Where(value =>
                string.Equals(id(value), RequirementSetId, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Expected exactly one {RequirementSetId} set in {source}.");
    }

    private static T Read<T>(string path, string label) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options)
        ?? throw new InvalidDataException(label + " is null.");

    private static string RequiredString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(propertyValue.GetString()))
        {
            throw new InvalidDataException(
                $"Snapshot property {property} is missing.");
        }
        return propertyValue.GetString()!;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
