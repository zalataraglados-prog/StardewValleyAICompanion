using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> DwarfKingStatuePowerSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "choose_dwarf_statue_power", 0),
                Kind = "choose_dwarf_statue_power",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "mining_mastery_unlocked=true",
                    "no_active_dwarf_statue_buff=true",
                    "exact_daily_offer_identity_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "small_model_selects_only_one_exact_offered_power_id",
                    "compiler_rebinds_all_mechanical_fields_from_fresh_snapshot",
                    "native_Object_checkForAction_and_ChooseFromIconsMenu_receiveLeftClick_only",
                    "never_directly_apply_or_remove_a_production_buff"
                },
                FailurePolicy = new[] { "stop_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
