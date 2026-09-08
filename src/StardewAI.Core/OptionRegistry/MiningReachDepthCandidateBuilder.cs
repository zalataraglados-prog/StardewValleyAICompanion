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
    public static class MiningReachDepthCandidateBuilder
    {
        private static readonly string[] RequiredGroups =
        {
            "current_mine",
            "tiles",
            "objects",
            "resource_clumps",
            "monsters",
            "floor_objectives",
            "reward_chests",
            "player_resources"
        };

        public static EventCandidate[] Build(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters)
        {
            var missing = MissingMiningGroups(snapshot);
            if (missing.Length > 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var skillTrainingTarget =
                ReadParameter(parameters, "skill_training_target_id");
            var trainCombat = string.Equals(
                skillTrainingTarget,
                "combat",
                StringComparison.Ordinal);
            var targetDepth = ReadIntParameter(parameters, "target_depth") ??
                (trainCombat ? 120 : null);
            var targetFamily = ReadParameter(parameters, "target_location_family");
            var latestExitTime = ReadIntParameter(parameters, "latest_exit_time");
            var minReserveHealth = ReadIntParameter(parameters, "minimum_reserve_health");
            var minReserveEnergy = ReadIntParameter(parameters, "minimum_reserve_energy");
            var resourcePreservationPolicy =
                ReadParameter(parameters, "resource_preservation_policy");
            if (string.IsNullOrWhiteSpace(resourcePreservationPolicy))
            {
                resourcePreservationPolicy =
                    MiningResourcePreservationPolicies.PreserveStaircases;
            }
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
            var targetSkillLevel = ReadIntParameter(
                    parameters,
                    "target_skill_level") ??
                10;
            var combatSkill = trainCombat
                ? ReadSkill(snapshot, "combat")
                : null;
            blocks.AddRange(ValidateSkillTraining(
                snapshot,
                parameters,
                currentFamily));
            if (!MiningResourcePreservationPolicies.IsSupported(
                    resourcePreservationPolicy))
            {
                blocks.Add(
                    "unsupported_resource_preservation_policy:" +
                    resourcePreservationPolicy);
            }
            var elevatorStart = ElevatorStartFor(currentDepth, targetDepth, currentFamily, deepestMineLevel);
            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, new MiningFloorObjective
            {
                Kind = trainCombat
                    ? MiningObjectiveKinds.TrainCombat
                    : MiningObjectiveKinds.ReachDepth,
                MinimumReserveHealth = minReserveHealth ?? 0,
                MinimumReserveEnergy = minReserveEnergy,
                LatestExitTime = latestExitTime,
                TargetDepth = targetDepth,
                ResourcePreservationPolicy = resourcePreservationPolicy
            });
            if (trainCombat &&
                (floorStep.StepKind is MiningFloorStepKinds.CombatMonster or
                    MiningFloorStepKinds.ShootMonster) &&
                (floorStep.ExpectedSkillExperience is null or <= 0 ||
                    !string.Equals(
                        floorStep.SkillExperienceSkillId,
                        "combat",
                        StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(
                        floorStep.SkillExperienceCondition)))
            {
                blocks.Add(
                    "combat_training_target_experience_projection_incomplete");
            }
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
                    CandidateId = trainCombat
                        ? "mining:train_combat:" +
                            currentDepth + ":" + targetSkillLevel
                        : "mining:reach_depth:" +
                            (targetDepth?.ToString() ?? "missing"),
                    Kind = trainCombat
                        ? "mining_combat_training_plan_envelope"
                        : "mining_reach_depth_plan_envelope",
                    Available = available,
                    LocationId = ReadString(currentMine.Value, "location_id"),
                    ExpectedEffect = "current_depth=" + currentDepth +
                        ";target_depth=" +
                        (targetDepth?.ToString() ?? "missing") +
                        ";skill_training_target_id=" +
                        (trainCombat ? "combat" : "none") +
                        ";rolling_floor_step=" + floorStep.StepKind +
                        ";execution_option_id=" + executionOptionId,
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
                        Parameter(
                            "skill_training_target_id",
                            trainCombat ? "combat" : string.Empty),
                        Parameter(
                            "target_skill_level",
                            trainCombat
                                ? targetSkillLevel.ToString()
                                : string.Empty),
                        Parameter(
                            "current_skill_level",
                            combatSkill?.Level.ToString() ?? string.Empty),
                        Parameter(
                            "current_skill_experience",
                            combatSkill?.Experience.ToString() ??
                                string.Empty),
                        Parameter(
                            "experience_to_next_level",
                            combatSkill?.ExperienceToNextLevel
                                ?.ToString() ?? string.Empty),
                        Parameter("latest_exit_time", latestExitTime?.ToString() ?? string.Empty),
                        Parameter("minimum_reserve_health", minReserveHealth?.ToString() ?? string.Empty),
                        Parameter("minimum_reserve_energy", minReserveEnergy?.ToString() ?? string.Empty),
                        Parameter(
                            "resource_preservation_policy",
                            resourcePreservationPolicy),
                        Parameter("estimate_status", "rolling_horizon_current_floor_step"),
                        Parameter("required_executor_profile", "mining_perfect_executor"),
                        Parameter("runtime_boundary", available ? "current_floor_step_executable" : floorStep.Reason)
                    }.Concat(executionParameters).ToArray()
                }
            };
        }

        public static string[] ValidateSkillTraining(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] parameters,
            string currentFamily)
        {
            var skillTrainingTarget =
                ReadParameter(parameters, "skill_training_target_id");
            if (string.IsNullOrWhiteSpace(skillTrainingTarget))
            {
                return Array.Empty<string>();
            }
            if (!string.Equals(
                    skillTrainingTarget,
                    "combat",
                    StringComparison.Ordinal))
            {
                return new[]
                {
                    "unsupported_skill_training_target:" +
                    skillTrainingTarget
                };
            }

            var reasons = new List<string>();
            if (!string.Equals(
                    currentFamily,
                    "ordinary_mines",
                    StringComparison.Ordinal))
            {
                reasons.Add(
                    "combat_training_requires_ordinary_mines");
            }

            var combatSkill = ReadSkill(snapshot, "combat");
            var targetSkillLevel = ReadIntParameter(
                    parameters,
                    "target_skill_level") ??
                10;
            if (combatSkill is null)
            {
                reasons.Add(
                    "player_combat_skill_detail_unavailable");
            }
            else if (targetSkillLevel is < 1 or > 10)
            {
                reasons.Add(
                    "combat_training_target_level_out_of_range");
            }
            else if (combatSkill.Value.Level >= targetSkillLevel)
            {
                reasons.Add(
                    "combat_training_target_level_already_reached");
            }

            return reasons.ToArray();
        }

        private static (
            int Level,
            int Experience,
            int? ExperienceToNextLevel)? ReadSkill(
                SnapshotEnvelope snapshot,
                string skillId)
        {
            var details = ReadStateFieldValue(
                snapshot,
                "player",
                "skills_detail");
            if (!details.HasValue ||
                details.Value.ValueKind != JsonValueKind.Object ||
                !details.Value.TryGetProperty("skills", out var skills) ||
                skills.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var matches = skills.EnumerateArray()
                .Where(skill => string.Equals(
                    ReadString(skill, "skill_id"),
                    skillId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                return null;
            }

            return (
                ReadInt(matches[0], "unmodified_level"),
                ReadInt(matches[0], "experience"),
                ReadIntOptional(matches[0], "experience_to_next_level"));
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

            if (family == "quarry_mine")
            {
                blocks.Add("quarry_mine_uses_acquire_golden_scythe_objective");
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

    }
}
