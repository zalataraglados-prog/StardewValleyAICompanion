using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> PlayerCustomizationSteps(PolicyEventCandidatePrediction candidate)
    {
        var mode = CandidateParameter(candidate, "customization_mode");
        if (mode is not ("wizard_shrine" or "desert_makeover") ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "customization_reason")) ||
            CandidateParameter(candidate, "confirm_customization") != "true")
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "customize_player", 0),
                Kind = "customize_player",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "explicit_player_command_and_confirmation_still_authorized=true",
                    "customization_mode_and_exact_projected_result_remain_current=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "player_command_only_and_excluded_from_strategy_training",
                    "fresh_compiler_rebinds_native_endpoint_price_inventory_stylist_rng_and_expected_result",
                    "wizard_uses_native_shrine_dialogue_and_CharacterCustomization_controls_only",
                    "desert_uses_native_touch_action_and_skippable_event_completion_callback_only",
                    "no_direct_money_appearance_equipment_daily_flag_or_ReceiveMakeOver_mutation"
                },
                FailurePolicy = new[] { "stop_native_input_refresh_snapshot_and_require_fresh_player_command" },
                Parameters = candidate.Parameters
            }
        };
    }
}
