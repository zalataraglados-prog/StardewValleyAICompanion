using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> CandidateSteps(PolicyEventCandidatePrediction candidate)
        {
            if (candidate.Kind == "interact_endpoint")
            {
                return InteractEndpointSteps(candidate);
            }

            if (candidate.Kind == "buy_shop_item")
            {
                return BuyShopItemSteps(candidate);
            }

            if (candidate.Kind == "recovery_refresh_plan")
            {
                return RecoveryRefreshSteps(candidate);
            }

            if (candidate.Kind == "recovery_close_menu")
            {
                return RecoveryCloseMenuSteps(candidate);
            }

            if (candidate.Kind == "recovery_return_home" ||
                candidate.Kind == "recovery_sleep_immediately" ||
                candidate.Kind == "recovery_sleep_before_collapse")
            {
                return RecoveryExecutionSteps(candidate);
            }

            if (candidate.Kind == "route_connector_tile")
            {
                return RouteConnectorSteps(candidate);
            }

            if (candidate.Kind == "water_crop_tile")
            {
                return WaterCropTileSteps(candidate);
            }

            if (candidate.Kind == "catch_fish")
            {
                return CatchFishSteps(candidate);
            }

            if (candidate.Kind == "harvest_crop_tile")
            {
                return HarvestCropTileSteps(candidate);
            }

            if (candidate.Kind == "harvest_giant_crop_tile")
            {
                return HarvestGiantCropTileSteps(candidate);
            }

            if (candidate.Kind == "pickup_debris_item")
            {
                return PickupDebrisItemSteps(candidate);
            }

            if (candidate.Kind == "collect_machine_output_tile")
            {
                return CollectMachineOutputSteps(candidate);
            }

            if (candidate.Kind == "load_machine_input_tile")
            {
                return LoadMachineInputSteps(candidate);
            }

            if (candidate.Kind == "clear_obstacle_tile")
            {
                return ClearObstacleTileSteps(candidate);
            }

            if (candidate.Kind == "plant_seed_tile")
            {
                return PlantSeedTileSteps(candidate);
            }

            if (candidate.Kind == "social_talk_current" || candidate.Kind == "social_gift_current")
            {
                return SocialInteractionSteps(candidate);
            }

            if (candidate.Kind == "social_continuation_retry_wait")
            {
                return SocialContinuationRetryWaitSteps(candidate);
            }

            if (candidate.Kind == "ship_inventory_item_to_bin")
            {
                return ShipInventoryItemToBinSteps(candidate);
            }

            return Array.Empty<SmallModelPlanStep>();
        }

        private static IEnumerable<SmallModelPlanStep> SocialContinuationRetryWaitSteps(PolicyEventCandidatePrediction candidate)
        {
            var waitTicks = CandidateInt(candidate, "retry_wait_ticks") ?? 600;
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "social_continuation_retry_wait", 0),
                    Kind = "wait_ticks",
                    WaitTicks = Math.Clamp(waitTicks, 1, MaxWaitTicksPerStep),
                    EstimatedMinutes = Math.Max(1, waitTicks / 60),
                    Preconditions = new[] { "same_social_objective_active=true", "current_social_interaction_not_executable=true" },
                    ExpectedEffects = new[] { "time_advances", "fresh_snapshot_replan_required=true" },
                    SafetyConstraints = new[] { "do_not_wait_with_danger_or_active_menu", "do_not_compile_social_interaction_until_reachable" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = ContinuationParameters(candidate)
                }
            };
        }

    }
}
