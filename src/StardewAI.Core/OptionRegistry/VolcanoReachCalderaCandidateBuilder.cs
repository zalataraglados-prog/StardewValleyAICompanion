using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public static class VolcanoReachCalderaCandidateBuilder
    {
        private static readonly string[] RequiredGroups =
        {
            "current_level",
            "tiles",
            "connectors",
            "gates",
            "objects",
            "monsters",
            "player_resources"
        };

        public static EventCandidate[] Build(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters)
        {
            var missing = MissingVolcanoGroups(snapshot);
            if (missing.Length > 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentLevel = ReadStateFieldValue(snapshot, "volcano", "current_level");
            if (!currentLevel.HasValue)
            {
                return Array.Empty<EventCandidate>();
            }

            var floorStep = new VolcanoFloorStepPlanner().Plan(snapshot);
            var executionOptionId = VolcanoFloorStepCompiler.ExecutionOptionId(floorStep);
            var blocks = new List<string>();
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                blocks.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                blocks.Add("volcano_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            var available = blocks.Count == 0;
            var level = ReadInt(currentLevel.Value, "level");
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "volcano:reach_caldera",
                    Kind = "volcano_reach_caldera_plan_envelope",
                    Available = available,
                    LocationId = ReadString(currentLevel.Value, "location_id"),
                    TileX = floorStep.TargetTileX,
                    TileY = floorStep.TargetTileY,
                    ExpectedEffect = "current_level=" + level + ";target_location=Caldera;rolling_floor_step=" + floorStep.StepKind + ";execution_option_id=" + executionOptionId,
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    AvailabilityClass = available ? "available_rolling_horizon_floor_step" : "blocked_current_floor_step",
                    BlockReasons = blocks.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters.Concat(new[]
                    {
                        Parameter("current_volcano_level", level.ToString()),
                        Parameter("target_volcano_level", "9"),
                        Parameter("target_location", "Caldera"),
                        Parameter("estimate_status", "rolling_horizon_current_floor_step"),
                        Parameter("required_executor_profile", "volcano_perfect_executor"),
                        Parameter("runtime_boundary", available ? "current_floor_step_executable" : floorStep.Reason)
                    }).Concat(VolcanoFloorStepCompiler.BuildExecutionParameters(floorStep)).ToArray()
                }
            };
        }

        public static string[] MissingVolcanoGroups(SnapshotEnvelope snapshot)
        {
            var missing = RequiredGroups
                .Where(group => !ReadableStatus(ReadStateFieldStatus(snapshot, "volcano", group)))
                .Select(group => "volcano." + group)
                .ToList();

            var completeness = ReadStateFieldValue(snapshot, "volcano", "completeness");
            if (!completeness.HasValue ||
                !string.Equals(ReadString(completeness.Value, "status"), "complete", StringComparison.Ordinal))
            {
                missing.Add("volcano.completeness");
            }

            return missing.Distinct(StringComparer.Ordinal).ToArray();
        }

    }
}
