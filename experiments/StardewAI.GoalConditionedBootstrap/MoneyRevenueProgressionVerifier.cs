using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyRevenueProgressionVerifier
{
    private const int StageOneDeadlineExclusive = 224;

    internal static MoneyRevenueTransitionEvidence VerifyImmediateShopSale(
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var transition = ProjectTransition(before, after);
        Require(
            transition.After.Money >= transition.Before.Money &&
            transition.After.TotalMoneyEarned >=
                transition.Before.TotalMoneyEarned,
            "Money progression cash or cumulative earnings regressed.");
        var moneyDelta = checked(transition.After.Money - transition.Before.Money);
        var earnedDelta = checked(
            transition.After.TotalMoneyEarned -
            transition.Before.TotalMoneyEarned);
        Require(
            transition.After.TotalDay == transition.Before.TotalDay &&
            moneyDelta > 0 &&
            earnedDelta == (uint)moneyDelta &&
            SameShippingState(transition.Before, transition.After),
            "Money progression is not one isolated immediate shop sale.");
        return new MoneyRevenueTransitionEvidence(
            MoneyRevenueTransitionKinds.ImmediateShopSale,
            transition.Before,
            transition.After,
            moneyDelta,
            earnedDelta,
            0);
    }

    internal static MoneyRevenueTransitionEvidence VerifyShippingDaySettlement(
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var transition = ProjectTransition(before, after);
        Require(
            transition.After.Money >= transition.Before.Money &&
            transition.After.TotalMoneyEarned >=
                transition.Before.TotalMoneyEarned,
            "Money progression cash or cumulative earnings regressed.");
        var moneyDelta = checked(transition.After.Money - transition.Before.Money);
        var earnedDelta = checked(
            transition.After.TotalMoneyEarned -
            transition.Before.TotalMoneyEarned);
        var pending = transition.Before.PendingShippingSettlementValue;
        Require(
            transition.After.TotalDay == transition.Before.TotalDay + 1 &&
            pending > 0 &&
            transition.After.PendingShippingSettlementValue == 0 &&
            moneyDelta == pending &&
            earnedDelta == (uint)pending &&
            transition.Before.ShippingScopeId ==
                transition.After.ShippingScopeId,
            "Money progression is not one exact native shipping settlement.");
        return new MoneyRevenueTransitionEvidence(
            MoneyRevenueTransitionKinds.NativeShippingDaySettlement,
            transition.Before,
            transition.After,
            moneyDelta,
            earnedDelta,
            pending);
    }

    private static (MoneyRevenueCheckpoint Before, MoneyRevenueCheckpoint After)
        ProjectTransition(
            SnapshotEnvelope before,
            SnapshotEnvelope after)
    {
        var beforeProjection = Project(before);
        var afterProjection = Project(after);
        Require(
            beforeProjection.SaveId == afterProjection.SaveId &&
            beforeProjection.PlayerId == afterProjection.PlayerId &&
            before.GameVersion == after.GameVersion &&
            after.GameTick > before.GameTick,
            "Money progression actor, game, or tick identity drifted.");
        return (beforeProjection, afterProjection);
    }

    internal static MoneyRevenueCheckpoint Project(SnapshotEnvelope snapshot)
    {
        Require(
            snapshot.SchemaVersion == "snapshot.v1" &&
            snapshot.GameVersion == "1.6.15" &&
            !string.IsNullOrWhiteSpace(snapshot.BridgeVersion) &&
            !string.IsNullOrWhiteSpace(snapshot.StateHash) &&
            snapshot.StateHash == SnapshotHash.ComputeStateHash(snapshot.State),
            "Money progression snapshot identity or hash is invalid.");
        var saveId = RequiredIdentity(snapshot.SaveId, "save_id");
        var playerId = RequiredIdentity(snapshot.PlayerId, "player_id");
        var totalDay = ReadInt(snapshot, "time", "total_days");
        Require(totalDay >= 0 && totalDay < StageOneDeadlineExclusive,
            "Money progression is outside the Stage-1 deadline.");
        var shipping = ProjectShipping(snapshot, playerId);
        return new MoneyRevenueCheckpoint(
            snapshot.StateHash,
            saveId,
            playerId,
            totalDay,
            ReadInt(snapshot, "player", "money"),
            ReadUInt(snapshot, "player", "total_money_earned"),
            shipping.ScopeId,
            shipping.PendingValue,
            shipping.ContentsSignature);
    }

    private static ShippingProjection ProjectShipping(
        SnapshotEnvelope snapshot,
        string playerId)
    {
        var value = ReadValue(snapshot, "farm", "shipping_bins");
        Require(value.ValueKind == JsonValueKind.Array,
            "Money progression shipping bins are not an array.");
        var endpoints = value.EnumerateArray()
            .Where(row => RequiredBool(row, "completed"))
            .Select(row =>
            {
                var scope = RequiredString(row, "bin_scope");
                var endpointPlayerId = RequiredScalarString(row, "player_id");
                Require(scope is "shared" or "personal" &&
                        endpointPlayerId == playerId &&
                        RequiredBool(row, "pending_settlement_value_complete") &&
                        !RequiredBool(row, "contents_truncated") &&
                        RequiredString(row, "pending_settlement_timing") ==
                            "next_native_day_transition",
                    "Money progression shipping scope metadata is invalid.");
                var pending = RequiredInt(row, "pending_settlement_value");
                Require(pending >= 0,
                    "Money progression pending shipping value is negative.");
                return new ShippingProjection(
                    scope == "shared" ? "shared" : "personal:" + playerId,
                    pending,
                    RequiredString(row, "contents_signature"));
            })
            .ToArray();
        Require(endpoints.Length > 0,
            "Money progression has no completed shipping endpoint.");
        var scopes = endpoints.GroupBy(row => row.ScopeId, StringComparer.Ordinal)
            .ToArray();
        Require(scopes.Length == 1 && scopes[0].All(row =>
                row.PendingValue == scopes[0].First().PendingValue &&
                row.ContentsSignature == scopes[0].First().ContentsSignature),
            "Money progression duplicated shipping endpoints disagree.");
        return scopes[0].First();
    }

    private static bool SameShippingState(
        MoneyRevenueCheckpoint left,
        MoneyRevenueCheckpoint right) =>
        left.ShippingScopeId == right.ShippingScopeId &&
        left.PendingShippingSettlementValue ==
            right.PendingShippingSettlementValue &&
        left.ShippingContentsSignature == right.ShippingContentsSignature;

    private static JsonElement ReadValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            sectionValue.ValueKind != JsonValueKind.Object ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not
                (FieldStatus.Available or FieldStatus.Derived) ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                $"Money progression field {section}.{field} is unavailable.");
        }
        return value;
    }

    private static int ReadInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadValue(snapshot, section, field).GetInt32();

    private static uint ReadUInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadValue(snapshot, section, field).GetUInt32();

    private static string RequiredIdentity(
        FieldEnvelope<string?> value,
        string label)
    {
        Require(FieldEnvelopeValidator.IsReadableStatus(value.Status) &&
                !string.IsNullOrWhiteSpace(value.Value),
            "Money progression " + label + " is unavailable.");
        return value.Value!;
    }

    private static int RequiredInt(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) &&
        result.TryGetInt32(out var number)
            ? number
            : throw new InvalidDataException(
                "Money progression shipping integer is unavailable: " + property);

    private static bool RequiredBool(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) &&
        result.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? result.GetBoolean()
            : throw new InvalidDataException(
                "Money progression shipping boolean is unavailable: " + property);

    private static string RequiredString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) &&
        result.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(result.GetString())
            ? result.GetString()!
            : throw new InvalidDataException(
                "Money progression shipping string is unavailable: " + property);

    private static string RequiredScalarString(
        JsonElement value,
        string property)
    {
        if (!value.TryGetProperty(property, out var result))
        {
            throw new InvalidDataException(
                "Money progression shipping scalar is unavailable: " + property);
        }
        return result.ValueKind switch
        {
            JsonValueKind.String => result.GetString() ?? string.Empty,
            JsonValueKind.Number => result.GetInt64().ToString(
                CultureInfo.InvariantCulture),
            _ => throw new InvalidDataException(
                "Money progression shipping scalar is invalid: " + property)
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record ShippingProjection(
        string ScopeId,
        int PendingValue,
        string ContentsSignature);
}
