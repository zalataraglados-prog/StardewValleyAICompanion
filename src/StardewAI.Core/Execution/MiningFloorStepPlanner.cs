using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution
{
    public static class MiningFloorStepKinds
    {
        public const string DescendLadder = "descend_ladder";
        public const string MineStone = "mine_stone";
        public const string CombatMonster = "combat_monster";
        public const string Blocked = "blocked";
    }

    public sealed class MiningPathTile
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    public sealed class MiningFloorStepPlan
    {
        public string Status { get; set; } = "blocked";

        public string StepKind { get; set; } = MiningFloorStepKinds.Blocked;

        public string Reason { get; set; } = string.Empty;

        public int? TargetTileX { get; set; }

        public int? TargetTileY { get; set; }

        public int? StandTileX { get; set; }

        public int? StandTileY { get; set; }

        public int EstimatedMovementTiles { get; set; }

        public int EstimatedToolSwings { get; set; }

        public bool DeterministicLadderAfterBreak { get; set; }

        public MiningPathTile[] Path { get; set; } = Array.Empty<MiningPathTile>();
    }

    public sealed class MiningFloorStepPlanner
    {
        private static readonly (int X, int Y)[] Directions =
        {
            (0, -1),
            (-1, 0),
            (1, 0),
            (0, 1)
        };

        public MiningFloorStepPlan Plan(SnapshotEnvelope snapshot)
        {
            if (!snapshot.State.TryGetValue("mining", out var mining) || mining.ValueKind != JsonValueKind.Object)
            {
                return Blocked("mining_state_unavailable");
            }

            if (!TryFieldValue(mining, "tiles", out var tiles) ||
                !TryFieldValue(mining, "objects", out var objects) ||
                !TryFieldValue(mining, "monsters", out var monsters) ||
                !TryFieldValue(mining, "floor_objectives", out var objectives))
            {
                return Blocked("mining_required_group_unavailable");
            }

            if (!TryCollision(tiles, out var grid, out var start))
            {
                return Blocked("mining_collision_context_unavailable");
            }

            var search = Search(grid, start);
            var ladderPlan = SelectActionTile(tiles, "ladders", MiningFloorStepKinds.DescendLadder, "reachable_ladder_available", search, grid);
            if (ladderPlan is not null)
            {
                return ladderPlan;
            }

            var mustKillAll = ReadBool(objectives, "must_kill_all_monsters_to_advance");
            if (mustKillAll)
            {
                return SelectMonster(monsters, search, grid, "kill_all_floor_requires_combat") ??
                    Blocked("kill_all_floor_has_no_reachable_monster");
            }

            var stonePlan = SelectStone(objects, search, grid);
            if (stonePlan is not null)
            {
                return stonePlan;
            }

            var combatPlan = SelectMonster(monsters, search, grid, "no_reachable_stone_clear_dynamic_monster");
            return combatPlan ?? Blocked("no_reachable_ladder_stone_or_monster");
        }

        private static MiningFloorStepPlan? SelectActionTile(JsonElement tiles, string propertyName, string stepKind, string reason, SearchResult search, bool[,] grid)
        {
            if (!tiles.TryGetProperty(propertyName, out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return candidates.EnumerateArray()
                .Select(candidate => TargetCandidate(candidate, search, grid, estimatedSwings: 0, deterministicLadder: false))
                .Where(candidate => candidate is not null)
                .OrderBy(candidate => candidate!.Distance)
                .ThenBy(candidate => candidate!.TargetY)
                .ThenBy(candidate => candidate!.TargetX)
                .Select(candidate => Build(stepKind, reason, candidate!))
                .FirstOrDefault();
        }

        private static MiningFloorStepPlan? SelectMonster(JsonElement monsters, SearchResult search, bool[,] grid, string reason)
        {
            if (monsters.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return monsters.EnumerateArray()
                .Select(monster => TargetCandidate(monster, search, grid, estimatedSwings: 0, deterministicLadder: false))
                .Where(candidate => candidate is not null)
                .OrderBy(candidate => candidate!.Distance)
                .ThenBy(candidate => candidate!.TargetY)
                .ThenBy(candidate => candidate!.TargetX)
                .Select(candidate => Build(MiningFloorStepKinds.CombatMonster, reason, candidate!))
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

            Candidate? best = null;
            foreach (var direction in Directions)
            {
                var standX = targetX.Value + direction.X;
                var standY = targetY.Value + direction.Y;
                if (!InBounds(grid, standX, standY) || grid[standX, standY] || !search.Distance.TryGetValue(Key(standX, standY), out var distance))
                {
                    continue;
                }

                var candidate = new Candidate(targetX.Value, targetY.Value, standX, standY, distance, estimatedSwings, deterministicLadder, search.PathTo(standX, standY));
                if (best is null || candidate.Distance < best.Distance ||
                    candidate.Distance == best.Distance && (candidate.StandY < best.StandY || candidate.StandY == best.StandY && candidate.StandX < best.StandX))
                {
                    best = candidate;
                }
            }

            return best;
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

        private sealed class SearchResult
        {
            private readonly (int X, int Y) start;
            private readonly Dictionary<string, string> previous;

            public SearchResult((int X, int Y) start, Dictionary<string, int> distance, Dictionary<string, string> previous)
            {
                this.start = start;
                Distance = distance;
                this.previous = previous;
            }

            public Dictionary<string, int> Distance { get; }

            public MiningPathTile[] PathTo(int x, int y)
            {
                var path = new List<MiningPathTile>();
                var key = Key(x, y);
                while (true)
                {
                    var split = key.Split(',');
                    path.Add(new MiningPathTile { X = int.Parse(split[0]), Y = int.Parse(split[1]) });
                    if (key == Key(start.X, start.Y))
                    {
                        break;
                    }

                    if (!previous.TryGetValue(key, out key))
                    {
                        return Array.Empty<MiningPathTile>();
                    }
                }

                path.Reverse();
                return path.ToArray();
            }
        }

        private sealed class Candidate
        {
            public Candidate(int targetX, int targetY, int standX, int standY, int distance, int swings, bool deterministicLadder, MiningPathTile[] path)
            {
                TargetX = targetX;
                TargetY = targetY;
                StandX = standX;
                StandY = standY;
                Distance = distance;
                Swings = swings;
                DeterministicLadder = deterministicLadder;
                Path = path;
            }

            public int TargetX { get; }
            public int TargetY { get; }
            public int StandX { get; }
            public int StandY { get; }
            public int Distance { get; }
            public int Swings { get; }
            public bool DeterministicLadder { get; }
            public MiningPathTile[] Path { get; }
        }
    }
}
