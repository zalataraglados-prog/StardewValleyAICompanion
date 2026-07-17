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
    public static class MiningSkullKeyCandidateBuilder
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
                blocks.Add("skull_key_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            var available = blocks.Count == 0;
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "mining:obtain_skull_key",
                    Kind = "mining_obtain_skull_key_plan_envelope",
                    Available = available,
                    LocationId = ReadString(currentMine.Value, "location_id"),
                    ExpectedEffect = "player.has_skull_key=true;target_depth=120;rolling_floor_step=" + floorStep.StepKind + ";execution_option_id=" + executionOptionId,
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    AvailabilityClass = available ? "available_rolling_horizon_floor_step" : "blocked_current_floor_step",
                    BlockReasons = blocks.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters.Concat(new[]
                    {
                        Parameter("target_location_family", "ordinary_mines"),
                        Parameter("target_depth", "120"),
                        Parameter("required_terminal_interaction", "skull_key_reward_chest"),
                        Parameter("required_postcondition", "player.has_skull_key=true"),
                        Parameter("estimate_status", "rolling_horizon_current_floor_step"),
                        Parameter("required_executor_profile", "mining_perfect_executor"),
                        Parameter("runtime_boundary", available ? "current_floor_step_executable" : floorStep.Reason)
                    }).Concat(MiningFloorStepCompiler.BuildExecutionParameters(floorStep)).ToArray()
                }
            };
        }

        public static string[] ValidateCurrentMine(JsonElement currentMine)
        {
            var level = ReadInt(currentMine, "mine_level");
            return level >= 1 && level <= 120 &&
                string.Equals(ReadString(currentMine, "mine_kind"), "ordinary_mines", StringComparison.Ordinal)
                ? Array.Empty<string>()
                : new[] { "skull_key_requires_ordinary_mines_1_120" };
        }

        public static MiningFloorObjective Objective(SmallModelActionParameter[] parameters)
        {
            return new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.AcquireSkullKey,
                MinimumReserveHealth = ReadIntParameter(parameters, "minimum_reserve_health") ?? 0,
                MinimumReserveEnergy = ReadIntParameter(parameters, "minimum_reserve_energy"),
                LatestExitTime = ReadIntParameter(parameters, "latest_exit_time")
            };
        }

    }
}
