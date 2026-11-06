using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public sealed partial class CurrentMarriageHouseSettlementProjector
{
    private static bool TryCalendar(SnapshotEnvelope snapshot, CurrentMarriageHouseState current, int total,
        out HashSet<string> blocked, out string error)
    {
        blocked = new HashSet<string>(StringComparer.Ordinal);
        error = "marriage_house_native_wedding_calendar_missing_or_inconsistent";
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "world_progress", "game_state_query_calendar_state"))) return false;
        var calendar = ReadStateFieldValue(snapshot, "world_progress", "game_state_query_calendar_state");
        if (!calendar.HasValue || calendar.Value.ValueKind != JsonValueKind.Object) return false;
        var root = calendar.Value;
        if (!Integer(root, "current_total_day", out var capturedDay) || capturedDay != total ||
            !root.TryGetProperty("save_unique_id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetUInt64(out var seed) ||
            !root.TryGetProperty("festival_date_keys", out var festivals) || festivals.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("wedding_calendar", out var native) || native.ValueKind != JsonValueKind.Object ||
            !Text(native, "schema_version", "native_wedding_calendar.v1") || !Text(native, "native_predicate", "Game1.canHaveWeddingOnDay") ||
            !Integer(native, "evaluated_date_count", out var dateCount) || dateCount != 112 ||
            !Integer(native, "source_year", out var capturedYear) || capturedYear != current.Year ||
            !native.TryGetProperty("save_unique_id", out var nativeSeed) || nativeSeed.ValueKind != JsonValueKind.Number || !nativeSeed.TryGetUInt64(out var cachedSeed) || cachedSeed != seed ||
            !native.TryGetProperty("festival_date_keys", out var nativeFestivals) || nativeFestivals.ValueKind != JsonValueKind.Array ||
            !native.TryGetProperty("blocked_date_keys", out var dates) || dates.ValueKind != JsonValueKind.Array) return false;
        var allDates = Enumerable.Range(0, 112).Select(DateKey).ToHashSet(StringComparer.Ordinal);
        var currentKeys = festivals.EnumerateArray().Select(row => row.ValueKind == JsonValueKind.String ? row.GetString()! : "").ToArray();
        var cachedKeys = nativeFestivals.EnumerateArray().Select(row => row.ValueKind == JsonValueKind.String ? row.GetString()! : "").ToArray();
        var blockedKeys = dates.EnumerateArray().Select(row => row.ValueKind == JsonValueKind.String ? row.GetString()! : "").ToArray();
        if (currentKeys.Distinct(StringComparer.Ordinal).Count() != currentKeys.Length ||
            cachedKeys.Distinct(StringComparer.Ordinal).Count() != cachedKeys.Length ||
            !currentKeys.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(cachedKeys.OrderBy(key => key, StringComparer.Ordinal)) ||
            currentKeys.Any(key => !allDates.Contains(key)) || blockedKeys.Any(key => !allDates.Contains(key)) ||
            blockedKeys.Distinct(StringComparer.Ordinal).Count() != blockedKeys.Length) return false;
        var blockedSet = blockedKeys.ToHashSet(StringComparer.Ordinal);
        if (currentKeys.Any(key => !blockedSet.Contains(key))) return false;
        blocked = blockedSet;
        error = string.Empty;
        return true;
    }

    private static bool Text(JsonElement row, string name, string expected) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == expected;

    private static bool Integer(JsonElement row, string name, out int value)
    {
        value = 0;
        return row.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out value);
    }
}
