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

            if (candidate.Kind == "sell_shop_item")
            {
                return SellShopItemSteps(candidate);
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
                candidate.Kind == "recovery_sleep_before_collapse" ||
                candidate.Kind == "recovery_resume_sleep_prompt")
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

            if (candidate.Kind == "collect_spawned_object")
            {
                return CollectSpawnedObjectSteps(candidate);
            }

            if (candidate.Kind == "harvest_ginger")
            {
                return HarvestGingerSteps(candidate);
            }

            if (candidate.Kind == "harvest_bush")
            {
                return HarvestBushSteps(candidate);
            }

            if (candidate.Kind == "claim_mine_reward_chest")
            {
                return ClaimMineRewardChestSteps(candidate);
            }

            if (candidate.Kind == "collect_crab_pot")
            {
                return CollectCrabPotSteps(candidate);
            }

            if (candidate.Kind == "collect_fish_pond_output" || candidate.Kind == "complete_fish_pond_request")
            {
                return FishPondSteps(candidate);
            }

            if (candidate.Kind == "collect_animal_product")
            {
                return CollectAnimalProductSteps(candidate);
            }
            if (candidate.Kind == "pet_daily_interaction" || candidate.Kind == "fill_pet_bowl")
            {
                return PetCareSteps(candidate);
            }
            if (candidate.Kind == "donate_museum_item")
            {
                return MuseumDonationSteps(candidate);
            }
            if (candidate.Kind == "donate_community_center_item")
            {
                return CommunityCenterDonationSteps(candidate);
            }
            if (candidate.Kind == "purchase_joja_membership" || candidate.Kind == "purchase_joja_project")
            {
                return JojaDevelopmentSteps(candidate);
            }
            if (candidate.Kind == "purchase_farmhouse_upgrade" || candidate.Kind == "purchase_farmhouse_expansion")
            {
                return FarmhouseUpgradeSteps(candidate);
            }
            if (candidate.Kind == "pan_ore_spot")
            {
                return PanOreSpotSteps(candidate);
            }

            if (candidate.Kind == "collect_machine_output_tile")
            {
                return CollectMachineOutputSteps(candidate);
            }

            if (candidate.Kind == "load_machine_input_tile")
            {
                return LoadMachineInputSteps(candidate);
            }

            if (candidate.Kind == "name_hatched_animal")
            {
                return NameHatchedAnimalSteps(candidate);
            }

            if (candidate.Kind == "craft_machine_item")
            {
                return CraftMachineItemSteps(candidate);
            }

            if (candidate.Kind == "craft_storage_item")
            {
                return CraftStorageItemSteps(candidate);
            }

            if (candidate.Kind == "place_machine_item")
            {
                return PlaceMachineItemSteps(candidate);
            }

            if (candidate.Kind == "relocate_machine_item")
            {
                return RelocateMachineItemSteps(candidate);
            }

            if (candidate.Kind == "place_storage_item")
            {
                return PlaceStorageItemSteps(candidate);
            }

            if (candidate.Kind == "read_inventory_book")
            {
                return ReadInventoryBookSteps(candidate);
            }

            if (candidate.Kind == "clear_obstacle_tile")
            {
                return ClearObstacleTileSteps(candidate);
            }

            if (candidate.Kind == "clear_farm_resource_clump")
            {
                return ClearFarmResourceClumpSteps(candidate);
            }

            if (candidate.Kind == "clear_green_rain_resource_clump")
            {
                return ClearGreenRainResourceClumpSteps(candidate);
            }

            if (candidate.Kind == "plant_seed_tile")
            {
                return PlantSeedTileSteps(candidate);
            }

            if (candidate.Kind == "social_talk_current" ||
                candidate.Kind == "social_gift_current" ||
                candidate.Kind == "quest_npc_interaction")
            {
                return SocialInteractionSteps(candidate);
            }

            if (candidate.Kind == "social_continuation_retry_wait")
            {
                return SocialContinuationRetryWaitSteps(candidate);
            }

            if (candidate.Kind == "quest_drop_box_donation")
            {
                return QuestDropBoxDonationSteps(candidate);
            }

            if (candidate.Kind == "ship_inventory_item_to_bin")
            {
                return ShipInventoryItemToBinSteps(candidate);
            }

            if (candidate.Kind == "mining_reach_depth_plan_envelope" ||
                candidate.Kind == "mining_slay_monsters_plan_envelope" ||
                candidate.Kind == "mining_collect_quest_resource_plan_envelope" ||
                candidate.Kind == "mining_acquire_golden_scythe_plan_envelope" ||
                candidate.Kind == "mining_obtain_skull_key_plan_envelope" ||
                candidate.Kind == "volcano_reach_caldera_plan_envelope")
            {
                return RollingDungeonPrimitiveSteps(candidate);
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
