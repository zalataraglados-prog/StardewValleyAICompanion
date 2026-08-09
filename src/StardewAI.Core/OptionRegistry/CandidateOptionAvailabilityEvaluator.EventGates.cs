using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static string[] SocialCandidateGateBlockingReasons(string optionId, EventCandidate[] socialCandidates, bool hasBoundParameters)
        {
            if (optionId != "social.talk_npc" &&
                optionId != "social.gift_npc" &&
                optionId != "social.advance_partnership")
            {
                return Array.Empty<string>();
            }

            if (hasBoundParameters)
            {
                return Array.Empty<string>();
            }

            return EventCandidateAvailabilityReasons(
                socialCandidates,
                "no_social_current_state_candidates",
                "no_available_social_current_state_candidates");
        }

        private static bool IsUnboundSocialCandidate(OptionAvailabilityCandidate candidate)
        {
            return candidate.Parameters.Length == 0 &&
                (candidate.OptionId == "social.talk_npc" ||
                    candidate.OptionId == "social.gift_npc" ||
                    candidate.OptionId == "social.advance_partnership");
        }

        private static bool IsSocialContinuationCandidate(OptionAvailabilityCandidate candidate)
        {
            return (candidate.OptionId == "social.talk_npc" ||
                    candidate.OptionId == "social.gift_npc" ||
                    candidate.OptionId == "social.advance_partnership") &&
                !string.IsNullOrWhiteSpace(ReadParameter(candidate.Parameters, "continuation.npc_name")) &&
                !string.IsNullOrWhiteSpace(ReadParameter(candidate.Parameters, "continuation.target_location"));
        }

        private static string[] EventCandidateGateBlockingReasons(string optionId, EventCandidate[] eventCandidates, bool hasBoundParameters)
        {
            if (optionId == "inventory.transfer_item")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "material_transfer_intent_required",
                    "no_available_material_transfer_candidate");
            }

            if (optionId == "mining.reach_depth")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_mining_reach_depth_candidates",
                    "no_available_mining_reach_depth_candidates");
            }

            if (optionId == "mining.acquire_golden_scythe")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_mining_acquire_golden_scythe_candidates",
                    "no_available_mining_acquire_golden_scythe_candidates");
            }

            if (optionId == "mining.obtain_skull_key")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_mining_obtain_skull_key_candidates",
                    "no_available_mining_obtain_skull_key_candidates");
            }

            if (optionId == "mining.claim_reward_chests")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_mine_reward_chest_candidates",
                    "no_available_mine_reward_chest_candidates");
            }

            if (optionId == "volcano.reach_caldera")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_volcano_reach_caldera_candidates",
                    "no_available_volcano_reach_caldera_candidates");
            }

            if (optionId == "recovery.stabilize_day")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_recovery_candidates",
                    "no_available_recovery_candidates");
            }

            if (optionId == "foraging.collect_spawned_objects")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_spawned_object_candidates",
                    "no_available_spawned_object_candidates");
            }

            if (optionId == "foraging.harvest_ginger")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_ginger_harvest_candidates",
                    "no_available_ginger_harvest_candidates");
            }

            if (optionId == "foraging.harvest_bushes")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_bush_harvest_candidates",
                    "no_available_bush_harvest_candidates");
            }

            if (optionId == "foraging.pan_ore_spot")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_active_ore_pan_candidate",
                    "no_available_ore_pan_candidate");
            }

            if (optionId == "fishing.collect_crab_pots")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_crab_pot_candidates",
                    "no_available_crab_pot_candidates");
            }

            if (optionId == "fishing.service_fish_ponds")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_fish_pond_candidates",
                    "no_available_fish_pond_candidates");
            }

            if (optionId == "farm.collect_animal_products")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_animal_product_candidates",
                    "no_available_animal_product_candidates");
            }

            if (optionId == "farm.process_machines")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_machine_processing_candidates",
                    "no_available_machine_processing_candidates");
            }

            if (optionId == "farm.collect_machine_outputs")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_machine_output_candidates",
                    "no_available_machine_output_candidates");
            }

            if (optionId == "farm.load_supported_machine_input")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_supported_machine_input_candidates",
                    "no_available_supported_machine_input_candidates");
            }

            if (optionId == "farm.establish_supported_machine_capacity")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_supported_machine_capacity_lifecycle_candidates",
                    "no_available_supported_machine_capacity_lifecycle_candidates");
            }

            if (optionId == "farm.fulfill_machine_task_demand")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_machine_task_demand_candidates",
                    "no_available_machine_task_demand_candidates");
            }

            if (optionId == "farm.care_for_pets")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_pet_care_candidates",
                    "no_available_pet_care_candidates");
            }

            if (optionId == "skills.read_books")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_inventory_book_candidates",
                    "no_available_inventory_book_candidates");
            }

            if (hasBoundParameters)
            {
                return Array.Empty<string>();
            }

            if (optionId == "executor.interact")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_interact_endpoint_candidates",
                    "no_available_interact_endpoint_candidates");
            }

            if (optionId == "exploration.visit_location")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_route_connector_candidates",
                    "no_available_route_connector_candidates");
            }

            if (optionId == "executor.clear_obstacle")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_clear_obstacle_candidates",
                    "no_available_clear_obstacle_candidates");
            }

            if (optionId == "executor.plant_seed")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_plant_seed_candidates",
                    "no_available_plant_seed_candidates");
            }

            if (optionId == "fishing.catch_fish")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_fishing_candidates",
                    "no_available_fishing_candidates");
            }
            if (optionId == "quest.advance")
            {
                return QuestCandidateGateBlockingReasons(eventCandidates);
            }

            return Array.Empty<string>();
        }

        private static string[] QuestCandidateGateBlockingReasons(EventCandidate[] eventCandidates)
        {
            if (eventCandidates.Length == 0)
            {
                return new[] { "no_quest_current_state_candidates" };
            }

            if (eventCandidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            return eventCandidates
                .SelectMany(candidate => candidate.BlockReasons)
                .Concat(new[] { "no_available_quest_current_state_candidates" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] EventCandidateAvailabilityReasons(EventCandidate[] eventCandidates, string emptyReason, string noneAvailableReason)
        {
            if (eventCandidates.Length == 0)
            {
                return new[] { emptyReason };
            }

            if (eventCandidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            return eventCandidates
                .SelectMany(candidate => candidate.BlockReasons)
                .Concat(new[] { noneAvailableReason })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private EventCandidate[] EventCandidates(
            SnapshotEnvelope snapshot,
            string optionId,
            string[] missingStateFactors,
            SmallModelActionParameter[] parameters,
            StrategyCommitmentLedger? commitmentLedger)
        {
            if (missingStateFactors.Length > 0)
            {
                return Array.Empty<EventCandidate>();
            }

            if (string.Equals(optionId, "farm.process_machines", StringComparison.Ordinal))
            {
                return MachineProcessingCandidates(snapshot, commitmentLedger);
            }

            if (string.Equals(optionId, "farm.collect_machine_outputs", StringComparison.Ordinal))
            {
                return MachineOutputCollectionCandidates(snapshot, commitmentLedger);
            }

            if (string.Equals(optionId, "farm.load_supported_machine_input", StringComparison.Ordinal))
            {
                return SupportedMachineInputCandidates(snapshot, commitmentLedger);
            }

            if (string.Equals(optionId, "farm.establish_supported_machine_capacity", StringComparison.Ordinal))
            {
                return SupportedMachineCapacityLifecycleCandidates(snapshot, commitmentLedger);
            }

            if (string.Equals(optionId, "farm.fulfill_machine_task_demand", StringComparison.Ordinal))
            {
                return TaskMachineDemandCandidates(snapshot, commitmentLedger);
            }

            return eventCandidateProviders.TryGetValue(optionId, out var provider)
                ? provider(snapshot, parameters)
                : Array.Empty<EventCandidate>();
        }

    }
}
