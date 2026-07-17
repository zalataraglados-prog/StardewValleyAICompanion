using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private static Candidate? WalkCandidate(JsonElement element, SearchResult search, bool[,] grid)
        {
            var x = ReadInt(element, "tile_x");
            var y = ReadInt(element, "tile_y");
            if (!x.HasValue || !y.HasValue || !InBounds(grid, x.Value, y.Value) || grid[x.Value, y.Value] || !search.Distance.TryGetValue(Key(x.Value, y.Value), out var distance))
            {
                return null;
            }
            return new Candidate(x.Value, y.Value, x.Value, y.Value, distance, 0, false, search.PathTo(x.Value, y.Value));
        }

        private static bool NeedsHealing(JsonElement resources, JsonElement monsters, MiningFloorObjective objective, out string reason)
        {
            var health = ReadInt(resources, "health") ?? 0;
            var maxDamage = monsters.ValueKind == JsonValueKind.Array
                ? monsters.EnumerateArray().Select(monster => ReadInt(monster, "damage_to_farmer") ?? 0).DefaultIfEmpty(0).Max()
                : 0;
            var floor = Math.Max(objective.MinimumReserveHealth, maxDamage * 2);
            reason = health <= floor ? "health_below_two_hit_or_configured_reserve" : string.Empty;
            return health <= floor;
        }

        private static MiningFloorStepPlan? SelectFood(JsonElement resources, int minimumReserveHealth, int? restoreSlot)
        {
            if (!resources.TryGetProperty("food_slots", out var foods) || foods.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var health = ReadInt(resources, "health") ?? 0;
            return foods.EnumerateArray()
                .Where(food => (ReadInt(food, "health_recovery") ?? 0) > 0)
                .OrderBy(food => Math.Abs(health + (ReadInt(food, "health_recovery") ?? 0) - Math.Max(health, minimumReserveHealth)))
                .ThenBy(food => ReadInt(food, "sell_price") ?? int.MaxValue)
                .Select(food => new MiningFloorStepPlan
                {
                    Status = "ready",
                    StepKind = MiningFloorStepKinds.ConsumeFood,
                    Reason = "health_recovery_required",
                    FoodSlotIndex = ReadInt(food, "slot_index"),
                    RestoreSlotIndex = restoreSlot,
                    TargetQualifiedItemId = ReadString(food, "qualified_item_id"),
                    SafetyWindowStatus = "native_eating_lifecycle_handles_recovery_window"
                })
                .FirstOrDefault();
        }

        private static MiningFloorStepPlan? SelectStone(JsonElement objects, SearchResult search, bool[,] grid)
        {
            if (objects.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return objects.EnumerateArray()
                .Where(obj => ReadBool(obj, "is_breakable_stone"))
                .Select(obj =>
                {
                    var swings = Math.Max(1, ReadInt(obj, "best_pickaxe_hits_remaining") ?? 1);
                    var deterministic = obj.TryGetProperty("ladder_preview", out var preview) &&
                        preview.ValueKind == JsonValueKind.Object &&
                        ReadBool(preview, "creates_ladder");
                    return TargetCandidate(obj, search, grid, swings, deterministic);
                })
                .Where(candidate => candidate is not null)
                .OrderBy(candidate => candidate!.DeterministicLadder ? 0 : 1)
                .ThenBy(candidate => candidate!.Distance + candidate.Swings)
                .ThenBy(candidate => candidate!.Swings)
                .ThenBy(candidate => candidate!.Distance)
                .ThenBy(candidate => candidate!.TargetY)
                .ThenBy(candidate => candidate!.TargetX)
                .Select(candidate => Build(
                    MiningFloorStepKinds.MineStone,
                    candidate!.DeterministicLadder ? "deterministic_ladder_stone_reachable" : "lowest_reachable_movement_and_swing_cost",
                    candidate))
                .FirstOrDefault();
        }

        private static Candidate? TargetCandidate(JsonElement element, SearchResult search, bool[,] grid, int estimatedSwings, bool deterministicLadder)
        {
            var targetX = ReadInt(element, "tile_x");
            var targetY = ReadInt(element, "tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return null;
            }
            return TargetCandidate(targetX.Value, targetY.Value, search, grid, estimatedSwings, deterministicLadder);
        }

        private static Candidate? TargetCandidate(int targetX, int targetY, SearchResult search, bool[,] grid, int estimatedSwings, bool deterministicLadder)
        {
            Candidate? best = null;
            foreach (var direction in Directions)
            {
                var standX = targetX + direction.X;
                var standY = targetY + direction.Y;
                if (!InBounds(grid, standX, standY) || grid[standX, standY] || !search.Distance.TryGetValue(Key(standX, standY), out var distance))
                {
                    continue;
                }

                var candidate = new Candidate(targetX, targetY, standX, standY, distance, estimatedSwings, deterministicLadder, search.PathTo(standX, standY));
                if (best is null || candidate.Distance < best.Distance ||
                    candidate.Distance == best.Distance && (candidate.StandY < best.StandY || candidate.StandY == best.StandY && candidate.StandX < best.StandX))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static Candidate? RectangleTargetCandidate(
            int tileX,
            int tileY,
            int width,
            int height,
            SearchResult search,
            bool[,] grid,
            int estimatedSwings)
        {
            Candidate? best = null;
            for (var targetX = tileX; targetX < tileX + width; targetX++)
            {
                for (var targetY = tileY; targetY < tileY + height; targetY++)
                {
                    foreach (var direction in Directions)
                    {
                        var standX = targetX + direction.X;
                        var standY = targetY + direction.Y;
                        if (standX >= tileX && standX < tileX + width &&
                            standY >= tileY && standY < tileY + height)
                        {
                            continue;
                        }
                        if (!InBounds(grid, standX, standY) ||
                            grid[standX, standY] ||
                            !search.Distance.TryGetValue(Key(standX, standY), out var distance))
                        {
                            continue;
                        }

                        var candidate = new Candidate(
                            targetX,
                            targetY,
                            standX,
                            standY,
                            distance,
                            estimatedSwings,
                            deterministicLadder: false,
                            search.PathTo(standX, standY));
                        if (best is null ||
                            candidate.Distance < best.Distance ||
                            candidate.Distance == best.Distance &&
                            (candidate.TargetY < best.TargetY ||
                                candidate.TargetY == best.TargetY && candidate.TargetX < best.TargetX))
                        {
                            best = candidate;
                        }
                    }
                }
            }
            return best;
        }

        private static MiningFloorStepPlan BuildRangedCombat(string reason, Candidate target, (int X, int Y) playerTile)
        {
            return new MiningFloorStepPlan
            {
                Status = "ready",
                StepKind = MiningFloorStepKinds.ShootMonster,
                Reason = reason,
                TargetTileX = target.TargetX,
                TargetTileY = target.TargetY,
                StandTileX = playerTile.X,
                StandTileY = playerTile.Y,
                EstimatedMovementTiles = 0,
                Path = new[] { new MiningPathTile { X = playerTile.X, Y = playerTile.Y } }
            };
        }

        private static bool HasClearProjectileLine((int X, int Y) start, (int X, int Y) target, bool[,] grid)
        {
            if (!InBounds(grid, target.X, target.Y))
            {
                return false;
            }
            var x = start.X;
            var y = start.Y;
            var deltaX = Math.Abs(target.X - start.X);
            var stepX = start.X < target.X ? 1 : -1;
            var deltaY = -Math.Abs(target.Y - start.Y);
            var stepY = start.Y < target.Y ? 1 : -1;
            var error = deltaX + deltaY;
            while (x != target.X || y != target.Y)
            {
                var doubled = 2 * error;
                if (doubled >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }
                if (doubled <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }
                if ((x != target.X || y != target.Y) && (!InBounds(grid, x, y) || grid[x, y]))
                {
                    return false;
                }
            }
            return true;
        }

        private static MiningFloorStepPlan Build(string stepKind, string reason, Candidate candidate)
        {
            return new MiningFloorStepPlan
            {
                Status = "ready",
                StepKind = stepKind,
                Reason = reason,
                TargetTileX = candidate.TargetX,
                TargetTileY = candidate.TargetY,
                StandTileX = candidate.StandX,
                StandTileY = candidate.StandY,
                EstimatedMovementTiles = candidate.Distance,
                EstimatedToolSwings = candidate.Swings,
                DeterministicLadderAfterBreak = candidate.DeterministicLadder,
                Path = candidate.Path
            };
        }

        private static MiningFloorStepPlan Blocked(string reason)
        {
            return new MiningFloorStepPlan { Reason = reason };
        }

        private static bool TryCollision(JsonElement tiles, out bool[,] grid, out (int X, int Y) start)
        {
            grid = new bool[0, 0];
            start = default;
            if (!tiles.TryGetProperty("player_tile", out var playerTile) ||
                !tiles.TryGetProperty("collision_context", out var collision) ||
                ReadString(collision, "status") != "available" ||
                ReadString(collision, "encoding") != "row_major_strings_1_blocked_0_passable" ||
                !collision.TryGetProperty("blocked_rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var width = ReadInt(collision, "width") ?? 0;
            var height = ReadInt(collision, "height") ?? 0;
            var rowValues = rows.EnumerateArray().Select(row => row.GetString() ?? string.Empty).ToArray();
            if (width <= 0 || height <= 0 || rowValues.Length != height || rowValues.Any(row => row.Length != width || row.Any(value => value != '0' && value != '1')))
            {
                return false;
            }

            grid = new bool[width, height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    grid[x, y] = rowValues[y][x] == '1';
                }
            }

            var startX = ReadInt(playerTile, "tile_x");
            var startY = ReadInt(playerTile, "tile_y");
            if (!startX.HasValue || !startY.HasValue || !InBounds(grid, startX.Value, startY.Value))
            {
                return false;
            }

            grid[startX.Value, startY.Value] = false;
            start = (startX.Value, startY.Value);
            return true;
        }

        private static SearchResult Search(bool[,] grid, (int X, int Y) start)
        {
            var distance = new Dictionary<string, int>(StringComparer.Ordinal) { [Key(start.X, start.Y)] = 0 };
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = (X: current.X + direction.X, Y: current.Y + direction.Y);
                    var key = Key(next.X, next.Y);
                    if (!InBounds(grid, next.X, next.Y) || grid[next.X, next.Y] || distance.ContainsKey(key))
                    {
                        continue;
                    }

                    distance[key] = distance[Key(current.X, current.Y)] + 1;
                    previous[key] = Key(current.X, current.Y);
                    queue.Enqueue(next);
                }
            }

            return new SearchResult(start, distance, previous);
        }

        private static bool TryFieldValue(JsonElement section, string field, out JsonElement value)
        {
            value = default;
            return section.TryGetProperty(field, out var envelope) &&
                envelope.ValueKind == JsonValueKind.Object &&
                (ReadString(envelope, "status") == "available" || ReadString(envelope, "status") == "derived") &&
                envelope.TryGetProperty("value", out value);
        }

        private static bool? SnapshotBool(SnapshotEnvelope snapshot, string sectionName, string fieldName)
        {
            if (!snapshot.State.TryGetValue(sectionName, out var section) ||
                section.ValueKind != JsonValueKind.Object ||
                !TryFieldValue(section, fieldName, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.True
                ? true
                : value.ValueKind == JsonValueKind.False ? false : null;
        }

        private static bool InBounds(bool[,] grid, int x, int y)
        {
            return x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1);
        }

        private static string Key(int x, int y) => x + "," + y;

        private static int? ReadInt(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed)
                ? parsed
                : (int?)null;
        }

        private static double? ReadDouble(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.TryGetDouble(out var parsed)
                ? parsed
                : (double?)null;
        }

        private static bool ReadBool(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
        }

        private static string ReadString(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static string[] ReadStrings(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>();
        }

        private static string[] ReadStringsWithLegacyFallback(JsonElement element, string property, string fallbackProperty)
        {
            return element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Array
                    ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
                    : ReadStrings(element, fallbackProperty);
        }

    }
}
