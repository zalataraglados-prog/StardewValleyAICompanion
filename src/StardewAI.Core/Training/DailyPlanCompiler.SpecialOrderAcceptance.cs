using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> SpecialOrderBoardApproachSteps(
        PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "special_order_board_approach", 0),
                Kind = "move_to_tile",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "special_order_board_endpoint_matches=true" },
                ExpectedEffects = new[] { "player_at_special_order_board_stand_tile=true", "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "native_collision_path_only", "do_not_warp" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> SpecialOrderBoardOpenSteps(
        PolicyEventCandidatePrediction candidate)
    {
        var actionToken = CandidateParameter(candidate, "special_order_action_token");
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || string.IsNullOrWhiteSpace(actionToken))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "special_order_board_open", 0),
                Kind = "interact",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "player_at_special_order_board_stand_tile=true", "map_action=" + actionToken },
                ExpectedEffects = new[] { "native_special_order_board_interaction_started=true", "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "interaction_kind=map_action", "expected_action_type=" + actionToken },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                    {
                        Parameter("interaction_kind", "map_action"),
                        Parameter("expected_action_type", actionToken),
                        Parameter("target_tile_x", candidate.TileX.Value.ToString(CultureInfo.InvariantCulture)),
                        Parameter("target_tile_y", candidate.TileY.Value.ToString(CultureInfo.InvariantCulture))
                    }
                    .Concat(candidate.Parameters)
                    .ToArray()
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> SpecialOrderBoardDialogueSteps(
        PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "special_order_board_dialogue_advance", 0),
                Kind = "close_menu",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[] { "active_menu.type=DialogueBox", "dialogue.speaker=Marlon", "dialogue.has_special_order_board_callback=true" },
                ExpectedEffects = new[] { "active_menu.type=SpecialOrdersBoard", "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "native_dialogue_input_only", "expected_menu_type_after_dialogue=SpecialOrdersBoard" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters.Concat(new[]
                {
                    Parameter("social_continuation_dialogue_recovery", "true"),
                    Parameter("expected_menu_type_after_dialogue", "SpecialOrdersBoard")
                }).ToArray()
            }
        };

    private static IEnumerable<SmallModelPlanStep> SpecialOrderAcceptanceSteps(
        PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "quest_offer_fingerprint")) ||
            !int.TryParse(CandidateParameter(candidate, "special_order_selection_index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
            index is < 0 or > 1)
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "accept_special_order", 0),
                Kind = "accept_special_order",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[] { "active_menu.type=SpecialOrdersBoard", "selected_offer_identity_matches=true" },
                ExpectedEffects = new[] { "matching_special_order_added_to_team=true", "accepted_special_order_type=true" },
                SafetyConstraints = new[] { "native_SpecialOrdersBoard_receiveLeftClick_only", "do_not_mutate_special_order_state_directly" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
