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
        private static bool HasAvailableBomb(JsonElement resources)
        {
            return resources.TryGetProperty("bomb_slots", out var bombSlots) &&
                bombSlots.ValueKind == JsonValueKind.Array &&
                bombSlots.EnumerateArray().Any(bomb =>
                    (ReadInt(bomb, "stack") ?? 0) > 0 &&
                    (ReadInt(bomb, "radius_tiles") ?? 0) > 0 &&
                    ReadInt(bomb, "slot_index").HasValue);
        }

        private static MiningFloorStepPlan? SelectMummyBombFinisher(
            JsonElement objects,
            JsonElement monsters,
            JsonElement resources,
            SearchResult search,
            bool[,] grid,
            double? movementTileDurationMs,
            MiningFloorObjective objective,
            bool mustKillAll)
        {
            if (monsters.ValueKind != JsonValueKind.Array ||
                !resources.TryGetProperty("bomb_slots", out var bombSlots) ||
                bombSlots.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var protectedObjects = objects.ValueKind == JsonValueKind.Array
                ? objects.EnumerateArray()
                    .Where(obj => ReadBool(obj, "is_placed_staircase") ||
                        (!ReadBool(obj, "is_breakable_stone") && !ReadBool(obj, "is_container")))
                    .Select(obj => (X: ReadInt(obj, "tile_x"), Y: ReadInt(obj, "tile_y")))
                    .Where(row => row.X.HasValue && row.Y.HasValue)
                    .Select(row => (X: row.X!.Value, Y: row.Y!.Value))
                    .ToArray()
                : Array.Empty<(int X, int Y)>();

            MummyBombFinisherCandidate? best = null;
            foreach (var monster in monsters.EnumerateArray())
            {
                if (!monster.TryGetProperty("bomb_damage_semantics", out var semantics) ||
                    !string.Equals(ReadString(semantics, "special_effect"), "bomb_finalizes_reviving_mummy", StringComparison.Ordinal))
                {
                    continue;
                }
                if (!MummyFinishRequired(monster, search.Start, objective, mustKillAll))
                {
                    continue;
                }
                var targetX = ReadInt(monster, "tile_x");
                var targetY = ReadInt(monster, "tile_y");
                if (!targetX.HasValue || !targetY.HasValue)
                {
                    continue;
                }
                foreach (var bomb in bombSlots.EnumerateArray().OrderBy(bomb => ReadInt(bomb, "radius_tiles") ?? int.MaxValue))
                {
                    var radius = ReadInt(bomb, "radius_tiles") ?? 0;
                    var slot = ReadInt(bomb, "slot_index");
                    if (radius <= 0 || !slot.HasValue || (ReadInt(bomb, "stack") ?? 0) <= 0)
                    {
                        continue;
                    }
                    foreach (var center in MummyBombPlacementCenters(targetX.Value, targetY.Value, radius))
                    {
                        if (!InBounds(grid, center.X, center.Y) ||
                            grid[center.X, center.Y] ||
                            protectedObjects.Any(obj => obj.X == center.X && obj.Y == center.Y) ||
                            objects.ValueKind == JsonValueKind.Array && objects.EnumerateArray().Any(obj =>
                                ReadInt(obj, "tile_x") == center.X && ReadInt(obj, "tile_y") == center.Y))
                        {
                            continue;
                        }
                        var placement = TargetCandidate(center.X, center.Y, search, grid, 0, false);
                        if (placement is null ||
                            protectedObjects.Any(obj => ExactExplosionMask(radius).Contains((obj.X - center.X, obj.Y - center.Y))))
                        {
                            continue;
                        }
                        var escapeSearch = Search(grid, (placement.StandX, placement.StandY));
                        var escape = escapeSearch.Distance
                            .Select(entry =>
                            {
                                var values = entry.Key.Split(',');
                                return new
                                {
                                    X = int.Parse(values[0], CultureInfo.InvariantCulture),
                                    Y = int.Parse(values[1], CultureInfo.InvariantCulture),
                                    Distance = entry.Value
                                };
                            })
                            .Where(tile => Math.Abs(tile.X - center.X) > radius || Math.Abs(tile.Y - center.Y) > radius)
                            .OrderBy(tile => tile.Distance)
                            .ThenBy(tile => tile.Y)
                            .ThenBy(tile => tile.X)
                            .FirstOrDefault();
                        var maximumEscapeTiles = movementTileDurationMs.HasValue && movementTileDurationMs.Value > 0d
                            ? Math.Max(2, (int)Math.Floor(1900d / movementTileDurationMs.Value))
                            : 6;
                        if (escape is null || escape.Distance > maximumEscapeTiles)
                        {
                            continue;
                        }
                        var candidate = new MummyBombFinisherCandidate(monster, bomb, placement, escape.X, escape.Y, escape.Distance);
                        if (best is null || radius < best.Radius ||
                            radius == best.Radius && candidate.TotalDistance < best.TotalDistance)
                        {
                            best = candidate;
                        }
                    }
                }
            }
            if (best is null)
            {
                return null;
            }
            var plan = Build(MiningFloorStepKinds.PlaceBomb, "reviving_mummy_requires_bomb_finish", best.Placement);
            plan.TargetRuntimeIdentity = ReadString(best.Monster, "runtime_identity");
            plan.TargetRuntimeType = ReadString(best.Monster, "runtime_type");
            plan.TargetName = ReadString(best.Monster, "name");
            plan.CombatMethod = "bomb";
            plan.CombatTerminalState = "mummy_finalized";
            plan.BombSlotIndex = ReadInt(best.Bomb, "slot_index");
            plan.BombQualifiedItemId = ReadString(best.Bomb, "qualified_item_id");
            plan.BombRadiusTiles = best.Radius;
            plan.EscapeTileX = best.EscapeX;
            plan.EscapeTileY = best.EscapeY;
            plan.ExpectedBombMonsterHits = 1;
            plan.SafetyWindowStatus = "mummy_bomb_finish_fuse_escape_verified";
            return plan;
        }

        private static IEnumerable<(int X, int Y)> MummyBombPlacementCenters(int targetX, int targetY, int radius)
        {
            return Enumerable.Range(-radius, radius * 2 + 1)
                .SelectMany(offsetX => Enumerable.Range(-radius, radius * 2 + 1)
                    .Select(offsetY => (X: targetX + offsetX, Y: targetY + offsetY, OffsetX: offsetX, OffsetY: offsetY)))
                .Where(row => row.OffsetX != 0 || row.OffsetY != 0)
                .Where(row => Math.Abs(row.OffsetX) <= radius && Math.Abs(row.OffsetY) <= radius)
                .OrderBy(row => Math.Abs(row.OffsetX) + Math.Abs(row.OffsetY))
                .ThenBy(row => row.Y)
                .ThenBy(row => row.X)
                .Select(row => (row.X, row.Y));
        }

        private static bool MummyFinishRequired(
            JsonElement monster,
            (int X, int Y) playerTile,
            MiningFloorObjective objective,
            bool mustKillAll)
        {
            if (mustKillAll)
            {
                return true;
            }
            if (objective.Kind == MiningObjectiveKinds.CollectMonsterDrop)
            {
                var targets = new HashSet<string>(objective.TargetQualifiedItemIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                return targets.Count > 0 && ReadStrings(monster, "possible_drop_qualified_item_ids").Any(targets.Contains);
            }
            var x = ReadInt(monster, "tile_x");
            var y = ReadInt(monster, "tile_y");
            return x.HasValue && y.HasValue &&
                Math.Abs(x.Value - playerTile.X) + Math.Abs(y.Value - playerTile.Y) <= Math.Max(1, objective.ThreatRadiusTiles);
        }

        private static bool IsRevivingMummy(JsonElement monster)
        {
            return monster.TryGetProperty("bomb_damage_semantics", out var semantics) &&
                string.Equals(ReadString(semantics, "special_effect"), "bomb_finalizes_reviving_mummy", StringComparison.Ordinal);
        }

        private static MiningFloorStepPlan? SelectBombCluster(
            JsonElement objects,
            JsonElement monsters,
            JsonElement resources,
            SearchResult search,
            bool[,] grid,
            (int X, int Y) start,
            double? movementTileDurationMs)
        {
            if (!resources.TryGetProperty("bomb_slots", out var bombSlots) || bombSlots.ValueKind != JsonValueKind.Array ||
                objects.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var objectRows = objects.EnumerateArray()
                .Select(obj => new
                {
                    X = ReadInt(obj, "tile_x"),
                    Y = ReadInt(obj, "tile_y"),
                    IsUseful = ReadBool(obj, "is_breakable_stone") || ReadBool(obj, "is_container"),
                    IsProtected = ReadBool(obj, "is_placed_staircase") ||
                        (!ReadBool(obj, "is_breakable_stone") && !ReadBool(obj, "is_container"))
                })
                .Where(row => row.X.HasValue && row.Y.HasValue)
                .ToArray();
            var monsterRows = monsters.ValueKind == JsonValueKind.Array
                ? monsters.EnumerateArray().Select(monster => (X: ReadInt(monster, "tile_x"), Y: ReadInt(monster, "tile_y"))).ToArray()
                : Array.Empty<(int? X, int? Y)>();

            BombCandidate? best = null;
            foreach (var bomb in bombSlots.EnumerateArray())
            {
                var radius = ReadInt(bomb, "radius_tiles") ?? 0;
                var slotIndex = ReadInt(bomb, "slot_index");
                var stack = ReadInt(bomb, "stack") ?? 0;
                if (radius <= 0 || !slotIndex.HasValue || stack <= 0)
                {
                    continue;
                }
                var mask = ExactExplosionMask(radius);
                var minimumUsefulObjects = radius switch { <= 3 => 4, <= 5 => 7, _ => 10 };
                foreach (var pair in search.Distance.OrderBy(pair => pair.Value))
                {
                    var split = pair.Key.Split(',');
                    var center = (X: int.Parse(split[0], CultureInfo.InvariantCulture), Y: int.Parse(split[1], CultureInfo.InvariantCulture));
                    if (!InBounds(grid, center.X, center.Y) || grid[center.X, center.Y])
                    {
                        continue;
                    }
                    var candidate = TargetCandidate(center.X, center.Y, search, grid, 0, false);
                    if (candidate is null)
                    {
                        continue;
                    }
                    var affectedObjects = objectRows.Count(row => mask.Contains((row.X!.Value - center.X, row.Y!.Value - center.Y)));
                    var usefulObjects = objectRows.Count(row => row.IsUseful && mask.Contains((row.X!.Value - center.X, row.Y!.Value - center.Y)));
                    var protectedObjects = objectRows.Count(row => row.IsProtected && mask.Contains((row.X!.Value - center.X, row.Y!.Value - center.Y)));
                    var affectedMonsters = monsterRows.Count(row => row.X.HasValue && row.Y.HasValue &&
                        Math.Abs(row.X.Value - center.X) <= radius && Math.Abs(row.Y.Value - center.Y) <= radius);
                    if (protectedObjects > 0 || affectedObjects != usefulObjects ||
                        usefulObjects < minimumUsefulObjects && !(usefulObjects >= 2 && affectedMonsters >= 2))
                    {
                        continue;
                    }

                    var placementSearch = Search(grid, (candidate.StandX, candidate.StandY));
                    var escape = placementSearch.Distance
                        .Select(entry =>
                        {
                            var values = entry.Key.Split(',');
                            return new { X = int.Parse(values[0], CultureInfo.InvariantCulture), Y = int.Parse(values[1], CultureInfo.InvariantCulture), Distance = entry.Value };
                        })
                        .Where(tile => Math.Abs(tile.X - center.X) > radius || Math.Abs(tile.Y - center.Y) > radius)
                        .OrderBy(tile => tile.Distance)
                        .ThenBy(tile => tile.Y)
                        .ThenBy(tile => tile.X)
                        .FirstOrDefault();
                    var maximumEscapeTiles = movementTileDurationMs.HasValue && movementTileDurationMs.Value > 0d
                        ? Math.Max(2, (int)Math.Floor(1900d / movementTileDurationMs.Value))
                        : 6;
                    if (escape is null || escape.Distance > maximumEscapeTiles)
                    {
                        continue;
                    }

                    var score = usefulObjects * 10 + affectedMonsters * 4 - candidate.Distance - radius;
                    var row = new BombCandidate(
                        candidate,
                        slotIndex.Value,
                        ReadString(bomb, "qualified_item_id"),
                        radius,
                        escape.X,
                        escape.Y,
                        usefulObjects,
                        affectedMonsters,
                        score);
                    if (best is null || row.Score > best.Score ||
                        row.Score == best.Score && row.Candidate.Distance < best.Candidate.Distance)
                    {
                        best = row;
                    }
                }
            }

            if (best is null)
            {
                return null;
            }
            var plan = Build(MiningFloorStepKinds.PlaceBomb, "dense_breakable_cluster_with_verified_fuse_escape", best.Candidate);
            plan.BombSlotIndex = best.SlotIndex;
            plan.BombQualifiedItemId = best.QualifiedItemId;
            plan.BombRadiusTiles = best.Radius;
            plan.EscapeTileX = best.EscapeX;
            plan.EscapeTileY = best.EscapeY;
            plan.ExpectedBombObjectHits = best.ObjectHits;
            plan.ExpectedBombMonsterHits = best.MonsterHits;
            plan.SafetyWindowStatus = "bomb_fuse_escape_path_verified";
            return plan;
        }

        private static HashSet<(int X, int Y)> ExactExplosionMask(int radius)
        {
            var outline = new bool[radius * 2 + 1, radius * 2 + 1];
            var decision = 1 - radius;
            var deltaEast = 1;
            var deltaSouthEast = -2 * radius;
            var x = 0;
            var y = radius;
            outline[radius, radius + radius] = true;
            outline[radius, 0] = true;
            outline[radius + radius, radius] = true;
            outline[0, radius] = true;
            while (x < y)
            {
                if (decision >= 0)
                {
                    y--;
                    deltaSouthEast += 2;
                    decision += deltaSouthEast;
                }
                x++;
                deltaEast += 2;
                decision += deltaEast;
                outline[radius + x, radius + y] = true;
                outline[radius - x, radius + y] = true;
                outline[radius + x, radius - y] = true;
                outline[radius - x, radius - y] = true;
                outline[radius + y, radius + x] = true;
                outline[radius - y, radius + x] = true;
                outline[radius + y, radius - x] = true;
                outline[radius - y, radius - x] = true;
            }

            var affected = new HashSet<(int X, int Y)>();
            var fill = 0;
            for (var column = 0; column < radius * 2 + 1; column++)
            {
                for (var row = 0; row < radius * 2 + 1; row++)
                {
                    var include = false;
                    if (column == 0 || row == 0 || column == radius * 2 || row == radius * 2)
                    {
                        fill = outline[column, row] ? 1 : 0;
                    }
                    else if (outline[column, row])
                    {
                        fill += row <= radius ? 1 : -1;
                        include = fill <= 0;
                    }
                    if (fill >= 1)
                    {
                        include = true;
                    }
                    if (include)
                    {
                        affected.Add((column - radius, row - radius));
                    }
                }
            }
            return affected;
        }

    }
}
