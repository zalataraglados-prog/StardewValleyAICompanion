using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> SocialInteractionSteps(PolicyEventCandidatePrediction candidate)
        {
            var npcName = CandidateParameter(candidate, "npc_name");
            var standTileXStr = CandidateParameter(candidate, "stand_tile_x");
            var standTileYStr = CandidateParameter(candidate, "stand_tile_y");
            var npcTileXStr = CandidateParameter(candidate, "npc_tile_x");
            var npcTileYStr = CandidateParameter(candidate, "npc_tile_y");
            if (string.IsNullOrWhiteSpace(npcName) ||
                !int.TryParse(standTileXStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var standTileX) ||
                !int.TryParse(standTileYStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var standTileY) ||
                !int.TryParse(npcTileXStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var npcTileX) ||
                !int.TryParse(npcTileYStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var npcTileY))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var routeDistance = CandidateInt(candidate, "route_distance_tiles") ?? 0;
            var actionKind = candidate.Kind == "social_talk_current"
                ? "talk"
                : candidate.Kind == "social_gift_current"
                    ? "gift"
                    : CandidateParameter(candidate, "quest_interaction_kind");
            if (candidate.Kind == "quest_npc_interaction" &&
                actionKind != "report" &&
                actionKind != "offer_item")
            {
                return Array.Empty<SmallModelPlanStep>();
            }
            var parameters = new List<SmallModelActionParameter>(candidate.Parameters)
            {
                Parameter("social_action_kind", actionKind)
            };
            var steps = new List<SmallModelPlanStep>
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_social_stand", 0),
                    Kind = "move_to_tile",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = standTileX,
                    TargetTileY = standTileY,
                    EstimatedMinutes = Math.Max(1, (int)Math.Ceiling(routeDistance / 5d)),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTileX + "," + standTileY },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler", "no_direct_coordinate_teleport" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_movement_tiles", Math.Max(1, routeDistance).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, candidate.Kind == "quest_npc_interaction" ? "quest_npc_interact" : "social_interact", 1),
                    Kind = candidate.Kind == "quest_npc_interaction" ? "quest_npc_interact" : "social_interact",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = npcTileX,
                    TargetTileY = npcTileY,
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "player_adjacent_to_npc_stand_tile=" + standTileX + "," + standTileY
                    },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[]
                    {
                        "npc_from_transparent_current_state",
                        "npc_adjacent_checked_by_move_to_tile_predecessor",
                        candidate.Kind == "quest_npc_interaction"
                            ? "quest_identity_and_progress_rebound_by_action_queue_compiler"
                            : "social_legality_rebound_by_action_queue_compiler"
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };

            return steps;
        }

        private static IEnumerable<SmallModelPlanStep> RouteConnectorSteps(PolicyEventCandidatePrediction candidate)
        {
            var targetTileX = candidate.TileX ?? CandidateInt(candidate, "target_tile_x");
            var targetTileY = candidate.TileY ?? CandidateInt(candidate, "target_tile_y");
            var connectorKind = CandidateParameter(candidate, "connector_kind");
            var expectedTargetLocation = CandidateParameter(candidate, "expected_target_location");
            if (string.IsNullOrWhiteSpace(expectedTargetLocation))
            {
                expectedTargetLocation = ParseValue(candidate.ExpectedEffect, "expected_target_location=");
            }

            if (!targetTileX.HasValue ||
                !targetTileY.HasValue ||
                string.IsNullOrWhiteSpace(candidate.LocationId) ||
                string.IsNullOrWhiteSpace(connectorKind) ||
                string.IsNullOrWhiteSpace(expectedTargetLocation))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("connector_kind", connectorKind),
                Parameter("expected_target_location", expectedTargetLocation)
            };
            foreach (var name in new[]
            {
                "expected_arrival_tile_x",
                "expected_arrival_tile_y",
                "max_movement_tiles",
                "estimated_ticks",
                "estimated_minutes",
                "continuation.option_id",
                "continuation.npc_name",
                "continuation.target_location",
                "continuation.slot_index",
                "continuation.qualified_item_id",
                "continuation.quest_candidate_id",
                "continuation.machine_location_id",
                "continuation.machine_tile_x",
                "continuation.machine_tile_y",
                "social_route.remaining_connector_count",
                "social_route.position_source",
                "social_route.future_schedule_projection",
                "machine_route.remaining_connector_count",
                "machine_route.snapshot_policy"
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
                    StepId = StepId(candidate, "traverse_connector", 0),
                    Kind = "traverse_connector",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = targetTileX,
                    TargetTileY = targetTileY,
                    EstimatedMinutes = CandidateInt(candidate, "estimated_minutes") ?? TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "current_location=" + candidate.LocationId,
                        "transparent_connector_still_matches_snapshot=true"
                    },
                    ExpectedEffects = new[]
                    {
                        "player.location_id=" + expectedTargetLocation,
                        "fresh_snapshot_replan_required=true"
                    },
                    SafetyConstraints = new[]
                    {
                        "connector_target_from_transparent_current_map_index",
                        "connector_gate_checked_upstream",
                        "no_direct_coordinate_teleport",
                        "one_connector_per_replan"
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

    }
}
