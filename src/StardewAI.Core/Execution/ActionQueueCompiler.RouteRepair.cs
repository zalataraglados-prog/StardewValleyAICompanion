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
        private static SmallModelPlanStep[] ExpandMoveRouteRepairs(SmallModelPlanStep[] steps, SnapshotEnvelope snapshot)
        {
            var expanded = new List<SmallModelPlanStep>();
            foreach (var step in steps)
            {
                if (string.Equals(step.Kind, "move_to_tile", StringComparison.Ordinal))
                {
                    expanded.AddRange(MoveRepairSteps(step, snapshot));
                }

                expanded.Add(step);
            }

            return expanded.ToArray();
        }

        private static SmallModelPlanStep[] MoveRepairSteps(SmallModelPlanStep moveStep, SnapshotEnvelope snapshot)
        {
            if (!moveStep.TargetTileX.HasValue || !moveStep.TargetTileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var repairs = FindMoveRepairObstacles(snapshot, moveStep.TargetTileX.Value, moveStep.TargetTileY.Value, ReadMoveRepairClearLimit(moveStep));
            if (repairs.Length == 0)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            // The original move step already owns travel time. This budget is only
            // for the extra native tool work introduced by route repair.
            var repairMinutes = repairs.Sum(repair => repair.ClearMinutes);
            var allowedRepairMinutes = ReadMoveRepairMinuteBudget(moveStep);
            if (allowedRepairMinutes.HasValue && repairMinutes > allowedRepairMinutes.Value)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var repairEnergy = repairs.Sum(repair => repair.EnergyCost);
            var availableEnergy = ReadStateFieldDoubleOptional(snapshot, "player", "energy");
            if (availableEnergy.HasValue && repairEnergy > availableEnergy.Value)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            for (var index = 0; index < repairs.Length; index++)
            {
                var repair = repairs[index];
                steps.Add(new SmallModelPlanStep
                {
                    StepId = RepairStepId(moveStep, "move_to_clear_stand", index),
                    Kind = "move_to_tile",
                    TargetLocation = moveStep.TargetLocation,
                    TargetTileX = repair.StandX,
                    TargetTileY = repair.StandY,
                    EstimatedMinutes = repair.MovementMinutes,
                    Preconditions = new[] { "compiler_inserted_move_route_repair=true", "route_repair_index=" + index },
                    ExpectedEffects = new[] { "player.tile=" + repair.StandX + "," + repair.StandY },
                    SafetyConstraints = new[] { "route_repair_stand_tile_reachable_before_clear" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_movement_tiles", Math.Max(1, repair.PathTiles + 1).ToString())
                    }
                });
                steps.Add(new SmallModelPlanStep
                {
                    StepId = RepairStepId(moveStep, "clear_route_obstacle", index),
                    Kind = "clear_obstacle",
                    TargetLocation = moveStep.TargetLocation,
                    TargetTileX = repair.ObstacleX,
                    TargetTileY = repair.ObstacleY,
                    EstimatedMinutes = repair.ClearMinutes,
                    Preconditions = new[] { "compiler_inserted_move_route_repair=true", "route_repair_index=" + index, "target_obstacle_clearable=true", "target_tile_adjacent=true" },
                    ExpectedEffects = new[] { "move_route_repair_for=" + (moveStep.StepId ?? "move_to_tile") + ";current_location.obstacle[" + repair.ObstacleX + "," + repair.ObstacleY + "]=clear" },
                    SafetyConstraints = new[]
                    {
                        "clear_obstacle_from_transparent_current_location_state",
                        "max_route_repair_clears=" + repairs.Length,
                        "route_repair_minutes_budget=" + repairMinutes + "/" + (allowedRepairMinutes.HasValue ? allowedRepairMinutes.Value.ToString() : "unbounded"),
                        "route_repair_energy_budget=" + repairEnergy + "/" + (availableEnergy.HasValue ? availableEnergy.Value.ToString() : "unknown")
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_tool_swings", repair.MaxToolSwings.ToString()),
                        Parameter("route_repair_clear_kind", repair.ClearKind),
                        Parameter("route_repair_energy_cost", repair.EnergyCost.ToString())
                    }
                });
            }

            return steps.ToArray();
        }

        private static string RepairStepId(SmallModelPlanStep moveStep, string suffix, int index)
        {
            return (string.IsNullOrWhiteSpace(moveStep.StepId) ? "move_to_tile" : moveStep.StepId) + ".repair." + index + "." + suffix;
        }

        private static int ReadMoveRepairClearLimit(SmallModelPlanStep moveStep)
        {
            var value = moveStep.Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, "max_route_repair_clears", StringComparison.OrdinalIgnoreCase))?.Value;
            return int.TryParse(value, out var parsed)
                ? Math.Clamp(parsed, 0, HardMaxMoveRouteRepairClears)
                : DefaultMaxMoveRouteRepairClears;
        }

        private static int? ReadMoveRepairMinuteBudget(SmallModelPlanStep moveStep)
        {
            var explicitValue = moveStep.Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, "max_route_repair_minutes", StringComparison.OrdinalIgnoreCase))?.Value;
            if (int.TryParse(explicitValue, out var explicitParsed))
            {
                return Math.Max(0, explicitParsed);
            }

            return moveStep.EstimatedMinutes.HasValue
                ? Math.Max(0, moveStep.EstimatedMinutes.Value)
                : null;
        }

        private static MoveRepairObstacle[] FindMoveRepairObstacles(SnapshotEnvelope snapshot, int targetX, int targetY, int maxClears)
        {
            if (maxClears <= 0)
            {
                return Array.Empty<MoveRepairObstacle>();
            }

            var startX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!startX.HasValue || !startY.HasValue || !grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<MoveRepairObstacle>();
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return Array.Empty<MoveRepairObstacle>();
            }

            var blocked = ReadBlockedCollisionTiles(grid.Value);
            var unsupported = ReadUnsupportedRouteActionTiles(snapshot);
            if (PathExists(startX.Value, startY.Value, targetX, targetY, width, height, blocked, unsupported))
            {
                return Array.Empty<MoveRepairObstacle>();
            }

            var repairs = new List<MoveRepairObstacle>();
            var currentX = startX.Value;
            var currentY = startY.Value;
            var clearableObstacles = ClearableObstacleTiles(snapshot)
                .GroupBy(obstacle => TileKey(obstacle.X, obstacle.Y))
                .Select(group => group.OrderBy(obstacle => obstacle.EnergyCost).First())
                .ToArray();
            var clearableObstacleCosts = clearableObstacles
                .ToDictionary(
                    obstacle => TileKey(obstacle.X, obstacle.Y),
                    obstacle => obstacle.RouteCost,
                    StringComparer.Ordinal);
            while (repairs.Count < maxClears)
            {
                var remainingClearBudget = maxClears - repairs.Count;
                var requiredCostBefore =
                    MinimumRouteRepairCostToTarget(
                        currentX,
                        currentY,
                        targetX,
                        targetY,
                        width,
                        height,
                        blocked,
                        unsupported,
                        clearableObstacleCosts,
                        remainingClearBudget);
                var repair = clearableObstacles
                    .Where(obstacle => blocked.Contains(TileKey(obstacle.X, obstacle.Y)))
                    .Select(obstacle => RepairCandidateForObstacle(currentX, currentY, targetX, targetY, width, height, blocked, unsupported, obstacle))
                    .Where(candidate => candidate is not null)
                    .Select(candidate =>
                    {
                        var simulatedBlocked =
                            new HashSet<string>(
                                blocked,
                                StringComparer.Ordinal);
                        simulatedBlocked.Remove(
                            TileKey(
                                candidate!.ObstacleX,
                                candidate.ObstacleY));
                        return new
                        {
                            Repair = candidate,
                            RemainingCost =
                                MinimumRouteRepairCostToTarget(
                                    candidate.StandX,
                                    candidate.StandY,
                                    targetX,
                                    targetY,
                                    width,
                                    height,
                                    simulatedBlocked,
                                    unsupported,
                                    clearableObstacleCosts,
                                    remainingClearBudget - 1)
                        };
                    })
                    .Where(candidate =>
                        candidate.RemainingCost != int.MaxValue &&
                        candidate.Repair!.RouteCost + candidate.RemainingCost <=
                            requiredCostBefore)
                    .OrderBy(candidate =>
                        candidate.Repair!.RouteCost + candidate.RemainingCost)
                    .ThenBy(candidate =>
                        Math.Abs(
                            currentX -
                            candidate.Repair!.StandX) +
                        Math.Abs(
                            currentY -
                            candidate.Repair.StandY))
                    .ThenBy(candidate => candidate.Repair!.EnergyCost)
                    .Select(candidate => candidate.Repair)
                    .FirstOrDefault();
                if (repair is null)
                {
                    break;
                }

                repairs.Add(repair);
                blocked.Remove(TileKey(repair.ObstacleX, repair.ObstacleY));
                currentX = repair.StandX;
                currentY = repair.StandY;
                if (PathExists(currentX, currentY, targetX, targetY, width, height, blocked, unsupported))
                {
                    return repairs.ToArray();
                }
            }

            return PathExists(currentX, currentY, targetX, targetY, width, height, blocked, unsupported)
                ? repairs.ToArray()
                : Array.Empty<MoveRepairObstacle>();
        }

        private static int MinimumRouteRepairCostToTarget(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int width,
            int height,
            HashSet<string> blocked,
            HashSet<string> unsupported,
            IReadOnlyDictionary<string, int> clearableCosts,
            int maxClears)
        {
            if (!TileInBounds(startX, startY, width, height) ||
                !TileInBounds(targetX, targetY, width, height) ||
                unsupported.Contains(TileKey(startX, startY)) ||
                unsupported.Contains(TileKey(targetX, targetY)) ||
                maxClears < 0)
            {
                return int.MaxValue;
            }

            var distances = new Dictionary<(int X, int Y, int Clears), int>
            {
                [(startX, startY, 0)] = 0
            };
            var pending = new SortedSet<(int Cost, int Clears, int Y, int X)>();
            pending.Add((0, 0, startY, startX));
            while (pending.Count > 0)
            {
                var current = pending.Min;
                pending.Remove(current);
                var state = (current.X, current.Y, current.Clears);
                if (!distances.TryGetValue(state, out var currentCost) ||
                    currentCost != current.Cost)
                {
                    continue;
                }
                if (current.X == targetX && current.Y == targetY)
                {
                    return currentCost;
                }

                foreach (var next in Neighbors(
                    current.X,
                    current.Y))
                {
                    if (!TileInBounds(
                            next.X,
                            next.Y,
                            width,
                            height))
                    {
                        continue;
                    }

                    var nextKey = TileKey(next.X, next.Y);
                    if (unsupported.Contains(nextKey))
                    {
                        continue;
                    }

                    var blockedStep = blocked.Contains(nextKey);
                    var clearCost = 0;
                    if (blockedStep && !clearableCosts.TryGetValue(nextKey, out clearCost))
                    {
                        continue;
                    }

                    var nextClears = current.Clears + (blockedStep ? 1 : 0);
                    if (nextClears > maxClears)
                    {
                        continue;
                    }

                    var nextCost = currentCost + (blockedStep ? clearCost : 0);
                    var nextState = (next.X, next.Y, nextClears);
                    if (distances.TryGetValue(nextState, out var existingCost) &&
                        existingCost <= nextCost)
                    {
                        continue;
                    }

                    distances[nextState] = nextCost;
                    pending.Add((nextCost, nextClears, next.Y, next.X));
                }
            }

            return int.MaxValue;
        }

        private static MoveRepairObstacle? RepairCandidateForObstacle(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int width,
            int height,
            HashSet<string> blocked,
            HashSet<string> unsupported,
            ClearableObstacle obstacle)
        {
            return Neighbors(obstacle.X, obstacle.Y)
                .Where(tile => TileInBounds(tile.X, tile.Y, width, height))
                .Where(tile => !blocked.Contains(TileKey(tile.X, tile.Y)))
                .Select(stand => new
                {
                    Stand = stand,
                    PathTiles = ShortestPathLength(
                        startX,
                        startY,
                        stand.X,
                        stand.Y,
                        width,
                        height,
                        blocked,
                        unsupported)
                })
                .Where(candidate => candidate.PathTiles.HasValue)
                .OrderBy(candidate => candidate.PathTiles)
                .ThenBy(candidate => candidate.Stand.Y)
                .ThenBy(candidate => candidate.Stand.X)
                .Select(candidate => new MoveRepairObstacle(
                    obstacle.X,
                    obstacle.Y,
                    candidate.Stand.X,
                    candidate.Stand.Y,
                    obstacle.ClearKind,
                    obstacle.EnergyCost,
                    obstacle.MaxToolSwings,
                    obstacle.ClearMinutes,
                    obstacle.RouteCost,
                    candidate.PathTiles!.Value))
                .FirstOrDefault();
        }

        private static IEnumerable<ClearableObstacle> ClearableObstacleTiles(SnapshotEnvelope snapshot)
        {
            foreach (var item in ReadClearanceRows(
                         snapshot,
                         "current_location",
                         "objects"))
            {
                var clearKind = ClearableObjectKind(
                    ReadString(item, "qualified_item_id"),
                    ReadString(item, "name"));
                if (!string.IsNullOrWhiteSpace(clearKind))
                {
                    yield return new ClearableObstacle(
                        ReadInt(item, "tile_x"),
                        ReadInt(item, "tile_y"),
                        clearKind,
                        ClearObstacleEnergyCost(clearKind));
                }
            }

            foreach (var item in ReadRouteBlockerRows(snapshot, "objects"))
            {
                var clearKind = ClearableObjectKind(
                    ReadString(item, "qualified_item_id"),
                    ReadString(item, "name"));
                if (!string.IsNullOrWhiteSpace(clearKind))
                {
                    yield return new ClearableObstacle(
                        ReadInt(item, "tile_x"),
                        ReadInt(item, "tile_y"),
                        clearKind,
                        ClearObstacleEnergyCost(clearKind));
                }
            }

            foreach (var feature in ReadClearanceRows(
                         snapshot,
                         "current_location",
                         "terrain_features"))
            {
                var clearKind = ClearableTerrainFeatureKind(
                    ReadString(feature, "type"));
                if (!string.IsNullOrWhiteSpace(clearKind))
                {
                    var expectedSwings = clearKind is "tree" or "fruit_tree"
                        ? NullableReadInt(feature, "expected_axe_hits_to_clear") ??
                          NullableReadInt(feature, "tree_chop_expected_tool_swings") ?? 8
                        : 8;
                    var energyCost = clearKind == "tree"
                        ? (int)Math.Ceiling(ReadDouble(
                            feature,
                            "tree_chop_energy_cost",
                            ClearObstacleEnergyCost(clearKind)))
                        : ClearObstacleEnergyCost(clearKind);
                    yield return new ClearableObstacle(
                        ReadInt(feature, "tile_x"),
                        ReadInt(feature, "tile_y"),
                        clearKind,
                        energyCost,
                        expectedSwings);
                }
            }

            foreach (var feature in ReadRouteBlockerRows(
                         snapshot,
                         "terrain_features"))
            {
                var clearKind = ClearableTerrainFeatureKind(
                    ReadString(feature, "type"));
                if (string.Equals(clearKind, "grass", StringComparison.Ordinal))
                {
                    yield return new ClearableObstacle(
                        ReadInt(feature, "tile_x"),
                        ReadInt(feature, "tile_y"),
                        clearKind,
                        ClearObstacleEnergyCost(clearKind),
                        8);
                }
            }
        }

        private static IEnumerable<JsonElement> ReadClearanceRows(
            SnapshotEnvelope snapshot,
            string section,
            string field)
        {
            var value = ReadStateFieldValue(snapshot, section, field);
            return value.HasValue && value.Value.ValueKind == JsonValueKind.Array
                ? value.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object)
                    .ToArray()
                : Array.Empty<JsonElement>();
        }

        private static IEnumerable<JsonElement> ReadRouteBlockerRows(
            SnapshotEnvelope snapshot,
            string field)
        {
            var blockers = ReadStateFieldValue(
                snapshot,
                "locations",
                "route_blockers");
            if (!blockers.HasValue ||
                blockers.Value.ValueKind != JsonValueKind.Object ||
                !blockers.Value.TryGetProperty(field, out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<JsonElement>();
            }

            return rows.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .ToArray();
        }

        private static string ClearableObjectKind(string qualifiedId, string name)
        {
            if (qualifiedId is "(O)343" or "(O)450")
            {
                return "stone";
            }

            if (qualifiedId is "(O)294" or "(O)295")
            {
                return "twig";
            }

            if (qualifiedId is "(O)674" or "(O)675" or "(O)784" ||
                qualifiedId.StartsWith("(O)Weeds", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("weed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "weeds";
            }

            return string.Empty;
        }

        private static string ClearableTerrainFeatureKind(string type)
        {
            if (type.EndsWith(".Grass", StringComparison.Ordinal) || type == "Grass")
            {
                return "grass";
            }

            if (type.EndsWith(".Tree", StringComparison.Ordinal) || type == "Tree")
            {
                return "tree";
            }

            if (type.EndsWith(".FruitTree", StringComparison.Ordinal) || type == "FruitTree")
            {
                return "fruit_tree";
            }

            return string.Empty;
        }

        private static int ClearObstacleEnergyCost(string clearKind)
        {
            return clearKind switch
            {
                "grass" => 0,
                "weeds" => 1,
                "stone" => 2,
                "twig" => 2,
                "tree" => 10,
                "fruit_tree" => 10,
                _ => 2
            };
        }

        private sealed class ClearableObstacle
        {
            public ClearableObstacle(
                int x,
                int y,
                string clearKind,
                int energyCost,
                int maxToolSwings = 8)
            {
                X = x;
                Y = y;
                ClearKind = clearKind;
                EnergyCost = energyCost;
                MaxToolSwings = Math.Max(1, maxToolSwings);
                ClearMinutes = Math.Max(
                    DefaultMoveRouteRepairMinutesPerClear,
                    (int)Math.Ceiling(MaxToolSwings / 4d));
                RouteCost = checked(ClearMinutes * 100 + EnergyCost);
            }

            public int X { get; }
            public int Y { get; }
            public string ClearKind { get; }
            public int EnergyCost { get; }
            public int MaxToolSwings { get; }
            public int ClearMinutes { get; }
            public int RouteCost { get; }
        }

        private sealed class MoveRepairObstacle
        {
            public MoveRepairObstacle(
                int obstacleX,
                int obstacleY,
                int standX,
                int standY,
                string clearKind,
                int energyCost,
                int maxToolSwings,
                int clearMinutes,
                int routeCost,
                int pathTiles)
            {
                ObstacleX = obstacleX;
                ObstacleY = obstacleY;
                StandX = standX;
                StandY = standY;
                ClearKind = clearKind;
                EnergyCost = energyCost;
                MaxToolSwings = maxToolSwings;
                ClearMinutes = clearMinutes;
                RouteCost = routeCost;
                PathTiles = pathTiles;
            }

            public int ObstacleX { get; }
            public int ObstacleY { get; }
            public int StandX { get; }
            public int StandY { get; }
            public string ClearKind { get; }
            public int EnergyCost { get; }
            public int MaxToolSwings { get; }
            public int ClearMinutes { get; }
            public int RouteCost { get; }
            public int PathTiles { get; }
            public int MovementMinutes => Math.Max(1, (int)Math.Ceiling((PathTiles + 1) / 5d));
        }

    }
}
