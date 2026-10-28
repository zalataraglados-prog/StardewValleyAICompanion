using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileFishingItemGrabRewardStep(
        SmallModelAction action)
    {
        var itemId = ReadParameter(
            action,
            "fishing_reward_qualified_item_id");
        var slotIndex = ReadIntParameter(
            action,
            "fishing_reward_menu_slot_index");
        if (string.IsNullOrWhiteSpace(itemId) || !slotIndex.HasValue)
            return Array.Empty<CompiledActionStep>();

        return new[]
        {
            Step(
                "claim_fishing_item_grab_reward",
                "ItemGrabMenu:slot=" +
                slotIndex.Value.ToString(CultureInfo.InvariantCulture) +
                ":item=" + itemId,
                "fishing_reward_claimed=true;fresh_snapshot_replan_required=true",
                ReadParameter(action, "fishing_reward_requires_discard") ==
                    "true"
                    ? 18
                    : 10)
        };
    }

    private static string[] ValidateFishingItemGrabRewardPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (!string.Equals(
                action.OptionId,
                "executor.claim_fishing_item_grab_reward",
                StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        if (!ActiveMenuOpen(snapshot) ||
            !string.Equals(
                ActiveMenuType(snapshot),
                "ItemGrabMenu",
                StringComparison.Ordinal))
        {
            reasons.Add("fishing_item_grab_menu_not_open");
        }

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
            return reasons
                .Append("fishing_item_grab_transparent_state_missing")
                .ToArray();
        }

        if (ReadBool(menu.Value, "essential") != true)
            reasons.Add("fishing_item_grab_menu_not_essential");
        if (ReadBool(menu.Value, "held_item_present") == true)
            reasons.Add("fishing_item_grab_menu_held_item_present");
        if (!string.Equals(
                ReadString(menu.Value, "context_type"),
                "StardewValley.Tools.FishingRod",
                StringComparison.Ordinal))
        {
            reasons.Add("fishing_item_grab_menu_context_mismatch");
        }

        var targetSlot = ReadIntParameter(
            action,
            "fishing_reward_menu_slot_index");
        var target = ReadMenuPendingItem(menu.Value, targetSlot);
        if (!target.HasValue)
        {
            reasons.Add("fishing_reward_target_slot_missing");
            return reasons.ToArray();
        }

        if (!string.Equals(
                ReadParameter(action, "fishing_reward_qualified_item_id"),
                ReadString(target.Value, "qualified_item_id"),
                StringComparison.Ordinal) ||
            ReadIntParameter(action, "fishing_reward_stack_before") !=
                ReadInt(target.Value, "stack") ||
            ReadIntParameter(action, "fishing_reward_quality") !=
                ReadInt(target.Value, "quality") ||
            ReadIntParameter(action, "fishing_reward_pending_count_before") !=
                ReadInt(menu.Value, "pending_item_count") ||
            ReadIntParameter(action, "fishing_reward_menu_source") !=
                ReadInt(menu.Value, "source") ||
            !string.Equals(
                ReadParameter(action, "fishing_reward_menu_context_type"),
                ReadString(menu.Value, "context_type"),
                StringComparison.Ordinal))
        {
            reasons.Add("fishing_reward_menu_binding_drift");
        }

        var canAccept =
            ReadBool(target.Value, "player_can_accept_whole_stack") == true;
        var requiresDiscard = string.Equals(
            ReadParameter(action, "fishing_reward_requires_discard"),
            "true",
            StringComparison.Ordinal);
        if (requiresDiscard == canAccept)
            reasons.Add("fishing_reward_discard_requirement_mismatch");

        if (requiresDiscard)
        {
            reasons.AddRange(
                ValidateFishingRewardDiscardBinding(action, snapshot));
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> ValidateFishingRewardDiscardBinding(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (!string.Equals(
                ReadParameter(
                    action,
                    "fishing_reward_discard_authorization"),
                "objective_safe_unprotected_lowest_total_store_value",
                StringComparison.Ordinal))
        {
            return new[]
            {
                "fishing_reward_discard_authorization_missing"
            };
        }

        var slotIndex = ReadIntParameter(
            action,
            "fishing_reward_discard_slot_index");
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        if (!slotIndex.HasValue ||
            !inventory.HasValue ||
            inventory.Value.ValueKind != JsonValueKind.Array)
        {
            return new[] { "fishing_reward_discard_slot_missing" };
        }

        var item = inventory.Value.EnumerateArray()
            .FirstOrDefault(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "slot_index") == slotIndex.Value);
        if (item.ValueKind != JsonValueKind.Object)
            return new[] { "fishing_reward_discard_slot_missing" };

        var reasons = new List<string>();
        if (ReadBool(item, "is_object") != true ||
            ReadBool(item, "can_be_trashed") != true ||
            ReadBool(item, "protected_from_auto_sell") == true)
        {
            reasons.Add("fishing_reward_discard_slot_not_objective_safe");
        }
        if (!string.Equals(
                ReadParameter(
                    action,
                    "fishing_reward_discard_qualified_item_id"),
                ReadString(item, "qualified_item_id"),
                StringComparison.Ordinal) ||
            ReadIntParameter(
                action,
                "fishing_reward_discard_stack_before") !=
                ReadInt(item, "stack") ||
            ReadIntParameter(
                action,
                "fishing_reward_discard_quality") !=
                ReadInt(item, "quality"))
        {
            reasons.Add("fishing_reward_discard_slot_binding_drift");
        }

        return reasons;
    }

    private static JsonElement? ReadMenuPendingItem(
        JsonElement menu,
        int? slotIndex)
    {
        if (!slotIndex.HasValue ||
            !menu.TryGetProperty("pending_items", out var pending) ||
            pending.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in pending.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                ReadInt(item, "slot_index") == slotIndex.Value)
            {
                return item;
            }
        }

        return null;
    }
}
