using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class ExactCommunityCenterPaymentReceiptVerifier
{
    private const string RequirementPrefix = "community_center:bundle:";

    public static AcquisitionTerminalTransitionEvidence Verify(
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string requirementId,
        int alternativeIndex,
        int requiredAmount)
    {
        var reasons = new List<string>();
        var bundleKey = requirementId.StartsWith(
                RequirementPrefix,
                StringComparison.Ordinal)
            ? requirementId[RequirementPrefix.Length..]
            : string.Empty;
        if (string.IsNullOrWhiteSpace(bundleKey) ||
            alternativeIndex < 0 ||
            requiredAmount <= 0)
        {
            reasons.Add("payment_receipt_requirement_invalid");
        }
        var beforeMoney = ReadStateInt(before, "player", "money");
        var afterMoney = ReadStateInt(after, "player", "money");
        if (!beforeMoney.HasValue || !afterMoney.HasValue ||
            beforeMoney.Value < 0 || afterMoney.Value < 0)
        {
            reasons.Add("payment_receipt_money_unavailable");
        }
        var beforeCompletion = ReadBundleAlternative(
            before,
            bundleKey,
            alternativeIndex);
        var afterCompletion = ReadBundleAlternative(
            after,
            bundleKey,
            alternativeIndex);
        if (!beforeCompletion.HasValue || !afterCompletion.HasValue)
            reasons.Add("payment_receipt_bundle_transition_unavailable");

        int? decrease = null;
        if (beforeMoney.HasValue && afterMoney.HasValue)
        {
            try
            {
                decrease = checked(beforeMoney.Value - afterMoney.Value);
            }
            catch (OverflowException)
            {
                reasons.Add("payment_receipt_money_delta_overflow");
            }
        }
        var distinct = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new AcquisitionTerminalTransitionEvidence
        {
            TransitionKind = "native_community_center_payment_completion",
            RequiredAmount = requiredAmount,
            BeforeMoney = beforeMoney,
            AfterMoney = afterMoney,
            MoneyDecrease = decrease,
            BeforeNativeCompletion = beforeCompletion,
            AfterNativeCompletion = afterCompletion,
            Resolved = distinct.Length == 0,
            Verified = distinct.Length == 0 &&
                beforeCompletion == false &&
                afterCompletion == true &&
                decrease.HasValue &&
                decrease.Value == requiredAmount,
            BlockingReasons = distinct
        };
    }

    private static bool? ReadBundleAlternative(
        SnapshotEnvelope snapshot,
        string bundleKey,
        int alternativeIndex)
    {
        if (!TryStateValue(snapshot, "world_progress", "community_center",
                out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("bundle_rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var bundles = rows.EnumerateArray()
            .Where(row => ReadString(row, "bundle_data_key") == bundleKey)
            .ToArray();
        if (bundles.Length != 1 ||
            !bundles[0].TryGetProperty("ingredients", out var ingredients) ||
            ingredients.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var matches = ingredients.EnumerateArray()
            .Where(row => ReadInt(row, "ingredient_index") == alternativeIndex)
            .ToArray();
        return matches.Length == 1 &&
            matches[0].TryGetProperty("completed", out var completed) &&
            completed.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? completed.GetBoolean()
                : null;
    }

    private static int? ReadStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) =>
        TryStateValue(snapshot, section, field, out var value) &&
        value.TryGetInt32(out var result)
            ? result
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

    private static string ReadString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.TryGetInt32(out var result)
            ? result
            : int.MinValue;
}
