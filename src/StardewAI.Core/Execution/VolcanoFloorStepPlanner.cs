using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public static class VolcanoFloorStepKinds
    {
        public const string PressDwarfSwitch = "press_dwarf_switch";
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
            var immediateThreat = SelectNearestMonster(monsters, resources, search, start, maximumDistanceFromPlayer: 3);
            if (immediateThreat is not null)
            {
                return immediateThreat;
            }

            var connectorStep = SelectForwardConnector(connectors, search);
            if (connectorStep is not null)
            {
                return connectorStep;
            }

            var switchStep = SelectUnpressedSwitch(gates, search);
            if (switchStep is not null)
            {
                return switchStep;
            }

            var lavaStep = SelectCoolableLava(tiles, resources, grid, search);
            if (lavaStep is not null)
            {
                lavaStep.Status = lavaStep.WateringCanSlotIndex.HasValue ? "ready" : "blocked";
                lavaStep.Reason = lavaStep.WateringCanSlotIndex.HasValue
                    ? "reachable_native_cooling_target"
                    : "volcano_cooling_requires_watering_can_with_water";
                return lavaStep;
            }

            var obstacleStep = SelectObstacle(objects, resources, grid, search);
            if (obstacleStep is not null)
            {
                return obstacleStep;
            }

            var remainingMonster = SelectNearestMonster(monsters, resources, search, start, maximumDistanceFromPlayer: null);
            if (remainingMonster is not null)
            {
                return remainingMonster;
            }

            return Blocked("volcano_forward_route_unresolved_from_loaded_state");
        }

        private static VolcanoFloorStepPlan? SelectUnpressedSwitch(JsonElement gates, SearchResult search)
        {
            if (gates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var candidates = new List<(int X, int Y, int Distance, VolcanoPathTile[] Path)>();
            foreach (var gate in gates.EnumerateArray())
            {
                if (ReadInt(gate, "gate_index") != 0 ||
                    ReadBool(gate, "opened") ||
                    !gate.TryGetProperty("switches", out var switches) ||
                    switches.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in switches.EnumerateArray())
                {
                    if (ReadBool(item, "pressed"))
                    {
                        continue;
                    }

                    var x = ReadInt(item, "tile_x");
                    var y = ReadInt(item, "tile_y");
                    if (search.TryPath(x, y, out var path))
                    {
                        candidates.Add((x, y, path.Length - 1, path));
                    }
                }
            }

            var selected = candidates
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Y)
                .ThenBy(candidate => candidate.X)
                .FirstOrDefault();
            if (selected.Path is null)
            {
                return null;
            }

            return new VolcanoFloorStepPlan
            {
                Status = "ready",
                StepKind = VolcanoFloorStepKinds.PressDwarfSwitch,
                Reason = "reachable_unpressed_dwarf_switch",
                TargetTileX = selected.X,
                TargetTileY = selected.Y,
                StandTileX = selected.X,
                StandTileY = selected.Y,
                EstimatedMovementTiles = selected.Distance,
                Path = selected.Path
            };
        }

        private static VolcanoFloorStepPlan? SelectForwardConnector(JsonElement connectors, SearchResult search)
        {
            if (!connectors.TryGetProperty("forward_warps", out var warps) || warps.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var candidates = new List<(JsonElement Warp, int Distance, VolcanoPathTile[] Path)>();
            foreach (var warp in warps.EnumerateArray())
            {
                var x = ReadInt(warp, "tile_x");
                var y = ReadInt(warp, "tile_y");
                if (search.TryPath(x, y, out var path))
                {
                    candidates.Add((warp, path.Length - 1, path));
                }
            }

            var selected = candidates
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => ReadInt(candidate.Warp, "tile_y"))
                .ThenBy(candidate => ReadInt(candidate.Warp, "tile_x"))
                .FirstOrDefault();
            if (selected.Path is null)
            {
                return null;
            }

            return new VolcanoFloorStepPlan
            {
                Status = "ready",
                StepKind = VolcanoFloorStepKinds.TraverseForwardConnector,
                Reason = "reachable_native_forward_warp",
                TargetTileX = ReadInt(selected.Warp, "tile_x"),
                TargetTileY = ReadInt(selected.Warp, "tile_y"),
                StandTileX = ReadInt(selected.Warp, "tile_x"),
                StandTileY = ReadInt(selected.Warp, "tile_y"),
                EstimatedMovementTiles = selected.Distance,
                ExpectedTargetLocation = ReadString(selected.Warp, "target_location"),
                ExpectedArrivalTileX = ReadInt(selected.Warp, "target_tile_x"),
                ExpectedArrivalTileY = ReadInt(selected.Warp, "target_tile_y"),
                Path = selected.Path
            };
        }

        private static VolcanoFloorStepPlan? SelectCoolableLava(
            JsonElement tiles,
            JsonElement resources,
            bool[][] grid,
            SearchResult search)
        {
            if (!tiles.TryGetProperty("coolable_uncooled_tiles", out var lava) || lava.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var wateringCanSlot = ReadWateringCanSlot(resources);
            var candidates = new List<(int X, int Y, int StandX, int StandY, int Distance, VolcanoPathTile[] Path)>();
            foreach (var tile in lava.EnumerateArray())
            {
                var x = ReadInt(tile, "tile_x");
                var y = ReadInt(tile, "tile_y");
                foreach (var direction in Directions)
                {
                    var standX = x + direction.X;
                    var standY = y + direction.Y;
                    if (!InBounds(grid, standX, standY) || grid[standY][standX] || !search.TryPath(standX, standY, out var path))
                    {
                        continue;
                    }

                    candidates.Add((x, y, standX, standY, path.Length - 1, path));
                }
            }

            var selected = candidates
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Y)
                .ThenBy(candidate => candidate.X)
                .FirstOrDefault();
            if (selected.Path is null)
            {
                return null;
            }

            return new VolcanoFloorStepPlan
            {
                Status = "blocked",
                StepKind = VolcanoFloorStepKinds.CoolLavaTile,
                TargetTileX = selected.X,
                TargetTileY = selected.Y,
                StandTileX = selected.StandX,
                StandTileY = selected.StandY,
                EstimatedMovementTiles = selected.Distance,
                WateringCanSlotIndex = wateringCanSlot,
                Path = selected.Path
            };
        }

        private static VolcanoFloorStepPlan? SelectObstacle(JsonElement objects, JsonElement resources, bool[][] grid, SearchResult search)
        {
            if (objects.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var candidates = new List<(JsonElement Item, int StandX, int StandY, int Distance, VolcanoPathTile[] Path)>();
            foreach (var item in objects.EnumerateArray())
            {
                if (!ReadBool(item, "is_breakable_stone") && !ReadBool(item, "is_breakable_container"))
                {
                    continue;
                }

                var x = ReadInt(item, "tile_x");
                var y = ReadInt(item, "tile_y");
                foreach (var direction in Directions)
                {
                    var standX = x + direction.X;
                    var standY = y + direction.Y;
                    if (!InBounds(grid, standX, standY) || grid[standY][standX] || !search.TryPath(standX, standY, out var path))
                    {
                        continue;
                    }

                    candidates.Add((item, standX, standY, path.Length - 1, path));
                }
            }

            var selected = candidates
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => ReadInt(candidate.Item, "tile_y"))
                .ThenBy(candidate => ReadInt(candidate.Item, "tile_x"))
                .FirstOrDefault();
            if (selected.Path is null)
            {
                return null;
            }

            var isStone = ReadBool(selected.Item, "is_breakable_stone");
            var toolSlot = isStone
                ? ReadBestSlot(resources, "pickaxe_slots", "damage_per_hit")
                : ReadBestSlot(resources, "heavy_hitter_slots", "container_damage_per_hit");
            var damagePerUse = toolSlot.HasValue
                ? ReadSlotScore(
                    resources,
                    isStone ? "pickaxe_slots" : "heavy_hitter_slots",
                    toolSlot.Value,
                    isStone ? "damage_per_hit" : "container_damage_per_hit")
                : 0;
            var targetHealth = Math.Max(1, ReadInt(selected.Item, "health_or_hits_remaining"));
            return new VolcanoFloorStepPlan
            {
                Status = toolSlot.HasValue ? "ready" : "blocked",
                StepKind = isStone ? VolcanoFloorStepKinds.BreakStone : VolcanoFloorStepKinds.BreakContainer,
                Reason = toolSlot.HasValue
                    ? isStone ? "reachable_native_pickaxe_target" : "reachable_native_heavy_hitter_target"
                    : isStone ? "volcano_break_stone_pickaxe_unavailable" : "volcano_break_container_heavy_hitter_unavailable",
                TargetTileX = ReadInt(selected.Item, "tile_x"),
                TargetTileY = ReadInt(selected.Item, "tile_y"),
                StandTileX = selected.StandX,
                StandTileY = selected.StandY,
                EstimatedMovementTiles = selected.Distance,
                EstimatedToolUses = damagePerUse > 0
                    ? (int)Math.Ceiling((double)targetHealth / damagePerUse)
                    : 0,
                TargetRuntimeType = ReadString(selected.Item, "runtime_type"),
                TargetQualifiedItemId = ReadString(selected.Item, "qualified_item_id"),
                ToolSlotIndex = toolSlot,
                Path = selected.Path
            };
        }

        private static VolcanoFloorStepPlan? SelectNearestMonster(
            JsonElement monsters,
            JsonElement resources,
            SearchResult search,
            (int X, int Y) start,
            int? maximumDistanceFromPlayer)
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
                if (maximumDistanceFromPlayer.HasValue &&
                    Math.Abs(x - start.X) + Math.Abs(y - start.Y) > maximumDistanceFromPlayer.Value)
                {
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
                Path = selected.Path
            };
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
            grid = Array.Empty<bool[]>();
            start = default;
            if (!tiles.TryGetProperty("player_tile", out var playerTile) ||
                !tiles.TryGetProperty("collision_context", out var collision) ||
                !string.Equals(ReadString(collision, "status"), "available", StringComparison.Ordinal) ||
                !collision.TryGetProperty("blocked_rows", out var rows) ||
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

    public static class VolcanoFloorStepCompiler
    {
        public static string ExecutionOptionId(VolcanoFloorStepPlan plan)
        {
            return plan.StepKind switch
            {
                VolcanoFloorStepKinds.PressDwarfSwitch => "executor.move_to_tile",
                VolcanoFloorStepKinds.TraverseForwardConnector => "executor.traverse_connector",
                VolcanoFloorStepKinds.CoolLavaTile => "executor.cool_volcano_lava",
                VolcanoFloorStepKinds.BreakStone => "executor.break_volcano_stone",
                VolcanoFloorStepKinds.BreakContainer => "executor.break_volcano_container",
                VolcanoFloorStepKinds.CombatMonster => "executor.combat_volcano_monster",
                _ => string.Empty
            };
        }

        public static SmallModelActionParameter[] BuildExecutionParameters(VolcanoFloorStepPlan plan)
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("execution_option_id", ExecutionOptionId(plan)),
                Parameter("volcano_step_kind", plan.StepKind),
                Parameter("volcano_step_reason", plan.Reason),
                Parameter("estimated_movement_tiles", plan.EstimatedMovementTiles.ToString(CultureInfo.InvariantCulture)),
                Parameter("estimated_tool_uses", plan.EstimatedToolUses.ToString(CultureInfo.InvariantCulture))
            };
            Add(parameters, "target_tile_x", plan.TargetTileX);
            Add(parameters, "target_tile_y", plan.TargetTileY);
            Add(parameters, "stand_tile_x", plan.StandTileX);
            Add(parameters, "stand_tile_y", plan.StandTileY);
            Add(parameters, "max_movement_tiles", plan.EstimatedMovementTiles > 0 ? Math.Max(8, plan.EstimatedMovementTiles + 8) : (int?)null);
            Add(parameters, "max_tool_swings", plan.EstimatedToolUses > 0 ? plan.EstimatedToolUses + 1 : (int?)null);
            Add(parameters, "expected_target_location", plan.ExpectedTargetLocation);
            Add(parameters, "expected_arrival_tile_x", plan.ExpectedArrivalTileX);
            Add(parameters, "expected_arrival_tile_y", plan.ExpectedArrivalTileY);
            Add(parameters, "watering_can_slot_index", plan.WateringCanSlotIndex);
            Add(parameters, "tool_slot_index", plan.ToolSlotIndex);
            Add(parameters, "combat_weapon_slot_index", plan.CombatWeaponSlotIndex);
            Add(parameters, "target_runtime_identity", plan.TargetRuntimeIdentity);
            Add(parameters, "target_runtime_type", plan.TargetRuntimeType);
            Add(parameters, "target_name", plan.TargetName);
            Add(parameters, "qualified_item_id", plan.TargetQualifiedItemId);
            if (plan.StepKind == VolcanoFloorStepKinds.PressDwarfSwitch)
            {
                parameters.Add(Parameter("expected_touch_action", "DwarfSwitch"));
            }
            return parameters.ToArray();
        }

        private static void Add(List<SmallModelActionParameter> parameters, string name, int? value)
        {
            if (value.HasValue)
            {
                parameters.Add(Parameter(name, value.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static void Add(List<SmallModelActionParameter> parameters, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add(Parameter(name, value));
            }
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }
    }
}
