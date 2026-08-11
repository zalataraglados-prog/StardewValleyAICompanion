using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> AnimalPurchaseResponseSteps(
        PolicyEventCandidatePrediction candidate)
    {
        var expectedDialogueKey = CandidateParameter(candidate, "expected_dialogue_key");
        var responseKey = CandidateParameter(candidate, "dialogue_response_key");
        var expectedMenuType = CandidateParameter(candidate, "expected_menu_type_after");
        if (string.IsNullOrWhiteSpace(expectedDialogueKey) ||
            string.IsNullOrWhiteSpace(responseKey) ||
            string.IsNullOrWhiteSpace(expectedMenuType))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "choose_animal_purchase_response", 0),
                Kind = "choose_animal_purchase_response",
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "active_menu.type=DialogueBox",
                    "active_menu.last_question_key=" + expectedDialogueKey
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect, "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[]
                {
                    "response_present_in_transparent_dialogue_state",
                    "native_GameLocation.answerDialogue_only",
                    "no_direct_menu_or_animal_state_mutation"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = AnimalPurchaseExecutionParameters(candidate)
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> PurchaseAnimalSteps(
        PolicyEventCandidatePrediction candidate)
    {
        var required = new[]
        {
            "continuation.animal_type_id",
            "continuation.possible_actual_type_ids_json",
            "continuation.target_location_id",
            "continuation.home_building_type",
            "continuation.home_building_tile_x",
            "continuation.home_building_tile_y",
            "continuation.generated_animal_name",
            "continuation.expected_price",
            "continuation.expected_money_before",
            "continuation.expected_money_after",
            "continuation.expected_home_occupant_count_before",
            "continuation.expected_home_capacity",
            "continuation.candidate_identity_sha256"
        };
        if (required.Any(name => string.IsNullOrWhiteSpace(CandidateParameter(candidate, name))))
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "purchase_animal", 0),
                Kind = "purchase_animal",
                TargetLocation = CandidateParameter(candidate, "continuation.target_location_id"),
                TargetTileX = CandidateInt(candidate, "continuation.home_building_tile_x"),
                TargetTileY = CandidateInt(candidate, "continuation.home_building_tile_y"),
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "active_menu.type=PurchaseAnimalsMenu",
                    "animal_purchase_projection_still_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_PurchaseAnimalsMenu_lifecycle_only",
                    "exact_stock_home_capacity_money_and_name_receipt",
                    "no_direct_animal_adoption_or_money_mutation"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = AnimalPurchaseExecutionParameters(candidate)
            }
        };
    }

    private static SmallModelActionParameter[] AnimalPurchaseExecutionParameters(
        PolicyEventCandidatePrediction candidate)
    {
        return candidate.Parameters
            .Select(parameter => new SmallModelActionParameter
            {
                Name = parameter.Name.StartsWith("continuation.", StringComparison.Ordinal)
                    ? parameter.Name["continuation.".Length..]
                    : parameter.Name,
                Value = parameter.Value
            })
            .GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }
}
