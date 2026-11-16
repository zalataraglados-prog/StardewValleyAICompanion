using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CookingSteps(PolicyEventCandidatePrediction candidate)
    {
        var names = new[]
        {
            "recipe_name", "craft_count", "cooking_reason", "cooking_source_id", "cooking_source_kind",
            "location_id", "interaction_tile_x", "interaction_tile_y", "stand_tile_x", "stand_tile_y",
            "output_item_id", "output_qualified_item_id", "output_count", "expected_output_quality",
            "expected_output_order_data", "recipes_cooked_before", "ingredient_rows_json",
            "seasoning_rows_json", "material_container_ids_json", "max_movement_tiles"
        };
        var parameters = names.Select(name => Parameter(name, CandidateParameter(candidate, name))).ToList();
        if (candidate.Parameters.Any(row => PreparedCookingConsumptionBinding.IsControlParameter(row.Name)))
            parameters.AddRange(candidate.Parameters.Where(row => PreparedCookingConsumptionBinding.IsControlParameter(row.Name) ||
                CropSaleStockMaterialTransferBinding.IsControlParameter(row.Name) || CropSaleStockMaterialTransferBinding.IsForeignControlParameter(row.Name) ||
                row.Name == PreparedMaterialTransferContract.BindingParameter).Select(row => Parameter(row.Name, row.Value)));
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "cook_recipe", 0),
                Kind = "cook_recipe",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = Math.Max(1, TicksToMinutes(candidate.EstimatedTicks)),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "learned_recipe_source_and_material_plan_rebound=true",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_kitchen_or_cookout_entry_only",
                    "native_CraftingPage_click_only",
                    "no_direct_inventory_recipe_stat_quest_or_achievement_mutation",
                    "runtime_verify_exact_material_seasoning_output_and_recipe_count_delta"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            }
        };
    }
}
