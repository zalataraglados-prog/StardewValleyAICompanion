using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

internal static class GoldenWalnutFreshSaveProgressProjectionComparer
{
    public static bool SameGingerIslandProgress(
        JsonElement left,
        JsonElement right)
    {
        string[][] paths =
        [
            ["status"],
            ["community_center_or_warehouse_done"],
            ["invitation", "received"],
            ["invitation", "pending_for_tomorrow"],
            ["invitation", "present_in_mailbox"],
            ["repairs", "battery", "complete_or_pending"],
            ["repairs", "hull", "complete_or_pending"],
            ["repairs", "anchor", "complete_or_pending"],
            ["repairs", "all_complete_or_pending"],
            ["repairs", "boat_fixed_received"],
            ["repairs", "boat_fixed_pending"],
            ["journey", "departure_event_active"],
            ["journey", "boat_journey_active"],
            ["journey", "boat_rides_to_island"],
            ["journey", "island_reached"],
            ["walnuts", "current"],
            ["walnuts", "found"],
            ["first_parrot", "complete_or_pending"],
            ["resort_parrot", "complete_or_pending"],
            ["western_turtle_parrot", "complete_or_pending"],
            ["island_house_parrot", "complete_or_pending"],
            ["target_access", "island_south_accessible"],
            ["target_access", "island_north_accessible"],
            ["target_access", "island_west_accessible"],
            ["target_access", "island_south_east_accessible"],
            ["target_access", "island_south_east_cave_accessible"]
        ];
        return SameProgressPaths(left, right, paths);
    }

    public static bool SameGoldenWalnutSourceProgress(
        JsonElement left,
        JsonElement right)
    {
        string[][] aggregatePaths =
        [
            ["tracked_collected_quantity"],
            ["tracked_remaining_quantity"],
            ["pre_resort_remaining_gross_quantity"],
            ["pre_resort_spend_safe_remaining_quantity"],
            ["world_golden_walnuts_found"],
            ["tracker_to_world_found_delta"]
        ];
        if (!SameProgressPaths(left, right, aggregatePaths))
            return false;
        if (!TryReadPath(left, ["sources"], out var leftSources) ||
            !TryReadPath(right, ["sources"], out var rightSources) ||
            leftSources.ValueKind != JsonValueKind.Array ||
            rightSources.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var leftRows = SourceProgressRows(leftSources);
        var rightRows = SourceProgressRows(rightSources);
        return leftRows is not null &&
            rightRows is not null &&
            leftRows.Count == rightRows.Count &&
            leftRows.All(pair =>
                rightRows.TryGetValue(pair.Key, out var rightRow) &&
                string.Equals(pair.Value, rightRow, StringComparison.Ordinal));
    }

    private static Dictionary<string, string>? SourceProgressRows(
        JsonElement sources)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources.EnumerateArray())
        {
            if (!source.TryGetProperty("source_key", out var keyElement) ||
                keyElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            var key = keyElement.GetString() ?? string.Empty;
            string[][] paths =
            [
                ["native_quantity"],
                ["collected_quantity"],
                ["remaining_quantity"],
                ["required_mail_satisfied"],
                ["remaining_optional_unlock_walnut_cost"],
                ["required_secret_note_satisfied"]
            ];
            var values = new List<string>(paths.Length);
            foreach (var path in paths)
            {
                values.Add(TryReadPath(source, path, out var value)
                    ? value.GetRawText()
                    : "<missing>");
            }
            if (string.IsNullOrWhiteSpace(key) ||
                !rows.TryAdd(key, string.Join('|', values)))
            {
                return null;
            }
        }
        return rows;
    }

    private static bool SameProgressPaths(
        JsonElement left,
        JsonElement right,
        IReadOnlyList<string[]> paths)
    {
        if (left.ValueKind != JsonValueKind.Object ||
            right.ValueKind != JsonValueKind.Object)
        {
            return left.ValueKind == right.ValueKind &&
                left.GetRawText() == right.GetRawText();
        }

        foreach (var path in paths)
        {
            var hasLeft = TryReadPath(left, path, out var leftValue);
            var hasRight = TryReadPath(right, path, out var rightValue);
            if (hasLeft != hasRight ||
                (hasLeft && leftValue.GetRawText() != rightValue.GetRawText()))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryReadPath(
        JsonElement root,
        IReadOnlyList<string> path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty(segment, out value))
            {
                value = default;
                return false;
            }
        }
        return true;
    }
}
