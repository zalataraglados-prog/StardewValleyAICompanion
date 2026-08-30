using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> MultiplayerWalletSteps(PolicyEventCandidatePrediction candidate)
    {
        var operation = CandidateParameter(candidate, "wallet_operation");
        if (string.IsNullOrWhiteSpace(operation) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "wallet_reason")) ||
            CandidateParameter(candidate, "confirm_wallet_operation") != "true")
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "manage_multiplayer_wallet", 0),
                Kind = "manage_multiplayer_wallet",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "explicit_player_command_and_exact_wallet_operation_still_authorized=true",
                    "complete_fresh_multiplayer_wallet_projection_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "player_command_only_and_explicit_confirmation_required",
                    "host_required_only_for_schedule_or_cancel_mode_change",
                    "transfer_requires_exact_recipient_amount_and_separate_wallets",
                    "shared_bfs_then_native_LedgerBook_DialogueBox_and_DigitEntryMenu_input_only",
                    "mode_change_settles_only_through_normal_Game1_newDay_wallet_barrier",
                    "no_direct_wallet_flag_money_individual_balance_or_stat_mutation"
                },
                FailurePolicy = new[] { "close_native_menu_refresh_snapshot_and_require_fresh_player_command" },
                Parameters = candidate.Parameters
            }
        };
    }
}
