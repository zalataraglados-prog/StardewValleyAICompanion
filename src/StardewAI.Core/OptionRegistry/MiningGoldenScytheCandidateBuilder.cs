using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public static class MiningGoldenScytheCandidateBuilder
    {
        public static EventCandidate[] Build(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters)
        {
            var missing = MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot);
            if (missing.Length > 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            if (!currentMine.HasValue)
            {
                return Array.Empty<EventCandidate>();
            }

            var blocks = ValidateCurrentMine(currentMine.Value).ToList();
            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, Objective(parameters));
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                blocks.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                blocks.Add("golden_scythe_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            var available = blocks.Count == 0;
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "mining:acquire_golden_scythe",
                    Kind = "mining_acquire_golden_scythe_plan_envelope",
                    Available = available,
                    LocationId = ReadString(currentMine.Value, "location_id"),
                    ExpectedEffect = "golden_scythe=(W)53;rolling_floor_step=" + floorStep.StepKind + ";execution_option_id=" + executionOptionId,
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    AvailabilityClass = available ? "available_rolling_horizon_floor_step" : "blocked_current_floor_step",
                    BlockReasons = blocks.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters.Concat(new[]
                    {
                        Parameter("target_location_family", "quarry_mine"),
                        Parameter("target_mine_level", "77377"),
                        Parameter("target_qualified_item_id", "(W)53"),
                        Parameter("estimate_status", "rolling_horizon_current_floor_step"),
                        Parameter("required_executor_profile", "mining_perfect_executor"),
                        Parameter("runtime_boundary", available ? "current_floor_step_executable" : floorStep.Reason)
                    }).Concat(MiningFloorStepCompiler.BuildExecutionParameters(floorStep)).ToArray()
                }
            };
        }

        public static string[] ValidateCurrentMine(JsonElement currentMine)
        {
            return ReadInt(currentMine, "mine_level") == 77377 &&
                string.Equals(ReadString(currentMine, "mine_kind"), "quarry_mine", StringComparison.Ordinal)
                ? Array.Empty<string>()
                : new[] { "golden_scythe_requires_quarry_mine_77377" };
        }

        public static MiningFloorObjective Objective(SmallModelActionParameter[] parameters)
        {
            return new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.AcquireGoldenScythe,
                MinimumReserveHealth = ReadIntParameter(parameters, "minimum_reserve_health") ?? 0,
                MinimumReserveEnergy = ReadIntParameter(parameters, "minimum_reserve_energy"),
                LatestExitTime = ReadIntParameter(parameters, "latest_exit_time")
            };
        }

    }
}
