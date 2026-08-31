using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> MasteryClaimSteps(PolicyEventCandidatePrediction candidate)
    {
        var skill = candidate.Parameters.FirstOrDefault(parameter => parameter.Name == "mastery_skill_key")?.Value ?? "unknown";
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "claim_mastery", 0),
                Kind = "claim_mastery",
                TargetLocation = candidate.LocationId ?? "MasteryCave",
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 4,
                Preconditions = new[]
                {
                    "all_five_base_skills_level_ten",
                    "unspent_mastery_level>0",
                    "selected_" + skill + "_plaque_unclaimed",
                    "fresh_projection_and_option_identity_match",
                    "native_MasteryCave_skill_endpoint_reachable",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects = new[]
                {
                    "selected_mastery_stat+=1",
                    "masteryLevelsSpent+=1",
                    "all_exact_recipe_and_direct_rewards_settled_by_native_menu",
                    "fresh_snapshot_replan_required"
                },
                SafetyConstraints = new[]
                {
                    "small_model_selects_exactly_one_currently_claimable_skill",
                    "one_native_claim_per_fresh_snapshot",
                    "preserve_selected_skill_and_option_identity_across_routes",
                    "use_shared_route_and_BFS_only",
                    "do_not_mutate_mastery_stats_recipes_inventory_trinket_slots_or_finale_directly"
                },
                FailurePolicy = new[] { "close_only_owned_MasteryTrackerMenu_when_safe", "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
