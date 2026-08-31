using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> PrizeTicketRewardSteps(PolicyEventCandidatePrediction candidate)
    {
        var stage = candidate.Parameters.FirstOrDefault(parameter => parameter.Name == "prize_ticket_stage")?.Value ?? string.Empty;
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "claim_prize_ticket", 0),
                Kind = "claim_prize_ticket",
                TargetLocation = candidate.LocationId ?? (stage == "redeem_prize" ? "ManorHouse" : "Town"),
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = stage == "redeem_prize" ? 8 : 4,
                Preconditions = stage == "redeem_prize"
                    ? new[] { "inventory_PrizeTicket>0", "fresh_current_reward_identity_matches", "native_PrizeMachine_endpoint_reachable", "menus.active_menu.is_open=false" }
                    : new[] { "specialOrderPrizeTickets>0", "inventory_accepts_one_PrizeTicket", "native_SpecialOrdersPrizeTickets_endpoint_reachable", "menus.active_menu.is_open=false" },
                ExpectedEffects = stage == "redeem_prize"
                    ? new[] { "inventory_PrizeTicket-=1", "ticketPrizesClaimed+=1", "exact_current_reward_delivered_to_inventory_or_debris" }
                    : new[] { "specialOrderPrizeTickets-=1", "inventory_PrizeTicket+=1", "fresh_snapshot_continues_same_reward_objective" },
                SafetyConstraints = new[]
                {
                    "one_native_stage_per_fresh_snapshot",
                    "preserve_expected_prize_level_and_reward_fingerprint_across_routes",
                    "use_shared_route_and_BFS_only",
                    "do_not_mutate_ticket_stats_inventory_or_rewards_directly"
                },
                FailurePolicy = new[] { "close_only_owned_PrizeTicketMenu_when_safe", "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
