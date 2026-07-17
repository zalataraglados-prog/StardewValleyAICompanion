using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateConnectorPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.traverse_connector")
            {
                return Array.Empty<string>();
            }

            var errors = new List<string>();
            if (!ReadIntParameter(action, "target_tile_x").HasValue || !ReadIntParameter(action, "target_tile_y").HasValue)
            {
                errors.Add("connector_target_tile_required");
            }

            if (string.IsNullOrWhiteSpace(ReadParameter(action, "expected_target_location")))
            {
                errors.Add("connector_expected_target_location_required");
            }

            var hasArrivalX = ReadIntParameter(action, "expected_arrival_tile_x").HasValue;
            var hasArrivalY = ReadIntParameter(action, "expected_arrival_tile_y").HasValue;
            if (hasArrivalX != hasArrivalY)
            {
                errors.Add("connector_expected_arrival_tile_pair_required");
            }

            var kind = (ReadParameter(action, "connector_kind") ?? string.Empty).ToLowerInvariant();
            if (!IsRecoveryConnectorKind(kind))
            {
                errors.Add("connector_kind_unsupported");
            }

            if (errors.Count > 0)
            {
                return errors.Distinct(StringComparer.Ordinal).ToArray();
            }

            var edge = new RouteGraphEdge(
                kind,
                ReadStateFieldString(snapshot, "player", "location_id"),
                ReadParameter(action, "expected_target_location") ?? string.Empty,
                ReadIntParameter(action, "target_tile_x"),
                ReadIntParameter(action, "target_tile_y"),
                ReadIntParameter(action, "expected_arrival_tile_x"),
                ReadIntParameter(action, "expected_arrival_tile_y"));
            var connector = FindMatchingCurrentRouteConnector(snapshot, edge);
            if (!connector.HasValue)
            {
                errors.Add("connector_not_transparently_confirmed");
                return errors.ToArray();
            }

            var gateBlock = RecoveryConnectorGateBlock(snapshot, edge);
            if (!string.IsNullOrWhiteSpace(gateBlock))
            {
                errors.Add(gateBlock.Replace("recovery_current_connector_", "connector_", StringComparison.Ordinal));
            }

            if (!RecoveryConnectorPathTiles(snapshot, edge, connector.Value).HasValue)
            {
                errors.Add("connector_start_segment_unreachable");
            }

            return errors.ToArray();
        }

        private static string[] ValidateRouteActionBranches(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "exploration.visit_location")
            {
                return Array.Empty<string>();
            }

            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue || coverage.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            if (!coverage.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return Array.Empty<string>();
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "tile_x") == targetX.Value &&
                    ReadInt(row, "tile_y") == targetY.Value &&
                    row.TryGetProperty("route_training_blocked", out var blocked) &&
                    blocked.ValueKind == JsonValueKind.True)
                {
                    return new[] { "unsupported_route_action_branch_at_target" };
                }
            }

            return Array.Empty<string>();
        }

        private static string[] ValidateRoutePathPreview(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "exploration.visit_location")
            {
                return Array.Empty<string>();
            }

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var startX = ReadIntParameter(action, "start_tile_x") ?? ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadIntParameter(action, "start_tile_y") ?? ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            if (!targetX.HasValue || !targetY.HasValue || !startX.HasValue || !startY.HasValue)
            {
                return Array.Empty<string>();
            }

            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return Array.Empty<string>();
            }

            var blockedTiles = ReadBlockedCollisionTiles(grid.Value);
            var unsupportedTiles = ReadUnsupportedRouteActionTiles(snapshot);

            var pathTarget = ResolveBoundaryWarpStandTile(snapshot, targetX.Value, targetY.Value, width, height);
            if (!TileInBounds(startX.Value, startY.Value, width, height) || pathTarget is null)
            {
                return new[] { "route_path_target_out_of_collision_grid" };
            }

            if (blockedTiles.Contains(TileKey(pathTarget.X, pathTarget.Y)))
            {
                return new[] { "route_path_target_blocked_by_collision_grid" };
            }

            if (PathExists(startX.Value, startY.Value, pathTarget.X, pathTarget.Y, width, height, blockedTiles, unsupportedTiles))
            {
                return Array.Empty<string>();
            }

            if (PathExists(startX.Value, startY.Value, pathTarget.X, pathTarget.Y, width, height, blockedTiles, new HashSet<string>(StringComparer.Ordinal)))
            {
                return new[] { "unsupported_route_action_branch_on_path" };
            }

            return new[] { "route_path_blocked_by_collision_grid" };
        }

        private static string[] ValidateRouteGraphPreview(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "exploration.visit_location")
            {
                return Array.Empty<string>();
            }

            var targetLocation = ReadParameter(action, "target_location");
            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (string.IsNullOrWhiteSpace(targetLocation) || string.IsNullOrWhiteSpace(currentLocation) || string.Equals(targetLocation, currentLocation, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<string>();
            }

            var graph = ReadStateFieldValue(snapshot, "locations", "route_graph");
            if (!graph.HasValue || graph.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            var path = FindResolvedRouteGraphPath(graph.Value, currentLocation, targetLocation);
            if (path.Length == 0)
            {
                return new[] { "route_graph_no_resolved_path" };
            }

            return ValidateRouteGraphStartSegment(action, snapshot, path[0]);
        }

        private static string[] ValidateRouteGraphStartSegment(SmallModelAction action, SnapshotEnvelope snapshot, RouteGraphEdge firstEdge)
        {
            if (!firstEdge.FromX.HasValue || !firstEdge.FromY.HasValue)
            {
                return Array.Empty<string>();
            }

            var startX = ReadIntParameter(action, "start_tile_x") ?? ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadIntParameter(action, "start_tile_y") ?? ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!startX.HasValue || !startY.HasValue || !grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return Array.Empty<string>();
            }

            var startConnectorTile = ResolveBoundaryWarpStandTile(
                snapshot,
                firstEdge.FromX.Value,
                firstEdge.FromY.Value,
                width,
                height,
                firstEdge.Kind);
            if (startConnectorTile is null)
            {
                return new[] { "route_graph_start_connector_out_of_collision_grid" };
            }

            var blockedTiles = ReadBlockedCollisionTiles(grid.Value);
            if (blockedTiles.Contains(TileKey(startConnectorTile.X, startConnectorTile.Y)))
            {
                return new[] { "route_graph_start_connector_blocked_by_collision_grid" };
            }

            var unsupportedTiles = ReadUnsupportedRouteActionTiles(snapshot);
            if (PathExists(startX.Value, startY.Value, startConnectorTile.X, startConnectorTile.Y, width, height, blockedTiles, unsupportedTiles))
            {
                return Array.Empty<string>();
            }

            if (PathExists(startX.Value, startY.Value, startConnectorTile.X, startConnectorTile.Y, width, height, blockedTiles, new HashSet<string>(StringComparer.Ordinal)))
            {
                return new[] { "unsupported_route_action_branch_on_start_segment" };
            }

            return new[] { "route_graph_start_segment_blocked_by_collision_grid" };
        }

        private static SleepStandTile? ResolveBoundaryWarpStandTile(
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY,
            int width,
            int height,
            string? knownKind = null)
        {
            if (TileInBounds(targetX, targetY, width, height))
            {
                return new SleepStandTile(targetX, targetY);
            }

            var kind = knownKind;
            if (string.IsNullOrWhiteSpace(kind))
            {
                var connectors = ReadStateFieldValue(snapshot, "locations", "route_connectors");
                if (connectors.HasValue &&
                    connectors.Value.ValueKind == JsonValueKind.Object &&
                    connectors.Value.TryGetProperty("connectors", out var rows) &&
                    rows.ValueKind == JsonValueKind.Array)
                {
                    kind = rows.EnumerateArray()
                        .Where(row => row.ValueKind == JsonValueKind.Object &&
                            ReadNullableInt(row, "tile_x") == targetX &&
                            ReadNullableInt(row, "tile_y") == targetY)
                        .Select(row => ReadString(row, "kind"))
                        .FirstOrDefault();
                }
            }

            if (!string.Equals(kind, "warp", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (targetX < 0 && targetY >= 0 && targetY < height)
            {
                return new SleepStandTile(0, targetY);
            }
            if (targetX >= width && targetY >= 0 && targetY < height)
            {
                return new SleepStandTile(width - 1, targetY);
            }
            if (targetY < 0 && targetX >= 0 && targetX < width)
            {
                return new SleepStandTile(targetX, 0);
            }
            if (targetY >= height && targetX >= 0 && targetX < width)
            {
                return new SleepStandTile(targetX, height - 1);
            }

            return null;
        }

        private sealed class RouteGraphEdge
        {
            public RouteGraphEdge(string kind, string fromLocation, string targetLocation, int? fromX, int? fromY, int? targetX, int? targetY)
            {
                Kind = kind;
                FromLocation = fromLocation;
                TargetLocation = targetLocation;
                FromX = fromX;
                FromY = fromY;
                TargetX = targetX;
                TargetY = targetY;
            }

            public string Kind { get; }
            public string FromLocation { get; }
            public string TargetLocation { get; }
            public int? FromX { get; }
            public int? FromY { get; }
            public int? TargetX { get; }
            public int? TargetY { get; }
        }

        private sealed class SleepStandTile
        {
            public SleepStandTile(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }

        private sealed class SleepMacroTarget
        {
            public string HomeLocation { get; set; } = "FarmHouse";
            public int BedX { get; set; }
            public int BedY { get; set; }
            public int StandX { get; set; }
            public int StandY { get; set; }
            public int FaceDirection { get; set; }
            public int EstimatedTicks { get; set; }
        }

        private sealed class RecoveryRouteStep
        {
            public RouteGraphEdge Edge { get; set; } = null!;
            public int PathTiles { get; set; }
            public int EstimatedTicks { get; set; }
            public int RemainingConnectorCount { get; set; }
        }

        private sealed class RecoveryRoutePlanResult
        {
            public RecoveryRouteStep? Step { get; set; }
            public string[] BlockReasons { get; set; } = Array.Empty<string>();
        }

        private static RecoveryRoutePlanResult BuildRecoveryRoutePlan(SnapshotEnvelope snapshot)
        {
            var homeContext = ReadStateFieldValue(snapshot, "current_location", "home_context");
            if (!homeContext.HasValue || homeContext.Value.ValueKind != JsonValueKind.Object)
            {
                return BlockedRecoveryRoute("recovery_home_context_unavailable");
            }

            var homeLocation = ReadString(homeContext.Value, "home_location_id");
            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (!ReadBool(homeContext.Value, "home_available") ||
                string.IsNullOrWhiteSpace(homeLocation) ||
                string.IsNullOrWhiteSpace(currentLocation))
            {
                return BlockedRecoveryRoute("recovery_home_route_target_unavailable");
            }

            if (ReadBool(homeContext.Value, "current_location_is_home") ||
                string.Equals(currentLocation, homeLocation, StringComparison.OrdinalIgnoreCase))
            {
                return BlockedRecoveryRoute("sleep_target_unavailable");
            }

            var graph = ReadStateFieldValue(snapshot, "locations", "route_graph");
            if (!graph.HasValue || graph.Value.ValueKind != JsonValueKind.Object)
            {
                return BlockedRecoveryRoute("recovery_route_graph_unavailable");
            }

            var graphEdges = ReadResolvedRouteGraphEdges(graph.Value)
                .Where(edge => IsRecoveryConnectorKind(edge.Kind))
                .ToArray();
            var outgoing = graphEdges
                .Where(edge =>
                    string.Equals(edge.FromLocation, currentLocation, StringComparison.OrdinalIgnoreCase) &&
                    edge.FromX.HasValue &&
                    edge.FromY.HasValue)
                .ToArray();
            if (outgoing.Length == 0)
            {
                return BlockedRecoveryRoute("recovery_route_graph_no_executable_outgoing_connector");
            }

            var candidates = new List<RecoveryRouteStep>();
            var observedBlocks = new List<string>();
            foreach (var edge in outgoing)
            {
                var remainingPath = string.Equals(edge.TargetLocation, homeLocation, StringComparison.OrdinalIgnoreCase)
                    ? Array.Empty<RouteGraphEdge>()
                    : FindResolvedRouteGraphPath(graphEdges, edge.TargetLocation, homeLocation);
                if (!string.Equals(edge.TargetLocation, homeLocation, StringComparison.OrdinalIgnoreCase) && remainingPath.Length == 0)
                {
                    observedBlocks.Add("recovery_route_graph_no_path_home");
                    continue;
                }

                var connector = FindMatchingCurrentRouteConnector(snapshot, edge);
                if (!connector.HasValue)
                {
                    observedBlocks.Add("recovery_current_connector_not_transparently_confirmed");
                    continue;
                }

                var gateBlock = RecoveryConnectorGateBlock(snapshot, edge);
                if (!string.IsNullOrWhiteSpace(gateBlock))
                {
                    observedBlocks.Add(gateBlock);
                    continue;
                }

                var pathTiles = RecoveryConnectorPathTiles(snapshot, edge, connector.Value);
                if (!pathTiles.HasValue)
                {
                    observedBlocks.Add("recovery_current_connector_start_segment_unreachable");
                    continue;
                }

                candidates.Add(new RecoveryRouteStep
                {
                    Edge = edge,
                    PathTiles = pathTiles.Value,
                    EstimatedTicks = Math.Max(60, (pathTiles.Value + 1) * 60),
                    RemainingConnectorCount = remainingPath.Length + 1
                });
            }

            var selected = candidates
                .OrderBy(candidate => candidate.RemainingConnectorCount)
                .ThenBy(candidate => candidate.PathTiles)
                .ThenBy(candidate => candidate.Edge.FromY)
                .ThenBy(candidate => candidate.Edge.FromX)
                .FirstOrDefault();
            return selected is null
                ? new RecoveryRoutePlanResult
                {
                    BlockReasons = observedBlocks
                        .DefaultIfEmpty("recovery_route_home_step_unavailable")
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                }
                : new RecoveryRoutePlanResult { Step = selected };
        }

        private static RecoveryRoutePlanResult BlockedRecoveryRoute(string reason)
        {
            return new RecoveryRoutePlanResult { BlockReasons = new[] { reason } };
        }

        private static bool IsRecoveryConnectorKind(string kind)
        {
            return kind is "warp" or "touch_action_warp" or "action_warp" or "locked_door_warp" or "building_door";
        }

        private static JsonElement? FindMatchingCurrentRouteConnector(SnapshotEnvelope snapshot, RouteGraphEdge edge)
        {
            var routeConnectors = ReadStateFieldValue(snapshot, "locations", "route_connectors");
            if (!routeConnectors.HasValue ||
                routeConnectors.Value.ValueKind != JsonValueKind.Object ||
                !routeConnectors.Value.TryGetProperty("connectors", out var connectors) ||
                connectors.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var connectorLocation = ReadString(routeConnectors.Value, "location_id");
            if (string.IsNullOrWhiteSpace(connectorLocation) ||
                !string.Equals(connectorLocation, edge.FromLocation, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            foreach (var connector in connectors.EnumerateArray())
            {
                if (connector.ValueKind != JsonValueKind.Object ||
                    ReadBool(connector, "resolved") != true ||
                    !string.Equals(ReadString(connector, "kind"), edge.Kind, StringComparison.OrdinalIgnoreCase) ||
                    ReadNullableInt(connector, "tile_x") != edge.FromX ||
                    ReadNullableInt(connector, "tile_y") != edge.FromY ||
                    !string.Equals(ReadString(connector, "target_location"), edge.TargetLocation, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var sourceProperty = ReadString(connector, "source_property");
                if ((edge.Kind is "action_warp" or "locked_door_warp") &&
                    !string.Equals(sourceProperty, "Buildings.Action", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (edge.Kind == "touch_action_warp" &&
                    !string.Equals(sourceProperty, "Back.TouchAction", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var connectorTargetX = ReadNullableInt(connector, "target_x");
                var connectorTargetY = ReadNullableInt(connector, "target_y");
                if (edge.TargetX.HasValue && edge.TargetY.HasValue &&
                    (connectorTargetX != edge.TargetX || connectorTargetY != edge.TargetY))
                {
                    continue;
                }

                return connector;
            }

            return null;
        }

        private static string? RecoveryConnectorGateBlock(SnapshotEnvelope snapshot, RouteGraphEdge edge)
        {
            if (edge.Kind is "warp" or "building_door")
            {
                return null;
            }

            var gateContext = ReadStateFieldValue(snapshot, "locations", "route_gate_context");
            if (!gateContext.HasValue ||
                gateContext.Value.ValueKind != JsonValueKind.Object ||
                !gateContext.Value.TryGetProperty("action_gates", out var gates) ||
                gates.ValueKind != JsonValueKind.Array)
            {
                return "recovery_current_connector_gate_context_unavailable";
            }

            foreach (var gate in gates.EnumerateArray())
            {
                if (gate.ValueKind != JsonValueKind.Object ||
                    ReadNullableInt(gate, "tile_x") != edge.FromX ||
                    ReadNullableInt(gate, "tile_y") != edge.FromY)
                {
                    continue;
                }

                if (!gate.TryGetProperty("allowed_now", out var allowed) ||
                    allowed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return "recovery_current_connector_gate_unresolved";
                }

                return allowed.ValueKind == JsonValueKind.True
                    ? null
                    : "recovery_current_connector_gate_closed";
            }

            return "recovery_current_connector_gate_unresolved";
        }

        private static int? RecoveryConnectorPathTiles(SnapshotEnvelope snapshot, RouteGraphEdge edge, JsonElement connector)
        {
            var startX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!startX.HasValue || !startY.HasValue || !grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0 || !TileInBounds(startX.Value, startY.Value, width, height))
            {
                return null;
            }

            var blocked = ReadBlockedCollisionTiles(grid.Value);
            var unsupported = ReadUnsupportedRouteActionTiles(snapshot);
            var standTiles = new List<SleepStandTile>();
            var standX = ReadNullableInt(connector, "stand_tile_x");
            var standY = ReadNullableInt(connector, "stand_tile_y");
            if (standX.HasValue && standY.HasValue)
            {
                standTiles.Add(new SleepStandTile(standX.Value, standY.Value));
            }
            else if (edge.FromX!.Value < 0)
            {
                standTiles.Add(new SleepStandTile(0, Math.Clamp(edge.FromY!.Value, 0, height - 1)));
            }
            else if (edge.FromX.Value >= width)
            {
                standTiles.Add(new SleepStandTile(width - 1, Math.Clamp(edge.FromY!.Value, 0, height - 1)));
            }
            else if (edge.FromY!.Value < 0)
            {
                standTiles.Add(new SleepStandTile(Math.Clamp(edge.FromX.Value, 0, width - 1), 0));
            }
            else if (edge.FromY.Value >= height)
            {
                standTiles.Add(new SleepStandTile(Math.Clamp(edge.FromX.Value, 0, width - 1), height - 1));
            }
            else
            {
                standTiles.AddRange(new[]
                {
                    new SleepStandTile(edge.FromX.Value + 1, edge.FromY.Value),
                    new SleepStandTile(edge.FromX.Value - 1, edge.FromY.Value),
                    new SleepStandTile(edge.FromX.Value, edge.FromY.Value + 1),
                    new SleepStandTile(edge.FromX.Value, edge.FromY.Value - 1)
                });
            }

            return standTiles
                .Where(tile =>
                    TileInBounds(tile.X, tile.Y, width, height) &&
                    !blocked.Contains(TileKey(tile.X, tile.Y)) &&
                    !unsupported.Contains(TileKey(tile.X, tile.Y)))
                .Select(tile => ShortestPathLength(startX.Value, startY.Value, tile.X, tile.Y, width, height, blocked, unsupported))
                .Where(length => length.HasValue)
                .OrderBy(length => length)
                .FirstOrDefault();
        }

        private static RouteGraphEdge[] FindResolvedRouteGraphPath(JsonElement graph, string startLocation, string targetLocation)
        {
            return FindResolvedRouteGraphPath(ReadResolvedRouteGraphEdges(graph), startLocation, targetLocation);
        }

        private static RouteGraphEdge[] ReadResolvedRouteGraphEdges(JsonElement graph)
        {
            if (!graph.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<RouteGraphEdge>();
            }

            var resolvedEdges = new List<RouteGraphEdge>();
            foreach (var edge in edges.EnumerateArray())
            {
                if (edge.ValueKind != JsonValueKind.Object ||
                    !edge.TryGetProperty("resolved", out var resolved) ||
                    resolved.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                var kind = ReadString(edge, "kind");
                var from = ReadString(edge, "from_location");
                var target = ReadString(edge, "target_location");
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                resolvedEdges.Add(new RouteGraphEdge(
                    string.IsNullOrWhiteSpace(kind) ? "unknown" : kind,
                    from,
                    target,
                    ReadNullableInt(edge, "from_x"),
                    ReadNullableInt(edge, "from_y"),
                    ReadNullableInt(edge, "target_x"),
                    ReadNullableInt(edge, "target_y")));
            }

            return resolvedEdges.ToArray();
        }

        private static RouteGraphEdge[] FindResolvedRouteGraphPath(RouteGraphEdge[] edges, string startLocation, string targetLocation)
        {
            var adjacency = new Dictionary<string, List<RouteGraphEdge>>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in edges)
            {
                if (!adjacency.TryGetValue(edge.FromLocation, out var targets))
                {
                    targets = new List<RouteGraphEdge>();
                    adjacency[edge.FromLocation] = targets;
                }

                targets.Add(edge);
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startLocation };
            var queue = new Queue<(string Location, RouteGraphEdge[] Path)>();
            queue.Enqueue((startLocation, Array.Empty<RouteGraphEdge>()));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (string.Equals(current.Location, targetLocation, StringComparison.OrdinalIgnoreCase))
                {
                    return current.Path;
                }

                if (!adjacency.TryGetValue(current.Location, out var nextEdges))
                {
                    continue;
                }

                foreach (var next in nextEdges)
                {
                    if (visited.Add(next.TargetLocation))
                    {
                        queue.Enqueue((next.TargetLocation, current.Path.Concat(new[] { next }).ToArray()));
                    }
                }
            }

            return Array.Empty<RouteGraphEdge>();
        }

        private static HashSet<string> ReadBlockedCollisionTiles(JsonElement collisionGrid)
        {
            var blockedTiles = new HashSet<string>(StringComparer.Ordinal);
            if (!collisionGrid.TryGetProperty("notable_tiles", out var tiles) || tiles.ValueKind != JsonValueKind.Array)
            {
                return blockedTiles;
            }

            foreach (var tile in tiles.EnumerateArray())
            {
                if (tile.ValueKind == JsonValueKind.Object &&
                    tile.TryGetProperty("collision_blocked", out var blocked) &&
                    blocked.ValueKind == JsonValueKind.True)
                {
                    blockedTiles.Add(TileKey(ReadInt(tile, "tile_x"), ReadInt(tile, "tile_y")));
                }
            }

            return blockedTiles;
        }

        private static HashSet<string> ReadUnsupportedRouteActionTiles(SnapshotEnvelope snapshot)
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
                if (row.ValueKind == JsonValueKind.Object &&
                    row.TryGetProperty("route_training_blocked", out var blocked) &&
                    blocked.ValueKind == JsonValueKind.True)
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
            if (blockedTiles.Contains(startKey) || blockedTiles.Contains(targetKey) || extraBlockedTiles.Contains(startKey) || extraBlockedTiles.Contains(targetKey))
            {
                return false;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal) { startKey };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.X == targetX && current.Y == targetY)
                {
                    return true;
                }

                foreach (var next in Neighbors(current.X, current.Y))
                {
                    if (!TileInBounds(next.X, next.Y, width, height))
                    {
                        continue;
                    }

                    var key = TileKey(next.X, next.Y);
                    if (visited.Contains(key) || blockedTiles.Contains(key) || extraBlockedTiles.Contains(key))
                    {
                        continue;
                    }

                    visited.Add(key);
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static int? ShortestPathLength(int startX, int startY, int targetX, int targetY, int width, int height, HashSet<string> blockedTiles, HashSet<string> extraBlockedTiles)
        {
            var startKey = TileKey(startX, startY);
            var targetKey = TileKey(targetX, targetY);
            if (blockedTiles.Contains(startKey) || blockedTiles.Contains(targetKey) ||
                extraBlockedTiles.Contains(startKey) || extraBlockedTiles.Contains(targetKey))
            {
                return null;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal) { startKey };
            var queue = new Queue<(int X, int Y, int Distance)>();
            queue.Enqueue((startX, startY, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.X == targetX && current.Y == targetY)
                {
                    return current.Distance;
                }

                foreach (var next in Neighbors(current.X, current.Y))
                {
                    if (!TileInBounds(next.X, next.Y, width, height))
                    {
                        continue;
                    }

                    var key = TileKey(next.X, next.Y);
                    if (!visited.Add(key) || blockedTiles.Contains(key) || extraBlockedTiles.Contains(key))
                    {
                        continue;
                    }

                    queue.Enqueue((next.X, next.Y, current.Distance + 1));
                }
            }

            return null;
        }

        private static IEnumerable<(int X, int Y)> Neighbors(int x, int y)
        {
            yield return (x + 1, y);
            yield return (x - 1, y);
            yield return (x, y + 1);
            yield return (x, y - 1);
        }

        private static bool TileInBounds(int x, int y, int width, int height)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        private static string TileKey(int x, int y)
        {
            return x + "," + y;
        }

    }
}
