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
                "crafting_source"
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
                        "native_personal_CraftingPage_click_only",
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
