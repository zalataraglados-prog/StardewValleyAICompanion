using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> CraftMachineItemSteps(PolicyEventCandidatePrediction candidate)
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
                "machine_demand_class",
                "machine_scale",
                "machine_horizon_status",
                "machine_timing_status",
                "machine_demand_priority",
                "priority_task_required",
                "priority_task_sources_json",
                "production_capacity_required",
                "potential_input_count",
                "backlog_input_units",
                "placed_same_machine_count",
                "idle_same_machine_count",
                "process_cycle_minutes",
                "next_arrival_days",
                "next_arrival_units",
                "next_arrival_service_interval_days",
                "capacity_before_next_arrival",
                "capacity_deficit_units",
                "capacity_between_arrival_waves",
                "arrival_wave_capacity_deficit_units",
                "required_additional_machine_count",
                "latest_build_lead_minutes",
                "minutes_until_next_arrival",
                "machine_build_window_open",
                "next_arrival_source",
                "commitment_ledger_id",
                "commitment_ledger_revision",
                "commitment_ids_json",
                "collection_path_required",
                "collection_path_source"
            })
            {
                parameters.Add(Parameter(name, CandidateParameter(candidate, name)));
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "craft_machine_item", 0),
                    Kind = "craft_machine_item",
                    TargetLocation = candidate.LocationId,
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "learned_machine_recipe_and_material_plan_rebound=true",
                        "menus.active_menu.is_open=false"
                    },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[]
                    {
                        "native_CraftingPage_click_only",
                        "workbench_source_requires_native_MultipleMutexRequest",
                        "no_direct_inventory_or_progress_mutation",
                        "runtime_verify_exact_material_and_output_delta"
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }
    }
}
