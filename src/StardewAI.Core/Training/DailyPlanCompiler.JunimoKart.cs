using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> JunimoKartSteps(
        PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue ||
            !int.TryParse(CandidateParameter(candidate, "stand_tile_x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var standX) ||
            !int.TryParse(CandidateParameter(candidate, "stand_tile_y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var standY) ||
            !int.TryParse(CandidateParameter(candidate, "minigame_target_score"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetScore) ||
            targetScore <= 0)
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        var questParameters = candidate.Parameters
            .Where(parameter => parameter.Name.StartsWith("quest_", StringComparison.Ordinal))
            .ToArray();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "junimo_kart_move", 0),
                Kind = "move_to_tile",
                TargetLocation = candidate.LocationId,
                TargetTileX = standX,
                TargetTileY = standY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "player.has_skull_key=true" },
                ExpectedEffects = new[] { "player_at_arcade_stand_tile=true" },
                SafetyConstraints = new[] { "native_collision_path_only", "do_not_warp" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                    {
                        Parameter("target_tile_x", standX.ToString(CultureInfo.InvariantCulture)),
                        Parameter("target_tile_y", standY.ToString(CultureInfo.InvariantCulture)),
                        Parameter("max_movement_tiles", CandidateParameter(candidate, "max_movement_tiles"))
                    }
                    .Concat(questParameters)
                    .ToArray()
            },
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "junimo_kart_interact", 1),
                Kind = "interact",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "player_at_arcade_stand_tile=true", "map_action=Arcade_Minecart" },
                ExpectedEffects = new[] { "menus.active_menu.is_open=true", "DialogueBox", "interact_map_action_Arcade_Minecart" },
                SafetyConstraints = new[] { "interaction_kind=map_action", "expected_action_type=Arcade_Minecart" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                    {
                        Parameter("interaction_kind", "map_action"),
                        Parameter("expected_action_type", "Arcade_Minecart"),
                        Parameter("target_tile_x", candidate.TileX.Value.ToString(CultureInfo.InvariantCulture)),
                        Parameter("target_tile_y", candidate.TileY.Value.ToString(CultureInfo.InvariantCulture))
                    }
                    .Concat(questParameters)
                    .ToArray()
            },
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "junimo_kart_endless", 2),
                Kind = "choose_dialogue_response",
                EstimatedMinutes = 1,
                Preconditions = new[] { "active_menu.type=DialogueBox", "dialogue_key=MinecartGame" },
                ExpectedEffects = new[] { "menus.active_menu.is_open=false", "current_minigame=MineCart", "minigame_mode=2" },
                SafetyConstraints = new[] { "dialogue_response_key=Endless" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                    {
                        Parameter("expected_dialogue_key", "MinecartGame"),
                        Parameter("dialogue_response_key", "Endless"),
                        Parameter("minigame_id", "MineCart"),
                        Parameter("minigame_mode", "2")
                    }
                    .Concat(questParameters)
                    .ToArray()
            },
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "junimo_kart_play", 3),
                Kind = "play_junimo_kart",
                EstimatedMinutes = 15,
                Preconditions = new[] { "current_minigame=MineCart", "minigame_mode=2", "exact_special_order_objective_active=true" },
                ExpectedEffects = new[] { "endless_score_submitted_at_or_above=" + targetScore, "quest_objective_progress_verified=true" },
                SafetyConstraints = new[]
                {
                    "timed_equivalent_is_training_singleplayer_only",
                    "timed_equivalent_must_be_labeled_simulated_equivalent",
                    "native_perfect_controller_remains_available"
                },
                FailurePolicy = new[] { "block_with_minigame_trace", "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
