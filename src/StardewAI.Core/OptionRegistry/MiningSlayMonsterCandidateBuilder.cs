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
    public static class MiningSlayMonsterCandidateBuilder
    {
        public static EventCandidate[] Build(
            SnapshotEnvelope snapshot,
            string[] targetMonsterNameFragments,
            string targetLocationFamily,
            bool matchAnySlimeName,
            bool ignoreFarmMonsters)
        {
            var blocks = new List<string>(MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot));
            var targets = targetMonsterNameFragments
                .Where(target => !string.IsNullOrWhiteSpace(target))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (targets.Length == 0)
            {
                blocks.Add("quest_target_monster_names_missing");
            }
            if (targetLocationFamily is not ("ordinary_mines" or "skull_cavern"))
            {
                blocks.Add("quest_target_mine_family_unresolved");
            }

            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            if (!currentMine.HasValue || currentMine.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentFamily = ReadString(currentMine.Value, "mine_kind");
            if (!string.Equals(currentFamily, targetLocationFamily, StringComparison.Ordinal))
            {
                blocks.Add("quest_target_location_family_mismatch_current_mine");
            }

            var objective = new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.SlayNamedMonster,
                TargetMonsterNameFragments = targets,
                MatchAnySlimeName = matchAnySlimeName,
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
                blocks.Add("quest_slay_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            var available = blocks.Count == 0;
            var targetJson = JsonSerializer.Serialize(targets);
            var executionParameters = MiningFloorStepCompiler.BuildExecutionParameters(floorStep);
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "mining:slay_named_monster:" + string.Join("+", targets),
                    Kind = "mining_slay_monsters_plan_envelope",
                    Available = available,
                    LocationId = ReadString(currentMine.Value, "location_id"),
                    ExpectedEffect = "quest_target_monster_names=" + targetJson +
                        ";rolling_floor_step=" + floorStep.StepKind +
                        ";execution_option_id=" + executionOptionId +
                        ";fresh_snapshot_replan_required=true",
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    AvailabilityClass = available
                        ? "available_rolling_horizon_quest_slay_step"
                        : "blocked_current_quest_slay_step",
                    BlockReasons = blocks
                        .Where(reason => !string.IsNullOrWhiteSpace(reason))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    Parameters = new[]
                    {
                        Parameter("quest_target_monster_names_json", targetJson),
                        Parameter("quest_target_location_family", targetLocationFamily),
                        Parameter("quest_match_any_slime_name", matchAnySlimeName.ToString().ToLowerInvariant()),
                        Parameter("quest_ignore_farm_monsters", ignoreFarmMonsters.ToString().ToLowerInvariant()),
                        Parameter(
                            "quest_slay_target_step",
                            (floorStep.SourceMatchStatus is "native_monster_name_contains" or "native_quest15_slime_name_match")
                                .ToString()
                                .ToLowerInvariant()),
                        Parameter("latest_exit_time", "2400"),
                        Parameter("minimum_reserve_health", "1"),
                        Parameter("estimate_status", "rolling_horizon_current_floor_step"),
                        Parameter("required_executor_profile", "mining_perfect_executor"),
                        Parameter("runtime_boundary", available ? "current_floor_step_executable" : floorStep.Reason)
                    }.Concat(executionParameters).ToArray()
                }
            };
        }

        public static string ResolveSpecialOrderLocationFamily(string questKey)
        {
            return questKey switch
            {
                "Clint" or "Wizard2" => "ordinary_mines",
                "DesertFestivalMarlon1" => "skull_cavern",
                _ => string.Empty
            };
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }
    }
}
