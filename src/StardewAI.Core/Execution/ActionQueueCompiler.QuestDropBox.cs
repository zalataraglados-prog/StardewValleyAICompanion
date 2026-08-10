using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateQuestDropBoxDonatePlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.quest_drop_box_donate")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var family = ReadParameter(action, "quest_family") ?? string.Empty;
            var candidateId = ReadParameter(action, "quest_candidate_id") ?? string.Empty;
            var questKey = ReadParameter(action, "quest_key") ?? string.Empty;
            var runtimeType = ReadParameter(action, "quest_runtime_type") ?? string.Empty;
            var objectiveIndex = ReadIntParameter(action, "quest_objective_index");
            var expectedCurrent = ReadIntParameter(action, "quest_expected_current_count");
            var expectedTarget = ReadIntParameter(action, "quest_expected_target_count");
            var dropBoxId = ReadParameter(action, "quest_drop_box_id") ?? string.Empty;
            var targetLocation = ReadParameter(action, "target_location") ?? string.Empty;
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var slotIndex = ReadIntParameter(action, "slot_index");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            var expectedStackBefore = ReadIntParameter(action, "item_stack_before");
            var expectedAccepted = ReadIntParameter(action, "quest_drop_box_expected_accepted_count");

            if (family != "special_order") reasons.Add("quest_drop_box_requires_special_order_family");
            if (string.IsNullOrWhiteSpace(candidateId)) reasons.Add("quest_candidate_id_required");
            if (string.IsNullOrWhiteSpace(questKey)) reasons.Add("quest_key_required");
            if (runtimeType != "SpecialOrder") reasons.Add("quest_drop_box_runtime_type_invalid");
            if (string.IsNullOrWhiteSpace(dropBoxId)) reasons.Add("quest_drop_box_id_required");
            if (string.IsNullOrWhiteSpace(targetLocation)) reasons.Add("quest_drop_box_target_location_required");
            if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue)
            {
                reasons.Add("quest_drop_box_action_and_stand_tiles_required");
            }
            else if (Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1)
            {
                reasons.Add("quest_drop_box_stand_not_adjacent_to_action_tile");
            }
            if (!slotIndex.HasValue || string.IsNullOrWhiteSpace(qualifiedItemId) ||
                !expectedStackBefore.HasValue || !expectedAccepted.HasValue || expectedAccepted <= 0)
            {
                reasons.Add("quest_drop_box_inventory_projection_required");
            }

            ValidateQuestIdentityAgainstSnapshot(
                snapshot,
                family,
                candidateId,
                string.Empty,
                questKey,
                runtimeType,
                objectiveIndex,
                expectedCurrent,
                expectedTarget,
                reasons);

            var ordersState = ReadStateFieldValue(snapshot, "quests", "special_orders");
            var orders = ordersState.HasValue && ordersState.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(ordersState.Value.GetRawText()) ??
                    Array.Empty<SpecialOrderProgressRef>()
                : Array.Empty<SpecialOrderProgressRef>();
            var order = orders.SingleOrDefault(row =>
                string.Equals(row.QuestKey, questKey, StringComparison.Ordinal));
            SpecialOrderObjectiveProgressRef? selectedObjective = null;
            if (order is null ||
                !objectiveIndex.HasValue ||
                objectiveIndex.Value < 0 ||
                objectiveIndex.Value >= order.Objectives.Length)
            {
                reasons.Add("quest_drop_box_live_objective_not_found");
            }
            else
            {
                selectedObjective = order.Objectives[objectiveIndex.Value];
                var fields = selectedObjective.PerTypeFields;
                var resolvedLocation = !string.IsNullOrWhiteSpace(fields.ResolvedDropBoxGameLocation)
                    ? fields.ResolvedDropBoxGameLocation
                    : fields.DropBoxGameLocation;
                if (selectedObjective.RuntimeType != "DonateObjective" ||
                    !fields.Available ||
                    !string.Equals(fields.DropBox, dropBoxId, StringComparison.Ordinal) ||
                    !string.Equals(resolvedLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("quest_drop_box_objective_projection_drifted");
                }
            }

            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("quest_drop_box_current_location_mismatch");
            }

            var actionTiles = ReadStateFieldValue(snapshot, "current_location", "drop_box_action_tiles");
            var actionMatches = actionTiles.HasValue &&
                actionTiles.Value.ValueKind == JsonValueKind.Array &&
                targetX.HasValue &&
                targetY.HasValue &&
                actionTiles.Value.EnumerateArray().Any(tile =>
                    ReadInt(tile, "tile_x") == targetX.Value &&
                    ReadInt(tile, "tile_y") == targetY.Value &&
                    string.Equals(ReadString(tile, "box_id"), dropBoxId, StringComparison.Ordinal));
            if (!actionMatches)
            {
                reasons.Add("quest_drop_box_native_action_tile_drifted");
            }

            var inventoryState = ReadStateFieldValue(snapshot, "player", "inventory");
            JsonElement? inventoryItem = null;
            if (inventoryState.HasValue &&
                inventoryState.Value.ValueKind == JsonValueKind.Array &&
                slotIndex.HasValue)
            {
                foreach (var item in inventoryState.Value.EnumerateArray())
                {
                    if (
                        ReadInt(item, "slot_index") == slotIndex.Value &&
                        ReadBool(item, "is_empty") != true &&
                        string.Equals(
                            ReadString(item, "qualified_item_id"),
                            qualifiedItemId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        inventoryItem = item.Clone();
                        break;
                    }
                }
            }
            if (!inventoryItem.HasValue ||
                ReadInt(inventoryItem.Value, "stack") != expectedStackBefore)
            {
                reasons.Add("quest_drop_box_inventory_identity_or_stack_drifted");
            }
            else if (order is not null && selectedObjective is not null)
            {
                if (!QuestContextTagMatcher.MatchesDonateObjective(
                        inventoryItem.Value,
                        selectedObjective.PerTypeFields.AcceptableContextTagSets))
                {
                    reasons.Add("quest_drop_box_item_no_longer_matches_selected_objective");
                }
                var acceptedCapacity = order.Objectives
                    .Where(objective =>
                        objective.RuntimeType == "DonateObjective" &&
                        objective.PerTypeFields.Available &&
                        QuestContextTagMatcher.MatchesDonateObjective(
                            inventoryItem.Value,
                            objective.PerTypeFields.AcceptableContextTagSets))
                    .Sum(objective => Math.Max(0, objective.MaxCount - objective.CurrentCount));
                var liveExpectedAccepted = Math.Min(
                    ReadInt(inventoryItem.Value, "stack"),
                    acceptedCapacity);
                if (liveExpectedAccepted != expectedAccepted)
                {
                    reasons.Add("quest_drop_box_native_accept_capacity_drifted");
                }
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("quest_drop_box_menu_must_be_clear");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
