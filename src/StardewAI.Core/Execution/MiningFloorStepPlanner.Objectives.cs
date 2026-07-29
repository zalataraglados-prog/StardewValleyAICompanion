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
        private const int ObjectiveApproachHorizonTiles = 4;

        private static MiningFloorStepPlan? SelectGoldenScytheAltarStep(
            JsonElement altars,
            SearchResult search,
            bool[,] grid)
        {
            var candidate = altars.EnumerateArray()
                .Select(altar => TargetCandidate(altar, search, grid, estimatedSwings: 0, deterministicLadder: false))
                .Where(row => row is not null)
                .OrderBy(row => row!.Distance)
                .ThenBy(row => row!.TargetY)
                .ThenBy(row => row!.TargetX)
                .FirstOrDefault();
            if (candidate is null)
            {
                return null;
            }

            if (candidate.Distance > 0)
            {
                var approach = BuildObjectiveApproachStep(
                    candidate,
                    MiningFloorStepKinds.MoveToGoldenScytheAltar,
                    "approach_golden_scythe_altar",
                    "golden_scythe_route_clear");
                approach.TargetQualifiedItemId = "(W)53";
                return approach;
            }

            var step = Build(
                MiningFloorStepKinds.ClaimGoldenScythe,
                "golden_scythe_altar_adjacent_and_safe",
                candidate);
            step.TargetQualifiedItemId = "(W)53";
            step.SafetyWindowStatus = "golden_scythe_interaction_window_clear";
            return step;
        }

        private static MiningFloorStepPlan? SelectSkullKeyChestStep(
            JsonElement rewardChests,
            SearchResult search,
            bool[,] grid)
        {
            var candidate = rewardChests.EnumerateArray()
                .Where(chest => ReadBool(chest, "contains_skull_key"))
                .Select(chest => TargetCandidate(chest, search, grid, estimatedSwings: 0, deterministicLadder: false))
                .Where(row => row is not null)
                .OrderBy(row => row!.Distance)
                .ThenBy(row => row!.TargetY)
                .ThenBy(row => row!.TargetX)
                .FirstOrDefault();
            if (candidate is null)
            {
                return null;
            }

            if (candidate.Distance > 0)
            {
                var approach = BuildObjectiveApproachStep(
                    candidate,
                    MiningFloorStepKinds.MoveToSkullKeyChest,
                    "approach_skull_key_reward_chest",
                    "skull_key_reward_route_clear");
                approach.TargetName = "SkullKeyChest";
                return approach;
            }

            var step = Build(MiningFloorStepKinds.ClaimSkullKey, "skull_key_reward_chest_adjacent", candidate);
            step.TargetName = "SkullKeyChest";
            step.SafetyWindowStatus = "skull_key_reward_interaction_window_clear";
            return step;
        }

        private static MiningFloorStepPlan BuildObjectiveApproachStep(
            Candidate candidate,
            string stepKind,
            string reason,
            string safetyWindowStatus)
        {
            var waypointIndex = Math.Min(
                ObjectiveApproachHorizonTiles,
                candidate.Path.Length - 1);
            var waypoint = candidate.Path[waypointIndex];
            return new MiningFloorStepPlan
            {
                Status = "ready",
                StepKind = stepKind,
                Reason = reason,
                TargetTileX = waypoint.X,
                TargetTileY = waypoint.Y,
                StandTileX = waypoint.X,
                StandTileY = waypoint.Y,
                EstimatedMovementTiles = waypointIndex,
                EstimatedToolSwings = 0,
                SafetyWindowStatus = safetyWindowStatus,
                Path = candidate.Path
                    .Take(waypointIndex + 1)
                    .ToArray()
            };
        }

        private static MiningFloorStepPlan BuildObjectiveApproachStep(
            MiningFloorStepPlan source,
            string stepKind,
            string reason,
            string safetyWindowStatus)
        {
            var waypointIndex = Math.Min(
                ObjectiveApproachHorizonTiles,
                source.Path.Length - 1);
            var waypoint = source.Path[waypointIndex];
            return new MiningFloorStepPlan
            {
                Status = "ready",
                StepKind = stepKind,
                Reason = reason,
                TargetTileX = waypoint.X,
                TargetTileY = waypoint.Y,
                StandTileX = waypoint.X,
                StandTileY = waypoint.Y,
                EstimatedMovementTiles = waypointIndex,
                EstimatedToolSwings = 0,
                SafetyWindowStatus = safetyWindowStatus,
                Path = source.Path
                    .Take(waypointIndex + 1)
                    .ToArray()
            };
        }

        private static MiningFloorStepPlan? SelectMineRewardChest(JsonElement rewardChests, SearchResult search, bool[,] grid)
        {
            return rewardChests.EnumerateArray()
                .Where(chest => string.Equals(ReadString(chest, "status"), "ready", StringComparison.Ordinal) && !ReadBool(chest, "contains_skull_key"))
                .Select(chest => new
                {
                    Chest = chest,
                    Candidate = TargetCandidate(chest, search, grid, estimatedSwings: 0, deterministicLadder: false)
                })
                .Where(row => row.Candidate is not null)
                .OrderBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.Candidate!.TargetY)
                .ThenBy(row => row.Candidate!.TargetX)
                .Select(row =>
                {
                    var item = row.Chest.TryGetProperty("item", out var itemValue) ? itemValue : default;
                    var step = Build(MiningFloorStepKinds.ClaimRewardChest, "mandatory_loaded_floor_reward_before_descent", row.Candidate!);
                    step.TargetRuntimeType = ReadString(row.Chest, "runtime_type");
                    step.TargetQualifiedItemId = ReadString(item, "qualified_item_id");
                    step.TargetQuantity = ReadInt(item, "quantity");
                    step.TargetQuality = ReadInt(item, "quality");
                    step.RewardBranch = ReadString(row.Chest, "reward_branch");
                    step.ExpectedOutputItemsJson = ReadString(row.Chest, "expected_output_items_json");
                    step.NativeGainExperienceCallAmount = ReadInt(row.Chest, "native_gain_experience_call_amount");
                    step.ExpectedStardropMaxStaminaDelta = ReadInt(row.Chest, "expected_stardrop_max_stamina_delta");
                    step.SafetyWindowStatus = "reward_chest_route_and_immediate_threat_clear";
                    return step;
                })
                .FirstOrDefault();
        }

        private static int ReadInventoryEmptySlots(JsonElement resources)
        {
            if (!resources.TryGetProperty("inventory_capacity", out var capacity) ||
                capacity.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            return ReadInt(capacity, "empty_slots") ?? 0;
        }

        private static MiningFloorStepPlan? SelectShaft(
            JsonElement tiles,
            SearchResult search,
            bool[,] grid,
            JsonElement resources,
            bool hasResources,
            int minimumReserveHealth,
            string currentMineKind,
            bool requireReserve = true)
        {
            if (!string.Equals(currentMineKind, "skull_cavern", StringComparison.Ordinal) ||
                !tiles.TryGetProperty("shafts", out var shafts) ||
                shafts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return shafts.EnumerateArray()
                .Select(shaft => new { Shaft = shaft, Candidate = TargetCandidate(shaft, search, grid, estimatedSwings: 0, deterministicLadder: false) })
                .Where(row => row.Candidate is not null)
                .Where(row => !requireReserve || (hasResources && (ReadInt(row.Shaft, "expected_health_after") ?? 0) >= Math.Max(1, minimumReserveHealth)))
                .OrderBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.Candidate!.TargetY)
                .ThenBy(row => row.Candidate!.TargetX)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.DescendShaft, "reachable_safe_shaft_available", row.Candidate!);
                    plan.ExpectedMineLevelDelta = ReadInt(row.Shaft, "expected_level_delta");
                    plan.ExpectedMineLevelAfter = ReadInt(row.Shaft, "expected_mine_level_after");
                    plan.ExpectedHealthCost = ReadInt(row.Shaft, "expected_health_cost");
                    plan.ExpectedHealthAfter = ReadInt(row.Shaft, "expected_health_after");
                    plan.SafetyWindowStatus = requireReserve ? "shaft_health_reserve_verified" : "shaft_requires_recovery";
                    return plan;
                })
                .FirstOrDefault();
        }

        private static MiningFloorStepPlan? SelectMineExit(JsonElement tiles, SearchResult search, bool[,] grid, string reason)
        {
            if (!tiles.TryGetProperty("exits", out var exits) || exits.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return exits.EnumerateArray()
                .Select(exit => new { Exit = exit, Candidate = MineExitCandidate(exit, search, grid) })
                .Where(row => row.Candidate is not null)
                .OrderBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.Candidate!.TargetY)
                .ThenBy(row => row.Candidate!.TargetX)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.ExitMine, reason, row.Candidate!);
                    if (row.Exit.TryGetProperty("expected_destination", out var destination) && destination.ValueKind == JsonValueKind.Object)
                    {
                        plan.ExpectedTargetLocation = ReadString(destination, "location_id");
                        plan.ExpectedArrivalTileX = ReadInt(destination, "tile_x");
                        plan.ExpectedArrivalTileY = ReadInt(destination, "tile_y");
                    }
                    plan.SafetyWindowStatus = "mandatory_retreat_native_exit";
                    return plan;
                })
                .FirstOrDefault();
        }

        private static Candidate? MineExitCandidate(JsonElement element, SearchResult search, bool[,] grid)
        {
            var targetX = ReadInt(element, "tile_x");
            var targetY = ReadInt(element, "tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return null;
            }

            var adjacent = TargetCandidate(targetX.Value, targetY.Value, search, grid, estimatedSwings: 0, deterministicLadder: false);
            if (adjacent is not null)
            {
                return adjacent;
            }

            Candidate? best = null;
            for (var offsetX = -2; offsetX <= 2; offsetX++)
            {
                for (var offsetY = -2; offsetY <= 2; offsetY++)
                {
                    if (Math.Abs(offsetX) + Math.Abs(offsetY) != 2)
                    {
                        continue;
                    }
                    var standX = targetX.Value + offsetX;
                    var standY = targetY.Value + offsetY;
                    if (!InBounds(grid, standX, standY) || grid[standX, standY] ||
                        !search.Distance.TryGetValue(Key(standX, standY), out var distance))
                    {
                        continue;
                    }

                    var candidate = new Candidate(targetX.Value, targetY.Value, standX, standY, distance, 0, false, search.PathTo(standX, standY));
                    if (best is null || candidate.Distance < best.Distance ||
                        candidate.Distance == best.Distance && (candidate.StandY < best.StandY || candidate.StandY == best.StandY && candidate.StandX < best.StandX))
                    {
                        best = candidate;
                    }
                }
            }
            return best;
        }

        private static string MandatoryRetreatReason(JsonElement resources, MiningFloorObjective objective, int? currentDepth)
        {
            var reasons = new List<string>();
            if (objective.TargetDepth.HasValue && currentDepth.HasValue && currentDepth.Value >= objective.TargetDepth.Value)
            {
                reasons.Add("target_depth_reached");
            }
            if (objective.LatestExitTime.HasValue && (ReadInt(resources, "current_time") ?? 0) >= objective.LatestExitTime.Value)
            {
                reasons.Add("latest_exit_time_reached");
            }
            if (objective.MinimumReserveEnergy.HasValue && (ReadDouble(resources, "energy") ?? 0) <= objective.MinimumReserveEnergy.Value)
            {
                reasons.Add("minimum_reserve_energy_reached");
            }
            return reasons.Count == 0 ? string.Empty : "retreat_required:" + string.Join(",", reasons);
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

    }
}
