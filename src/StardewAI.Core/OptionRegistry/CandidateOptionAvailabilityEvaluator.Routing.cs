using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] RouteConnectorCandidates(SnapshotEnvelope snapshot, int maxCandidates = 32)
        {
            var cache = routeConnectorCandidatesBySnapshot.GetValue(
                snapshot,
                _ => new RouteConnectorCandidateCache());
            EventCandidate[] candidates;
            lock (cache)
            {
                if (cache.Candidates is null)
                {
                    cache.Candidates = BuildRouteConnectorCandidates(snapshot);
                    Interlocked.Increment(ref routeConnectorCandidateBuildCount);
                }

                candidates = cache.Candidates;
            }

            return candidates
                .Take(Math.Max(1, maxCandidates))
                .ToArray();
        }

        private EventCandidate[] BuildRouteConnectorCandidates(SnapshotEnvelope snapshot)
        {
            var routeConnectors = ReadStateFieldValue(snapshot, "locations", "route_connectors");
            if (!routeConnectors.HasValue ||
                routeConnectors.Value.ValueKind != JsonValueKind.Object ||
                !routeConnectors.Value.TryGetProperty("connectors", out var connectors) ||
                connectors.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadString(routeConnectors.Value, "location_id");
            if (string.IsNullOrWhiteSpace(locationId))
            {
                locationId = ReadStateFieldString(snapshot, "player", "location_id");
            }

            var routeCandidates = connectors.EnumerateArray()
                .Where(connector => connector.ValueKind == JsonValueKind.Object && HasNumber(connector, "tile_x") && HasNumber(connector, "tile_y"))
                .Select(connector => RouteConnectorCandidate(snapshot, connector, locationId))
                .ToArray();
            return routeCandidates
                .Concat(RouteRepairClearObstacleCandidates(snapshot, routeCandidates))
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY ?? 0)
                .ThenBy(candidate => candidate.TileX ?? 0)
                .ToArray();
        }

        private sealed class RouteConnectorCandidateCache
        {
            public EventCandidate[]? Candidates { get; set; }
        }

        private EventCandidate RouteConnectorCandidate(SnapshotEnvelope snapshot, JsonElement connector, string locationId)
        {
            var x = ReadInt(connector, "tile_x");
            var y = ReadInt(connector, "tile_y");
            var kind = ReadString(connector, "kind").ToLowerInvariant();
            var targetLocation = ReadString(connector, "target_location");
            var resolved = ReadBool(connector, "resolved") == true;
            var actionParameters = new List<SmallModelActionParameter>
            {
                Parameter("target_tile_x", x.ToString()),
                Parameter("target_tile_y", y.ToString()),
                Parameter("connector_kind", kind),
                Parameter("expected_target_location", targetLocation)
            };
            var targetX = ReadNullableInt(connector, "target_x");
            var targetY = ReadNullableInt(connector, "target_y");
            if (targetX.HasValue && targetY.HasValue)
            {
                actionParameters.Add(Parameter("expected_arrival_tile_x", targetX.Value.ToString()));
                actionParameters.Add(Parameter("expected_arrival_tile_y", targetY.Value.ToString()));
            }

            var routePreviewParameters = new List<SmallModelActionParameter>
            {
                Parameter("target_tile_x", x.ToString()),
                Parameter("target_tile_y", y.ToString())
            };
            if (!string.IsNullOrWhiteSpace(targetLocation))
            {
                routePreviewParameters.Add(Parameter("target_location", targetLocation));
            }

            var routePreviewBlocks = kind is "action_warp" or "locked_door_warp" or "building_door"
                ? Array.Empty<string>()
                : CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                {
                    OptionId = "exploration.visit_location",
                    Parameters = routePreviewParameters.ToArray()
                });
            var executionProbe = CompilerProbeItem(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.traverse_connector",
                Parameters = actionParameters.ToArray()
            });
            var blockReasons = routePreviewBlocks
                .Concat(CompilerProbeBlockingReasons(executionProbe))
                .Concat(!resolved
                    ? new[] { "route_connector_unresolved" }
                    : Array.Empty<string>())
                .Concat(string.IsNullOrWhiteSpace(targetLocation)
                    ? new[] { "route_connector_target_location_required" }
                    : Array.Empty<string>())
                .Concat(string.Equals(
                        locationId,
                        targetLocation,
                        StringComparison.OrdinalIgnoreCase)
                    ? new[] { "route_connector_cross_location_target_required" }
                    : Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var normalizedParameters = executionProbe?.NormalizedCommand.Parameters ?? actionParameters.ToArray();
            var candidateParameters = new[]
                {
                    Parameter("execution_option_id", "executor.traverse_connector"),
                    Parameter("route.source_location_id", locationId),
                    Parameter("route.target_location_id", targetLocation),
                    Parameter(
                        "route.connector_resolved",
                        resolved.ToString().ToLowerInvariant()),
                    Parameter(
                        "route.snapshot_policy",
                        "one_connector_then_fresh_snapshot"),
                    Parameter(
                        "route.training_scope",
                        "exact_current_cross_location_connector")
                }
                .Concat(normalizedParameters)
                .ToArray();
            var estimatedTicks = ReadParameterInt(normalizedParameters, "estimated_ticks") ?? 0;
            var gateTimeline = ReadRouteGateTimeline(snapshot, x, y);
            var expectedEffect = "player.tile=" + x + "," + y +
                ";route_source_location=" + locationId +
                ";route_connector=" + kind +
                ";expected_target_location=" + targetLocation +
                ";route_connector_resolved=" +
                resolved.ToString().ToLowerInvariant() +
                ";route_training_scope=exact_current_cross_location_connector" +
                ";fresh_snapshot_replan_required=true";
            if (targetX.HasValue && targetY.HasValue)
            {
                expectedEffect += ";expected_arrival_tile=" + targetX.Value + "," + targetY.Value;
            }

            return new EventCandidate
            {
                CandidateId = RouteCandidateId(
                    locationId,
                    x,
                    y,
                    kind,
                    targetLocation,
                    targetX,
                    targetY),
                Kind = "route_connector_tile",
                Available = blockReasons.Length == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                ExpectedEffect = expectedEffect,
                EstimatedTicks = estimatedTicks,
                EnergyCost = 0,
                AvailabilityClass = gateTimeline.HasValue ? "windowed_route_connector" : "state_gated_route_connector",
                AllowedNow = gateTimeline?.AllowedNow,
                AllowedToday = gateTimeline?.AllowedToday,
                NextOpenTime = gateTimeline?.NextOpenTime,
                EffectiveOpenTime = gateTimeline?.EffectiveOpenTime,
                ClosesAt = gateTimeline?.ClosesAt,
                WaitCost = gateTimeline?.WaitCost,
                GateReasons = gateTimeline?.GateReasons ?? Array.Empty<string>(),
                BlockReasons = blockReasons,
                Parameters = candidateParameters
            };
        }

        private static RouteGateTimeline? ReadRouteGateTimeline(SnapshotEnvelope snapshot, int tileX, int tileY)
        {
            var gateContext = ReadStateFieldValue(snapshot, "locations", "route_gate_context");
            if (!gateContext.HasValue ||
                gateContext.Value.ValueKind != JsonValueKind.Object ||
                !gateContext.Value.TryGetProperty("action_gates", out var gates) ||
                gates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var gate = gates.EnumerateArray().FirstOrDefault(candidate =>
                candidate.ValueKind == JsonValueKind.Object &&
                ReadNullableInt(candidate, "tile_x") == tileX &&
                ReadNullableInt(candidate, "tile_y") == tileY);
            if (gate.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(gate, "kind"), "locked_door_warp", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var allowedNow = ReadBool(gate, "allowed_now");
            var effectiveOpenTime = ReadNullableInt(gate, "effective_open_time") ?? ReadNullableInt(gate, "open_time");
            var closeTime = ReadNullableInt(gate, "close_time");
            var currentTime = ReadStateFieldInt(snapshot, "time", "time");
            var greenRainOverride = ReadBool(gate, "green_rain_override") == true;
            var hardGateOpen = ReadBool(gate, "festival_closed") != true &&
                ReadBool(gate, "seed_shop_wednesday_closed") != true &&
                (ReadBool(gate, "friendship_allowed") != false || greenRainOverride) &&
                string.IsNullOrWhiteSpace(ReadString(gate, "unresolved_reason"));
            var canOpenLaterToday = allowedNow == false &&
                hardGateOpen &&
                effectiveOpenTime.HasValue &&
                closeTime.HasValue &&
                currentTime < effectiveOpenTime.Value &&
                currentTime < closeTime.Value;
            var allowedToday = allowedNow == true || canOpenLaterToday;
            var reasons = new List<string>();
            if (allowedNow == false)
            {
                if (ReadBool(gate, "festival_closed") == true)
                {
                    reasons.Add("route_gate_festival_closed");
                }
                if (ReadBool(gate, "seed_shop_wednesday_closed") == true)
                {
                    reasons.Add("route_gate_seed_shop_wednesday_closed");
                }
                if (ReadBool(gate, "friendship_allowed") == false && !greenRainOverride)
                {
                    reasons.Add("route_gate_friendship_blocked");
                }
                if (effectiveOpenTime.HasValue && currentTime < effectiveOpenTime.Value && hardGateOpen)
                {
                    reasons.Add("route_gate_not_open_yet");
                }
                else if (closeTime.HasValue && currentTime >= closeTime.Value)
                {
                    reasons.Add("route_gate_closed_for_day");
                }
            }

            return new RouteGateTimeline(
                allowedNow,
                allowedToday,
                canOpenLaterToday ? effectiveOpenTime : null,
                effectiveOpenTime,
                closeTime,
                WaitCostTicks(currentTime, effectiveOpenTime, allowedNow, allowedToday),
                reasons.ToArray());
        }

        private readonly struct RouteGateTimeline
        {
            public RouteGateTimeline(
                bool? allowedNow,
                bool? allowedToday,
                int? nextOpenTime,
                int? effectiveOpenTime,
                int? closesAt,
                int? waitCost,
                string[] gateReasons)
            {
                AllowedNow = allowedNow;
                AllowedToday = allowedToday;
                NextOpenTime = nextOpenTime;
                EffectiveOpenTime = effectiveOpenTime;
                ClosesAt = closesAt;
                WaitCost = waitCost;
                GateReasons = gateReasons;
            }

            public bool? AllowedNow { get; }
            public bool? AllowedToday { get; }
            public int? NextOpenTime { get; }
            public int? EffectiveOpenTime { get; }
            public int? ClosesAt { get; }
            public int? WaitCost { get; }
            public string[] GateReasons { get; }
        }

        private EventCandidate[] RouteRepairClearObstacleCandidates(SnapshotEnvelope snapshot, IEnumerable<EventCandidate> routeCandidates)
        {
            var blockedRouteTargets = routeCandidates
                .Where(candidate => candidate.TileX.HasValue &&
                    candidate.TileY.HasValue &&
                    candidate.BlockReasons.Any(RouteBlockedByCollision))
                .ToArray();
            if (blockedRouteTargets.Length == 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var allClearCandidates = ClearObstacleCandidates(snapshot)
                .Where(candidate => candidate.TileX.HasValue && candidate.TileY.HasValue)
                .ToArray();
            var clearCandidates = allClearCandidates
                .Where(candidate => candidate.Available)
                .ToArray();
            if (clearCandidates.Length == 0)
            {
                return Array.Empty<EventCandidate>();
            }

            return blockedRouteTargets
                .SelectMany(route => clearCandidates
                    .Where(clear => ClearCandidateRepairsRoute(
                        snapshot,
                        route,
                        clear,
                        allClearCandidates))
                    .Select(clear => new EventCandidate
                    {
                        CandidateId = "route-repair:" + route.CandidateId + ":" + clear.CandidateId,
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
                            Parameter(
                                "route_repair.candidate_id",
                                route.CandidateId),
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
                    }))
                .ToArray();
        }

        private static string RouteCandidateId(
            string locationId,
            int tileX,
            int tileY,
            string kind,
            string targetLocation,
            int? targetX,
            int? targetY) =>
            "route:" + locationId + ":" + tileX + "," + tileY +
            ":" + kind + ":to=" + targetLocation +
            (targetX.HasValue && targetY.HasValue
                ? ":arrival=" + targetX.Value + "," + targetY.Value
                : string.Empty);

        private static bool ClearCandidateRepairsRoute(
            SnapshotEnvelope snapshot,
            EventCandidate route,
            EventCandidate clear,
            IEnumerable<EventCandidate> allClearCandidates)
        {
            if (!route.TileX.HasValue || !route.TileY.HasValue || !clear.TileX.HasValue || !clear.TileY.HasValue)
            {
                return false;
            }

            var startX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var startY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var target = ResolveRouteRepairTargetTile(
                route.TileX.Value,
                route.TileY.Value,
                width,
                height,
                ReadParameter(route.Parameters, "connector_kind"));
            if (target is null)
            {
                return false;
            }

            var blocked = ReadBlockedCollisionTileKeys(grid.Value);
            var unsupported = ReadUnsupportedRouteActionTileKeys(snapshot);
            var clearable = allClearCandidates
                .Where(candidate => candidate.TileX.HasValue && candidate.TileY.HasValue)
                .Select(candidate => TileKey(candidate.TileX!.Value, candidate.TileY!.Value))
                .ToHashSet(StringComparer.Ordinal);
            var clearKey = TileKey(clear.TileX.Value, clear.TileY.Value);
            if (!blocked.Contains(clearKey) || !clearable.Contains(clearKey))
            {
                return false;
            }

            var before = MinimumRouteClearCount(
                startX,
                startY,
                target.X,
                target.Y,
                width,
                height,
                blocked,
                unsupported,
                clearable);
            if (before <= 0 || before == int.MaxValue)
            {
                return false;
            }

            blocked.Remove(clearKey);
            var after = MinimumRouteClearCount(
                startX,
                startY,
                target.X,
                target.Y,
                width,
                height,
                blocked,
                unsupported,
                clearable);
            return after < before;
        }

        private static CandidateTile? ResolveRouteRepairTargetTile(
            int targetX,
            int targetY,
            int width,
            int height,
            string connectorKind)
        {
            if (TileInBounds(targetX, targetY, width, height))
            {
                return new CandidateTile(targetX, targetY);
            }
            if (!string.Equals(connectorKind, "warp", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (targetX < 0 && targetY >= 0 && targetY < height)
            {
                return new CandidateTile(0, targetY);
            }
            if (targetX >= width && targetY >= 0 && targetY < height)
            {
                return new CandidateTile(width - 1, targetY);
            }
            if (targetY < 0 && targetX >= 0 && targetX < width)
            {
                return new CandidateTile(targetX, 0);
            }
            if (targetY >= height && targetX >= 0 && targetX < width)
            {
                return new CandidateTile(targetX, height - 1);
            }

            return null;
        }

        private static int MinimumRouteClearCount(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int width,
            int height,
            HashSet<string> blocked,
            HashSet<string> unsupported,
            HashSet<string> clearable)
        {
            var startKey = TileKey(startX, startY);
            var targetKey = TileKey(targetX, targetY);
            if (!TileInBounds(startX, startY, width, height) ||
                !TileInBounds(targetX, targetY, width, height) ||
                unsupported.Contains(startKey) ||
                unsupported.Contains(targetKey))
            {
                return int.MaxValue;
            }

            var distances = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [startKey] = 0
            };
            var pending = new LinkedList<CandidateTile>();
            pending.AddFirst(new CandidateTile(startX, startY));
            while (pending.Count > 0)
            {
                var current = pending.First!.Value;
                pending.RemoveFirst();
                var currentKey = TileKey(current.X, current.Y);
                var currentDistance = distances[currentKey];
                if (current.X == targetX && current.Y == targetY)
                {
                    return currentDistance;
                }

                foreach (var next in AdjacentTiles(current.X, current.Y))
                {
                    if (!TileInBounds(next.X, next.Y, width, height))
                    {
                        continue;
                    }

                    var nextKey = TileKey(next.X, next.Y);
                    if (unsupported.Contains(nextKey))
                    {
                        continue;
                    }

                    var blockedStep = blocked.Contains(nextKey);
                    if (blockedStep && !clearable.Contains(nextKey))
                    {
                        continue;
                    }

                    var nextDistance = currentDistance + (blockedStep ? 1 : 0);
                    if (distances.TryGetValue(nextKey, out var existing) &&
                        existing <= nextDistance)
                    {
                        continue;
                    }

                    distances[nextKey] = nextDistance;
                    if (blockedStep)
                    {
                        pending.AddLast(next);
                    }
                    else
                    {
                        pending.AddFirst(next);
                    }
                }
            }

            return int.MaxValue;
        }

        private static bool RouteBlockedByCollision(string reason)
        {
            return reason is "route_path_target_blocked_by_collision_grid" or
                "route_path_blocked_by_collision_grid" or
                "route_graph_start_connector_blocked_by_collision_grid" or
                "route_graph_start_segment_blocked_by_collision_grid" or
                "connector_start_segment_unreachable";
        }

        private static HashSet<string> ReadBlockedCollisionTileKeys(JsonElement collisionGrid)
        {
            var blockedTiles = new HashSet<string>(StringComparer.Ordinal);
            if (!collisionGrid.TryGetProperty("notable_tiles", out var notableTiles) || notableTiles.ValueKind != JsonValueKind.Array)
            {
                return blockedTiles;
            }

            foreach (var tile in notableTiles.EnumerateArray())
            {
                if (tile.ValueKind == JsonValueKind.Object && ReadBool(tile, "collision_blocked") == true)
                {
                    blockedTiles.Add(TileKey(ReadInt(tile, "tile_x"), ReadInt(tile, "tile_y")));
                }
            }

            return blockedTiles;
        }

        private static HashSet<string> ReadUnsupportedRouteActionTileKeys(SnapshotEnvelope snapshot)
        {
            var unsupportedTiles = new HashSet<string>(StringComparer.Ordinal);
            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue ||
                coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return unsupportedTiles;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object && ReadBool(row, "route_training_blocked") == true)
                {
                    unsupportedTiles.Add(TileKey(ReadInt(row, "tile_x"), ReadInt(row, "tile_y")));
                }
            }

            return unsupportedTiles;
        }

        private static bool PathExists(int startX, int startY, int targetX, int targetY, int width, int height, HashSet<string> blockedTiles, HashSet<string> extraBlockedTiles)
        {
            var startKey = TileKey(startX, startY);
            var targetKey = TileKey(targetX, targetY);
            if (!TileInBounds(startX, startY, width, height) ||
                !TileInBounds(targetX, targetY, width, height) ||
                blockedTiles.Contains(startKey) ||
                blockedTiles.Contains(targetKey) ||
                extraBlockedTiles.Contains(targetKey))
            {
                return false;
            }

            var queue = new Queue<CandidateTile>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { startKey };
            queue.Enqueue(new CandidateTile(startX, startY));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.X == targetX && current.Y == targetY)
                {
                    return true;
                }

                foreach (var next in AdjacentTiles(current.X, current.Y))
                {
                    var key = TileKey(next.X, next.Y);
                    if (!TileInBounds(next.X, next.Y, width, height) ||
                        blockedTiles.Contains(key) ||
                        extraBlockedTiles.Contains(key) ||
                        !seen.Add(key))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static IEnumerable<CandidateTile> AdjacentTiles(int x, int y)
        {
            yield return new CandidateTile(x + 1, y);
            yield return new CandidateTile(x - 1, y);
            yield return new CandidateTile(x, y + 1);
            yield return new CandidateTile(x, y - 1);
        }

        private static bool TileInBounds(int x, int y, int width, int height)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        private static string TileKey(int x, int y)
        {
            return x.ToString() + "," + y.ToString();
        }

    }
}
