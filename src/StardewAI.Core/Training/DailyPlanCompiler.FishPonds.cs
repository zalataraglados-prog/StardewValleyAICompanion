using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FishPondSteps(PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
        {
            return Array.Empty<SmallModelPlanStep>();
        }
        var kind = candidate.Kind;
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, kind, 0),
                Kind = kind,
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "fish_pond_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_checkAction_only", "transparent_pond_edge_stand_tile", "no_direct_pond_inventory_or_skill_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> FishPondManagementSteps(PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
        {
            return Array.Empty<SmallModelPlanStep>();
        }
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "manage_fish_pond", 0),
                Kind = "manage_fish_pond",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "explicit_player_command_and_confirmation_still_valid=true", "fish_pond_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "player_command_only", "not_enabled_for_autonomous_daily_planning", "native_PondQueryMenu_only", "no_direct_fish_pond_state_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_require_new_player_confirmation" },
                Parameters = candidate.Parameters
            }
        };
    }
}
