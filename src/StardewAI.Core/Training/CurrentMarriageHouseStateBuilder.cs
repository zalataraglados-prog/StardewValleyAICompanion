using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public sealed class CurrentMarriageHouseStateBuilder
{
    public CurrentMarriageHouseState Build(SnapshotEnvelope snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.SchemaVersion != "snapshot.v1" || snapshot.GameVersion != "1.6.15" || snapshot.GameTick < 0)
            return Blocked("marriage_house_snapshot_version_or_tick_unsupported");
        if (snapshot.State is null || !SnapshotHash.MatchesStateHash(snapshot.State, snapshot.StateHash))
            return Blocked("marriage_house_state_hash_mismatch");
        var clock = SnapshotGameClockBudgetReader.CurrentTime(snapshot);
        if (!Value(snapshot, "time", "year", out var yearValue) || yearValue.ValueKind != JsonValueKind.Number ||
            !yearValue.TryGetInt32(out var year) || year < 1 ||
            !Value(snapshot, "time", "season", out var season) || season.ValueKind != JsonValueKind.String ||
            season.GetString() is not ("spring" or "summer" or "fall" or "winter") ||
            !Value(snapshot, "time", "day", out var dayValue) || dayValue.ValueKind != JsonValueKind.Number ||
            !dayValue.TryGetInt32(out var day) || day is < 1 or > 28 || !clock.HasValue)
            return Blocked("marriage_house_calendar_or_clock_unavailable");
        if (!Value(snapshot, "world_progress", "marriage_house", out var progress) || progress.ValueKind != JsonValueKind.Object ||
            !Player(snapshot, progress, "married_or_roommate", JsonValueKind.True, out var married) ||
            !Player(snapshot, progress, "engaged", JsonValueKind.True, out var engaged) ||
            !Player(snapshot, progress, "has_pending_roommate", JsonValueKind.True, out var roommate, "pending_roommate") ||
            !Player(snapshot, progress, "spouse", JsonValueKind.String, out var spouse) ||
            !Player(snapshot, progress, "farmhouse_upgrade_level", JsonValueKind.Number, out var house) ||
            !Player(snapshot, progress, "days_until_farmhouse_upgrade", JsonValueKind.Number, out var days) ||
            !Player(snapshot, progress, "money", JsonValueKind.Number, out var money))
            return Blocked("marriage_house_current_facts_missing_or_inconsistent");
        if (!house.TryGetInt32(out var level) || level is < 0 or > 3 ||
            !days.TryGetInt32(out var remaining) || remaining is < -1 or > 3 ||
            !money.TryGetInt32(out var gold) || gold < 0 ||
            !Property(progress, "grandpa_factor_satisfied", out var factor) ||
            factor.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            factor.GetBoolean() != (married.GetBoolean() && level >= 2) ||
            !Property(progress, "house_upgrade", out var quote) ||
            (level == 3 ? quote.ValueKind != JsonValueKind.Null : !QuoteMatches(quote, level)))
            return Blocked("marriage_house_projection_shape_or_joint_condition_invalid");
        var serviceFields = new[] { "location_accessible", "is_current_location", "building_under_construction", "robin_present_at_counter" };
        if (serviceFields.Any(name => !Property(progress, name, out var value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
            return Blocked("marriage_house_service_facts_incomplete");

        return new CurrentMarriageHouseState
        {
            Status = "ready_current_state_only", SourceStateHash = snapshot.StateHash, GameTick = snapshot.GameTick,
            Year = year, Season = season.GetString(), Day = day, CurrentTime = clock.Value,
            MarriedOrRoommate = married.GetBoolean(), Engaged = engaged.GetBoolean(),
            HasCurrentOrPendingRoommate = roommate.GetBoolean(), Spouse = spouse.GetString(),
            FarmhouseUpgradeLevel = level, DaysUntilFarmhouseUpgrade = remaining, Money = gold,
            CurrentCriterionSatisfied = factor.GetBoolean(), HouseLevelsMissing = Math.Max(0, 2 - level),
            PartnershipStillRequired = !married.GetBoolean(),
            // hasCurrentOrPendingRoommate also covers an already settled roommate, not just a pending one.
            NativeSettlementPending = engaged.GetBoolean() || remaining >= 0,
            HouseUpgradeQuote = quote.ValueKind == JsonValueKind.Null ? null : quote.Clone(),
            NativeServiceState = JsonSerializer.SerializeToElement(serviceFields.ToDictionary(name => name,
                name => progress.GetProperty(name).GetBoolean()))
        };
    }

    private static bool QuoteMatches(JsonElement quote, int level) =>
        quote.ValueKind == JsonValueKind.Object && Integer(quote, "level_before", out var before) && before == level &&
        Integer(quote, "level_after", out var after) && after == level + 1 &&
        Integer(quote, "price", out var price) && price >= 0 &&
        Property(quote, "required_item_id", out var item) && item.ValueKind == JsonValueKind.String &&
        Integer(quote, "required_item_count", out var count) && count >= 0 &&
        Integer(quote, "inventory_item_count", out var inventory) && inventory >= 0 &&
        Integer(quote, "construction_days", out var days) && days > 0 &&
        Property(quote, "action_status", out var status) && status.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(status.GetString());

    private static bool Player(SnapshotEnvelope snapshot, JsonElement projection, string field,
        JsonValueKind kind, out JsonElement value, string? projectedField = null)
    {
        value = default;
        return Value(snapshot, "player", field, out value) &&
            (kind == JsonValueKind.True ? value.ValueKind is JsonValueKind.True or JsonValueKind.False : value.ValueKind == kind) &&
            Property(projection, projectedField ?? field, out var other) && value.ValueKind == other.ValueKind &&
            (kind == JsonValueKind.String ? value.GetString() == other.GetString() :
                kind == JsonValueKind.Number ? value.TryGetInt32(out var first) && other.TryGetInt32(out var second) && first == second :
                value.GetBoolean() == other.GetBoolean());
    }

    private static bool Value(SnapshotEnvelope snapshot, string section, string field, out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var node) && Property(node, field, out var envelope) &&
            Property(envelope, "status", out var status) && status.ValueKind == JsonValueKind.String &&
            ReadableStatus(status.GetString()) && Property(envelope, "value", out value);
    }

    private static bool Integer(JsonElement value, string name, out int result)
    {
        result = 0;
        return Property(value, name, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out result);
    }

    private static bool Property(JsonElement value, string name, out JsonElement field)
    {
        field = default;
        if (value.ValueKind != JsonValueKind.Object) return false;
        var matches = value.EnumerateObject().Where(property => property.NameEquals(name)).ToArray();
        if (matches.Length != 1) return false;
        field = matches[0].Value;
        return true;
    }

    private static CurrentMarriageHouseState Blocked(string reason) => new() { BlockingReasons = new[] { reason } };
}
