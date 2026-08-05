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
                ["farm.care_for_pets"] = (snapshot, _) => PetCareCandidates(snapshot),
                ["museum.donate_items"] = (snapshot, _) => MuseumDonationCandidates(snapshot),
                ["community_center.donate_bundle_items"] = (snapshot, _) => CommunityCenterDonationCandidates(snapshot),
                ["joja.advance_development"] = (snapshot, _) => JojaDevelopmentCandidates(snapshot),
                ["housing.advance_farmhouse"] = (snapshot, _) => FarmhouseUpgradeCandidates(snapshot),
                ["skills.read_books"] = (snapshot, _) => BookReadCandidates(snapshot),
                ["executor.clear_obstacle"] = (snapshot, _) => ClearObstacleCandidates(snapshot),
                ["executor.plant_seed"] = (snapshot, _) => PlantSeedCandidates(snapshot),
                ["exploration.visit_location"] = (snapshot, _) => RouteConnectorCandidates(snapshot),
                ["executor.interact"] = (snapshot, _) => InteractEndpointCandidates(snapshot),
                ["recovery.stabilize_day"] = (snapshot, _) => RecoveryCandidates(snapshot),
                ["fishing.catch_fish"] = (snapshot, _) => FishingEventCandidateBuilder.Build(snapshot),
                ["fishing.collect_crab_pots"] = (snapshot, _) => CrabPotCollectCandidates(snapshot),
                ["fishing.service_fish_ponds"] = (snapshot, _) => FishPondServiceCandidates(snapshot),
                ["foraging.collect_spawned_objects"] = (snapshot, _) => SpawnedObjectForagingCandidates(snapshot),
                ["foraging.harvest_ginger"] = (snapshot, _) => GingerHarvestCandidates(snapshot),
                ["foraging.harvest_bushes"] = (snapshot, _) => BushHarvestCandidates(snapshot),
                ["foraging.clear_green_rain_bushes"] = (snapshot, _) => GreenRainResourceClumpCandidates(snapshot),
                ["foraging.pan_ore_spot"] = (snapshot, _) => PanningCandidates(snapshot),
                ["mining.reach_depth"] = MiningReachDepthCandidateBuilder.Build,
                ["mining.acquire_golden_scythe"] = MiningGoldenScytheCandidateBuilder.Build,
                ["mining.obtain_skull_key"] = MiningSkullKeyCandidateBuilder.Build,
                ["mining.claim_reward_chests"] = (snapshot, _) => MineRewardChestCandidates(snapshot),
                ["volcano.reach_caldera"] = VolcanoReachCalderaCandidateBuilder.Build,
                ["quest.advance"] = (snapshot, _) => QuestCandidates(snapshot),
                ["economy.buy_supplies"] = BuySupplyStageCandidates,
                ["economy.ship_items"] = (snapshot, _) => ShipCandidates(snapshot),
                ["inventory.transfer_item"] = MaterialTransferCandidates
            };
        }
    }
}
