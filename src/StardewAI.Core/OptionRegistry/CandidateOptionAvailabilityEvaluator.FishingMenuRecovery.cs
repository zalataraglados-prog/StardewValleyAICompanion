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
        private static EventCandidate[] FishingItemGrabRewardRecoveryCandidates(
            SnapshotEnvelope snapshot)
        {
            var menu = ReadStateFieldValue(
                snapshot,
                "menus",
                "menu_specific_state");
            if (!menu.HasValue ||
                menu.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(menu.Value, "kind"),
                    "fishing_item_grab",
                    StringComparison.Ordinal))
            {
                return Array.Empty<EventCandidate>();
            }

            var pending = ReadObjectArray(menu.Value, "pending_items")
                .OrderByDescending(item =>
                    ReadBool(item, "player_can_accept_whole_stack") == true)
                .ThenBy(item => ReadInt(item, "slot_index"))
                .FirstOrDefault();
            if (pending.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<EventCandidate>();
            }

            var canAccept =
                ReadBool(pending, "player_can_accept_whole_stack") == true;
            var discard = canAccept
                ? null
                : SelectObjectiveSafeFishingRewardDiscard(snapshot);
            var reasons = new List<string>();
            if (ReadBool(menu.Value, "essential") != true)
                reasons.Add("fishing_item_grab_menu_not_essential");
            if (!string.Equals(
                    ReadString(menu.Value, "context_type"),
                    "StardewValley.Tools.FishingRod",
                    StringComparison.Ordinal))
            {
                reasons.Add("fishing_item_grab_menu_context_mismatch");
            }
            if (ReadBool(menu.Value, "held_item_present") == true)
                reasons.Add("fishing_item_grab_menu_held_item_present");
            if (!canAccept && discard is null)
            {
                reasons.Add(
                    "fishing_reward_inventory_full_no_objective_safe_discard");
            }

            var targetSlot = ReadInt(pending, "slot_index");
            var targetQualifiedItemId =
                ReadString(pending, "qualified_item_id");
            var targetStack = ReadInt(pending, "stack");
            var targetQuality = ReadInt(pending, "quality");
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter(
                    "execution_option_id",
                    "executor.claim_fishing_item_grab_reward"),
                Parameter("fishing_reward_menu_source", ReadInt(menu.Value, "source").ToString(CultureInfo.InvariantCulture)),
                Parameter("fishing_reward_menu_context_type", ReadString(menu.Value, "context_type")),
                Parameter("fishing_reward_menu_slot_index", targetSlot.ToString(CultureInfo.InvariantCulture)),
                Parameter("fishing_reward_qualified_item_id", targetQualifiedItemId),
                Parameter("fishing_reward_stack_before", targetStack.ToString(CultureInfo.InvariantCulture)),
                Parameter("fishing_reward_quality", targetQuality.ToString(CultureInfo.InvariantCulture)),
                Parameter("fishing_reward_pending_count_before", ReadInt(menu.Value, "pending_item_count").ToString(CultureInfo.InvariantCulture)),
                Parameter("fishing_reward_requires_discard", (!canAccept).ToString().ToLowerInvariant())
            };
            if (discard is not null)
            {
                parameters.AddRange(new[]
                {
                    Parameter("fishing_reward_discard_slot_index", discard.SlotIndex.ToString(CultureInfo.InvariantCulture)),
                    Parameter("fishing_reward_discard_qualified_item_id", discard.QualifiedItemId),
                    Parameter("fishing_reward_discard_stack_before", discard.Stack.ToString(CultureInfo.InvariantCulture)),
                    Parameter("fishing_reward_discard_quality", discard.Quality.ToString(CultureInfo.InvariantCulture)),
                    Parameter("fishing_reward_discard_total_store_value", discard.TotalStoreValue.ToString(CultureInfo.InvariantCulture)),
                    Parameter("fishing_reward_discard_authorization", "objective_safe_unprotected_lowest_total_store_value")
                });
            }

            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "recovery:fishing_item_grab:" +
                        targetSlot.ToString(CultureInfo.InvariantCulture) + ":" +
                        targetQualifiedItemId,
                    Kind = "recovery_claim_fishing_item_grab_reward",
                    Available = reasons.Count == 0,
                    LocationId = ReadStateFieldString(
                        snapshot,
                        "player",
                        "location_id"),
                    ExpectedEffect =
                        "native_fishing_reward_claim=" + targetQualifiedItemId +
                        ";target_stack=" + targetStack.ToString(CultureInfo.InvariantCulture) +
                        ";objective_safe_discard=" +
                        (discard?.QualifiedItemId ?? "none") +
                        ";fresh_snapshot_replan_required=true",
                    EstimatedTicks = discard is null ? 10 : 18,
                    EnergyCost = 0,
                    BlockReasons = reasons.ToArray(),
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static FishingRewardDiscard? SelectObjectiveSafeFishingRewardDiscard(
            SnapshotEnvelope snapshot)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue ||
                inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return inventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Where(item => ReadBool(item, "is_empty") != true)
                .Where(item => ReadBool(item, "is_object") == true)
                .Where(item => ReadBool(item, "can_be_trashed") == true)
                .Where(item =>
                    ReadBool(item, "protected_from_auto_sell") != true)
                .Select(item => new FishingRewardDiscard(
                    ReadInt(item, "slot_index"),
                    ReadString(item, "qualified_item_id"),
                    ReadInt(item, "stack"),
                    ReadInt(item, "quality"),
                    Math.Max(0L, ReadInt(item, "sell_to_store_price")) *
                    Math.Max(1, ReadInt(item, "stack"))))
                .Where(item =>
                    item.SlotIndex >= 0 &&
                    !string.IsNullOrWhiteSpace(item.QualifiedItemId) &&
                    item.Stack > 0)
                .OrderBy(item => item.TotalStoreValue)
                .ThenBy(item => item.Stack)
                .ThenBy(item => item.SlotIndex)
                .FirstOrDefault();
        }

        private static JsonElement[] ReadObjectArray(
            JsonElement parent,
            string propertyName)
        {
            return parent.TryGetProperty(propertyName, out var value) &&
                   value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object)
                    .ToArray()
                : Array.Empty<JsonElement>();
        }

        private sealed record FishingRewardDiscard(
            int SlotIndex,
            string QualifiedItemId,
            int Stack,
            int Quality,
            long TotalStoreValue);
    }
}
