using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.OptionRegistry
{
    public static class MiningReachDepthCandidateBuilder
    {
        private static readonly string[] RequiredGroups =
        {
            "current_mine",
            "tiles",
            "objects",
            "monsters",
            "floor_objectives",
            "player_resources"
        };

        public static EventCandidate[] Build(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters)
        {
            var missing = MissingMiningGroups(snapshot);
            if (missing.Length > 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var targetDepth = ReadIntParameter(parameters, "target_depth");
            var targetFamily = ReadParameter(parameters, "target_location_family");
            var latestExitTime = ReadIntParameter(parameters, "latest_exit_time");
            var minReserveHealth = ReadIntParameter(parameters, "minimum_reserve_health");
            var minReserveEnergy = ReadIntParameter(parameters, "minimum_reserve_energy");
            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            var resources = ReadStateFieldValue(snapshot, "mining", "player_resources");
            if (!currentMine.HasValue || !resources.HasValue)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentDepth = ReadInt(currentMine.Value, "mine_level");
            var currentFamily = ReadString(currentMine.Value, "mine_kind");
            var deepestMineLevel = ReadIntOptional(resources.Value, "deepest_mine_level");
            var blocks = ValidateTarget(currentDepth, currentFamily, targetDepth, targetFamily).ToList();
            var elevatorStart = ElevatorStartFor(currentDepth, targetDepth, currentFamily, deepestMineLevel);
            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.ReachDepth,
                MinimumReserveHealth = minReserveHealth ?? 0,
                MinimumReserveEnergy = minReserveEnergy,
                LatestExitTime = latestExitTime,
                TargetDepth = targetDepth
            });
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                blocks.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                blocks.Add(floorStep.StepKind == MiningFloorStepKinds.DescendLadder
                    ? "mining_descend_ladder_executor_not_implemented"
                    : "mining_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            var available = blocks.Count == 0;
            var executionParameters = MiningFloorStepCompiler.BuildExecutionParameters(floorStep);

            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "mining:reach_depth:" + (targetDepth?.ToString() ?? "missing"),
                    Kind = "mining_reach_depth_plan_envelope",
                    Available = available,
                    LocationId = ReadString(currentMine.Value, "location_id"),
                    ExpectedEffect = "current_depth=" + currentDepth + ";target_depth=" + (targetDepth?.ToString() ?? "missing") + ";rolling_floor_step=" + floorStep.StepKind + ";execution_option_id=" + executionOptionId,
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    AvailabilityClass = available ? "available_rolling_horizon_floor_step" : "blocked_current_floor_step",
                    BlockReasons = blocks.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = new[]
                    {
                        Parameter("current_depth", currentDepth.ToString()),
                        Parameter("elevator_start_depth", elevatorStart?.ToString() ?? string.Empty),
                        Parameter("target_depth", targetDepth?.ToString() ?? string.Empty),
                        Parameter("target_location_family", string.IsNullOrWhiteSpace(targetFamily) ? currentFamily : targetFamily),
                        Parameter("latest_exit_time", latestExitTime?.ToString() ?? string.Empty),
                        Parameter("minimum_reserve_health", minReserveHealth?.ToString() ?? string.Empty),
                        Parameter("minimum_reserve_energy", minReserveEnergy?.ToString() ?? string.Empty),
                        Parameter("estimate_status", "rolling_horizon_current_floor_step"),
                        Parameter("required_executor_profile", "mining_perfect_executor"),
                        Parameter("runtime_boundary", available ? "current_floor_step_executable" : floorStep.Reason)
                    }.Concat(executionParameters).ToArray()
                }
            };
        }

        public static string[] MissingMiningGroups(SnapshotEnvelope snapshot)
        {
            var missing = RequiredGroups
                .Where(group => !ReadableStatus(ReadStateFieldStatus(snapshot, "mining", group)))
                .Select(group => "mining." + group)
                .ToList();

            var completeness = ReadStateFieldValue(snapshot, "mining", "completeness");
            if (!completeness.HasValue || !string.Equals(ReadString(completeness.Value, "status"), "complete", StringComparison.Ordinal))
            {
                missing.Add("mining.completeness");
            }

            foreach (var group in RequiredGroups)
            {
                var value = ReadStateFieldValue(snapshot, "mining", group);
                if (value.HasValue)
                {
                    missing.AddRange(UnreadableNestedStatuses(value.Value, "mining." + group));
                }
            }

            return missing.Distinct(StringComparer.Ordinal).ToArray();
        }

        public static string[] ValidateTarget(int currentDepth, string currentFamily, int? targetDepth, string? targetFamily)
        {
            var blocks = new List<string>();
            if (!targetDepth.HasValue)
            {
                blocks.Add("target_depth_required");
                return blocks.ToArray();
            }

            var family = string.IsNullOrWhiteSpace(targetFamily) ? currentFamily : targetFamily!;
            if (family == "ordinary_mines" && (targetDepth.Value < 1 || targetDepth.Value > 120))
            {
                blocks.Add("ordinary_mine_target_depth_out_of_range");
            }

            if (family == "skull_cavern" && targetDepth.Value <= 120)
            {
                blocks.Add("skull_cavern_target_depth_must_exceed_120");
            }

            if (family == "quarry_mine" && targetDepth.Value != 77377)
            {
                blocks.Add("quarry_mine_target_depth_must_be_77377");
            }

            if (!string.Equals(family, currentFamily, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(currentFamily))
            {
                blocks.Add("target_location_family_mismatch_current_mine");
            }

            return blocks.ToArray();
        }

        public static int? ElevatorStartFor(int currentDepth, int? targetDepth, string currentFamily, int? deepestMineLevel)
        {
            if (currentFamily != "ordinary_mines" || !targetDepth.HasValue)
            {
                return null;
            }

            if (!deepestMineLevel.HasValue)
            {
                return null;
            }

            var deepestElevatorFloor = Math.Min(120, deepestMineLevel.Value) / 5 * 5;
            var targetCheckpoint = targetDepth.Value / 5 * 5;
            var unlockedCheckpoint = Math.Max(0, Math.Min(deepestElevatorFloor, targetCheckpoint));
            return Math.Max(currentDepth, unlockedCheckpoint);
        }

        private static IEnumerable<string> UnreadableNestedStatuses(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("status", out var status) &&
                    status.ValueKind == JsonValueKind.String &&
                    ExplicitlyUnreadableNestedStatus(status.GetString()))
                {
                    yield return path;
                }

                foreach (var property in element.EnumerateObject())
                {
                    foreach (var nested in UnreadableNestedStatuses(property.Value, path + "." + property.Name))
                    {
                        yield return nested;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in UnreadableNestedStatuses(item, path + "[" + index + "]"))
                    {
                        yield return nested;
                    }

                    index++;
                }
            }
        }

        private static bool ExplicitlyUnreadableNestedStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return status == "unavailable" ||
                status == "missing" ||
                status == "error" ||
                status == "failed" ||
                status == "unreadable" ||
                status.StartsWith("unavailable_", StringComparison.Ordinal) ||
                status.StartsWith("missing_", StringComparison.Ordinal) ||
                status.StartsWith("error_", StringComparison.Ordinal) ||
                status.StartsWith("failed_", StringComparison.Ordinal) ||
                status.StartsWith("unreadable_", StringComparison.Ordinal);
        }

        private static bool ReadableStatus(string? status)
        {
            return status == "available" || status == "derived";
        }

        private static JsonElement? ReadStateFieldValue(SnapshotEnvelope snapshot, string section, string field)
        {
            if (!snapshot.State.TryGetValue(section, out var sectionElement) ||
                sectionElement.ValueKind != JsonValueKind.Object ||
                !sectionElement.TryGetProperty(field, out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object ||
                !envelope.TryGetProperty("value", out var value))
            {
                return null;
            }

            return value;
        }

        private static string ReadStateFieldStatus(SnapshotEnvelope snapshot, string section, string field)
        {
            if (!snapshot.State.TryGetValue(section, out var sectionElement) ||
                sectionElement.ValueKind != JsonValueKind.Object ||
                !sectionElement.TryGetProperty(field, out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object ||
                !envelope.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return status.GetString() ?? string.Empty;
        }

        private static string? ReadParameter(SmallModelActionParameter[] parameters, string name)
        {
            return parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private static int? ReadIntParameter(SmallModelActionParameter[] parameters, string name)
        {
            return int.TryParse(ReadParameter(parameters, name), out var value) ? value : null;
        }

        private static int ReadInt(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
        }

        private static int? ReadIntOptional(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;
        }

        private static string ReadString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }
    }
}
