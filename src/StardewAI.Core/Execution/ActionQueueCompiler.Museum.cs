using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileDonateMuseumItemStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var itemId = ReadParameter(action, "qualified_item_id");
        var tileX = ReadIntParameter(action, "donation_tile_x");
        var tileY = ReadIntParameter(action, "donation_tile_y");
        if (!slot.HasValue || string.IsNullOrWhiteSpace(itemId) || !tileX.HasValue || !tileY.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "donate_museum_item",
                "museum:slot=" + slot.Value + ":item=" + itemId + ":display=" + tileX.Value + "," + tileY.Value,
                "world_progress.museum.donated_count=" + ReadParameter(action, "expected_donated_count_after") +
                    ";player.inventory[" + slot.Value + "].stack=" + ReadParameter(action, "expected_stack_after") +
                    ";collection_complete=" + ReadParameter(action, "expected_collection_complete_after") +
                    ";museum_achievement=" + ReadParameter(action, "expected_complete_collection_achievement_after") +
                    ";field_guide_quest_completed=" + ReadParameter(action, "expected_field_guide_quest_completed_after") +
                    ";pending_rewards=" + ReadParameter(action, "pending_reward_ids_after_json") +
                    ";reaches_rusty_key_threshold=" + ReadParameter(action, "reaches_rusty_key_threshold"),
                240)
        };
    }

    private static string[] ValidateMuseumDonationPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.donate_museum_item")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var actionX = ReadIntParameter(action, "gunther_action_tile_x");
        var actionY = ReadIntParameter(action, "gunther_action_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var donationX = ReadIntParameter(action, "donation_tile_x");
        var donationY = ReadIntParameter(action, "donation_tile_y");
        var stackBefore = ReadIntParameter(action, "expected_stack_before");
        var stackAfter = ReadIntParameter(action, "expected_stack_after");
        var countBefore = ReadIntParameter(action, "expected_donated_count_before");
        var countAfter = ReadIntParameter(action, "expected_donated_count_after");
        var total = ReadIntParameter(action, "museum_total_donatable_items");
        var threshold = ReadIntParameter(action, "rusty_key_donation_threshold");
        if (!slot.HasValue || slot.Value < 0 || !targetX.HasValue || !targetY.HasValue ||
            !actionX.HasValue || !actionY.HasValue || targetX != actionX || targetY != actionY ||
            !standX.HasValue || !standY.HasValue || !donationX.HasValue || !donationY.HasValue ||
            !stackBefore.HasValue || !stackAfter.HasValue || stackBefore.Value < 1 || stackAfter.Value != stackBefore.Value - 1 ||
            !countBefore.HasValue || !countAfter.HasValue || countAfter.Value != countBefore.Value + 1 ||
            !total.HasValue || total.Value < countAfter.Value || !threshold.HasValue || threshold.Value < 1 ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) ||
            !TryBoolParameter(action, "expected_collection_complete_after", out var completesCollection) ||
            completesCollection != (countAfter.Value >= total.Value) ||
            !TryBoolParameter(action, "expected_complete_collection_achievement_after", out _) ||
            !TryBoolParameter(action, "field_guide_quest_present_before", out _) ||
            !TryBoolParameter(action, "field_guide_quest_completed_before", out _) ||
            !TryBoolParameter(action, "expected_field_guide_quest_completed_after", out _) ||
            !TryBoolParameter(action, "reaches_rusty_key_threshold", out var reachesThreshold) ||
            reachesThreshold != (countBefore.Value < threshold.Value && countAfter.Value >= threshold.Value) ||
            ReadParameter(action, "reward_projection_status") != "ready" ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "pending_reward_ids_before_json")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "pending_reward_ids_after_json")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "newly_pending_reward_ids_json")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "auto_applied_reward_ids_json")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "auto_applied_reward_actions_json")) ||
            ReadParameter(action, "rusty_key_reward_id") != "museum60" ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "rusty_key_reward_action")) ||
            ReadParameter(action, "native_contract") != "LibraryMuseum.OpenDonationMenu_then_MuseumMenu_fade_then_receiveLeftClick_inventory_and_display_then_okButton_native_exit")
        {
            return new[] { "museum_donation_typed_projection_required" };
        }

        if (Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1)
        {
            reasons.Add("museum_counter_stand_tile_not_adjacent");
        }
        if (reachesThreshold && ReadParameter(action, "rusty_key_reward_action") != "MarkEventSeen Host 295672")
        {
            reasons.Add("museum_rusty_key_native_reward_action_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("museum_donation_menu_must_be_clear");
        }
        var targetLocation = ReadParameter(action, "target_location");
        if (!string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("museum_donation_target_location_mismatch");
        }

        var museum = ReadStateFieldValue(snapshot, "world_progress", "museum");
        if (!museum.HasValue || museum.Value.ValueKind != JsonValueKind.Object ||
            ReadBool(museum.Value, "museum_is_current_location") != true ||
            ReadBool(museum.Value, "museum_mutex_locked") == true ||
            ReadString(museum.Value, "museum_location_id") != targetLocation ||
            NullableReadInt(museum.Value, "gunther_action_tile_x") != actionX ||
            NullableReadInt(museum.Value, "gunther_action_tile_y") != actionY ||
            NullableReadInt(museum.Value, "free_donation_tile_x") != donationX ||
            NullableReadInt(museum.Value, "free_donation_tile_y") != donationY ||
            ReadInt(museum.Value, "donated_count") != countBefore.Value ||
            ReadInt(museum.Value, "total_donatable_items") != total.Value ||
            ReadInt(museum.Value, "rusty_key_donation_threshold") != threshold.Value ||
            ReadString(museum.Value, "rusty_key_reward_action") != ReadParameter(action, "rusty_key_reward_action") ||
            MuseumRawJson(museum.Value, "pending_reward_ids") != ReadParameter(action, "pending_reward_ids_before_json") ||
            !TryFindMuseumDonationCandidate(museum.Value, slot.Value, ReadParameter(action, "qualified_item_id"), out var candidate) ||
            ReadString(candidate, "action_status") != "ready" ||
            ReadString(candidate, "item_id") != ReadParameter(action, "item_id") ||
            ReadString(candidate, "runtime_type") != ReadParameter(action, "target_runtime_type") ||
            ReadInt(candidate, "stack_before") != stackBefore.Value ||
            ReadInt(candidate, "stack_after") != stackAfter.Value ||
            ReadInt(candidate, "donated_count_before") != countBefore.Value ||
            ReadInt(candidate, "donated_count_after") != countAfter.Value ||
            ReadBool(candidate, "completes_collection") != completesCollection ||
            ReadBool(candidate, "reaches_rusty_key_threshold") != reachesThreshold ||
            ReadBool(candidate, "expected_complete_collection_achievement_after") != ReadBoolParameter(action, "expected_complete_collection_achievement_after") ||
            ReadBool(candidate, "field_guide_quest_present_before") != ReadBoolParameter(action, "field_guide_quest_present_before") ||
            ReadBool(candidate, "field_guide_quest_completed_before") != ReadBoolParameter(action, "field_guide_quest_completed_before") ||
            ReadBool(candidate, "expected_field_guide_quest_completed_after") != ReadBoolParameter(action, "expected_field_guide_quest_completed_after") ||
            MuseumRawJson(candidate, "pending_reward_ids_before") != ReadParameter(action, "pending_reward_ids_before_json") ||
            MuseumRawJson(candidate, "pending_reward_ids_after") != ReadParameter(action, "pending_reward_ids_after_json") ||
            MuseumRawJson(candidate, "newly_pending_reward_ids") != ReadParameter(action, "newly_pending_reward_ids_json") ||
            MuseumRawJson(candidate, "auto_applied_reward_ids") != ReadParameter(action, "auto_applied_reward_ids_json") ||
            MuseumRawJson(candidate, "auto_applied_reward_actions") != ReadParameter(action, "auto_applied_reward_actions_json") ||
            ReadString(candidate, "reward_projection_status") != "ready")
        {
            reasons.Add("museum_donation_projection_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool? ReadBoolParameter(SmallModelAction action, string name)
    {
        return TryBoolParameter(action, name, out var value) ? value : null;
    }

    private static string MuseumRawJson(JsonElement row, string propertyName)
    {
        return row.TryGetProperty(propertyName, out var value) ? value.GetRawText() : string.Empty;
    }

    private static bool TryFindMuseumDonationCandidate(JsonElement museum, int slot, string? qualifiedItemId, out JsonElement candidate)
    {
        candidate = default;
        if (!museum.TryGetProperty("donation_candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var row in candidates.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "slot_index") == slot &&
                ReadString(row, "qualified_item_id") == qualifiedItemId)
            {
                candidate = row;
                return true;
            }
        }
        return false;
    }
}
