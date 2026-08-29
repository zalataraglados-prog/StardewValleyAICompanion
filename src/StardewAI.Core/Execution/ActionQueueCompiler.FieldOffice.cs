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
    private static CompiledActionStep[] CompileDonateFieldOfficePieceStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var item = ReadParameter(action, "qualified_item_id");
        var piece = ReadIntParameter(action, "target_piece_index");
        if (!slot.HasValue || !piece.HasValue || string.IsNullOrWhiteSpace(item))
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("donate_field_office_piece",
                "field-office:slot=" + slot.Value + ":item=" + item + ":piece=" + piece.Value,
                "world_progress.island_field_office.pieces[" + piece.Value + "].donated=true" +
                ";player.inventory[" + slot.Value + "].stack=" + ReadParameter(action, "expected_stack_after") +
                ";uncollected_rewards=" + ReadParameter(action, "uncollected_rewards_after_json") +
                ";finale_ready=" + ReadParameter(action, "expected_finale_ready_after"),
                600)
        };
    }

    private static string[] ValidateFieldOfficeDonationPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.donate_field_office_piece")
            return Array.Empty<string>();
        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var piece = ReadIntParameter(action, "target_piece_index");
        var actionX = ReadIntParameter(action, "target_tile_x");
        var actionY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var stackBefore = ReadIntParameter(action, "expected_stack_before");
        var stackAfter = ReadIntParameter(action, "expected_stack_after");
        var countBefore = ReadIntParameter(action, "expected_donated_piece_count_before");
        var countAfter = ReadIntParameter(action, "expected_donated_piece_count_after");
        if (ReadParameter(action, "confirm_donation") != "true" || !slot.HasValue || slot < 0 ||
            !piece.HasValue || piece is < 0 or >= 11 || !actionX.HasValue || !actionY.HasValue ||
            !standX.HasValue || !standY.HasValue ||
            Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1 ||
            !stackBefore.HasValue || !stackAfter.HasValue || stackBefore < 1 || stackAfter != stackBefore - 1 ||
            !countBefore.HasValue || !countAfter.HasValue || countAfter != countBefore + 1 ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "target_piece_kind")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "target_set_kind")) ||
            !TryBoolParameter(action, "expected_completes_set", out _) ||
            !TryBoolParameter(action, "collected_nut_before", out _) ||
            !TryBoolParameter(action, "expected_finale_ready_after", out _) ||
            !TryBoolParameter(action, "plants_restored_left_before", out _) ||
            !TryBoolParameter(action, "plants_restored_right_before", out _) ||
            !TryBoolParameter(action, "finale_received_or_pending_before", out _) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "new_reward_items_json")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "uncollected_rewards_before_json")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "uncollected_rewards_after_json")) ||
            ReadParameter(action, "field_office_projection_status") != "exact_locked_base_1.6.15" ||
            ReadParameter(action, "native_contract") != "FieldOfficeDesk_mutex_then_Safari_Donate_then_FieldOfficeMenu_inventory_and_exact_piece_holder_then_native_ok_exit")
            return new[] { "field_office_donation_typed_projection_required" };

        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("field_office_donation_menu_must_be_clear");
        var location = ReadParameter(action, "target_location");
        if (!string.Equals(location, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("field_office_donation_target_location_mismatch");

        var office = ReadStateFieldValue(snapshot, "world_progress", "island_field_office");
        if (!office.HasValue || office.Value.ValueKind != JsonValueKind.Object ||
            ReadString(office.Value, "projection_status") != "exact_locked_base_1.6.15" ||
            ReadBool(office.Value, "is_current_location") != true ||
            ReadBool(office.Value, "professor_available") != true ||
            ReadBool(office.Value, "mutex_locked") == true ||
            ReadBool(office.Value, "menu_clear") != true ||
            ReadString(office.Value, "location_id") != location ||
            !FieldOfficeDeskMatches(office.Value, actionX.Value, actionY.Value, ReadParameter(action, "field_office_desk_action_raw")) ||
            ReadInt(office.Value, "donated_piece_count") != countBefore.Value ||
            ReadInt(office.Value, "golden_walnuts_found") != ReadIntParameter(action, "golden_walnuts_found_before") ||
            FieldOfficeRaw(office.Value, "uncollected_rewards") != ReadParameter(action, "uncollected_rewards_before_json") ||
            !TryFindFieldOfficeCandidate(office.Value, slot.Value, ReadParameter(action, "qualified_item_id"), piece.Value, out var candidate) ||
            ReadString(candidate, "action_status") != "ready" ||
            ReadString(candidate, "item_id") != ReadParameter(action, "item_id") ||
            ReadString(candidate, "runtime_type") != ReadParameter(action, "target_runtime_type") ||
            ReadInt(candidate, "stack_before") != stackBefore.Value || ReadInt(candidate, "stack_after") != stackAfter.Value ||
            ReadInt(candidate, "donated_piece_count_before") != countBefore.Value ||
            ReadInt(candidate, "donated_piece_count_after") != countAfter.Value ||
            ReadString(candidate, "target_piece_kind") != ReadParameter(action, "target_piece_kind") ||
            ReadString(candidate, "target_set_kind") != ReadParameter(action, "target_set_kind") ||
            ReadBool(candidate, "completes_set") != ReadBoolParameter(action, "expected_completes_set") ||
            ReadBool(candidate, "collected_nut_before") != ReadBoolParameter(action, "collected_nut_before") ||
            ReadBool(candidate, "expected_finale_ready_after") != ReadBoolParameter(action, "expected_finale_ready_after") ||
            FieldOfficeRaw(candidate, "new_reward_items") != ReadParameter(action, "new_reward_items_json") ||
            FieldOfficeRaw(candidate, "uncollected_rewards_before") != ReadParameter(action, "uncollected_rewards_before_json") ||
            FieldOfficeRaw(candidate, "uncollected_rewards_after") != ReadParameter(action, "uncollected_rewards_after_json") ||
            ReadString(candidate, "expected_collected_nut_key") != ReadParameter(action, "expected_collected_nut_key"))
            reasons.Add("field_office_donation_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool FieldOfficeDeskMatches(JsonElement office, int x, int y, string? raw) =>
        office.TryGetProperty("desk_action_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array &&
        tiles.EnumerateArray().Any(tile => tile.ValueKind == JsonValueKind.Object &&
            ReadInt(tile, "tile_x") == x && ReadInt(tile, "tile_y") == y && ReadString(tile, "action_raw") == raw);

    private static bool TryFindFieldOfficeCandidate(JsonElement office, int slot, string? item, int piece, out JsonElement candidate)
    {
        candidate = default;
        if (!office.TryGetProperty("donation_candidates", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "slot_index") == slot &&
                ReadString(row, "qualified_item_id") == item && ReadInt(row, "target_piece_index") == piece)
            {
                candidate = row;
                return true;
            }
        }
        return false;
    }

    private static string FieldOfficeRaw(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) ? value.GetRawText() : string.Empty;
}
