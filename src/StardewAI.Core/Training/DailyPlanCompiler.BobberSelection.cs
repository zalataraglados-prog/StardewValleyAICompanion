using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> BobberSelectionSteps(PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "bobber_reason")) ||
            CandidateParameter(candidate, "confirm_bobber_style") != "true")
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "choose_bobber_style", 0),
                Kind = "choose_bobber_style",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "explicit_player_command_and_confirmation_still_authorized=true",
                    "requested_style_is_random_or_currently_unlocked=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "player_command_only_and_excluded_from_strategy_training",
                    "compiler_rebinds_fish_count_unlock_and_live_FishShop_endpoint",
                    "shared_BFS_then_native_Bobbers_action_icon_click_and_close_input_only",
                    "no_direct_bobberStyle_usingRandomizedBobber_or_Game1.random_mutation"
                },
                FailurePolicy = new[] { "close_native_menu_refresh_snapshot_and_require_fresh_player_command" },
                Parameters = candidate.Parameters
            }
        };
    }
}
