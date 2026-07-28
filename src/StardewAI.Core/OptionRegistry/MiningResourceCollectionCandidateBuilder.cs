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
    public static class MiningResourceCollectionCandidateBuilder
    {
        public static EventCandidate[] Build(SnapshotEnvelope snapshot, string requiredQualifiedItemId)
        {
            if (string.IsNullOrWhiteSpace(requiredQualifiedItemId))
            {
                return Array.Empty<EventCandidate>();
            }

            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            if (!currentMine.HasValue || currentMine.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<EventCandidate>();
            }

            var blocks = new List<string>(MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot));
            var objective = new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectResourceOrArtifact,
                TargetQualifiedItemIds = new[] { requiredQualifiedItemId },
                MinimumReserveHealth = 1,
                LatestExitTime = 2400
            };
            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, objective);
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                blocks.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                blocks.Add("quest_resource_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            var available = blocks.Count == 0;
            var isTargetReceipt = floorStep.StepKind == MiningFloorStepKinds.PickupDebris &&
                string.Equals(
                    floorStep.TargetQualifiedItemId,
                    requiredQualifiedItemId,
                    StringComparison.OrdinalIgnoreCase);
            var isSourceStep = floorStep.StepKind == MiningFloorStepKinds.MineStone &&
                floorStep.ExpectedDropQualifiedItemIds.Contains(
                    requiredQualifiedItemId,
                    StringComparer.OrdinalIgnoreCase);
            var executionParameters = MiningFloorStepCompiler.BuildExecutionParameters(floorStep);
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "mining:collect_quest_resource:" + requiredQualifiedItemId,
                    Kind = "mining_collect_quest_resource_plan_envelope",
                    Available = available,
                    LocationId = ReadString(currentMine.Value, "location_id"),
                    QualifiedItemId = requiredQualifiedItemId,
                    Quantity = 1,
                    ExpectedEffect = "quest_required_item_id=" + requiredQualifiedItemId +
                        ";rolling_floor_step=" + floorStep.StepKind +
                        ";execution_option_id=" + executionOptionId +
                        ";fresh_snapshot_replan_required=true",
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    AvailabilityClass = available
                        ? "available_rolling_horizon_quest_resource_step"
                        : "blocked_current_quest_resource_step",
                    BlockReasons = blocks
                        .Where(reason => !string.IsNullOrWhiteSpace(reason))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    Parameters = new[]
                    {
                        Parameter("quest_required_item_id", requiredQualifiedItemId),
                        Parameter("quest_acquisition_target_step", isTargetReceipt.ToString().ToLowerInvariant()),
                        Parameter("quest_acquisition_source_step", isSourceStep.ToString().ToLowerInvariant()),
                        Parameter("latest_exit_time", "2400"),
                        Parameter("minimum_reserve_health", "1"),
                        Parameter("estimate_status", "rolling_horizon_current_floor_step"),
                        Parameter("required_executor_profile", "mining_perfect_executor"),
                        Parameter("runtime_boundary", available ? "current_floor_step_executable" : floorStep.Reason)
                    }.Concat(executionParameters).ToArray()
                }
            };
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }
    }
}
