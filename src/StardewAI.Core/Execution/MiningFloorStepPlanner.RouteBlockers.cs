using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private static MiningFloorStepPlan SelectAttributedQuarryRouteBlocker(
            MiningFloorStepPlan staticRoute,
            string routeObjectiveId,
            JsonElement objects,
            JsonElement resourceClumps,
            JsonElement monsters,
            SearchResult dynamicSearch,
            bool[,] dynamicGrid,
            double? movementTileDurationMs,
            bool bombFinisherAvailable)
        {
            for (var index = 1; index < staticRoute.Path.Length; index++)
            {
                var cell = staticRoute.Path[index];
                if (!InBounds(dynamicGrid, cell.X, cell.Y) ||
                    !dynamicGrid[cell.X, cell.Y])
                {
                    continue;
                }

                var matches = RouteBlockersAt(
                    objects,
                    resourceClumps,
                    monsters,
                    cell.X,
                    cell.Y);
                if (matches.Count != 1)
                {
                    return Blocked(
                        matches.Count == 0
                            ? "quarry_route_blocker_unattributed"
                            : "quarry_route_blocker_ambiguous");
                }

                var blocker = matches[0];
                MiningFloorStepPlan? plan = blocker.Kind switch
                {
                    RouteBlockerKinds.Monster => SelectMonster(
                        monsters,
                        dynamicSearch,
                        dynamicGrid,
                        "quarry_route_blocked_by_attributed_monster",
                        movementTileDurationMs:
                            movementTileDurationMs,
                        bombFinisherAvailable:
                            bombFinisherAvailable,
                        combatIntent:
                            TrainingCombatIntents
                                .TransitRouteClearance,
                        targetRuntimeIdentity:
                            ReadString(
                                blocker.Source,
                                "runtime_identity"),
                        requireMelee: true),
                    RouteBlockerKinds.Stone => SelectStone(
                        objects,
                        dynamicSearch,
                        dynamicGrid,
                        blocker.OriginX,
                        blocker.OriginY),
                    RouteBlockerKinds.Container => SelectContainer(
                        objects,
                        dynamicSearch,
                        dynamicGrid,
                        targetTileX: blocker.OriginX,
                        targetTileY: blocker.OriginY),
                    RouteBlockerKinds.ResourceClump =>
                        SelectResourceClump(
                            resourceClumps,
                            dynamicSearch,
                            dynamicGrid,
                            blocker.OriginX,
                            blocker.OriginY),
                    _ => null
                };
                if (plan is null)
                {
                    return Blocked(
                        "quarry_route_blocker_attributed_but_unreachable");
                }

                plan.Reason = blocker.Kind switch
                {
                    RouteBlockerKinds.Monster =>
                        "quarry_route_blocked_by_attributed_monster",
                    RouteBlockerKinds.Stone =>
                        "quarry_route_blocked_by_attributed_stone",
                    RouteBlockerKinds.Container =>
                        "quarry_route_blocked_by_attributed_container",
                    RouteBlockerKinds.ResourceClump =>
                        "quarry_route_blocked_by_attributed_resource_clump",
                    _ => plan.Reason
                };
                plan.RouteObjectiveId = routeObjectiveId;
                plan.RouteTargetTileX =
                    staticRoute.TargetTileX;
                plan.RouteTargetTileY =
                    staticRoute.TargetTileY;
                plan.RouteTargetStandTileX =
                    staticRoute.StandTileX;
                plan.RouteTargetStandTileY =
                    staticRoute.StandTileY;
                plan.BlockedRouteCellX = cell.X;
                plan.BlockedRouteCellY = cell.Y;
                plan.BlockerAttributionStatus =
                    "exact_static_route_cell_identity";
                plan.ExpectedConnectivityGain =
                    ExpectedRouteConnectivityGain(
                        staticRoute.Path,
                        index,
                        dynamicGrid,
                        blocker);
                plan.SafetyWindowStatus =
                    "target_bound_route_clearance";
                return plan;
            }

            return Blocked(
                "quarry_route_blocker_unattributed");
        }

        private static List<RouteBlocker> RouteBlockersAt(
            JsonElement objects,
            JsonElement resourceClumps,
            JsonElement monsters,
            int tileX,
            int tileY)
        {
            var matches = new List<RouteBlocker>();
            if (objects.ValueKind == JsonValueKind.Array)
            {
                foreach (var obj in objects.EnumerateArray())
                {
                    var originX = ReadInt(obj, "tile_x");
                    var originY = ReadInt(obj, "tile_y");
                    if (originX != tileX || originY != tileY)
                    {
                        continue;
                    }

                    if (ReadBool(obj, "is_breakable_stone"))
                    {
                        matches.Add(new RouteBlocker(
                            RouteBlockerKinds.Stone,
                            obj,
                            tileX,
                            tileY));
                    }
                    else if (ReadBool(obj, "is_container"))
                    {
                        matches.Add(new RouteBlocker(
                            RouteBlockerKinds.Container,
                            obj,
                            tileX,
                            tileY));
                    }
                }
            }

            if (resourceClumps.ValueKind == JsonValueKind.Array)
            {
                foreach (var clump in resourceClumps.EnumerateArray())
                {
                    var originX = ReadInt(clump, "tile_x");
                    var originY = ReadInt(clump, "tile_y");
                    var width = ReadInt(clump, "width");
                    var height = ReadInt(clump, "height");
                    if (!originX.HasValue ||
                        !originY.HasValue ||
                        !width.HasValue ||
                        !height.HasValue ||
                        tileX < originX.Value ||
                        tileY < originY.Value ||
                        tileX >= originX.Value + width.Value ||
                        tileY >= originY.Value + height.Value)
                    {
                        continue;
                    }

                    matches.Add(new RouteBlocker(
                        RouteBlockerKinds.ResourceClump,
                        clump,
                        originX.Value,
                        originY.Value));
                }
            }

            if (monsters.ValueKind == JsonValueKind.Array)
            {
                matches.AddRange(monsters.EnumerateArray()
                    .Where(monster =>
                        EntityBoundingBoxOccupiesTile(
                            monster,
                            tileX,
                            tileY))
                    .Select(monster => new RouteBlocker(
                        RouteBlockerKinds.Monster,
                        monster,
                        ReadInt(monster, "tile_x") ?? tileX,
                        ReadInt(monster, "tile_y") ?? tileY)));
            }

            return matches;
        }

        private static bool EntityBoundingBoxOccupiesTile(
            JsonElement entity,
            int tileX,
            int tileY)
        {
            if (!entity.TryGetProperty(
                    "bounding_box",
                    out var bounds) ||
                bounds.ValueKind != JsonValueKind.Object)
            {
                return ReadInt(entity, "tile_x") == tileX &&
                    ReadInt(entity, "tile_y") == tileY;
            }

            var x = ReadInt(bounds, "x");
            var y = ReadInt(bounds, "y");
            var width = ReadInt(bounds, "width");
            var height = ReadInt(bounds, "height");
            return x.HasValue &&
                y.HasValue &&
                width > 0 &&
                height > 0 &&
                x.Value < (tileX + 1) * 64 &&
                x.Value + width.Value > tileX * 64 &&
                y.Value < (tileY + 1) * 64 &&
                y.Value + height.Value > tileY * 64;
        }

        private static int ExpectedRouteConnectivityGain(
            MiningPathTile[] path,
            int blockedIndex,
            bool[,] dynamicGrid,
            RouteBlocker blocker)
        {
            var gain = 0;
            for (var index = blockedIndex;
                 index < path.Length;
                 index++)
            {
                var cell = path[index];
                if (!InBounds(dynamicGrid, cell.X, cell.Y))
                {
                    break;
                }

                if (dynamicGrid[cell.X, cell.Y] &&
                    !blocker.Occupies(cell.X, cell.Y))
                {
                    break;
                }
                gain++;
            }
            return Math.Max(1, gain);
        }

        private static class RouteBlockerKinds
        {
            public const string Monster = "monster";
            public const string Stone = "stone";
            public const string Container = "container";
            public const string ResourceClump = "resource_clump";
        }

        private sealed class RouteBlocker
        {
            public RouteBlocker(
                string kind,
                JsonElement source,
                int originX,
                int originY)
            {
                Kind = kind;
                Source = source;
                OriginX = originX;
                OriginY = originY;
            }

            public string Kind { get; }

            public JsonElement Source { get; }

            public int OriginX { get; }

            public int OriginY { get; }

            public bool Occupies(int tileX, int tileY)
            {
                return Kind switch
                {
                    RouteBlockerKinds.ResourceClump =>
                        tileX >= OriginX &&
                        tileY >= OriginY &&
                        tileX <
                            OriginX +
                            (ReadInt(Source, "width") ?? 0) &&
                        tileY <
                            OriginY +
                            (ReadInt(Source, "height") ?? 0),
                    RouteBlockerKinds.Monster =>
                        EntityBoundingBoxOccupiesTile(
                            Source,
                            tileX,
                            tileY),
                    _ => tileX == OriginX && tileY == OriginY
                };
            }
        }
    }
}
