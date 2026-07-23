using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] BindQuestCandidates(
            SnapshotEnvelope snapshot,
            QuestProgressRef[] quests,
            SpecialOrderProgressRef[] orders,
            QuestCandidateRef[] ordinaryCandidates,
            QuestCandidateRef[] specialOrderCandidates)
        {
            var results = new List<EventCandidate>();
            foreach (var candidate in ordinaryCandidates)
            {
                var quest = quests.FirstOrDefault(row =>
                    string.Equals(row.Id, candidate.QuestId, StringComparison.Ordinal) &&
                    string.Equals(row.RuntimeType, candidate.RuntimeType, StringComparison.Ordinal));
                results.AddRange(BindOrdinaryQuestCandidate(snapshot, candidate, quest));
            }

            foreach (var candidate in specialOrderCandidates)
            {
                var order = orders.FirstOrDefault(row =>
                    string.Equals(row.QuestKey, candidate.QuestKey, StringComparison.Ordinal));
                results.AddRange(BindSpecialOrderCandidate(snapshot, candidate, order));
            }

            return results.ToArray();
        }

        private IEnumerable<EventCandidate> BindOrdinaryQuestCandidate(
            SnapshotEnvelope snapshot,
            QuestCandidateRef candidate,
            QuestProgressRef? quest)
        {
            if (quest is null)
            {
                return new[] { BlockedQuestCandidate(snapshot, candidate, "quest_live_row_not_found") };
            }

            switch (candidate.NextActionCategory)
            {
                case "fish_for_item":
                    return BindExactFishingCandidate(snapshot, candidate);
                case "go_to_location":
                    return new[] { BindQuestLocationRoute(snapshot, candidate, candidate.RequiredTargetLocation) };
                case "deliver_to_npc":
                    return new[] { BindQuestNpcCandidate(snapshot, candidate, candidate.RequiredTargetNpc, "offer_item", candidate.RequiredItemId, null, candidate.RequiredTargetCount) };
                case "return_to_npc":
                    return new[] { BindQuestNpcCandidate(snapshot, candidate, candidate.RequiredTargetNpc, "report", string.Empty, null) };
                case "return_lost_item_to_npc":
                case "return_secret_lost_item_to_npc":
                    if (!FindQuestInventoryItem(snapshot, candidate.RequiredItemId, null, 1).HasValue)
                    {
                        return new[] { BlockedQuestCandidate(snapshot, candidate, "quest_lost_item_not_in_inventory") };
                    }
                    return new[] { BindQuestNpcCandidate(snapshot, candidate, candidate.RequiredTargetNpc, "report", string.Empty, null) };
                case "greet_npcs":
                    var targetNpc = quest.PerTypeFields?.WhoToGreet?.FirstOrDefault() ?? string.Empty;
                    return new[] { BindQuestNpcCandidate(snapshot, candidate, targetNpc, "report", string.Empty, null) };
                default:
                    return new[] { BlockedQuestCandidate(snapshot, candidate, "quest_objective_binding_not_executable:" + candidate.NextActionCategory) };
            }
        }

        private IEnumerable<EventCandidate> BindSpecialOrderCandidate(
            SnapshotEnvelope snapshot,
            QuestCandidateRef candidate,
            SpecialOrderProgressRef? order)
        {
            if (order is null ||
                candidate.SelectedObjectiveIndex < 0 ||
                candidate.SelectedObjectiveIndex >= order.Objectives.Length)
            {
                return new[] { BlockedQuestCandidate(snapshot, candidate, "special_order_selected_objective_not_found") };
            }

            var objective = order.Objectives[candidate.SelectedObjectiveIndex];
            var fields = objective.PerTypeFields;
            if (fields is null || !fields.Available)
            {
                return new[]
                {
                    BlockedQuestCandidate(
                        snapshot,
                        candidate,
                        "special_order_objective_fields_unavailable:" + (fields?.UnavailableReason ?? "missing"))
                };
            }
            switch (objective.RuntimeType)
            {
                case "DeliverObjective":
                    return new[]
                    {
                        BindQuestNpcCandidate(
                            snapshot,
                            candidate,
                            fields.TargetName,
                            "offer_item",
                            string.Empty,
                            fields.AcceptableContextTagSets,
                            Math.Max(1, candidate.RequiredTargetCount - candidate.CurrentProgressCount))
                    };
                case "ShipObjective":
                    return BindSpecialOrderShippingCandidates(snapshot, candidate, fields.AcceptableContextTagSets);
                case "ReachMineFloorObjective":
                    return BindSpecialOrderMineDepthCandidates(snapshot, candidate, fields.SkullCave);
                default:
                    return new[]
                    {
                        BlockedQuestCandidate(
                            snapshot,
                            candidate,
                            "special_order_objective_binding_not_executable:" + objective.RuntimeType)
                    };
            }
        }

        private IEnumerable<EventCandidate> BindExactFishingCandidate(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest)
        {
            var candidates = FishingEventCandidateBuilder.Build(snapshot)
                .Where(candidate => candidate.Kind == "catch_fish")
                .Where(candidate => ItemIdentityMatches(candidate.ItemId, candidate.QualifiedItemId, quest.RequiredItemId))
                .Select(candidate => AttachQuest(candidate, quest))
                .ToArray();
            return candidates.Length > 0
                ? candidates
                : new[] { BlockedQuestCandidate(snapshot, quest, "quest_required_fish_not_available_in_current_fishing_projection") };
        }

        private EventCandidate BindQuestLocationRoute(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest,
            string targetLocation)
        {
            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (string.IsNullOrWhiteSpace(targetLocation))
            {
                return BlockedQuestCandidate(snapshot, quest, "quest_target_location_missing");
            }
            if (string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            {
                return BlockedQuestCandidate(snapshot, quest, "quest_target_location_already_reached_requires_fresh_native_quest_snapshot");
            }

            var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue)
                .Where(candidate => candidate.Kind == "route_connector_tile")
                .ToArray();
            var plan = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation, routeCandidates);
            if (plan?.FirstConnectorCandidate is null)
            {
                return BlockedQuestCandidate(snapshot, quest, "quest_target_location_route_unavailable:" + targetLocation);
            }

            return AttachQuest(
                plan.FirstConnectorCandidate,
                quest,
                new[]
                {
                    Parameter("quest_target_location", targetLocation),
                    Parameter("quest_route_remaining_connector_count", plan.Path.Length.ToString(CultureInfo.InvariantCulture))
                });
        }

        private EventCandidate BindQuestNpcCandidate(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest,
            string npcName,
            string interactionKind,
            string exactItemId,
            string[]? contextTagSets,
            int requiredStack = 1)
        {
            if (string.IsNullOrWhiteSpace(npcName))
            {
                return BlockedQuestCandidate(snapshot, quest, "quest_target_npc_missing");
            }

            JsonElement? inventoryItem = null;
            if (interactionKind == "offer_item")
            {
                inventoryItem = FindQuestInventoryItem(snapshot, exactItemId, contextTagSets, requiredStack);
                if (!inventoryItem.HasValue)
                {
                    return BlockedQuestCandidate(snapshot, quest, "quest_matching_inventory_item_not_available");
                }
            }

            var social = SocialCandidates(
                    snapshot,
                    "social.talk_npc",
                    Array.Empty<string>(),
                    Array.Empty<SmallModelActionParameter>())
                .FirstOrDefault(candidate =>
                    string.Equals(ReadParameter(candidate.Parameters, "npc_name"), npcName, StringComparison.OrdinalIgnoreCase));
            if (social is null)
            {
                return BlockedQuestCandidate(snapshot, quest, "quest_target_npc_not_in_transparent_social_projection:" + npcName);
            }

            var extra = new List<SmallModelActionParameter>
            {
                Parameter("quest_interaction_kind", interactionKind)
            };
            if (inventoryItem.HasValue)
            {
                extra.Add(Parameter("slot_index", ReadInt(inventoryItem.Value, "slot_index").ToString(CultureInfo.InvariantCulture)));
                extra.Add(Parameter("item_id", ReadString(inventoryItem.Value, "item_id")));
                extra.Add(Parameter("qualified_item_id", ReadString(inventoryItem.Value, "qualified_item_id")));
                extra.Add(Parameter("item_stack_before", ReadInt(inventoryItem.Value, "stack").ToString(CultureInfo.InvariantCulture)));
            }
            if (social.Kind == "route_connector_tile")
            {
                extra.Add(Parameter("continuation.option_id", "quest.advance"));
                extra.Add(Parameter("continuation.quest_candidate_id", quest.CandidateId));
                extra.Add(Parameter("continuation.npc_name", npcName));
                extra.Add(Parameter("continuation.target_location", social.LocationId));
                if (inventoryItem.HasValue)
                {
                    extra.Add(Parameter("continuation.slot_index", ReadInt(inventoryItem.Value, "slot_index").ToString(CultureInfo.InvariantCulture)));
                    extra.Add(Parameter("continuation.qualified_item_id", ReadString(inventoryItem.Value, "qualified_item_id")));
                }
            }

            var kind = social.Kind == "social_talk_current"
                ? "quest_npc_interaction"
                : social.Kind;
            return AttachQuest(CloneCandidate(social, kind: kind), quest, extra);
        }

        private IEnumerable<EventCandidate> BindSpecialOrderShippingCandidates(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest,
            string[] contextTagSets)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new[] { BlockedQuestCandidate(snapshot, quest, "player_inventory_unavailable_for_special_order_shipping") };
            }

            var matchingSlots = inventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object && ReadBool(item, "is_empty") != true)
                .Where(item => MatchesContextTagSets(item, contextTagSets))
                .Select(item => ReadInt(item, "slot_index"))
                .ToHashSet();
            var candidates = ShipCandidates(snapshot)
                .Where(candidate => candidate.SlotIndex.HasValue && matchingSlots.Contains(candidate.SlotIndex.Value))
                .Select(candidate => AttachQuest(candidate, quest))
                .ToArray();
            return candidates.Length > 0
                ? candidates
                : new[] { BlockedQuestCandidate(snapshot, quest, "special_order_shippable_matching_item_not_available") };
        }

        private static IEnumerable<EventCandidate> BindSpecialOrderMineDepthCandidates(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest,
            bool skullCave)
        {
            var parameters = new[]
            {
                Parameter("target_depth", (skullCave ? quest.RequiredTargetCount + 120 : quest.RequiredTargetCount).ToString(CultureInfo.InvariantCulture)),
                Parameter("target_location_family", skullCave ? "skull_cavern" : "ordinary_mines"),
                Parameter("latest_exit_time", "2400"),
                Parameter("minimum_reserve_health", "1")
            };
            var candidates = MiningReachDepthCandidateBuilder.Build(snapshot, parameters)
                .Select(candidate => AttachQuest(candidate, quest))
                .ToArray();
            return candidates.Length > 0
                ? candidates
                : new[] { BlockedQuestCandidate(snapshot, quest, "special_order_mining_state_not_available") };
        }

        private static JsonElement? FindQuestInventoryItem(
            SnapshotEnvelope snapshot,
            string exactItemId,
            string[]? contextTagSets,
            int requiredStack)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    ReadBool(item, "is_empty") == true ||
                    ReadInt(item, "stack") < Math.Max(1, requiredStack))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(exactItemId) &&
                    ItemIdentityMatches(ReadString(item, "item_id"), ReadString(item, "qualified_item_id"), exactItemId))
                {
                    return item.Clone();
                }
                if (contextTagSets is { Length: > 0 } && MatchesContextTagSets(item, contextTagSets))
                {
                    return item.Clone();
                }
            }

            return null;
        }

        private static bool MatchesContextTagSets(JsonElement item, string[] contextTagSets)
        {
            if (contextTagSets is null || contextTagSets.Length == 0)
            {
                return false;
            }
            var tags = ReadStringArray(item, "context_tags").ToHashSet(StringComparer.Ordinal);
            foreach (var set in contextTagSets.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var allGroupsMatch = true;
                foreach (var rawGroup in set.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var group = rawGroup.Trim();
                    // Native color tags on preserved ColoredObject inputs use base-context tags
                    // that aren't projected on the inventory row yet, so fail closed here.
                    if (group.StartsWith("color", StringComparison.Ordinal))
                    {
                        allGroupsMatch = false;
                        break;
                    }
                    var alternatives = group.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(value => value.Trim());
                    if (!alternatives.Any(tags.Contains))
                    {
                        allGroupsMatch = false;
                        break;
                    }
                }
                if (allGroupsMatch)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ItemIdentityMatches(string itemId, string qualifiedItemId, string required)
        {
            if (string.IsNullOrWhiteSpace(required))
            {
                return false;
            }
            var normalized = required.StartsWith("(O)", StringComparison.Ordinal) ? required[3..] : required;
            return string.Equals(itemId, required, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemId, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(qualifiedItemId, required, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(qualifiedItemId, "(O)" + normalized, StringComparison.OrdinalIgnoreCase);
        }

        private static EventCandidate AttachQuest(
            EventCandidate source,
            QuestCandidateRef quest,
            IEnumerable<SmallModelActionParameter>? extra = null)
        {
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                "quest_candidate_id", "quest_family", "quest_id", "quest_key",
                "quest_runtime_type", "quest_next_action", "quest_objective_index",
                "quest_expected_current_count", "quest_expected_target_count"
            };
            if (extra is not null)
            {
                foreach (var parameter in extra)
                {
                    names.Add(parameter.Name);
                }
            }

            var parameters = source.Parameters
                .Where(parameter => !names.Contains(parameter.Name))
                .Concat(new[]
                {
                    Parameter("quest_candidate_id", quest.CandidateId),
                    Parameter("quest_family", quest.Family),
                    Parameter("quest_id", quest.QuestId),
                    Parameter("quest_key", quest.QuestKey),
                    Parameter("quest_runtime_type", quest.RuntimeType),
                    Parameter("quest_next_action", quest.NextActionCategory),
                    Parameter("quest_objective_index", quest.SelectedObjectiveIndex.ToString(CultureInfo.InvariantCulture)),
                    Parameter("quest_expected_current_count", quest.CurrentProgressCount.ToString(CultureInfo.InvariantCulture)),
                    Parameter("quest_expected_target_count", quest.RequiredTargetCount.ToString(CultureInfo.InvariantCulture))
                })
                .Concat(extra ?? Array.Empty<SmallModelActionParameter>())
                .ToArray();
            return CloneCandidate(
                source,
                candidateId: quest.CandidateId + ":bound:" + source.CandidateId,
                expectedEffect: source.ExpectedEffect + ";quest_candidate_id=" + quest.CandidateId +
                    ";quest_next_action=" + quest.NextActionCategory,
                parameters: parameters);
        }

        private static EventCandidate BlockedQuestCandidate(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest,
            params string[] reasons)
        {
            return new EventCandidate
            {
                CandidateId = quest.CandidateId,
                Kind = quest.Family == "special_order" ? "special_order_candidate" : "quest_candidate",
                Available = false,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                ExpectedEffect = "quest_candidate_family=" + quest.Family +
                    ";runtime_type=" + quest.RuntimeType +
                    ";next_action=" + quest.NextActionCategory +
                    ";target_count=" + quest.RequiredTargetCount +
                    ";current_count=" + quest.CurrentProgressCount,
                EstimatedTicks = -1,
                EnergyCost = -1,
                AvailabilityClass = "typed_quest_objective_blocked",
                BlockReasons = quest.BlockedDiagnostics
                    .Concat(reasons)
                    .Where(reason => !string.IsNullOrWhiteSpace(reason))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                Parameters = AttachQuestParameters(quest)
            };
        }

        private static SmallModelActionParameter[] AttachQuestParameters(QuestCandidateRef quest)
        {
            return new[]
            {
                Parameter("quest_candidate_id", quest.CandidateId),
                Parameter("quest_family", quest.Family),
                Parameter("quest_id", quest.QuestId),
                Parameter("quest_key", quest.QuestKey),
                Parameter("quest_runtime_type", quest.RuntimeType),
                Parameter("quest_next_action", quest.NextActionCategory),
                Parameter("quest_objective_index", quest.SelectedObjectiveIndex.ToString(CultureInfo.InvariantCulture)),
                Parameter("quest_expected_current_count", quest.CurrentProgressCount.ToString(CultureInfo.InvariantCulture)),
                Parameter("quest_expected_target_count", quest.RequiredTargetCount.ToString(CultureInfo.InvariantCulture)),
                Parameter("planning_eligible", "true")
            };
        }

        private static EventCandidate CloneCandidate(
            EventCandidate source,
            string? candidateId = null,
            string? kind = null,
            string? expectedEffect = null,
            SmallModelActionParameter[]? parameters = null)
        {
            return new EventCandidate
            {
                CandidateId = candidateId ?? source.CandidateId,
                Kind = kind ?? source.Kind,
                Available = source.Available,
                LocationId = source.LocationId,
                TileX = source.TileX,
                TileY = source.TileY,
                ExpectedEffect = expectedEffect ?? source.ExpectedEffect,
                ItemId = source.ItemId,
                QualifiedItemId = source.QualifiedItemId,
                SlotIndex = source.SlotIndex,
                Quantity = source.Quantity,
                ShopId = source.ShopId,
                EstimatedTicks = source.EstimatedTicks,
                EnergyCost = source.EnergyCost,
                AvailabilityClass = source.AvailabilityClass,
                AllowedNow = source.AllowedNow,
                AllowedToday = source.AllowedToday,
                NextOpenTime = source.NextOpenTime,
                EffectiveOpenTime = source.EffectiveOpenTime,
                ClosesAt = source.ClosesAt,
                WaitCost = source.WaitCost,
                GateReasons = source.GateReasons,
                BlockReasons = source.BlockReasons,
                Parameters = parameters ?? source.Parameters,
                FullShipmentKnown = source.FullShipmentKnown,
                FullShipmentEligible = source.FullShipmentEligible,
                FullShipmentCurrentShippedCount = source.FullShipmentCurrentShippedCount,
                FullShipmentAlreadyShipped = source.FullShipmentAlreadyShipped,
                FullShipmentContributes = source.FullShipmentContributes,
                AvailableStack = source.AvailableStack
            };
        }
    }
}
