using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    internal static class MiningBuriedItemCandidateBuilder
    {
        public static EventCandidate[] Build(SnapshotEnvelope snapshot)
        {
            if (MiningReachDepthCandidateBuilder
                    .MissingMiningGroups(snapshot).Length > 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentMine = ReadStateFieldValue(
                snapshot,
                "mining",
                "current_mine");
            if (!currentMine.HasValue ||
                ReadBool(currentMine.Value, "is_quarry_mine"))
            {
                return Array.Empty<EventCandidate>();
            }

            var floorStep = new MiningFloorStepPlanner().Plan(
                snapshot,
                new MiningFloorObjective
                {
                    Kind = MiningObjectiveKinds.AcquireBuriedItem,
                    TargetQualifiedItemIds = new[] { "(O)585" },
                    MinimumReserveHealth = 1,
                    LatestExitTime = 2400
                });
            if (floorStep.StepKind != MiningFloorStepKinds.DigBuriedItem ||
                floorStep.TargetQualifiedItemId != "(O)585")
            {
                return Array.Empty<EventCandidate>();
            }

            var executionOptionId =
                MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(
                    floorStep.Status,
                    "ready",
                    StringComparison.Ordinal) ||
                executionOptionId != "executor.till_soil")
            {
                return Array.Empty<EventCandidate>();
            }

            var sourceJson = MiningAuthoritativeRouteSourceBinding
                .ReadSelectedStepSources(snapshot, floorStep);
            if (sourceJson == "[]")
                return Array.Empty<EventCandidate>();

            var locationId = ReadString(currentMine.Value, "location_id");
            var executionParameters = MiningFloorStepCompiler
                .BuildExecutionParameters(floorStep);
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "mining:buried_item:(O)585:" +
                        floorStep.TargetTileX + "," +
                        floorStep.TargetTileY,
                    Kind = "mining_buried_item_plan_envelope",
                    Available = true,
                    LocationId = locationId,
                    TileX = floorStep.TargetTileX,
                    TileY = floorStep.TargetTileY,
                    QualifiedItemId = "(O)585",
                    Quantity = 1,
                    ExpectedEffect =
                        "native_mine_buried_item_attempt=true;" +
                        "target_qualified_item_id=(O)585;" +
                        "outcome_not_guaranteed=true;" +
                        "fresh_snapshot_replan_required=true;" +
                        "execution_option_id=executor.till_soil",
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    AvailabilityClass =
                        "available_native_stochastic_buried_item_attempt",
                    Parameters = new[]
                    {
                        Parameter(
                            "authoritative_route_sources_json",
                            sourceJson),
                        Parameter("target_location", locationId),
                        Parameter("acquisition_target_qualified_item_id", "(O)585"),
                        Parameter("stochastic_retry_receipt_required", "true"),
                        Parameter("fresh_snapshot_replan_required", "true"),
                        Parameter("required_executor_profile", "mining_perfect_executor")
                    }.Concat(executionParameters).ToArray()
                }
            };
        }

        private static SmallModelActionParameter Parameter(
            string name,
            string value) => new()
            {
                Name = name,
                Value = value
            };
    }
}
