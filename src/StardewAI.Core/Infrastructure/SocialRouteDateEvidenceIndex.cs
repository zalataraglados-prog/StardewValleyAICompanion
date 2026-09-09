using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure
{
    public sealed class SocialRouteDateMapEvidence
    {
        private readonly HashSet<(int X, int Y)> walkable;
        private readonly HashSet<(int X, int Y)> unsupportedActionTiles;
        private readonly Dictionary<string, int[]> distanceFields =
            new(StringComparer.Ordinal);

        internal SocialRouteDateMapEvidence(
            string locationId,
            int width,
            int height,
            bool accessibleOnCaptureDate,
            HashSet<(int X, int Y)> walkable,
            HashSet<(int X, int Y)> unsupportedActionTiles,
            JsonElement[] actionGates)
        {
            LocationId = locationId;
            Width = width;
            Height = height;
            AccessibleOnCaptureDate = accessibleOnCaptureDate;
            this.walkable = walkable;
            this.unsupportedActionTiles = unsupportedActionTiles;
            ActionGates = actionGates;
        }

        public string LocationId { get; }

        public int Width { get; }

        public int Height { get; }

        public bool AccessibleOnCaptureDate { get; }

        public JsonElement[] ActionGates { get; }

        public int? ShortestDistance(
            int startX,
            int startY,
            int targetX,
            int targetY,
            ISet<(int X, int Y)>? additionalBlocked = null)
        {
            var start = (X: startX, Y: startY);
            var target = (X: targetX, Y: targetY);
            if (startX < 0 || startY < 0 ||
                startX >= Width || startY >= Height ||
                !walkable.Contains(target) ||
                unsupportedActionTiles.Contains(target) ||
                additionalBlocked?.Contains(target) == true)
            {
                return null;
            }

            var distances = GetDistanceField(start, additionalBlocked);
            var distance = distances[target.Y * Width + target.X];
            return distance >= 0 ? distance : null;
        }

        private int[] GetDistanceField(
            (int X, int Y) start,
            ISet<(int X, int Y)>? additionalBlocked)
        {
            var cacheKey = DistanceFieldKey(start, additionalBlocked);
            if (distanceFields.TryGetValue(cacheKey, out var cached))
                return cached;

            var distances = Enumerable.Repeat(-1, Width * Height).ToArray();
            if (start.X < 0 || start.Y < 0 ||
                start.X >= Width || start.Y >= Height)
            {
                distanceFields[cacheKey] = distances;
                return distances;
            }

            var seen = new HashSet<(int X, int Y)> { start };
            var queue = new Queue<(int X, int Y, int Distance)>();
            queue.Enqueue((start.X, start.Y, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                distances[current.Y * Width + current.X] = current.Distance;

                foreach (var next in CardinalNeighbors(current.X, current.Y))
                {
                    if (!walkable.Contains(next) ||
                        unsupportedActionTiles.Contains(next) ||
                        additionalBlocked?.Contains(next) == true ||
                        !seen.Add(next))
                    {
                        continue;
                    }
                    queue.Enqueue((next.X, next.Y, current.Distance + 1));
                }
            }
            distanceFields[cacheKey] = distances;
            return distances;
        }

        private static string DistanceFieldKey(
            (int X, int Y) start,
            ISet<(int X, int Y)>? additionalBlocked)
        {
            if (additionalBlocked is null || additionalBlocked.Count == 0)
                return start.X + "," + start.Y + "|-";
            return start.X + "," + start.Y + "|" + string.Join(
                ";",
                additionalBlocked
                    .OrderBy(tile => tile.Y)
                    .ThenBy(tile => tile.X)
                    .Select(tile => tile.X + "," + tile.Y));
        }

        private static IEnumerable<(int X, int Y)> CardinalNeighbors(
            int x,
            int y)
        {
            yield return (x + 1, y);
            yield return (x - 1, y);
            yield return (x, y + 1);
            yield return (x, y - 1);
        }
    }

    public sealed class SocialRouteDateEvidenceIndex
    {
        private readonly IReadOnlyDictionary<string, SocialRouteDateMapEvidence> maps;

        private SocialRouteDateEvidenceIndex(
            int totalDays,
            IReadOnlyDictionary<string, SocialRouteDateMapEvidence> maps)
        {
            TotalDays = totalDays;
            this.maps = maps;
        }

        public int TotalDays { get; }

        public bool TryGetMap(
            string locationId,
            out SocialRouteDateMapEvidence map) =>
            maps.TryGetValue(locationId, out map!);

        public static bool TryCreate(
            JsonElement source,
            int requiredTotalDays,
            out SocialRouteDateEvidenceIndex index,
            out string[] blockingReasons)
        {
            index = new SocialRouteDateEvidenceIndex(
                requiredTotalDays,
                new Dictionary<string, SocialRouteDateMapEvidence>(
                    StringComparer.OrdinalIgnoreCase));
            var reasons = new List<string>();
            if (!TryUnwrap(source, out var root))
            {
                blockingReasons = new[] { "social_route_date_evidence_unavailable" };
                return false;
            }

            if (!string.Equals(
                    ReadString(root, "schema_version"),
                    "social_route_date_evidence.v2",
                    StringComparison.Ordinal) ||
                ReadInt(root, "capture_total_days", -1) != requiredTotalDays)
            {
                blockingReasons = new[] { "social_route_date_evidence_date_or_schema_mismatch" };
                return false;
            }
            if (ReadBool(root, "all_location_static_walkability_complete") != true ||
                !string.Equals(
                    ReadString(root, "projection_status"),
                    "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
                    StringComparison.Ordinal))
            {
                blockingReasons = new[] { "social_route_date_evidence_incomplete" };
                return false;
            }
            if (!root.TryGetProperty("locations", out var rows) ||
                rows.ValueKind != JsonValueKind.Array ||
                ReadInt(root, "location_count", -1) != rows.GetArrayLength())
            {
                blockingReasons = new[] { "social_route_date_location_catalog_invalid" };
                return false;
            }

            var parsed = new Dictionary<string, SocialRouteDateMapEvidence>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows.EnumerateArray())
            {
                if (!TryReadMap(row, out var map, out var reason))
                {
                    reasons.Add(reason);
                    continue;
                }
                if (!parsed.TryAdd(map.LocationId, map))
                    reasons.Add("social_route_date_location_duplicate:" + map.LocationId);
            }

            if (reasons.Count > 0 || parsed.Count != rows.GetArrayLength())
            {
                blockingReasons = reasons
                    .DefaultIfEmpty("social_route_date_location_catalog_incomplete")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return false;
            }

            index = new SocialRouteDateEvidenceIndex(requiredTotalDays, parsed);
            blockingReasons = Array.Empty<string>();
            return true;
        }

        private static bool TryReadMap(
            JsonElement row,
            out SocialRouteDateMapEvidence map,
            out string reason)
        {
            map = null!;
            reason = "social_route_date_location_invalid";
            if (row.ValueKind != JsonValueKind.Object)
                return false;

            var locationId = ReadString(row, "location_id");
            var width = ReadInt(row, "map_width");
            var height = ReadInt(row, "map_height");
            if (string.IsNullOrWhiteSpace(locationId) ||
                width <= 0 ||
                height <= 0 ||
                !string.Equals(
                    ReadString(row, "projection_status"),
                    "exact_current_date_static_native_walkability",
                    StringComparison.Ordinal) ||
                !row.TryGetProperty("static_walkable_tile_ranges", out var ranges) ||
                ranges.ValueKind != JsonValueKind.Array)
            {
                reason = "social_route_date_location_incomplete:" + locationId;
                return false;
            }

            var walkable = new HashSet<(int X, int Y)>();
            foreach (var range in ranges.EnumerateArray())
            {
                var y = ReadInt(range, "y", -1);
                var startX = ReadInt(range, "start_x", -1);
                var endX = ReadInt(range, "end_x", -1);
                if (y < 0 || y >= height ||
                    startX < 0 || endX < startX || endX >= width)
                {
                    reason = "social_route_date_walkable_range_invalid:" + locationId;
                    return false;
                }
                for (var x = startX; x <= endX; x++)
                {
                    if (!walkable.Add((x, y)))
                    {
                        reason = "social_route_date_walkable_range_overlap:" + locationId;
                        return false;
                    }
                }
            }
            if (ReadInt(row, "static_walkable_tile_count", -1) != walkable.Count)
            {
                reason = "social_route_date_walkable_count_mismatch:" + locationId;
                return false;
            }

            if (!TryReadUnsupportedActionTiles(
                    row,
                    width,
                    height,
                    out var unsupported))
            {
                reason = "social_route_date_unsupported_action_tiles_invalid:" + locationId;
                return false;
            }
            if (!row.TryGetProperty("action_gates", out var gates) ||
                gates.ValueKind != JsonValueKind.Array)
            {
                reason = "social_route_date_action_gates_invalid:" + locationId;
                return false;
            }

            var buildConditions = ReadString(row, "build_conditions");
            var accessible = string.IsNullOrWhiteSpace(buildConditions) ||
                ReadBool(row, "build_conditions_met") == true;
            map = new SocialRouteDateMapEvidence(
                locationId,
                width,
                height,
                accessible,
                walkable,
                unsupported,
                gates.EnumerateArray().Select(gate => gate.Clone()).ToArray());
            reason = string.Empty;
            return true;
        }

        private static bool TryReadUnsupportedActionTiles(
            JsonElement row,
            int width,
            int height,
            out HashSet<(int X, int Y)> tiles)
        {
            tiles = new HashSet<(int X, int Y)>();
            if (!row.TryGetProperty("unsupported_route_action_tiles", out var source) ||
                source.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            var recordCount = 0;
            foreach (var item in source.EnumerateArray())
            {
                var x = ReadInt(item, "tile_x", -1);
                var y = ReadInt(item, "tile_y", -1);
                if (x < 0 || x >= width || y < 0 || y >= height ||
                    !tiles.Add((x, y)) ||
                    !item.TryGetProperty("actions", out var actions) ||
                    actions.ValueKind != JsonValueKind.Array ||
                    actions.GetArrayLength() == 0 ||
                    ReadInt(item, "action_record_count", -1) != actions.GetArrayLength())
                {
                    return false;
                }
                foreach (var action in actions.EnumerateArray())
                {
                    if (action.ValueKind != JsonValueKind.Object ||
                        string.IsNullOrWhiteSpace(ReadString(action, "source_property")) ||
                        string.IsNullOrWhiteSpace(ReadString(action, "raw_action")))
                    {
                        return false;
                    }
                }
                recordCount += actions.GetArrayLength();
            }
            return ReadInt(row, "unsupported_route_action_tile_count", -1) == tiles.Count &&
                ReadInt(row, "unsupported_route_action_record_count", -1) == recordCount;
        }

        private static bool TryUnwrap(
            JsonElement source,
            out JsonElement value)
        {
            value = source;
            if (source.ValueKind != JsonValueKind.Object)
                return false;
            if (!source.TryGetProperty("value", out var wrapped))
                return true;
            if (ReadString(source, "status") != "available" ||
                wrapped.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            value = wrapped;
            return true;
        }

        internal static string ReadString(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        internal static int ReadInt(
            JsonElement source,
            string name,
            int fallback = 0) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var parsed)
                ? parsed
                : fallback;

        internal static bool? ReadBool(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;
    }
}
