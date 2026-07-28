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
                case "find_lost_item":
                    return new[] { BindLostItemCandidate(snapshot, candidate) };
                case "greet_npcs":
                    var targetNpc = quest.PerTypeFields?.WhoToGreet?.FirstOrDefault() ?? string.Empty;
                    return new[] { BindQuestNpcCandidate(snapshot, candidate, targetNpc, "report", string.Empty, null) };
                default:
                    return new[] { BlockedQuestCandidate(snapshot, candidate, "quest_objective_binding_not_executable:" + candidate.NextActionCategory) };
            }
        }

        private EventCandidate BindLostItemCandidate(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest)
        {
            if (string.IsNullOrWhiteSpace(quest.RequiredTargetLocation) ||
                !quest.RequiredTargetTileX.HasValue ||
                !quest.RequiredTargetTileY.HasValue ||
                string.IsNullOrWhiteSpace(quest.RequiredItemId))
            {
                return BlockedQuestCandidate(snapshot, quest, "lost_item_exact_target_fields_missing");
            }

            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (!string.Equals(currentLocation, quest.RequiredTargetLocation, StringComparison.OrdinalIgnoreCase))
            {
                return BindQuestLocationRoute(snapshot, quest, quest.RequiredTargetLocation);
            }

            var targetX = quest.RequiredTargetTileX.Value;
            var targetY = quest.RequiredTargetTileY.Value;
            var pickup = SpawnedObjectForagingCandidates(snapshot)
                .FirstOrDefault(candidate =>
                    candidate.TileX == targetX &&
                    candidate.TileY == targetY &&
                    ItemIdentityMatches(candidate.ItemId, candidate.QualifiedItemId, quest.RequiredItemId));
            if (pickup is null)
            {
                return BlockedQuestCandidate(
                    snapshot,
                    quest,
                    "lost_item_spawned_object_not_present_at_exact_target:" +
                    quest.RequiredTargetLocation + ":" + targetX + "," + targetY);
            }

            return AttachQuest(
                pickup,
                quest,
                new[]
                {
                    Parameter("quest_lost_item_target_tile_x", targetX.ToString(CultureInfo.InvariantCulture)),
                    Parameter("quest_lost_item_target_tile_y", targetY.ToString(CultureInfo.InvariantCulture))
                });
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
                case "DonateObjective":
                    return new[]
                    {
                        BindSpecialOrderDropBoxCandidate(snapshot, candidate, order, fields)
                    };
                case "FishObjective":
                    return BindSpecialOrderFishingCandidates(snapshot, candidate, fields.AcceptableContextTagSets);
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

        private IEnumerable<EventCandidate> BindSpecialOrderFishingCandidates(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest,
            string[] contextTagSets)
        {
            if (contextTagSets.Length == 0)
            {
                return new[] { BlockedQuestCandidate(snapshot, quest, "special_order_fish_context_tag_sets_missing") };
            }
            if (QuestContextTagMatcher.ContainsUnprojectedColorTag(contextTagSets))
            {
                return new[] { BlockedQuestCandidate(snapshot, quest, "special_order_fish_has_unprojected_color_tags") };
            }

            var candidates = FishingEventCandidateBuilder.Build(snapshot)
                .Where(candidate => candidate.Kind == "catch_fish" && candidate.Available)
                .Where(candidate => FishingCandidateMatchesContextTags(candidate, contextTagSets))
                .Select(candidate => AttachQuest(candidate, quest))
                .ToArray();
            return candidates.Length > 0
                ? candidates
                : new[] { BlockedQuestCandidate(snapshot, quest, "special_order_matching_fish_not_available_in_current_fishing_projection") };
        }

        private static bool FishingCandidateMatchesContextTags(
            EventCandidate candidate,
            string[] contextTagSets)
        {
            var distributionJson = ReadParameter(candidate.Parameters, "outcome_distribution_json");
            if (string.IsNullOrWhiteSpace(distributionJson))
            {
                return false;
            }
            try
            {
                using var document = JsonDocument.Parse(distributionJson);
                return document.RootElement.ValueKind == JsonValueKind.Array &&
                    document.RootElement.EnumerateArray().Any(outcome =>
                        string.Equals(
                            ReadString(outcome, "context_tags_projection_status"),
                            "exact_item_get_context_tags",
                            StringComparison.Ordinal) &&
                        QuestContextTagMatcher.Matches(outcome, contextTagSets));
            }
            catch (JsonException)
            {
                return false;
            }
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
                .Where(item => QuestContextTagMatcher.Matches(item, contextTagSets))
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

        private EventCandidate BindSpecialOrderDropBoxCandidate(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest,
            SpecialOrderProgressRef order,
            PerTypeObjectiveFields fields)
        {
            if (string.IsNullOrWhiteSpace(fields.DropBox))
            {
                return BlockedQuestCandidate(snapshot, quest, "special_order_drop_box_id_missing");
            }

            var targetLocation = !string.IsNullOrWhiteSpace(fields.ResolvedDropBoxGameLocation)
                ? fields.ResolvedDropBoxGameLocation
                : fields.DropBoxGameLocation;
            if (string.IsNullOrWhiteSpace(targetLocation))
            {
                return BlockedQuestCandidate(snapshot, quest, "special_order_drop_box_location_missing");
            }

            var inventoryItem = FindQuestInventoryItem(snapshot, string.Empty, fields.AcceptableContextTagSets, 1);
            if (!inventoryItem.HasValue)
            {
                return BlockedQuestCandidate(snapshot, quest, "special_order_drop_box_matching_inventory_item_not_available");
            }

            var slotIndex = ReadInt(inventoryItem.Value, "slot_index");
            var qualifiedItemId = ReadString(inventoryItem.Value, "qualified_item_id");
            var itemStack = ReadInt(inventoryItem.Value, "stack");
            if (order.Objectives.Any(objective =>
                    string.Equals(objective.RuntimeType, "DonateObjective", StringComparison.Ordinal) &&
                    QuestContextTagMatcher.ContainsUnprojectedColorTag(
                        objective.PerTypeFields.AcceptableContextTagSets)))
            {
                return BlockedQuestCandidate(
                    snapshot,
                    quest,
                    "special_order_drop_box_native_accept_capacity_has_unprojected_color_tags");
            }
            var acceptedCapacity = order.Objectives
                .Where(objective =>
                    string.Equals(objective.RuntimeType, "DonateObjective", StringComparison.Ordinal) &&
                    objective.PerTypeFields.Available &&
                    QuestContextTagMatcher.Matches(inventoryItem.Value, objective.PerTypeFields.AcceptableContextTagSets))
                .Sum(objective => Math.Max(0, objective.MaxCount - objective.CurrentCount));
            var expectedAcceptedCount = Math.Min(itemStack, acceptedCapacity);
            if (expectedAcceptedCount <= 0)
            {
                return BlockedQuestCandidate(snapshot, quest, "special_order_drop_box_native_accept_capacity_zero");
            }

            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            {
                var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue)
                    .Where(candidate => candidate.Kind == "route_connector_tile")
                    .ToArray();
                var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation, routeCandidates);
                if (route?.FirstConnectorCandidate is null)
                {
                    return BlockedQuestCandidate(snapshot, quest, "special_order_drop_box_route_unavailable:" + targetLocation);
                }

                return AttachQuest(
                    route.FirstConnectorCandidate,
                    quest,
                    new[]
                    {
                        Parameter("continuation.option_id", "quest.advance"),
                        Parameter("continuation.quest_candidate_id", quest.CandidateId),
                        Parameter("continuation.target_location", targetLocation),
                        Parameter("continuation.slot_index", slotIndex.ToString(CultureInfo.InvariantCulture)),
                        Parameter("continuation.qualified_item_id", qualifiedItemId),
                        Parameter("quest_drop_box_id", fields.DropBox),
                        Parameter("quest_drop_box_target_location", targetLocation),
                        Parameter("quest_route_remaining_connector_count", route.Path.Length.ToString(CultureInfo.InvariantCulture))
                    });
            }

            var actionTiles = ReadStateFieldValue(snapshot, "current_location", "drop_box_action_tiles");
            if (!actionTiles.HasValue || actionTiles.Value.ValueKind != JsonValueKind.Array)
            {
                return BlockedQuestCandidate(snapshot, quest, "current_location_drop_box_action_tiles_unavailable");
            }

            JsonElement? selectedAction = null;
            CandidateTile? selectedStand = null;
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var bestDistance = int.MaxValue;
            foreach (var action in actionTiles.Value.EnumerateArray())
            {
                if (action.ValueKind != JsonValueKind.Object ||
                    !string.Equals(ReadString(action, "box_id"), fields.DropBox, StringComparison.Ordinal))
                {
                    continue;
                }

                var actionX = ReadInt(action, "tile_x");
                var actionY = ReadInt(action, "tile_y");
                var stand = FindBestStandTile(snapshot, actionX, actionY);
                if (stand is null)
                {
                    continue;
                }

                var distance = Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                if (distance < bestDistance)
                {
                    selectedAction = action.Clone();
                    selectedStand = stand;
                    bestDistance = distance;
                }
            }

            if (!selectedAction.HasValue)
            {
                return BlockedQuestCandidate(snapshot, quest, "matching_drop_box_action_tile_not_found:" + fields.DropBox);
            }
            if (selectedStand is null)
            {
                return BlockedQuestCandidate(snapshot, quest, "matching_drop_box_action_tile_has_no_stand_tile:" + fields.DropBox);
            }

            var targetX = ReadInt(selectedAction.Value, "tile_x");
            var targetY = ReadInt(selectedAction.Value, "tile_y");
            var source = new EventCandidate
            {
                CandidateId = "quest_drop_box:" + quest.QuestKey + ":" + quest.SelectedObjectiveIndex,
                Kind = "quest_drop_box_donation",
                Available = true,
                LocationId = currentLocation,
                TileX = targetX,
                TileY = targetY,
                ItemId = ReadString(inventoryItem.Value, "item_id"),
                QualifiedItemId = qualifiedItemId,
                SlotIndex = slotIndex,
                Quantity = expectedAcceptedCount,
                AvailableStack = itemStack,
                ExpectedEffect = "special_order.drop_box=" + fields.DropBox +
                    ";native_accepted_count=" + expectedAcceptedCount +
                    ";selected_objective_progress_increases=true",
                EstimatedTicks = Math.Max(120, bestDistance * 12 + 120),
                EnergyCost = 0,
                AvailabilityClass = "typed_special_order_drop_box_native_menu",
                AllowedNow = true,
                AllowedToday = true,
                Parameters = new[]
                {
                    Parameter("quest_drop_box_id", fields.DropBox),
                    Parameter("quest_drop_box_target_location", targetLocation),
                    Parameter("target_tile_x", targetX.ToString(CultureInfo.InvariantCulture)),
                    Parameter("target_tile_y", targetY.ToString(CultureInfo.InvariantCulture)),
                    Parameter("stand_tile_x", selectedStand.X.ToString(CultureInfo.InvariantCulture)),
                    Parameter("stand_tile_y", selectedStand.Y.ToString(CultureInfo.InvariantCulture)),
                    Parameter("route_distance_tiles", bestDistance.ToString(CultureInfo.InvariantCulture)),
                    Parameter("max_movement_tiles", Math.Max(1, bestDistance + 16).ToString(CultureInfo.InvariantCulture)),
                    Parameter("slot_index", slotIndex.ToString(CultureInfo.InvariantCulture)),
                    Parameter("item_id", ReadString(inventoryItem.Value, "item_id")),
                    Parameter("qualified_item_id", qualifiedItemId),
                    Parameter("item_stack_before", itemStack.ToString(CultureInfo.InvariantCulture)),
                    Parameter("quest_drop_box_expected_accepted_count", expectedAcceptedCount.ToString(CultureInfo.InvariantCulture)),
                    Parameter("quest_drop_box_native_action", ReadString(selectedAction.Value, "action"))
                }
            };
            return AttachQuest(source, quest);
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
                if (contextTagSets is { Length: > 0 } && QuestContextTagMatcher.Matches(item, contextTagSets))
                {
                    return item.Clone();
                }
            }

            return null;
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
