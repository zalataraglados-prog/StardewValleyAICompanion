using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] RouteRepairClearObstacleCandidates(
            SnapshotEnvelope snapshot,
            IEnumerable<EventCandidate> routeCandidates)
        {
            var blockedRouteTargets = routeCandidates
                .Where(candidate => candidate.TileX.HasValue &&
                    candidate.TileY.HasValue &&
                    candidate.BlockReasons.Any(RouteBlockedByCollision))
                .ToArray();
            if (blockedRouteTargets.Length == 0)
                return Array.Empty<EventCandidate>();

            var allClearCandidates = ClearObstacleCandidates(snapshot)
                .Where(candidate => candidate.TileX.HasValue &&
                    candidate.TileY.HasValue)
                .ToArray();
            var clearCandidates = allClearCandidates
                .Where(candidate => candidate.Available)
                .ToArray();
            if (clearCandidates.Length == 0)
                return Array.Empty<EventCandidate>();

            var grid = ReadStateFieldValue(
                snapshot,
                "locations",
                "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
                return Array.Empty<EventCandidate>();

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            var startX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var startY = ReadStateFieldInt(snapshot, "player", "tile_y");
            if (width <= 0 || height <= 0 ||
                !TileInBounds(startX, startY, width, height))
            {
                return Array.Empty<EventCandidate>();
            }

            var blocked = ReadBlockedCollisionTiles(
                grid.Value,
                width,
                height);
            var unsupported = ReadUnsupportedRouteActionTiles(
                snapshot,
                width,
                height);
            var clearable = ReadClearableTiles(
                allClearCandidates,
                width,
                height);
            var startIndex = TileIndex(startX, startY, width);
            if (unsupported[startIndex])
                return Array.Empty<EventCandidate>();

            var fromStart = RouteClearDistances(
                startIndex,
                width,
                height,
                blocked,
                unsupported,
                clearable,
                reverse: false);
            var toTargetByIndex = new Dictionary<int, int[]>();
            var result = new List<EventCandidate>();
            foreach (var route in blockedRouteTargets)
            {
                var target = ResolveRouteRepairTargetTile(
                    route.TileX!.Value,
                    route.TileY!.Value,
                    width,
                    height,
                    ReadParameter(route.Parameters, "connector_kind"));
                if (target is null)
                    continue;

                var targetIndex = TileIndex(target.X, target.Y, width);
                var before = fromStart[targetIndex];
                if (before <= 0 || before == int.MaxValue ||
                    unsupported[targetIndex])
                {
                    continue;
                }

                if (!toTargetByIndex.TryGetValue(
                        targetIndex,
                        out var toTarget))
                {
                    toTarget = RouteClearDistances(
                        targetIndex,
                        width,
                        height,
                        blocked,
                        unsupported,
                        clearable,
                        reverse: true);
                    toTargetByIndex.Add(targetIndex, toTarget);
                }

                foreach (var clear in clearCandidates)
                {
                    if (!TileInBounds(
                            clear.TileX!.Value,
                            clear.TileY!.Value,
                            width,
                            height))
                    {
                        continue;
                    }
                    var clearIndex = TileIndex(
                        clear.TileX.Value,
                        clear.TileY.Value,
                        width);
                    if (clearIndex == startIndex ||
                        !blocked[clearIndex] ||
                        !clearable[clearIndex] ||
                        fromStart[clearIndex] == int.MaxValue ||
                        toTarget[clearIndex] == int.MaxValue ||
                        fromStart[clearIndex] + toTarget[clearIndex] != before)
                    {
                        continue;
                    }

                    result.Add(RouteRepairCandidate(route, clear));
                }
            }

            return result.ToArray();
        }

        private static EventCandidate RouteRepairCandidate(
            EventCandidate route,
            EventCandidate clear) => new()
            {
                CandidateId = "route-repair:" + route.CandidateId + ":" +
                    clear.CandidateId,
                Kind = clear.Kind,
                Available = true,
                LocationId = clear.LocationId,
                TileX = clear.TileX,
                TileY = clear.TileY,
                ExpectedEffect = "route_repair_for=" + route.CandidateId +
                    ";route_repair_expected_target_location=" +
                    ReadParameter(
                        route.Parameters,
                        "expected_target_location") +
                    ";fresh_snapshot_replan_required=true;" +
                    clear.ExpectedEffect,
                EstimatedTicks = clear.EstimatedTicks,
                EnergyCost = clear.EnergyCost,
                AvailabilityClass = "route_repair_clearable_obstacle",
                BlockReasons = Array.Empty<string>(),
                Parameters = clear.Parameters.Concat(new[]
                {
                    Parameter("route_repair.candidate_id", route.CandidateId),
                    Parameter(
                        "route_repair.expected_target_location",
                        ReadParameter(
                            route.Parameters,
                            "expected_target_location")),
                    Parameter(
                        "route_repair.snapshot_policy",
                        "clear_one_obstacle_then_fresh_snapshot"),
                    Parameter(
                        "route_repair.training_scope",
                        "exact_current_cross_location_connector")
                }).ToArray()
            };

        private static int[] RouteClearDistances(
            int originIndex,
            int width,
            int height,
            bool[] blocked,
            bool[] unsupported,
            bool[] clearable,
            bool reverse)
        {
            var distances = Enumerable.Repeat(
                    int.MaxValue,
                    checked(width * height))
                .ToArray();
            distances[originIndex] = 0;
            var pending = new LinkedList<int>();
            pending.AddFirst(originIndex);
            while (pending.Count > 0)
            {
                var current = pending.First!.Value;
                pending.RemoveFirst();
                var x = current % width;
                var y = current / width;
                RelaxRouteDistance(
                    current,
                    x + 1,
                    y,
                    width,
                    height,
                    blocked,
                    unsupported,
                    clearable,
                    reverse,
                    distances,
                    pending);
                RelaxRouteDistance(
                    current,
                    x - 1,
                    y,
                    width,
                    height,
                    blocked,
                    unsupported,
                    clearable,
                    reverse,
                    distances,
                    pending);
                RelaxRouteDistance(
                    current,
                    x,
                    y + 1,
                    width,
                    height,
                    blocked,
                    unsupported,
                    clearable,
                    reverse,
                    distances,
                    pending);
                RelaxRouteDistance(
                    current,
                    x,
                    y - 1,
                    width,
                    height,
                    blocked,
                    unsupported,
                    clearable,
                    reverse,
                    distances,
                    pending);
            }

            return distances;
        }

        private static void RelaxRouteDistance(
            int current,
            int nextX,
            int nextY,
            int width,
            int height,
            bool[] blocked,
            bool[] unsupported,
            bool[] clearable,
            bool reverse,
            int[] distances,
            LinkedList<int> pending)
        {
            if (!TileInBounds(nextX, nextY, width, height))
                return;
            var next = TileIndex(nextX, nextY, width);
            if (unsupported[next])
                return;

            var entered = reverse ? current : next;
            if (blocked[entered] && !clearable[entered])
                return;
            var stepCost = blocked[entered] ? 1 : 0;
            var nextDistance = distances[current] + stepCost;
            if (distances[next] <= nextDistance)
                return;

            distances[next] = nextDistance;
            if (stepCost == 0)
                pending.AddFirst(next);
            else
                pending.AddLast(next);
        }

        private static CandidateTile? ResolveRouteRepairTargetTile(
            int targetX,
            int targetY,
            int width,
            int height,
            string connectorKind)
        {
            if (TileInBounds(targetX, targetY, width, height))
                return new CandidateTile(targetX, targetY);
            if (!string.Equals(
                    connectorKind,
                    "warp",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (targetX < 0 && targetY >= 0 && targetY < height)
                return new CandidateTile(0, targetY);
            if (targetX >= width && targetY >= 0 && targetY < height)
                return new CandidateTile(width - 1, targetY);
            if (targetY < 0 && targetX >= 0 && targetX < width)
                return new CandidateTile(targetX, 0);
            if (targetY >= height && targetX >= 0 && targetX < width)
                return new CandidateTile(targetX, height - 1);
            return null;
        }

        private static bool[] ReadBlockedCollisionTiles(
            JsonElement collisionGrid,
            int width,
            int height)
        {
            var result = new bool[checked(width * height)];
            if (!collisionGrid.TryGetProperty(
                    "notable_tiles",
                    out var notableTiles) ||
                notableTiles.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var tile in notableTiles.EnumerateArray())
            {
                if (tile.ValueKind != JsonValueKind.Object ||
                    ReadBool(tile, "collision_blocked") != true)
                {
                    continue;
                }
                var x = ReadInt(tile, "tile_x");
                var y = ReadInt(tile, "tile_y");
                if (TileInBounds(x, y, width, height))
                    result[TileIndex(x, y, width)] = true;
            }
            return result;
        }

        private static bool[] ReadUnsupportedRouteActionTiles(
            SnapshotEnvelope snapshot,
            int width,
            int height)
        {
            var result = new bool[checked(width * height)];
            var coverage = ReadStateFieldValue(
                snapshot,
                "locations",
                "route_action_branch_coverage");
            if (!coverage.HasValue ||
                coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object ||
                    ReadBool(row, "route_training_blocked") != true)
                {
                    continue;
                }
                var x = ReadInt(row, "tile_x");
                var y = ReadInt(row, "tile_y");
                if (TileInBounds(x, y, width, height))
                    result[TileIndex(x, y, width)] = true;
            }
            return result;
        }

        private static bool[] ReadClearableTiles(
            IEnumerable<EventCandidate> candidates,
            int width,
            int height)
        {
            var result = new bool[checked(width * height)];
            foreach (var candidate in candidates)
            {
                var x = candidate.TileX!.Value;
                var y = candidate.TileY!.Value;
                if (TileInBounds(x, y, width, height))
                    result[TileIndex(x, y, width)] = true;
            }
            return result;
        }

        private static bool RouteBlockedByCollision(string reason) =>
            reason is "route_path_target_blocked_by_collision_grid" or
                "route_path_blocked_by_collision_grid" or
                "route_graph_start_connector_blocked_by_collision_grid" or
                "route_graph_start_segment_blocked_by_collision_grid" or
                "connector_start_segment_unreachable";

        private static int TileIndex(int x, int y, int width) =>
            checked((y * width) + x);

        private static string TileKey(int x, int y) =>
            x.ToString() + "," + y.ToString();

        private static bool TileInBounds(
            int x,
            int y,
            int width,
            int height) =>
            x >= 0 && y >= 0 && x < width && y < height;
    }
}
