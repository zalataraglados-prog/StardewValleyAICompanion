using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public static class VolcanoFloorStepKinds
    {
        public const string PressDwarfSwitch = "press_dwarf_switch";
        public const string WaitForDwarfGate = "wait_for_dwarf_gate";
        public const string TraverseForwardConnector = "traverse_forward_connector";
        public const string CoolLavaTile = "cool_lava_tile";
        public const string CombatMonster = "combat_monster";
        public const string BreakStone = "break_stone";
        public const string BreakContainer = "break_container";
        public const string Blocked = "blocked";
    }

    public sealed class VolcanoPathTile
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    public sealed class VolcanoFloorStepPlan
    {
        public string Status { get; set; } = "blocked";

        public string StepKind { get; set; } = VolcanoFloorStepKinds.Blocked;

        public string Reason { get; set; } = string.Empty;

        public int? TargetTileX { get; set; }

        public int? TargetTileY { get; set; }

        public int? StandTileX { get; set; }

        public int? StandTileY { get; set; }

        public int EstimatedMovementTiles { get; set; }

        public int EstimatedToolUses { get; set; }

        public string ExpectedTargetLocation { get; set; } = string.Empty;

        public int? ExpectedArrivalTileX { get; set; }

        public int? ExpectedArrivalTileY { get; set; }

        public int? WateringCanSlotIndex { get; set; }

        public int? ToolSlotIndex { get; set; }

        public int? CombatWeaponSlotIndex { get; set; }

        public string CombatIntent { get; set; } =
            TrainingCombatIntents.TargetDefeat;

        public string TargetRuntimeIdentity { get; set; } = string.Empty;

        public string TargetRuntimeType { get; set; } = string.Empty;

        public string TargetName { get; set; } = string.Empty;

        public string TargetQualifiedItemId { get; set; } = string.Empty;

        public VolcanoPathTile[] Path { get; set; } = Array.Empty<VolcanoPathTile>();
    }

    public sealed class VolcanoFloorStepPlanner
    {
        private static readonly (int X, int Y)[] Directions =
        {
            (0, -1),
            (-1, 0),
            (1, 0),
            (0, 1)
        };

        public VolcanoFloorStepPlan Plan(SnapshotEnvelope snapshot)
        {
            if (!snapshot.State.TryGetValue("volcano", out var volcano) || volcano.ValueKind != JsonValueKind.Object)
            {
                return Blocked("volcano_state_unavailable");
            }

            if (!TryFieldValue(volcano, "current_level", out var currentLevel) ||
                !TryFieldValue(volcano, "tiles", out var tiles) ||
                !TryFieldValue(volcano, "connectors", out var connectors) ||
                !TryFieldValue(volcano, "gates", out var gates) ||
                !TryFieldValue(volcano, "objects", out var objects) ||
                !TryFieldValue(volcano, "monsters", out var monsters) ||
                !TryFieldValue(volcano, "player_resources", out var resources))
            {
                return Blocked("volcano_required_group_unavailable");
            }

            var level = ReadInt(currentLevel, "level");
            if (level < 0 || level > 9)
            {
                return Blocked("volcano_level_out_of_range");
            }

            if (!TryCollision(tiles, out var grid, out var start))
            {
                return Blocked("volcano_collision_context_unavailable");
            }

            var search = Search(grid, start);
            var hasDynamicCollision = TryCollision(
                tiles,
                preferStaticRows: false,
                out var dynamicGrid,
                out var dynamicStart);
            var immediateStart = hasDynamicCollision
                ? dynamicStart
                : start;
            var immediateGrid = CloneGrid(
                hasDynamicCollision ? dynamicGrid : grid);
            BlockConnectorTiles(
                immediateGrid,
                connectors,
                immediateStart);
            var immediateSearch = Search(
                immediateGrid,
                immediateStart);
            var immediateThreat = SelectNearestMonster(
                monsters,
                resources,
                immediateSearch,
                immediateStart,
                connectors,
                TrainingCombatIntents.TransitSelfDefense,
                maximumGroundDistanceFromPlayer: 3,
                maximumGliderDistanceFromPlayer: 3);
            if (immediateThreat is not null)
            {
                return immediateThreat;
            }

            var openingGate = SelectOpeningDwarfGate(gates);
            if (openingGate is not null)
            {
                return openingGate;
            }

            var routeProgress = SelectRouteProgress(
                tiles,
                connectors,
                gates,
                objects,
                monsters,
                resources,
                grid,
                start);
            if (routeProgress is not null)
            {
                if (!IsRouteStandDynamicallyReachable(
                        routeProgress,
                        immediateSearch))
                {
                    var dynamicRouteBlocker = SelectNearestMonster(
                        monsters,
                        resources,
                        immediateSearch,
                        immediateStart,
                        connectors,
                        TrainingCombatIntents.TransitRouteClearance,
                        maximumGroundDistanceFromPlayer: null,
                        maximumGliderDistanceFromPlayer: null);
                    if (dynamicRouteBlocker is not null)
                    {
                        return dynamicRouteBlocker;
                    }
                }

                return routeProgress;
            }

            return Blocked("volcano_forward_route_unresolved_from_loaded_state");
        }

        private static bool IsRouteStandDynamicallyReachable(
            VolcanoFloorStepPlan plan,
            SearchResult dynamicSearch)
        {
            if (plan.StepKind is not (
                    VolcanoFloorStepKinds.PressDwarfSwitch or
                    VolcanoFloorStepKinds.CoolLavaTile or
                    VolcanoFloorStepKinds.CombatMonster or
                    VolcanoFloorStepKinds.BreakStone or
                    VolcanoFloorStepKinds.BreakContainer) ||
                !plan.StandTileX.HasValue ||
                !plan.StandTileY.HasValue)
            {
                return true;
            }

            return dynamicSearch.TryPath(
                plan.StandTileX.Value,
                plan.StandTileY.Value,
                out _);
        }

        private static bool[][] CloneGrid(bool[][] source)
        {
            return source.Select(row => row.ToArray()).ToArray();
        }

        private static void BlockConnectorTiles(
            bool[][] grid,
            JsonElement connectors,
            (int X, int Y) start)
        {
            foreach (var propertyName in new[]
                     {
                         "warps",
                         "forward_warps",
                         "backward_warps"
                     })
            {
                if (!connectors.TryGetProperty(
                        propertyName,
                        out var warps) ||
                    warps.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var warp in warps.EnumerateArray())
                {
                    var x = ReadInt(warp, "tile_x");
                    var y = ReadInt(warp, "tile_y");
                    if ((x, y) == start ||
                        y < 0 ||
                        y >= grid.Length ||
                        x < 0 ||
                        x >= grid[y].Length)
                    {
                        continue;
                    }

                    grid[y][x] = false;
                }
            }
        }

        private static VolcanoFloorStepPlan? SelectOpeningDwarfGate(
            JsonElement gates)
        {
            if (gates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var gate in gates.EnumerateArray()
                .OrderBy(item => ReadInt(item, "gate_index")))
            {
                if (ReadBool(gate, "opened") ||
                    !ReadBool(gate, "all_switches_pressed"))
                {
                    continue;
                }

                return new VolcanoFloorStepPlan
                {
                    Status = "ready",
                    StepKind = VolcanoFloorStepKinds.WaitForDwarfGate,
                    Reason = "dwarf_gate_native_opening_settle",
                    TargetTileX = ReadInt(gate, "blocking_tile_x"),
                    TargetTileY = ReadInt(gate, "blocking_tile_y")
                };
            }

            return null;
        }

        private static VolcanoFloorStepPlan? SelectNearestMonster(
            JsonElement monsters,
            JsonElement resources,
            SearchResult search,
            (int X, int Y) start,
            JsonElement connectors,
            string combatIntent,
            int? maximumGroundDistanceFromPlayer,
            int? maximumGliderDistanceFromPlayer = null)
        {
            if (monsters.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var candidates = new List<(JsonElement Monster, int StandX, int StandY, int Distance, VolcanoPathTile[] Path)>();
            foreach (var monster in monsters.EnumerateArray())
            {
                var x = ReadInt(monster, "tile_x");
                var y = ReadInt(monster, "tile_y");
                var isGlider = ReadBool(monster, "is_glider");
                var maximumDistance = isGlider
                    ? maximumGliderDistanceFromPlayer ?? maximumGroundDistanceFromPlayer
                    : maximumGroundDistanceFromPlayer;
                if (maximumDistance.HasValue &&
                    Math.Abs(x - start.X) + Math.Abs(y - start.Y) > maximumDistance.Value)
                {
                    continue;
                }

                if (isGlider)
                {
                    var hasReachableApproach = Directions.Any(
                        direction => search.TryPath(
                            x + direction.X,
                            y + direction.Y,
                            out _));
                    if (!hasReachableApproach)
                    {
                        if (!maximumDistance.HasValue ||
                            ConnectorSeparatesTiles(
                                connectors,
                                start,
                                (x, y)))
                        {
                            continue;
                        }
                    }
                    candidates.Add((
                        monster,
                        start.X,
                        start.Y,
                        Math.Abs(x - start.X) +
                            Math.Abs(y - start.Y),
                        new[]
                        {
                            new VolcanoPathTile
                            {
                                X = start.X,
                                Y = start.Y
                            }
                        }));
                    continue;
                }

                foreach (var direction in Directions)
                {
                    var standX = x + direction.X;
                    var standY = y + direction.Y;
                    if (search.TryPath(standX, standY, out var path))
                    {
                        candidates.Add((monster, standX, standY, path.Length - 1, path));
                    }
                }
            }

            var selected = candidates
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => ReadInt(candidate.Monster, "tile_y"))
                .ThenBy(candidate => ReadInt(candidate.Monster, "tile_x"))
                .FirstOrDefault();
            if (selected.Path is null)
            {
                return null;
            }

            var supported = ReadBool(selected.Monster, "melee_executor_supported");
            var weaponSlot = ReadBestSlot(resources, "weapon_slots", "maximum_damage", rejectScythe: true);
            return new VolcanoFloorStepPlan
            {
                Status = supported && weaponSlot.HasValue ? "ready" : "blocked",
                StepKind = VolcanoFloorStepKinds.CombatMonster,
                Reason = !supported
                    ? ReadString(selected.Monster, "melee_executor_block_reason")
                    : weaponSlot.HasValue ? "reachable_native_melee_target" : "volcano_combat_melee_weapon_unavailable",
                TargetTileX = ReadInt(selected.Monster, "tile_x"),
                TargetTileY = ReadInt(selected.Monster, "tile_y"),
                StandTileX = selected.StandX,
                StandTileY = selected.StandY,
                EstimatedMovementTiles = selected.Distance,
                TargetRuntimeIdentity = ReadString(selected.Monster, "runtime_identity"),
                TargetRuntimeType = ReadString(selected.Monster, "runtime_type"),
                TargetName = ReadString(selected.Monster, "name"),
                CombatWeaponSlotIndex = weaponSlot,
                CombatIntent = combatIntent,
                Path = selected.Path
            };
        }

        private static bool ConnectorSeparatesTiles(
            JsonElement connectors,
            (int X, int Y) start,
            (int X, int Y) target)
        {
            var directDistance =
                Math.Abs(start.X - target.X) +
                Math.Abs(start.Y - target.Y);
            foreach (var propertyName in new[]
                     {
                         "warps",
                         "forward_warps",
                         "backward_warps"
                     })
            {
                if (!connectors.TryGetProperty(
                        propertyName,
                        out var warps) ||
                    warps.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var warp in warps.EnumerateArray())
                {
                    var connector = (
                        X: ReadInt(warp, "tile_x"),
                        Y: ReadInt(warp, "tile_y"));
                    var throughConnector =
                        Math.Abs(start.X - connector.X) +
                        Math.Abs(start.Y - connector.Y) +
                        Math.Abs(connector.X - target.X) +
                        Math.Abs(connector.Y - target.Y);
                    if (throughConnector == directDistance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static VolcanoFloorStepPlan? SelectRouteProgress(
            JsonElement tiles,
            JsonElement connectors,
            JsonElement gates,
            JsonElement objects,
            JsonElement monsters,
            JsonElement resources,
            bool[][] grid,
            (int X, int Y) start)
        {
            var obstacleByTile = ReadRouteObjects(objects);
            var lavaTiles = ReadRouteLavaTiles(tiles);
            var monsterByTile = ReadRouteMonsters(monsters);
            var closedGateTiles = ReadClosedGateTiles(gates);
            var connectorGoals = ReadConnectorGoals(connectors);
            var route = FindLowestCostRoute(
                grid,
                start,
                connectorGoals,
                obstacleByTile,
                lavaTiles,
                monsterByTile,
                closedGateTiles);
            if (route is null)
            {
                route = FindLowestCostRoute(
                    grid,
                    start,
                    ReadSwitchGoals(gates),
                    obstacleByTile,
                    lavaTiles,
                    monsterByTile,
                    closedGateTiles);
            }
            if (route is null)
            {
                return null;
            }

            for (var index = 1; index < route.Path.Length; index++)
            {
                var tile = route.Path[index];
                var stand = route.Path[index - 1];
                var pathToStand = route.Path.Take(index).ToArray();
                var key = (tile.X, tile.Y);
                if (monsterByTile.TryGetValue(key, out var monster))
                {
                    return BuildRouteMonsterPlan(
                        monster,
                        resources,
                        stand,
                        pathToStand);
                }
                if (obstacleByTile.TryGetValue(key, out var obstacle))
                {
                    return BuildRouteObstaclePlan(
                        obstacle,
                        resources,
                        stand,
                        pathToStand);
                }
                if (lavaTiles.Contains(key))
                {
                    var wateringCanSlot = ReadWateringCanSlot(resources);
                    return new VolcanoFloorStepPlan
                    {
                        Status = wateringCanSlot.HasValue
                            ? "ready"
                            : "blocked",
                        StepKind = VolcanoFloorStepKinds.CoolLavaTile,
                        Reason = wateringCanSlot.HasValue
                            ? "forward_route_native_cooling_target"
                            : "volcano_cooling_requires_watering_can_with_water",
                        TargetTileX = tile.X,
                        TargetTileY = tile.Y,
                        StandTileX = stand.X,
                        StandTileY = stand.Y,
                        EstimatedMovementTiles =
                            Math.Max(0, pathToStand.Length - 1),
                        WateringCanSlotIndex = wateringCanSlot,
                        Path = pathToStand
                    };
                }
            }

            if (route.Goal.Kind == RouteGoalKinds.Connector)
            {
                var targetX = ReadInt(
                    route.Goal.Source,
                    "target_tile_x");
                var targetY = ReadInt(
                    route.Goal.Source,
                    "target_tile_y");
                var targetLocation = ReadString(
                    route.Goal.Source,
                    "target_location");
                var hasExactArrival = !targetLocation.StartsWith(
                    "VolcanoDungeon",
                    StringComparison.OrdinalIgnoreCase);
                return new VolcanoFloorStepPlan
                {
                    Status = "ready",
                    StepKind =
                        VolcanoFloorStepKinds.TraverseForwardConnector,
                    Reason = "forward_route_reachable_native_warp",
                    TargetTileX = route.Goal.X,
                    TargetTileY = route.Goal.Y,
                    StandTileX = route.Goal.X,
                    StandTileY = route.Goal.Y,
                    EstimatedMovementTiles =
                        Math.Max(0, route.Path.Length - 1),
                    ExpectedTargetLocation = targetLocation,
                    ExpectedArrivalTileX = hasExactArrival
                        ? targetX
                        : (int?)null,
                    ExpectedArrivalTileY = hasExactArrival
                        ? targetY
                        : (int?)null,
                    Path = route.Path
                };
            }

            return new VolcanoFloorStepPlan
            {
                Status = "ready",
                StepKind = VolcanoFloorStepKinds.PressDwarfSwitch,
                Reason = "forward_route_reachable_unpressed_dwarf_switch",
                TargetTileX = route.Goal.X,
                TargetTileY = route.Goal.Y,
                StandTileX = route.Goal.X,
                StandTileY = route.Goal.Y,
                EstimatedMovementTiles =
                    Math.Max(0, route.Path.Length - 1),
                Path = route.Path
            };
        }

        private static VolcanoFloorStepPlan BuildRouteObstaclePlan(
            JsonElement obstacle,
            JsonElement resources,
            VolcanoPathTile stand,
            VolcanoPathTile[] path)
        {
            var isStone = ReadBool(obstacle, "is_breakable_stone");
            var slots = isStone ? "pickaxe_slots" : "heavy_hitter_slots";
            var score = isStone
                ? "damage_per_hit"
                : "container_damage_per_hit";
            var toolSlot = ReadBestSlot(
                resources,
                slots,
                score);
            var damagePerUse = toolSlot.HasValue
                ? ReadSlotScore(resources, slots, toolSlot.Value, score)
                : 0;
            var targetHealth = Math.Max(
                1,
                ReadInt(obstacle, "health_or_hits_remaining"));
            return new VolcanoFloorStepPlan
            {
                Status = toolSlot.HasValue ? "ready" : "blocked",
                StepKind = isStone
                    ? VolcanoFloorStepKinds.BreakStone
                    : VolcanoFloorStepKinds.BreakContainer,
                Reason = toolSlot.HasValue
                    ? isStone
                        ? "forward_route_native_pickaxe_target"
                        : "forward_route_native_heavy_hitter_target"
                    : isStone
                        ? "volcano_break_stone_pickaxe_unavailable"
                        : "volcano_break_container_heavy_hitter_unavailable",
                TargetTileX = ReadInt(obstacle, "tile_x"),
                TargetTileY = ReadInt(obstacle, "tile_y"),
                StandTileX = stand.X,
                StandTileY = stand.Y,
                EstimatedMovementTiles = Math.Max(0, path.Length - 1),
                EstimatedToolUses = damagePerUse > 0
                    ? (int)Math.Ceiling(
                        (double)targetHealth / damagePerUse)
                    : 0,
                TargetRuntimeType =
                    ReadString(obstacle, "runtime_type"),
                TargetQualifiedItemId =
                    ReadString(obstacle, "qualified_item_id"),
                ToolSlotIndex = toolSlot,
                Path = path
            };
        }

        private static VolcanoFloorStepPlan BuildRouteMonsterPlan(
            JsonElement monster,
            JsonElement resources,
            VolcanoPathTile stand,
            VolcanoPathTile[] path)
        {
            var supported = ReadBool(
                monster,
                "melee_executor_supported");
            var weaponSlot = ReadBestSlot(
                resources,
                "weapon_slots",
                "maximum_damage",
                rejectScythe: true);
            return new VolcanoFloorStepPlan
            {
                Status = supported && weaponSlot.HasValue
                    ? "ready"
                    : "blocked",
                StepKind = VolcanoFloorStepKinds.CombatMonster,
                Reason = !supported
                    ? ReadString(
                        monster,
                        "melee_executor_block_reason")
                    : weaponSlot.HasValue
                        ? "forward_route_native_melee_target"
                        : "volcano_combat_melee_weapon_unavailable",
                TargetTileX = ReadInt(monster, "tile_x"),
                TargetTileY = ReadInt(monster, "tile_y"),
                StandTileX = stand.X,
                StandTileY = stand.Y,
                EstimatedMovementTiles = Math.Max(0, path.Length - 1),
                TargetRuntimeIdentity =
                    ReadString(monster, "runtime_identity"),
                TargetRuntimeType =
                    ReadString(monster, "runtime_type"),
                TargetName = ReadString(monster, "name"),
                CombatWeaponSlotIndex = weaponSlot,
                CombatIntent =
                    TrainingCombatIntents.TransitRouteClearance,
                Path = path
            };
        }

        private static Dictionary<(int X, int Y), JsonElement>
            ReadRouteObjects(JsonElement objects)
        {
            var result =
                new Dictionary<(int X, int Y), JsonElement>();
            if (objects.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var item in objects.EnumerateArray())
            {
                if (ReadBool(item, "is_breakable_stone") ||
                    ReadBool(item, "is_breakable_container"))
                {
                    result[(
                        ReadInt(item, "tile_x"),
                        ReadInt(item, "tile_y"))] = item;
                }
            }
            return result;
        }

        private static HashSet<(int X, int Y)> ReadRouteLavaTiles(
            JsonElement tiles)
        {
            var result = new HashSet<(int X, int Y)>();
            if (!tiles.TryGetProperty(
                    "coolable_uncooled_tiles",
                    out var lava) ||
                lava.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var tile in lava.EnumerateArray())
            {
                result.Add((
                    ReadInt(tile, "tile_x"),
                    ReadInt(tile, "tile_y")));
            }
            return result;
        }

        private static Dictionary<(int X, int Y), JsonElement>
            ReadRouteMonsters(JsonElement monsters)
        {
            var result =
                new Dictionary<(int X, int Y), JsonElement>();
            if (monsters.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var monster in monsters.EnumerateArray())
            {
                if (ReadBool(monster, "is_glider"))
                {
                    continue;
                }
                result[(
                    ReadInt(monster, "tile_x"),
                    ReadInt(monster, "tile_y"))] = monster;
            }
            return result;
        }

        private static HashSet<(int X, int Y)> ReadClosedGateTiles(
            JsonElement gates)
        {
            var result = new HashSet<(int X, int Y)>();
            if (gates.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var gate in gates.EnumerateArray())
            {
                if (!ReadBool(gate, "opened") &&
                    !ReadBool(gate, "all_switches_pressed"))
                {
                    result.Add((
                        ReadInt(gate, "blocking_tile_x"),
                        ReadInt(gate, "blocking_tile_y")));
                }
            }
            return result;
        }

        private static List<RouteGoal> ReadConnectorGoals(
            JsonElement connectors)
        {
            var result = new List<RouteGoal>();
            if (!connectors.TryGetProperty(
                    "forward_warps",
                    out var warps) ||
                warps.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var warp in warps.EnumerateArray())
            {
                result.Add(new RouteGoal(
                    RouteGoalKinds.Connector,
                    ReadInt(warp, "tile_x"),
                    ReadInt(warp, "tile_y"),
                    warp));
            }
            return result;
        }

        private static List<RouteGoal> ReadSwitchGoals(
            JsonElement gates)
        {
            var result = new List<RouteGoal>();
            if (gates.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var gate in gates.EnumerateArray())
            {
                if (ReadBool(gate, "opened") ||
                    ReadBool(gate, "all_switches_pressed") ||
                    !gate.TryGetProperty("switches", out var switches) ||
                    switches.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (var item in switches.EnumerateArray())
                {
                    if (!ReadBool(item, "pressed"))
                    {
                        result.Add(new RouteGoal(
                            RouteGoalKinds.Switch,
                            ReadInt(item, "tile_x"),
                            ReadInt(item, "tile_y"),
                            item));
                    }
                }
            }
            return result;
        }

        private static RoutePlan? FindLowestCostRoute(
            bool[][] grid,
            (int X, int Y) start,
            IReadOnlyList<RouteGoal> goals,
            IReadOnlyDictionary<(int X, int Y), JsonElement>
                obstacleByTile,
            ISet<(int X, int Y)> lavaTiles,
            IReadOnlyDictionary<(int X, int Y), JsonElement>
                monsterByTile,
            ISet<(int X, int Y)> closedGateTiles)
        {
            if (goals.Count == 0)
            {
                return null;
            }

            var goalByTile = goals
                .GroupBy(goal => (goal.X, goal.Y))
                .ToDictionary(group => group.Key, group => group.First());
            var distance = new Dictionary<(int X, int Y), int>
            {
                [start] = 0
            };
            var previous =
                new Dictionary<(int X, int Y), (int X, int Y)?>();
            previous[start] = null;
            var frontier =
                new SortedSet<(int Cost, int Sequence, int X, int Y)>();
            var sequence = 0;
            frontier.Add((0, sequence++, start.X, start.Y));

            while (frontier.Count > 0)
            {
                var node = frontier.Min;
                frontier.Remove(node);
                var current = (node.X, node.Y);
                if (!distance.TryGetValue(current, out var currentCost) ||
                    currentCost != node.Cost)
                {
                    continue;
                }
                if (goalByTile.TryGetValue(current, out var goal))
                {
                    return new RoutePlan(
                        goal,
                        ReconstructRoute(previous, current));
                }

                foreach (var direction in Directions)
                {
                    var next = (
                        X: current.X + direction.X,
                        Y: current.Y + direction.Y);
                    if (!InBounds(grid, next.X, next.Y) ||
                        closedGateTiles.Contains(next) ||
                        grid[next.Y][next.X] &&
                        !obstacleByTile.ContainsKey(next) &&
                        !lavaTiles.Contains(next) &&
                        !goalByTile.ContainsKey(next))
                    {
                        continue;
                    }

                    var nextCost = currentCost + 1 +
                        RouteActionCost(
                            next,
                            obstacleByTile,
                            lavaTiles,
                            monsterByTile);
                    if (distance.TryGetValue(
                            next,
                            out var knownCost) &&
                        knownCost <= nextCost)
                    {
                        continue;
                    }
                    distance[next] = nextCost;
                    previous[next] = current;
                    frontier.Add((
                        nextCost,
                        sequence++,
                        next.X,
                        next.Y));
                }
            }
            return null;
        }

        private static int RouteActionCost(
            (int X, int Y) tile,
            IReadOnlyDictionary<(int X, int Y), JsonElement>
                obstacleByTile,
            ISet<(int X, int Y)> lavaTiles,
            IReadOnlyDictionary<(int X, int Y), JsonElement>
                monsterByTile)
        {
            if (monsterByTile.ContainsKey(tile))
            {
                return 120;
            }
            if (obstacleByTile.TryGetValue(tile, out var obstacle))
            {
                return 40 + Math.Max(
                    1,
                    ReadInt(obstacle, "health_or_hits_remaining"));
            }
            return lavaTiles.Contains(tile) ? 60 : 0;
        }

        private static VolcanoPathTile[] ReconstructRoute(
            IReadOnlyDictionary<
                (int X, int Y),
                (int X, int Y)?> previous,
            (int X, int Y) target)
        {
            var reversed = new List<VolcanoPathTile>();
            (int X, int Y)? current = target;
            while (current.HasValue)
            {
                reversed.Add(new VolcanoPathTile
                {
                    X = current.Value.X,
                    Y = current.Value.Y
                });
                current = previous[current.Value];
            }
            reversed.Reverse();
            return reversed.ToArray();
        }

        private static class RouteGoalKinds
        {
            public const string Connector = "connector";
            public const string Switch = "switch";
        }

        private sealed class RouteGoal
        {
            public RouteGoal(
                string kind,
                int x,
                int y,
                JsonElement source)
            {
                Kind = kind;
                X = x;
                Y = y;
                Source = source;
            }

            public string Kind { get; }
            public int X { get; }
            public int Y { get; }
            public JsonElement Source { get; }
        }

        private sealed class RoutePlan
        {
            public RoutePlan(
                RouteGoal goal,
                VolcanoPathTile[] path)
            {
                Goal = goal;
                Path = path;
            }

            public RouteGoal Goal { get; }
            public VolcanoPathTile[] Path { get; }
        }

        private static int? ReadBestSlot(JsonElement resources, string arrayProperty, string scoreProperty, bool rejectScythe = false)
        {
            if (!resources.TryGetProperty(arrayProperty, out var slots) || slots.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var selected = slots.EnumerateArray()
                .Where(slot => !rejectScythe || !ReadBool(slot, "is_scythe"))
                .OrderByDescending(slot => ReadInt(slot, scoreProperty))
                .ThenBy(slot => ReadInt(slot, "slot_index"))
                .FirstOrDefault();
            return selected.ValueKind == JsonValueKind.Object ? ReadInt(selected, "slot_index") : null;
        }

        private static int ReadSlotScore(JsonElement resources, string arrayProperty, int slotIndex, string scoreProperty)
        {
            if (!resources.TryGetProperty(arrayProperty, out var slots) || slots.ValueKind != JsonValueKind.Array)
            {
                return 0;
            }

            var selected = slots.EnumerateArray()
                .FirstOrDefault(slot => ReadInt(slot, "slot_index") == slotIndex);
            return selected.ValueKind == JsonValueKind.Object ? ReadInt(selected, scoreProperty) : 0;
        }

        private static int? ReadWateringCanSlot(JsonElement resources)
        {
            if (!resources.TryGetProperty("watering_can_slots", out var slots) || slots.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var slot in slots.EnumerateArray())
            {
                if (ReadBool(slot, "can_cool_lava_now"))
                {
                    return ReadInt(slot, "slot_index");
                }
            }
            return null;
        }

        private static bool TryCollision(JsonElement tiles, out bool[][] grid, out (int X, int Y) start)
        {
            return TryCollision(
                tiles,
                preferStaticRows: true,
                out grid,
                out start);
        }

        private static bool TryCollision(
            JsonElement tiles,
            bool preferStaticRows,
            out bool[][] grid,
            out (int X, int Y) start)
        {
            grid = Array.Empty<bool[]>();
            start = default;
            if (!tiles.TryGetProperty("player_tile", out var playerTile) ||
                !tiles.TryGetProperty("collision_context", out var collision) ||
                !string.Equals(ReadString(collision, "status"), "available", StringComparison.Ordinal) ||
                !(preferStaticRows &&
                        collision.TryGetProperty(
                            "static_blocked_rows",
                            out var rows) ||
                    collision.TryGetProperty("blocked_rows", out rows)) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<bool[]>();
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.String)
                {
                    return false;
                }
                parsed.Add((row.GetString() ?? string.Empty).Select(value => value == '1').ToArray());
            }

            if (parsed.Count == 0 || parsed.Any(row => row.Length != parsed[0].Length))
            {
                return false;
            }

            start = (ReadInt(playerTile, "tile_x"), ReadInt(playerTile, "tile_y"));
            if (!InBounds(parsed.ToArray(), start.X, start.Y))
            {
                return false;
            }

            grid = parsed.ToArray();
            grid[start.Y][start.X] = false;
            return true;
        }

        private static SearchResult Search(bool[][] grid, (int X, int Y) start)
        {
            var previous = new Dictionary<(int X, int Y), (int X, int Y)?>();
            var queue = new Queue<(int X, int Y)>();
            previous[start] = null;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = (current.X + direction.X, current.Y + direction.Y);
                    if (!InBounds(grid, next.Item1, next.Item2) || grid[next.Item2][next.Item1] || previous.ContainsKey(next))
                    {
                        continue;
                    }
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
            return new SearchResult(previous);
        }

        private static bool InBounds(bool[][] grid, int x, int y)
        {
            return y >= 0 && y < grid.Length && x >= 0 && x < grid[y].Length;
        }

        private static bool TryFieldValue(JsonElement section, string field, out JsonElement value)
        {
            value = default;
            return section.TryGetProperty(field, out var envelope) &&
                envelope.ValueKind == JsonValueKind.Object &&
                envelope.TryGetProperty("status", out var status) &&
                string.Equals(status.GetString(), "available", StringComparison.OrdinalIgnoreCase) &&
                envelope.TryGetProperty("value", out value);
        }

        private static VolcanoFloorStepPlan Blocked(string reason)
        {
            return new VolcanoFloorStepPlan { Status = "blocked", StepKind = VolcanoFloorStepKinds.Blocked, Reason = reason };
        }

        private sealed class SearchResult
        {
            private readonly Dictionary<(int X, int Y), (int X, int Y)?> previous;

            public SearchResult(Dictionary<(int X, int Y), (int X, int Y)?> previous)
            {
                this.previous = previous;
            }

            public bool TryPath(int x, int y, out VolcanoPathTile[] path)
            {
                path = Array.Empty<VolcanoPathTile>();
                var target = (X: x, Y: y);
                if (!previous.ContainsKey(target))
                {
                    return false;
                }

                var reversed = new List<VolcanoPathTile>();
                (int X, int Y)? current = target;
                while (current.HasValue)
                {
                    reversed.Add(new VolcanoPathTile { X = current.Value.X, Y = current.Value.Y });
                    current = previous[current.Value];
                }
                reversed.Reverse();
                path = reversed.ToArray();
                return true;
            }
        }
    }

}
