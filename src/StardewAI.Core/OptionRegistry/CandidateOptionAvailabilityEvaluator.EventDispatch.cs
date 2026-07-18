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
                ["farm.process_machines"] = (snapshot, _) => MachineProcessingCandidates(snapshot),
                ["executor.clear_obstacle"] = (snapshot, _) => ClearObstacleCandidates(snapshot),
                ["executor.plant_seed"] = (snapshot, _) => PlantSeedCandidates(snapshot),
                ["exploration.visit_location"] = (snapshot, _) => RouteConnectorCandidates(snapshot),
                ["executor.interact"] = (snapshot, _) => InteractEndpointCandidates(snapshot),
                ["recovery.stabilize_day"] = (snapshot, _) => RecoveryCandidates(snapshot),
                ["fishing.catch_fish"] = (snapshot, _) => FishingEventCandidateBuilder.Build(snapshot),
                ["fishing.collect_crab_pots"] = (snapshot, _) => CrabPotCollectCandidates(snapshot),
                ["foraging.collect_spawned_objects"] = (snapshot, _) => SpawnedObjectForagingCandidates(snapshot),
                ["mining.reach_depth"] = MiningReachDepthCandidateBuilder.Build,
                ["mining.acquire_golden_scythe"] = MiningGoldenScytheCandidateBuilder.Build,
                ["mining.obtain_skull_key"] = MiningSkullKeyCandidateBuilder.Build,
                ["volcano.reach_caldera"] = VolcanoReachCalderaCandidateBuilder.Build,
                ["quest.advance"] = (snapshot, _) => QuestCandidates(snapshot),
                ["economy.ship_items"] = (snapshot, _) => ShipCandidates(snapshot)
            };
        }
    }
}
