using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> ToolUpgradeSteps(
        PolicyEventCandidatePrediction candidate)
    {
        var claim = string.Equals(
            candidate.Kind,
            "claim_tool_upgrade",
            StringComparison.Ordinal);
        return
        [
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, candidate.Kind, 0),
                Kind = candidate.Kind,
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = Math.Max(
                    1,
                    TicksToMinutes(candidate.EstimatedTicks)),
                Preconditions =
                [
                    "candidate_id:" + candidate.CandidateId,
                    "fresh_player_tool_upgrade_projection_required=true",
                    claim
                        ? "completed_pending_axe_and_claim_capacity_remain_exact=true"
                        : "source_axe_money_and_trade_bars_remain_exact=true"
                ],
                ExpectedEffects = [candidate.ExpectedEffect],
                SafetyConstraints =
                [
                    "native_Blacksmith_counter_and_ClintUpgrade_menu_only",
                    "one_submission_or_one_claim_per_action",
                    "no_direct_money_inventory_pending_tool_or_upgrade_timer_mutation"
                ],
                FailurePolicy =
                [
                    "stop_native_input_refresh_snapshot_and_replan"
                ],
                Parameters = candidate.Parameters
            }
        ];
    }
}
