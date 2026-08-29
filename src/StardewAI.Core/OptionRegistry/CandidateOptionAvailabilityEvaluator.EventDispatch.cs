using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private IReadOnlyDictionary<string, Func<SnapshotEnvelope, SmallModelActionParameter[], EventCandidate[]>> CreateEventCandidateProviders()
        {
            return new Dictionary<string, Func<SnapshotEnvelope, SmallModelActionParameter[], EventCandidate[]>>(StringComparer.Ordinal)
            {
                ["farm.maintain_crops"] = (snapshot, _) => FarmMaintenanceCandidates(snapshot),
                ["farm.collect_animal_products"] = (snapshot, _) => AnimalProductCandidates(snapshot),
                ["animals.purchase"] = AnimalPurchaseStageCandidates,
                ["animals.manage_animal"] = AnimalManagementCandidates,
                ["crafting.cook_recipe"] = CookingCandidates,
                ["crafting.forge_item"] = ForgeCandidates,
                ["farm.care_for_pets"] = (snapshot, _) => PetCareCandidates(snapshot),
                ["museum.donate_items"] = (snapshot, _) => MuseumDonationCandidates(snapshot),
                ["festival.manage_grange_display"] = (snapshot, _) => GrangeDisplayCandidates(snapshot),
                ["festival.play_fishing_game"] = (snapshot, _) => FairFishingGameCandidates(snapshot),
                ["festival.play_slingshot_game"] = (snapshot, _) => FairSlingshotGameCandidates(snapshot),
                ["festival.play_strength_game"] = (snapshot, _) => FairStrengthGameCandidates(snapshot),
                ["festival.spin_wheel"] = (snapshot, _) => FairWheelSpinCandidates(snapshot),
                ["community_center.donate_bundle_items"] = (snapshot, _) => CommunityCenterDonationCandidates(snapshot),
                ["joja.advance_development"] = (snapshot, _) => JojaDevelopmentCandidates(snapshot),
                ["quest.accept_daily"] = (snapshot, _) => DailyQuestAcceptanceCandidates(snapshot),
                ["quest.accept_special_order"] = (snapshot, _) => SpecialOrderAcceptanceCandidates(snapshot),
                ["quest.claim_reward"] = (snapshot, _) => QuestRewardClaimCandidates(snapshot),
                ["housing.advance_farmhouse"] = (snapshot, _) => FarmhouseUpgradeCandidates(snapshot),
                ["skills.read_books"] = (snapshot, _) => BookReadCandidates(snapshot),
                ["skills.choose_profession"] = (snapshot, _) => ProfessionChoiceCandidates(snapshot),
                ["mail.process_letter"] = (snapshot, _) => MailProcessingCandidates(snapshot),
                ["executor.clear_obstacle"] = (snapshot, _) => ClearObstacleCandidates(snapshot),
                ["executor.plant_seed"] = (snapshot, _) => PlantSeedCandidates(snapshot),
                ["exploration.visit_location"] = (snapshot, _) => RouteConnectorCandidates(snapshot),
                ["executor.interact"] = (snapshot, _) => InteractEndpointCandidates(snapshot),
                ["recovery.stabilize_day"] = (snapshot, _) => RecoveryCandidates(snapshot),
                ["recovery.escape_object_trap"] = (snapshot, _) => ObjectTrapRecoveryCandidates(snapshot),
                ["fishing.catch_fish"] = (snapshot, _) => FishingEventCandidateBuilder.Build(snapshot),
                ["fishing.collect_crab_pots"] = (snapshot, _) => CrabPotCollectCandidates(snapshot),
                ["fishing.service_fish_ponds"] = (snapshot, _) => FishPondServiceCandidates(snapshot),
                ["foraging.collect_spawned_objects"] = (snapshot, _) => SpawnedObjectForagingCandidates(snapshot),
                ["foraging.harvest_ginger"] = (snapshot, _) => GingerHarvestCandidates(snapshot),
                ["foraging.harvest_bushes"] = (snapshot, _) => BushHarvestCandidates(snapshot),
                ["foraging.clear_green_rain_bushes"] = (snapshot, _) => GreenRainResourceClumpCandidates(snapshot),
                ["foraging.pan_ore_spot"] = (snapshot, _) => PanningCandidates(snapshot),
                ["mining.reach_depth"] = MiningReachDepthCandidates,
                ["mining.use_elevator"] = MiningElevatorCandidates,
                ["mining.acquire_golden_scythe"] = MiningGoldenScytheCandidateBuilder.Build,
                ["mining.obtain_skull_key"] = MiningSkullKeyCandidateBuilder.Build,
                ["mining.claim_reward_chests"] = (snapshot, _) => MineRewardChestCandidates(snapshot),
                ["rewards.claim_pot_of_gold"] = (snapshot, _) => PotOfGoldCandidates(snapshot),
                ["mining.choose_dwarf_statue_power"] = (snapshot, _) => DwarfKingStatuePowerCandidates(snapshot),
                ["rewards.claim_statue_blessing"] = (snapshot, _) => StatueBlessingCandidates(snapshot),
                ["world.rotate_house_plant"] = (snapshot, _) => HousePlantRotationCandidates(snapshot),
                ["world.play_singing_stone"] = (snapshot, _) => SingingStoneCandidates(snapshot),
                ["world.tune_flute_block"] = (snapshot, _) => FluteBlockCandidates(snapshot),
                ["world.tune_drum_block"] = (snapshot, _) => DrumBlockCandidates(snapshot),
                ["farming.read_farm_computer_report"] = (snapshot, _) => FarmComputerReportCandidates(snapshot),
                ["farming.collect_slime_ball"] = (snapshot, _) => SlimeBallCollectionCandidates(snapshot),
                ["animals.withdraw_feed_hopper_hay"] = (snapshot, _) => FeedHopperWithdrawalCandidates(snapshot),
                ["animals.collect_auto_grabber_contents"] = (snapshot, _) => AutoGrabberCollectionCandidates(snapshot),
                ["movement.use_mini_obelisk"] = (snapshot, _) => MiniObeliskCandidates(snapshot),
                ["volcano.reach_caldera"] = VolcanoReachCalderaCandidateBuilder.Build,
                ["economy.buy_supplies"] = BuySupplyStageCandidates,
                ["economy.sell_items"] = SellItemStageCandidates,
                ["economy.ship_items"] = ShipItemStageCandidates,
                ["inventory.transfer_item"] = MaterialTransferCandidates
            };
        }
    }
}
