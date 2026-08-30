using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> AdventureGuildRewardSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "claim_adventure_guild_reward", 0),
                Kind = "claim_adventure_guild_reward",
                TargetLocation = candidate.LocationId ?? "AdventureGuild",
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 10,
                Preconditions = new[]
                {
                    "all_projected_monster_eradication_goals_complete_and_unclaimed=true",
                    "entire_reward_batch_inventory_capacity_proven=true",
                    "current_location=AdventureGuild",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects = new[]
                {
                    "all_pending_Gil_goal_flags=true",
                    "all_projected_reward_items_collected=true",
                    "native_reward_mail_and_flags_applied=true"
                },
                SafetyConstraints = new[]
                {
                    "native_AdventureGuild_checkAction_and_menu_clicks_only",
                    "claim_complete_native_batch_without_partial_selection",
                    "do_not_write_kill_counts_mail_flags_or_inventory_directly",
                    "block_before_interaction_if_entire_batch_no_longer_fits"
                },
                FailurePolicy = new[] { "close_only_owned_reward_menu_when_safe", "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
