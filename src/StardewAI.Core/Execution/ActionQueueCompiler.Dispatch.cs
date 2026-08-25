using System;
using System.Collections.Generic;
using System.Linq;
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
                ["recovery.stabilize_day"] = (_, snapshot) => CompileRecoverySteps(snapshot),
                ["executor.move_to_tile"] = (action, _) => CompileMoveToTileStep(action),
                ["executor.traverse_connector"] = (action, _) => CompileTraverseConnectorStep(action),
                ["executor.face_direction"] = (action, _) => CompileFaceDirectionStep(action),
                ["executor.interact"] = (action, _) => CompileInteractStep(action),
                ["executor.accept_daily_quest"] = (action, _) => CompileAcceptDailyQuestStep(action),
                ["executor.accept_special_order"] = (action, _) => CompileAcceptSpecialOrderStep(action),
                ["executor.claim_quest_reward"] = (action, _) => CompileClaimQuestRewardStep(action),
                ["executor.buy_shop_item"] = CompileBuyShopItemStep,
                ["executor.sell_shop_item"] = (action, _) => CompileSellShopItemStep(action),
                ["executor.choose_dialogue_response"] = (action, _) => CompileChooseDialogueResponseStep(action),
                ["executor.choose_animal_purchase_response"] = (action, _) => CompileChooseAnimalPurchaseResponseStep(action),
                ["executor.purchase_animal"] = (action, _) => CompilePurchaseAnimalStep(action),
                ["executor.manage_animal"] = (action, _) => CompileAnimalManagementStep(action),
                ["executor.cook_recipe"] = (action, _) => CompileCookRecipeStep(action),
                ["executor.forge_item"] = (action, _) => CompileForgeItemStep(action),
                ["executor.sleep"] = (action, snapshot) => CompileSleepSteps(snapshot, action),
                ["executor.wait_ticks"] = (action, _) => CompileWaitTicksStep(action),
                ["executor.clear_obstacle"] = (action, _) => CompileClearObstacleStep(action),
                ["executor.break_farm_resource_clump"] = (action, _) => CompileFarmResourceClumpStep(action),
                ["executor.break_current_location_resource_clump"] = (action, _) => CompileCurrentLocationResourceClumpStep(action),
                ["executor.water_crop"] = CompileWaterCropStep,
                ["executor.apply_fertilizer"] = (action, _) => CompileApplyFertilizerStep(action),
                ["executor.apply_tree_treatment"] = (action, _) => CompileApplyTreeTreatmentStep(action),
                ["executor.till_soil"] = CompileTillSoilStep,
                ["executor.plant_seed"] = (action, _) => CompilePlantSeedStep(action),
                ["executor.harvest_crop"] = (action, _) => CompileHarvestCropStep(action),
                ["executor.harvest_giant_crop"] = (action, _) => CompileHarvestGiantCropStep(action),
                ["executor.pickup_debris"] = CompilePickupDebrisStep,
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
                ["executor.donate_community_center_item"] = (action, _) => CompileDonateCommunityCenterItemStep(action),
                ["executor.purchase_joja_membership"] = (action, _) => CompilePurchaseJojaStep(action),
                ["executor.purchase_joja_project"] = (action, _) => CompilePurchaseJojaStep(action),
                ["executor.purchase_farmhouse_upgrade"] = (action, _) => CompilePurchaseFarmhouseUpgradeStep(action),
                ["executor.pan_ore_spot"] = (action, _) => CompilePanOreSpotStep(action),
                ["executor.collect_machine_output"] = (action, _) => CompileCollectMachineOutputStep(action),
                ["executor.load_machine_input"] = (action, _) => CompileLoadMachineInputStep(action),
                ["executor.name_hatched_animal"] = (action, _) => CompileNameHatchedAnimalStep(action),
                ["executor.craft_machine_item"] = (action, _) => CompileCraftMachineItemStep(action),
                ["executor.craft_storage_item"] = (action, _) => CompileCraftStorageItemStep(action),
                ["executor.craft_quest_item"] = (action, _) => CompileCraftQuestItemStep(action),
                ["executor.construct_building"] = (action, _) => CompileConstructBuildingStep(action),
                ["executor.change_building_skin"] = (action, _) => CompileBuildingAppearanceStep(action),
                ["executor.place_machine"] = (action, _) => CompilePlaceMachineStep(action),
                ["executor.remove_machine"] = (action, _) => CompileRemoveMachineStep(action),
                ["executor.place_storage"] = (action, _) => CompilePlaceStorageStep(action),
                ["executor.place_cookout_kit"] = (action, _) => CompilePlaceCookoutKitStep(action),
                ["executor.place_crab_pot"] = (action, _) => CompilePlaceCrabPotStep(action),
                ["executor.place_fence"] = (action, _) => CompilePlaceFenceStep(action),
                ["executor.place_flooring"] = (action, _) => CompilePlaceFlooringStep(action),
                ["executor.place_furniture"] = (action, _) => CompilePlaceFurnitureStep(action),
                ["executor.load_crab_pot_bait"] = (action, _) => CompileLoadCrabPotBaitStep(action),
                ["executor.read_book"] = (action, _) => CompileReadBookStep(action),
                ["executor.catch_fish"] = (action, _) => CompileCatchFishStep(action),
                ["executor.play_junimo_kart"] = (action, _) => CompilePlayJunimoKartStep(action),
                ["executor.cool_volcano_lava"] = (action, _) => CompileCoolVolcanoLavaStep(action),
                ["executor.break_volcano_stone"] = (action, _) => CompileVolcanoNativePrimitiveStep(action),
                ["executor.break_volcano_container"] = (action, _) => CompileVolcanoNativePrimitiveStep(action),
                ["executor.combat_volcano_monster"] = (action, _) => CompileVolcanoNativePrimitiveStep(action),
                ["executor.mine_stone"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.break_container"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.break_resource_clump"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.combat_monster"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.shoot_monster"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.place_bomb"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.place_staircase"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.consume_food"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.descend_ladder"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.descend_shaft"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.exit_mine"] = (action, _) => CompileMiningNativePrimitiveStep(action),
                ["executor.social_interact"] = (action, _) => CompileSocialInteractStep(action),
                ["executor.quest_npc_interact"] = (action, _) => CompileQuestNpcInteractStep(action),
                ["executor.quest_drop_box_donate"] = (action, _) => CompileQuestDropBoxDonateStep(action),
                ["executor.select_safe_item_slot"] = CompileSelectSafeItemSlotStep,
                ["executor.close_menu"] = (_, snapshot) => CompileCloseMenuStep(snapshot),
                ["executor.ship_inventory_item_to_bin"] = (action, _) => CompileShippingBinStep(action),
                ["executor.transfer_material"] = (action, _) => CompileMaterialTransferStep(action)
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
                ["social.advance_partnership"] = BuildSocialParameters,
                ["inventory.transfer_item"] = BuildMaterialTransferParameters,
                ["executor.transfer_material"] = BuildMaterialTransferParameters
            };

        public static IReadOnlyCollection<string> StepCompilerOptionIds =>
            ActionStepCompilers.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

        public static IReadOnlyCollection<string> ParameterCompilerOptionIds =>
            ActionParameterCompilers.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

        public static bool HasStepCompiler(string optionId)
        {
            return ActionStepCompilers.ContainsKey(optionId);
        }

        public static bool HasParameterCompiler(string optionId)
        {
            return ActionParameterCompilers.ContainsKey(optionId);
        }
    }
}
