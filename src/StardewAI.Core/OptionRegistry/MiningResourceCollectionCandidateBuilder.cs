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
            return BuildCore(
                snapshot,
                new[] { requiredQualifiedItemId },
                requiredQualifiedItemId,
                monsterDropsOnly: false);
        }

        public static EventCandidate[] BuildMonsterDrops(
            SnapshotEnvelope snapshot,
            string[] targetQualifiedItemIds,
            string candidateKey)
        {
            return BuildCore(
                snapshot,
                targetQualifiedItemIds,
                candidateKey,
                monsterDropsOnly: true);
        }

        private static EventCandidate[] BuildCore(
            SnapshotEnvelope snapshot,
            string[] targetQualifiedItemIds,
            string candidateKey,
            bool monsterDropsOnly)
        {
            var targets = targetQualifiedItemIds
                .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(itemId => itemId, StringComparer.Ordinal)
                .ToArray();
            if (targets.Length == 0 || string.IsNullOrWhiteSpace(candidateKey))
            {
                return Array.Empty<EventCandidate>();
            }

            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            if (!currentMine.HasValue || currentMine.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<EventCandidate>();
            }

            var blocks = new List<string>(MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot));
            var resourceObjective = new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectResourceOrArtifact,
                TargetQualifiedItemIds = targets,
                MinimumReserveHealth = 1,
                LatestExitTime = 2400
            };
            var monsterDropObjective = new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.CollectMonsterDrop,
                TargetQualifiedItemIds = targets,
                MinimumReserveHealth = 1,
                LatestExitTime = 2400
            };
            var planner = new MiningFloorStepPlanner();
            var resourceStep = planner.Plan(snapshot, resourceObjective);
            var monsterDropStep = planner.Plan(snapshot, monsterDropObjective);
            var resourceStepIsUnboundCombat =
                (resourceStep.StepKind == MiningFloorStepKinds.CombatMonster ||
                 resourceStep.StepKind == MiningFloorStepKinds.ShootMonster) &&
                !resourceStep.ExpectedDropQualifiedItemIds.Any(itemId =>
                    targets.Contains(itemId, StringComparer.OrdinalIgnoreCase));
            var floorStep = !monsterDropsOnly &&
                string.Equals(resourceStep.Status, "ready", StringComparison.Ordinal) &&
                (!resourceStepIsUnboundCombat ||
                 !string.Equals(monsterDropStep.Status, "ready", StringComparison.Ordinal))
                ? resourceStep
                : string.Equals(monsterDropStep.Status, "ready", StringComparison.Ordinal)
                    ? monsterDropStep
                    : monsterDropsOnly ? monsterDropStep : resourceStep;
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                blocks.Add(floorStep.Reason);
                if (!monsterDropsOnly &&
                    !string.Equals(
                        floorStep.Reason,
                        monsterDropStep.Reason,
                        StringComparison.Ordinal))
                {
                    blocks.Add(monsterDropStep.Reason);
                }
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                blocks.Add("quest_resource_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            var available = blocks.Count == 0;
            var isTargetReceipt = floorStep.StepKind == MiningFloorStepKinds.PickupDebris &&
                targets.Contains(
                    floorStep.TargetQualifiedItemId,
                    StringComparer.OrdinalIgnoreCase);
            var isStoneSource = floorStep.StepKind == MiningFloorStepKinds.MineStone &&
                floorStep.ExpectedDropQualifiedItemIds.Any(itemId =>
                    targets.Contains(itemId, StringComparer.OrdinalIgnoreCase));
            var isMonsterSource =
                (floorStep.StepKind == MiningFloorStepKinds.CombatMonster ||
                 floorStep.StepKind == MiningFloorStepKinds.ShootMonster) &&
                string.Equals(
                    floorStep.CombatTerminalState,
                    "defeat",
                    StringComparison.Ordinal) &&
                floorStep.ExpectedDropQualifiedItemIds.Any(itemId =>
                    targets.Contains(itemId, StringComparer.OrdinalIgnoreCase));
            var isSourceStep = isStoneSource || isMonsterSource;
            var executionParameters = MiningFloorStepCompiler.BuildExecutionParameters(floorStep);
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "mining:collect_quest_resource:" + candidateKey,
                    Kind = "mining_collect_quest_resource_plan_envelope",
                    Available = available,
                    LocationId = ReadString(currentMine.Value, "location_id"),
                    QualifiedItemId = floorStep.TargetQualifiedItemId,
                    Quantity = 1,
                    ExpectedEffect = "quest_target_qualified_item_ids=" +
                        JsonSerializer.Serialize(targets) +
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
                        Parameter(
                            "quest_required_item_id",
                            targets.Length == 1 ? targets[0] : string.Empty),
                        Parameter("quest_target_qualified_item_ids_json", JsonSerializer.Serialize(targets)),
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
