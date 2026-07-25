using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep>
        CraftStorageItemSteps(
            PolicyEventCandidatePrediction candidate)
    {
        var parameters = new List<SmallModelActionParameter>();
        foreach (var name in new[]
        {
            "recipe_name",
            "output_qualified_item_id",
            "output_item_id",
            "output_count",
            "times_crafted_before",
            "ingredient_rows_json",
            "crafting_source",
            "workbench_access_point_id",
            "workbench_container_node_ids_json",
            "location_id",
            "target_tile_x",
            "target_tile_y",
            "stand_tile_x",
            "stand_tile_y",
            "native_storage_branch",
            "special_chest_type",
            "actual_capacity",
            "storage_role",
            "storage_demand_class",
            "inventory_ordinary_storage_count",
            "usable_ordinary_storage_count",
            "usable_ordinary_free_stack_slots",
            "commitment_ledger_id",
            "commitment_ledger_revision",
            "material_reservation_guard_status",
            "material_reservation_ledger_id",
            "material_reservation_ledger_revision",
            "material_reservation_ids_json"
        })
        {
            parameters.Add(Parameter(
                name,
                CandidateParameter(candidate, name)));
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(
                    candidate,
                    "craft_storage_item",
                    0),
                Kind = "craft_storage_item",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "storage_capacity_demand_rebound=true",
                    "learned_storage_recipe_and_material_plan_rebound=true",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects =
                    new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_CraftingPage_click_only",
                    "workbench_source_requires_native_MultipleMutexRequest",
                    "no_direct_inventory_or_progress_mutation",
                    "runtime_verify_exact_material_and_output_delta"
                },
                FailurePolicy =
                    new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            }
        };
    }
}
