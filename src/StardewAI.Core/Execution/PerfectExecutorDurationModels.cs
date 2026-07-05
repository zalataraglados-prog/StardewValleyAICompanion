using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Execution
{
    public sealed class DurationEstimate
    {
        public int Minutes { get; set; }

        public string Estimator { get; set; } = string.Empty;

        public string[] Notes { get; set; } = Array.Empty<string>();
    }

    public sealed class MiningPerfectExecutorModel
    {
        public DurationEstimate Estimate(ActionQueueItem item)
        {
            var targetDepth = ParseInt(Parameter(item, "target_depth"));
            var startingDepth = ParseInt(Parameter(item, "start_depth")) ?? ElevatorStartFor(targetDepth);
            var levels = targetDepth.HasValue
                ? Math.Max(1, targetDepth.Value - startingDepth)
                : 15;
            var elevatorAdjustedLevels = Math.Max(1, Math.Min(levels, 5 + levels % 5));
            var minutes = 45 + elevatorAdjustedLevels * 8;

            return new DurationEstimate
            {
                Minutes = minutes,
                Estimator = "mining_perfect_executor.v1",
                Notes = new[]
                {
                    "execution_profile_assumes_perfect_human_player_inputs",
                    "random_mine_layout_affects_calibration_not_low_level_failure_penalty",
                    "uses_elevator_adjusted_level_delta_when_target_depth_is_known",
                    "decompile_evidence:MineShaft.mineLevel, MineShaft.mineRandom, MineShaft.findLadder"
                }
            };
        }

        private static int ElevatorStartFor(int? targetDepth)
        {
            if (!targetDepth.HasValue || targetDepth.Value <= 5)
            {
                return 0;
            }

            return Math.Max(0, ((targetDepth.Value - 1) / 5) * 5);
        }

        private static string? Parameter(ActionQueueItem item, string name)
        {
            return item.NormalizedCommand.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
                ?.Value;
        }

        private static int? ParseInt(string? value)
        {
            return int.TryParse(value, out var parsed) ? parsed : (int?)null;
        }
    }

    public sealed class FishingPerfectExecutorModel
    {
        public DurationEstimate Estimate(ActionQueueItem item)
        {
            var catches = Math.Max(1, ParseInt(Parameter(item, "target_catches")) ?? 1);
            var minutes = 15 + catches * 12;
            return new DurationEstimate
            {
                Minutes = minutes,
                Estimator = "fishing_perfect_executor.v1",
                Notes = new[]
                {
                    "execution_profile_assumes_perfect_human_player_inputs",
                    "bite_time_and_fish_difficulty_affect_calibration_not_low_level_failure_penalty",
                    "decompile_evidence:FishingRod.minFishingBiteTime, FishingRod.maxFishingBiteTime, FishingGame"
                }
            };
        }

        private static string? Parameter(ActionQueueItem item, string name)
        {
            return item.NormalizedCommand.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
                ?.Value;
        }

        private static int? ParseInt(string? value)
        {
            return int.TryParse(value, out var parsed) ? parsed : (int?)null;
        }
    }

    public sealed class NavigationPerfectExecutorModel
    {
        public DurationEstimate Estimate(ActionQueueItem item)
        {
            var routeTiles = Math.Max(1, ParseInt(Parameter(item, "route_tiles")) ?? 60);
            var minutes = 10 + (int)Math.Ceiling(routeTiles / 18.0);
            return new DurationEstimate
            {
                Minutes = minutes,
                Estimator = "navigation_perfect_executor.v1",
                Notes = new[]
                {
                    "execution_profile_assumes_perfect_human_player_inputs",
                    "passability_or_warp_failure_is_hard_feasibility_not_preference_penalty",
                    "decompile_evidence:PathFindController, GameLocation.isCollidingPosition, GameLocation.warps"
                }
            };
        }

        private static string? Parameter(ActionQueueItem item, string name)
        {
            return item.NormalizedCommand.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
                ?.Value;
        }

        private static int? ParseInt(string? value)
        {
            return int.TryParse(value, out var parsed) ? parsed : (int?)null;
        }
    }
}
