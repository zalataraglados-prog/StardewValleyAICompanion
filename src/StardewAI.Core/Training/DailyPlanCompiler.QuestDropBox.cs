using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> QuestDropBoxDonationSteps(
            PolicyEventCandidatePrediction candidate)
        {
            var standX = CandidateInt(candidate, "stand_tile_x");
            var standY = CandidateInt(candidate, "stand_tile_y");
            var actionX = CandidateInt(candidate, "target_tile_x");
            var actionY = CandidateInt(candidate, "target_tile_y");
            var routeDistance = CandidateInt(candidate, "route_distance_tiles") ?? 0;
            if (!standX.HasValue ||
                !standY.HasValue ||
                !actionX.HasValue ||
                !actionY.HasValue ||
                string.IsNullOrWhiteSpace(CandidateParameter(candidate, "quest_drop_box_id")) ||
                string.IsNullOrWhiteSpace(CandidateParameter(candidate, "qualified_item_id")))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_quest_drop_box", 0),
                    Kind = "move_to_tile",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = standX,
                    TargetTileY = standY,
                    EstimatedMinutes = Math.Max(1, (int)Math.Ceiling(routeDistance / 5d)),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standX + "," + standY },
                    SafetyConstraints = new[]
                    {
                        "drop_box_action_tile_from_transparent_current_map_index",
                        "collision_checked_by_action_queue_compiler",
                        "no_direct_coordinate_teleport"
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter(
                            "max_movement_tiles",
                            Math.Max(1, CandidateInt(candidate, "max_movement_tiles") ?? routeDistance)
                                .ToString(System.Globalization.CultureInfo.InvariantCulture))
                    }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "quest_drop_box_donate", 1),
                    Kind = "quest_drop_box_donate",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = actionX,
                    TargetTileY = actionY,
                    EstimatedMinutes = 2,
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "player_adjacent_to_drop_box_stand_tile=" + standX + "," + standY
                    },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[]
                    {
                        "special_order_identity_and_objective_rebound_by_action_queue_compiler",
                        "native_GameLocation_checkAction_opens_QuestContainerMenu",
                        "native_menu_callbacks_update_and_confirm_donations",
                        "no_direct_quest_or_donated_items_mutation"
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = candidate.Parameters
                }
            };
        }
    }
}
