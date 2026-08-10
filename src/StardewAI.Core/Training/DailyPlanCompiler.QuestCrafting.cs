using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CraftQuestItemSteps(
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
            "quest_crafting_target_qualified_item_id",
            "quest_candidate_id",
            "quest_family",
            "quest_id",
            "quest_key",
            "quest_runtime_type",
            "quest_next_action",
            "quest_objective_index",
            "quest_expected_current_count",
            "quest_expected_target_count",
            "commitment_ledger_id",
            "commitment_ledger_revision",
            "material_reservation_guard_status",
            "material_reservation_ledger_id",
            "material_reservation_ledger_revision",
            "material_reservation_ids_json",
            "native_contract"
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
                StepId = StepId(candidate, "craft_quest_item", 0),
                Kind = "craft_quest_item",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "active_CraftingQuest_and_exact_recipe_rebound=true",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_CraftingPage_click_only",
                    "workbench_source_requires_native_MultipleMutexRequest",
                    "no_direct_inventory_recipe_stat_or_quest_mutation",
                    "runtime_verify_exact_material_output_and_quest_terminal"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            }
        };
    }
}
