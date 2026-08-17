using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> AnimalManagementSteps(
        PolicyEventCandidatePrediction candidate)
    {
        var required = new[]
        {
            "animal_id", "management_intent", "management_reason", "location_id",
            "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
            "target_runtime_type", "safe_slot_index", "expected_name_before",
            "expected_sell_price", "expected_money_before"
        };
        if (required.Any(name => string.IsNullOrWhiteSpace(CandidateParameter(candidate, name))))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "manage_animal", 0),
                Kind = "manage_animal",
                TargetLocation = CandidateParameter(candidate, "location_id"),
                TargetTileX = CandidateInt(candidate, "target_tile_x"),
                TargetTileY = CandidateInt(candidate, "target_tile_y"),
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "exact_animal_management_projection_still_matches=true",
                    "active_menu.type=none"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_FarmAnimal_pet_and_AnimalQueryMenu_lifecycle_only",
                    "explicit_management_intent_and_reason_required",
                    "irreversible_sale_confirmation_required",
                    "no_direct_animal_home_name_reproduction_health_or_money_mutation"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
                    .Select(parameter => new SmallModelActionParameter
                    {
                        Name = parameter.Name,
                        Value = parameter.Value
                    })
                    .ToArray()
            }
        };
    }
}
