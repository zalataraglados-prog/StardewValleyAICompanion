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
            return new DurationEstimate
            {
                Minutes = 0,
                Estimator = "mining_perfect_executor.unimplemented",
                Notes = new[]
                {
                    "mining_perfect_executor_not_implemented",
                    "duration_and_energy_unknown_until_decompile_backed_executor_model_exists",
                    "no arbitrary timing, ladder probability, or energy constants applied"
                }
            };
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
