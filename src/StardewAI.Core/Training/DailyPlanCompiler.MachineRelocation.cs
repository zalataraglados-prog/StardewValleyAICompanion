using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep>
            RelocateMachineItemSteps(
                PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue ||
                !candidate.TileY.HasValue ||
                string.IsNullOrWhiteSpace(candidate.LocationId) ||
                string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var standX = CandidateParameter(
                candidate,
                "stand_tile_x");
            var standY = CandidateParameter(
                candidate,
                "stand_tile_y");
            if (!int.TryParse(standX, out var parsedStandX) ||
                !int.TryParse(standY, out var parsedStandY))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("location_id", candidate.LocationId),
                Parameter(
                    "qualified_item_id",
                    candidate.QualifiedItemId),
                Parameter("item_id", candidate.ItemId),
                Parameter("stand_tile_x", parsedStandX.ToString()),
                Parameter("stand_tile_y", parsedStandY.ToString())
            };
            foreach (var name in new[]
            {
                "tool_slot_index",
                "tool_qualified_item_id",
                "native_contract",
                "machine_removal_projection_fingerprint",
                "machine_placement_projection_fingerprint",
                "relocation_intent_id",
                "relocation_target_location_id",
                "relocation_target_tile_x",
                "relocation_target_tile_y",
                "relocation_target_stand_tile_x",
                "relocation_target_stand_tile_y",
                "relocation_target_route_distance_tiles",
                "relocation_route_connector_count",
                "relocation_route_connector_kind",
                "relocation_route_expected_target_location",
                "relocation_route_estimated_ticks",
                "relocation_route_segments_json",
                "relocation_target_arrival_tile_x",
                "relocation_target_arrival_tile_y",
                "layout_current_cluster_distance",
                "layout_target_cluster_distance",
                "layout_service_interactions_per_cycle",
                "layout_saved_ticks_per_service_cycle",
                "layout_relocation_cost_ticks",
                "layout_evaluation_cycles",
                "layout_break_even_cycles",
                "layout_net_benefit_ticks",
                "layout_benefit_policy",
                "relocation_target_selection_policy",
                "layout_time_estimate_policy"
            })
            {
                var value = CandidateParameter(candidate, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parameters.Add(Parameter(name, value));
                }
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(
                        candidate,
                        "move_to_machine_removal_adjacent",
                        0),
                    Kind = "move_to_tile",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = parsedStandX,
                    TargetTileY = parsedStandY,
                    EstimatedMinutes =
                        TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId
                    },
                    ExpectedEffects = new[]
                    {
                        "player.tile=" + parsedStandX + "," +
                        parsedStandY
                    },
                    SafetyConstraints = new[]
                    {
                        "collision_checked_by_action_queue_compiler"
                    },
                    FailurePolicy = new[]
                    {
                        "refresh_snapshot_and_replan"
                    }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(
                        candidate,
                        "remove_machine_item",
                        1),
                    Kind = "remove_machine_item",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "farm.machines.target_removal_safe_now=true",
                        "relocation_intent_has_positive_projected_benefit=true"
                    },
                    ExpectedEffects = new[]
                    {
                        candidate.ExpectedEffect
                    },
                    SafetyConstraints = new[]
                    {
                        "runtime_native_pickaxe_removal_only",
                        "fresh_snapshot_required_before_recovered_machine_placement"
                    },
                    FailurePolicy = new[]
                    {
                        "refresh_snapshot_and_replan"
                    },
                    Parameters = parameters.ToArray()
                }
            };
        }
    }
}
