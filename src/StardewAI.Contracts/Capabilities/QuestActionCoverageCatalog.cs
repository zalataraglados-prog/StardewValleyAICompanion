using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StardewAI.Contracts.Capabilities
{
    public sealed class QuestActionCoverageDeclaration
    {
        public string Family { get; internal set; } = string.Empty;

        public string RuntimeType { get; internal set; } = string.Empty;

        public string ActionStage { get; internal set; } = string.Empty;

        public string BindingStatus { get; internal set; } = string.Empty;

        public string[] CandidateKinds { get; internal set; } = Array.Empty<string>();

        public string Evidence { get; internal set; } = string.Empty;

        public string GapReason { get; internal set; } = string.Empty;
    }

    public static class QuestActionCoverageCatalog
    {
        public const string Bound = "bound";
        public const string Blocked = "blocked";
        public const string NativeObservationOnly = "native_observation_only";

        public static IReadOnlyList<QuestActionCoverageDeclaration> All { get; } =
            new ReadOnlyCollection<QuestActionCoverageDeclaration>(new[]
            {
                Row("ordinary_quest", "Quest", "basic_no_action", NativeObservationOnly, Array.Empty<string>(), "Quest has no objective-specific native action."),
                Row("ordinary_quest", "Quest", "weeding_no_subclass", Blocked, Array.Empty<string>(), "Quest type 11 has no objective-specific transparent binding."),
                Row("ordinary_quest", "CraftingQuest", "craft_item", Blocked, Array.Empty<string>(), "General recipe crafting terminal is not bound."),
                Row("ordinary_quest", "ItemDeliveryQuest", "deliver_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "SlayMonsterQuest", "slay_monsters", Bound, new[] { "mining_slay_monsters_plan_envelope" }),
                Row("ordinary_quest", "SlayMonsterQuest", "return_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "SocializeQuest", "greet_npcs", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "SocializeQuest", "socialize_complete", NativeObservationOnly, Array.Empty<string>()),
                Row("ordinary_quest", "GoSomewhereQuest", "go_to_location", Bound, new[] { "route_connector_tile" }),
                Row("ordinary_quest", "FishingQuest", "fish_for_item", Bound, new[] { "catch_fish" }),
                Row("ordinary_quest", "FishingQuest", "return_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "HaveBuildingQuest", "construct_building", Blocked, Array.Empty<string>(), "Quest-specific construction purchase is not bound."),
                Row("ordinary_quest", "ItemHarvestQuest", "harvest_items", Bound, new[] { "harvest_crop_tile" }),
                Row("ordinary_quest", "ResourceCollectionQuest", "collect_resources", Bound, new[] { "collect_spawned_object", "pickup_debris_item", "clear_obstacle_tile", "harvest_bush", "harvest_ginger", "collect_machine_output_tile", "mining_collect_quest_resource_plan_envelope" }),
                Row("ordinary_quest", "ResourceCollectionQuest", "return_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "LostItemQuest", "find_lost_item", Bound, new[] { "collect_spawned_object", "route_connector_tile" }),
                Row("ordinary_quest", "LostItemQuest", "return_lost_item_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "SecretLostItemQuest", "find_secret_lost_item", Blocked, Array.Empty<string>(), "Secret item source is event-specific and not projected as a quest source."),
                Row("ordinary_quest", "SecretLostItemQuest", "return_secret_lost_item_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("special_order", "CollectObjective", "collect_items", Bound, new[] { "harvest_crop_tile", "pickup_debris_item", "harvest_bush", "harvest_ginger", "collect_machine_output_tile" }),
                Row("special_order", "DeliverObjective", "deliver_to_target", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("special_order", "DonateObjective", "donate_items", Bound, new[] { "quest_drop_box_donation", "route_connector_tile" }),
                Row("special_order", "FishObjective", "catch_fish", Bound, new[] { "catch_fish" }),
                Row("special_order", "GiftObjective", "give_gifts", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("special_order", "JKScoreObjective", "achieve_junimo_kart_score", Blocked, Array.Empty<string>(), "Junimo Kart play is not implemented."),
                Row("special_order", "ReachMineFloorObjective", "reach_mine_floor", Bound, new[] { "reach_mine_depth" }),
                Row("special_order", "ShipObjective", "ship_items", Bound, new[] { "ship_inventory_item" }),
                Row("special_order", "SlayObjective", "slay_monsters", Bound, new[] { "mining_slay_monsters_plan_envelope" })
            });

        public static IReadOnlyList<string> OrdinaryRuntimeTypes { get; } =
            new ReadOnlyCollection<string>(All
                .Where(row => row.Family == "ordinary_quest")
                .Select(row => row.RuntimeType)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        public static IReadOnlyList<string> SpecialOrderObjectiveRuntimeTypes { get; } =
            new ReadOnlyCollection<string>(All
                .Where(row => row.Family == "special_order")
                .Select(row => row.RuntimeType)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        static QuestActionCoverageCatalog()
        {
            var duplicate = All
                .GroupBy(row => row.Family + "\0" + row.RuntimeType + "\0" + row.ActionStage, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
            {
                throw new InvalidOperationException("Duplicate quest action coverage declaration: " + duplicate.Key);
            }
            if (All.Any(row => row.BindingStatus != Bound &&
                    row.BindingStatus != Blocked &&
                    row.BindingStatus != NativeObservationOnly))
            {
                throw new InvalidOperationException("Unknown quest action binding status.");
            }
            if (All.Any(row => row.BindingStatus == Bound && row.CandidateKinds.Length == 0))
            {
                throw new InvalidOperationException("Bound quest action coverage requires candidate kinds.");
            }
            if (All.Any(row => row.BindingStatus == Blocked && string.IsNullOrWhiteSpace(row.GapReason)))
            {
                throw new InvalidOperationException("Blocked quest action coverage requires a typed gap reason.");
            }
        }

        private static QuestActionCoverageDeclaration Row(
            string family,
            string runtimeType,
            string actionStage,
            string bindingStatus,
            string[] candidateKinds,
            string gapReason = "")
        {
            return new QuestActionCoverageDeclaration
            {
                Family = family,
                RuntimeType = runtimeType,
                ActionStage = actionStage,
                BindingStatus = bindingStatus,
                CandidateKinds = candidateKinds,
                Evidence = "Stardew Valley 1.6.15 native quest/objective decompile plus typed candidate binding",
                GapReason = gapReason
            };
        }
    }
}
