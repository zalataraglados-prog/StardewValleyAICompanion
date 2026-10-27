using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CraftRainTotemSteps(
        PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "craft_rain_totem", 0),
                Kind = "craft_rain_totem",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "authoritative_master_angler_tomorrow_rain_window_rebound=true",
                    "learned_rain_totem_recipe_and_material_plan_rebound=true",
                    "owned_rain_totem_supply_absent=true",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_CraftingPage_click_only",
                    "workbench_source_requires_native_MultipleMutexRequest",
                    "no_direct_inventory_weather_or_progress_mutation",
                    "runtime_verify_exact_material_and_output_delta",
                    "fresh_snapshot_and_scheduler_replan_required_before_use"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> UseRainTotemSteps(
        PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "use_rain_totem", 0),
                Kind = "use_rain_totem",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 3,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "authoritative_master_angler_tomorrow_rain_window_rebound=true",
                    "exact_owned_rain_totem_rebound=true",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_Object_performUseAction_only",
                    "no_direct_inventory_weather_or_progress_mutation",
                    "runtime_verify_exact_inventory_and_weather_delta",
                    "fresh_snapshot_and_scheduler_replan_required_after_use"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
