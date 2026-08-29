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
        public const string NativeUnreachable = "native_unreachable";

        public static IReadOnlyList<QuestActionCoverageDeclaration> All { get; } =
            new ReadOnlyCollection<QuestActionCoverageDeclaration>(new[]
            {
                Row("ordinary_quest", "Quest", "basic_no_action", NativeObservationOnly, Array.Empty<string>(), "Quest has no objective-specific native action."),
                Row("ordinary_quest", "Quest", "weeding_no_subclass", NativeUnreachable, Array.Empty<string>(), "Vanilla 1.6.15 retains the type_weeding=11 compatibility constant, but Data/Quests has no such row, Quest.getQuestFromId has no factory branch, and native quest sources never assign questType 11."),
                Row("ordinary_quest", "CraftingQuest", "craft_item", Bound, new[] { "craft_quest_item" }),
                Row("ordinary_quest", "ItemDeliveryQuest", "deliver_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "SlayMonsterQuest", "slay_monsters", Bound, new[] { "mining_slay_monsters_plan_envelope" }),
                Row("ordinary_quest", "SlayMonsterQuest", "return_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "SocializeQuest", "greet_npcs", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "SocializeQuest", "socialize_complete", NativeObservationOnly, Array.Empty<string>()),
                Row("ordinary_quest", "GoSomewhereQuest", "go_to_location", Bound, new[] { "route_connector_tile" }),
                Row("ordinary_quest", "FishingQuest", "fish_for_item", Bound, new[] { "catch_fish" }),
                Row("ordinary_quest", "FishingQuest", "return_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "HaveBuildingQuest", "construct_building", Bound, new[] { "construct_quest_building", "route_connector_tile", "recovery_refresh_plan", "recovery_return_home", "recovery_sleep_immediately", "recovery_resume_sleep_prompt" }),
                Row("ordinary_quest", "ItemHarvestQuest", "harvest_items", Bound, new[] { "harvest_crop_tile" }),
                Row("ordinary_quest", "ResourceCollectionQuest", "collect_resources", Bound, new[] { "collect_spawned_object", "pickup_debris_item", "clear_obstacle_tile", "harvest_bush", "harvest_ginger", "harvest_fruit_tree", "harvest_tree_product", "rummage_garbage", "harvest_crop_tile", "harvest_giant_crop_tile", "clear_green_rain_resource_clump", "catch_fish", "collect_machine_output_tile", "load_machine_input_tile", "mining_collect_quest_resource_plan_envelope" }),
                Row("ordinary_quest", "ResourceCollectionQuest", "return_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "LostItemQuest", "find_lost_item", Bound, new[] { "collect_spawned_object", "route_connector_tile" }),
                Row("ordinary_quest", "LostItemQuest", "return_lost_item_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("ordinary_quest", "SecretLostItemQuest", "find_secret_lost_item", NativeObservationOnly, Array.Empty<string>(), "Vanilla quests 128/129 are created inside Railroad.getFish after the necklace catch has already begun; acquisition is owned by the existing fishing transaction, and the not-found quest row is only its transient observation."),
                Row("ordinary_quest", "SecretLostItemQuest", "return_secret_lost_item_to_npc", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("special_order", "CollectObjective", "collect_items", Bound, new[] { "harvest_crop_tile", "harvest_giant_crop_tile", "pickup_debris_item", "harvest_bush", "harvest_ginger", "harvest_fruit_tree", "harvest_tree_product", "rummage_garbage", "clear_green_rain_resource_clump", "catch_fish", "collect_machine_output_tile", "load_machine_input_tile", "mining_collect_quest_resource_plan_envelope" }),
                Row("special_order", "DeliverObjective", "deliver_to_target", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("special_order", "DonateObjective", "donate_items", Bound, new[] { "quest_drop_box_donation", "route_connector_tile" }),
                Row("special_order", "FishObjective", "catch_fish", Bound, new[] { "catch_fish" }),
                Row("special_order", "GiftObjective", "give_gifts", Bound, new[] { "quest_npc_interaction", "route_connector_tile" }),
                Row("special_order", "JKScoreObjective", "achieve_junimo_kart_score", Bound, new[] { "play_junimo_kart", "route_connector_tile" }),
                Row("special_order", "ReachMineFloorObjective", "reach_mine_floor", Bound, new[] { "mining_reach_depth_plan_envelope" }),
                Row("special_order", "ShipObjective", "ship_items", Bound, new[] { "ship_inventory_item_to_bin" }),
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

        public static IReadOnlyList<string> BoundCandidateKinds { get; } =
            new ReadOnlyCollection<string>(All
                .Where(row => row.BindingStatus == Bound)
                .SelectMany(row => row.CandidateKinds)
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
                    row.BindingStatus != NativeObservationOnly &&
                    row.BindingStatus != NativeUnreachable))
            {
                throw new InvalidOperationException("Unknown quest action binding status.");
            }
            if (All.Any(row => row.BindingStatus == Bound && row.CandidateKinds.Length == 0))
            {
                throw new InvalidOperationException("Bound quest action coverage requires candidate kinds.");
            }
            if (All.Any(row => (row.BindingStatus == Blocked || row.BindingStatus == NativeUnreachable) &&
                    string.IsNullOrWhiteSpace(row.GapReason)))
            {
                throw new InvalidOperationException("Blocked or unreachable quest action coverage requires a typed reason.");
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
