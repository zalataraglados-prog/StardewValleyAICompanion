using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private delegate CompiledActionStep[] ActionStepCompiler(SmallModelAction action, SnapshotEnvelope snapshot);
        private delegate SmallModelActionParameter[] ActionParameterCompiler(SmallModelAction action, SnapshotEnvelope snapshot);

        private static readonly IReadOnlyDictionary<string, ActionStepCompiler> ActionStepCompilers =
            new Dictionary<string, ActionStepCompiler>(StringComparer.Ordinal)
            {
                ["farm.maintain_crops"] = CompileCropMaintenanceSteps,
                ["farm.process_machines"] = (_, snapshot) => CompileMachineProcessingSteps(snapshot),
                ["recovery.stabilize_day"] = (_, snapshot) => CompileRecoverySteps(snapshot),
                ["executor.move_to_tile"] = (action, _) => CompileMoveToTileStep(action),
                ["executor.traverse_connector"] = (action, _) => CompileTraverseConnectorStep(action),
                ["executor.face_direction"] = (action, _) => CompileFaceDirectionStep(action),
                ["executor.interact"] = (action, _) => CompileInteractStep(action),
                ["executor.buy_shop_item"] = CompileBuyShopItemStep,
                ["executor.choose_dialogue_response"] = (action, _) => CompileChooseDialogueResponseStep(action),
                ["executor.sleep"] = (action, snapshot) => CompileSleepSteps(snapshot, action),
                ["executor.wait_ticks"] = (action, _) => CompileWaitTicksStep(action),
                ["executor.clear_obstacle"] = (action, _) => CompileClearObstacleStep(action),
                ["executor.break_farm_resource_clump"] = (action, _) => CompileFarmResourceClumpStep(action),
                ["executor.break_current_location_resource_clump"] = (action, _) => CompileCurrentLocationResourceClumpStep(action),
                ["executor.till_soil"] = CompileTillSoilStep,
                ["executor.plant_seed"] = (action, _) => CompilePlantSeedStep(action),
                ["executor.harvest_crop"] = (action, _) => CompileHarvestCropStep(action),
                ["executor.harvest_giant_crop"] = (action, _) => CompileHarvestGiantCropStep(action),
                ["executor.pickup_debris"] = (action, _) => CompilePickupDebrisStep(action),
                ["executor.collect_spawned_object"] = (action, _) => CompileCollectSpawnedObjectStep(action),
                ["executor.harvest_ginger"] = (action, _) => CompileHarvestGingerStep(action),
                ["executor.harvest_bush"] = (action, _) => CompileHarvestBushStep(action),
                ["executor.claim_mine_reward_chest"] = (action, _) => CompileClaimMineRewardChestStep(action),
                ["executor.collect_crab_pot"] = (action, _) => CompileCollectCrabPotStep(action),
                ["executor.collect_fish_pond_output"] = (action, _) => CompileCollectFishPondOutputStep(action),
                ["executor.complete_fish_pond_request"] = (action, _) => CompileCompleteFishPondRequestStep(action),
                ["executor.collect_animal_product"] = (action, _) => CompileCollectAnimalProductStep(action),
                ["executor.pet_interact"] = (action, _) => CompilePetInteractStep(action),
                ["executor.fill_pet_bowl"] = (action, _) => CompileFillPetBowlStep(action),
                ["executor.donate_museum_item"] = (action, _) => CompileDonateMuseumItemStep(action),
                ["executor.pan_ore_spot"] = (action, _) => CompilePanOreSpotStep(action),
                ["executor.collect_machine_output"] = (action, _) => CompileCollectMachineOutputStep(action),
                ["executor.load_machine_input"] = (action, _) => CompileLoadMachineInputStep(action),
                ["executor.read_book"] = (action, _) => CompileReadBookStep(action),
                ["executor.catch_fish"] = (action, _) => CompileCatchFishStep(action),
                ["executor.cool_volcano_lava"] = (action, _) => CompileCoolVolcanoLavaStep(action),
                ["executor.break_volcano_stone"] = (action, _) => CompileVolcanoNativePrimitiveStep(action),
                ["executor.break_volcano_container"] = (action, _) => CompileVolcanoNativePrimitiveStep(action),
                ["executor.combat_volcano_monster"] = (action, _) => CompileVolcanoNativePrimitiveStep(action),
                ["executor.social_interact"] = (action, _) => CompileSocialInteractStep(action),
                ["executor.select_safe_item_slot"] = CompileSelectSafeItemSlotStep,
                ["executor.close_menu"] = (_, snapshot) => CompileCloseMenuStep(snapshot)
            };

        private static readonly IReadOnlyDictionary<string, ActionParameterCompiler> ActionParameterCompilers =
            new Dictionary<string, ActionParameterCompiler>(StringComparer.Ordinal)
            {
                ["exploration.visit_location"] = BuildRoutePreviewParameters,
                ["executor.traverse_connector"] = BuildTraverseConnectorParameters,
                ["executor.select_safe_item_slot"] = BuildSelectSafeItemSlotParameters,
                ["executor.close_menu"] = BuildCloseMenuParameters,
                ["mining.reach_depth"] = BuildMiningReachDepthParameters,
                ["mining.acquire_golden_scythe"] = BuildMiningGoldenScytheParameters,
                ["mining.obtain_skull_key"] = BuildMiningSkullKeyParameters,
                ["volcano.reach_caldera"] = BuildVolcanoReachCalderaParameters,
                ["recovery.stabilize_day"] = BuildRecoveryParameters,
                ["executor.buy_shop_item"] = BuildBuyShopItemParameters,
                ["social.talk_npc"] = BuildSocialParameters,
                ["social.gift_npc"] = BuildSocialParameters,
                ["farm.maintain_crops"] = BuildCropMaintenanceParameters
            };
    }
}
