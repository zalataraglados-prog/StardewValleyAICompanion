using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> DailyQuestBoardApproachSteps(
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
                StepId = StepId(candidate, "daily_quest_board_approach", 0),
                Kind = "move_to_tile",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "daily_quest_offer_identity_matches=true" },
                ExpectedEffects = new[] { "player_at_daily_quest_board_stand_tile=true", "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "native_collision_path_only", "do_not_warp" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> DailyQuestAcceptanceSteps(
        PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue ||
            !int.TryParse(CandidateParameter(candidate, "stand_tile_x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var standX) ||
            !int.TryParse(CandidateParameter(candidate, "stand_tile_y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var standY) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "quest_offer_fingerprint")))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "daily_quest_open_board", 0),
                Kind = "interact",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "player_at_daily_quest_board_stand_tile=true", "map_action=Billboard 3" },
                ExpectedEffects = new[] { "menus.active_menu.is_open=true", "Billboard", "daily_quest_board=true" },
                SafetyConstraints = new[] { "interaction_kind=map_action", "expected_action_type=Billboard" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                    {
                        Parameter("interaction_kind", "map_action"),
                        Parameter("expected_action_type", "Billboard"),
                        Parameter("target_tile_x", candidate.TileX.Value.ToString(CultureInfo.InvariantCulture)),
                        Parameter("target_tile_y", candidate.TileY.Value.ToString(CultureInfo.InvariantCulture))
                    }
                    .Concat(candidate.Parameters.Where(parameter => parameter.Name.StartsWith("quest_", StringComparison.Ordinal)))
                    .ToArray()
            },
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "daily_quest_accept", 1),
                Kind = "accept_daily_quest",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[] { "active_menu.type=Billboard", "daily_quest_accept_button.visible=true" },
                ExpectedEffects = new[] { "daily_quest_native_acceptance_verified=true", "player.accepted_daily_quest=true", "quest.days_left=2" },
                SafetyConstraints = new[] { "native_Billboard_receiveLeftClick_only", "do_not_mutate_quest_state_directly" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
