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
    private const string WarpTotemNativeContract =
        "Object.performUseAction((O)261|688|689|690|886)->2000ms_totem_animation->Object.totemWarp->1000ms_fadeAfterDelay->Object.totemWarpForReal->Farm_WarpTotemEntry_or_variant_destination->Game1.warpFarmer->active_or_passive_festival_routing";

    private static readonly IReadOnlyDictionary<string, string> WarpTotemBaseDestinations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["688"] = "Farm",
            ["689"] = "Mountain",
            ["690"] = "Beach",
            ["261"] = "Desert",
            ["886"] = "IslandSouth"
        };

    private static CompiledActionStep[] CompileUseWarpTotemStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var destination = ReadParameter(action, "effective_destination_location_id");
        var x = ReadIntParameter(action, "effective_destination_tile_x");
        var y = ReadIntParameter(action, "effective_destination_tile_y");
        if (!slot.HasValue || string.IsNullOrWhiteSpace(destination) || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("use_warp_totem",
                destination + ":" + x.Value + "," + y.Value + ":slot" + slot.Value + ":" + ReadParameter(action, "qualified_item_id"),
                "inventory_stack=" + ReadParameter(action, "inventory_stack_after") +
                ";route_mode=" + ReadParameter(action, "destination_route_mode") +
                ";base_destination=" + ReadParameter(action, "base_destination_location_id"), 320)
        };
    }

    private static string[] ValidateUseWarpTotemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.use_warp_totem")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var before = ReadIntParameter(action, "inventory_stack_before");
        var after = ReadIntParameter(action, "inventory_stack_after");
        var itemId = ReadParameter(action, "item_id") ?? string.Empty;
        if (!slot.HasValue || !before.HasValue || before < 1 || !after.HasValue ||
            !WarpTotemBaseDestinations.ContainsKey(itemId) ||
            !string.Equals(ReadParameter(action, "qualified_item_id"), "(O)" + itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal))
            return new[] { "use_warp_totem_typed_fields_required" };
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("use_warp_totem_menu_must_be_clear");
        if (!TargetLocationMatchesCurrent(action, snapshot))
            reasons.Add("use_warp_totem_requires_loaded_source_location");

        var context = ReadStateFieldValue(snapshot, "player", "warp_totem");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("use_warp_totem_projection_unavailable").Distinct(StringComparer.Ordinal).ToArray();
        if (!string.Equals(ReadParameter(action, "warp_totem_projection_fingerprint"),
                ReadString(context.Value, "projection_fingerprint"), StringComparison.Ordinal))
            reasons.Add("use_warp_totem_projection_fingerprint_drifted");
        if (!string.Equals(ReadString(context.Value, "native_use_gate_status"), "ready", StringComparison.Ordinal))
            reasons.Add("use_warp_totem_native_effect_gate_blocked");

        JsonElement? row = null;
        if (context.Value.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in rows.EnumerateArray())
            {
                if (ReadInt(candidate, "inventory_slot_index", -1) != slot)
                    continue;
                row = candidate;
                break;
            }
        }
        if (!row.HasValue || !string.Equals(ReadString(row.Value, "item_id"), itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "qualified_item_id"), "(O)" + itemId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            ReadBool(row.Value, "temporarily_invisible") == true || after != before - 1 ||
            ReadInt(row.Value, "stack_before", -1) != before || ReadInt(row.Value, "stack_after", -1) != after)
            reasons.Add("use_warp_totem_inventory_identity_drifted");
        if (!row.HasValue)
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        if (!string.Equals(ReadString(row.Value, "native_use_gate_status"), "ready", StringComparison.Ordinal))
            reasons.Add("use_warp_totem_native_effect_gate_blocked");

        if (!row.Value.TryGetProperty("destination_route", out var route) || route.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("use_warp_totem_destination_route_drifted");
        }
        else
        {
            var baseDestination = ReadString(route, "base_destination_location_id");
            var routeMode = ReadString(route, "destination_route_mode");
            if (!string.Equals(baseDestination, WarpTotemBaseDestinations[itemId], StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "base_destination_location_id"), baseDestination, StringComparison.Ordinal) ||
                ReadIntParameter(action, "requested_destination_tile_x") != ReadInt(route, "requested_destination_tile_x") ||
                ReadIntParameter(action, "requested_destination_tile_y") != ReadInt(route, "requested_destination_tile_y") ||
                !string.Equals(ReadParameter(action, "effective_destination_location_id"), ReadString(route, "effective_destination_location_id"), StringComparison.Ordinal) ||
                ReadIntParameter(action, "effective_destination_tile_x") != ReadInt(route, "effective_destination_tile_x") ||
                ReadIntParameter(action, "effective_destination_tile_y") != ReadInt(route, "effective_destination_tile_y") ||
                !string.Equals(ReadParameter(action, "destination_route_mode"), routeMode, StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "farm_destination_source"), ReadString(route, "farm_destination_source"), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "passive_festival_route_json"), ReadString(route, "passive_festival_route_json"), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "active_festival_id"), ReadString(route, "active_festival_id"), StringComparison.Ordinal) ||
                ReadIntParameter(action, "active_festival_start_time") != ReadInt(route, "active_festival_start_time") ||
                ReadIntParameter(action, "active_festival_end_time") != ReadInt(route, "active_festival_end_time") ||
                ReadIntParameter(action, "active_festival_entry_tile_x") != ReadInt(route, "active_festival_entry_tile_x") ||
                ReadIntParameter(action, "active_festival_entry_tile_y") != ReadInt(route, "active_festival_entry_tile_y") ||
                ReadIntParameter(action, "active_festival_entry_facing") != ReadInt(route, "active_festival_entry_facing") ||
                ReadBoolParameter(action, "festival_prestart_warp_cancelled") != ReadBool(route, "festival_prestart_warp_cancelled") ||
                ReadBoolParameter(action, "festival_ready_check_required") != ReadBool(route, "festival_ready_check_required") ||
                ReadBool(route, "festival_prestart_warp_cancelled") == true ||
                ReadBool(route, "festival_ready_check_required") == true ||
                !new[] { "ordinary", "passive_festival_replacement", "active_festival_entry" }.Contains(routeMode, StringComparer.Ordinal))
                reasons.Add("use_warp_totem_destination_route_drifted");
            if (routeMode == "active_festival_entry" &&
                (string.IsNullOrWhiteSpace(ReadString(route, "active_festival_id")) ||
                 ReadInt(route, "active_festival_start_time") < 0 ||
                 ReadInt(route, "active_festival_end_time") < ReadInt(route, "active_festival_start_time") ||
                 ReadInt(route, "active_festival_entry_tile_x") != ReadInt(route, "effective_destination_tile_x") ||
                 ReadInt(route, "active_festival_entry_tile_y") != ReadInt(route, "effective_destination_tile_y")))
                reasons.Add("use_warp_totem_destination_route_drifted");
        }

        if (!context.Value.TryGetProperty("native_animation_contract", out var animation) ||
            animation.ValueKind != JsonValueKind.Object ||
            ReadIntParameter(action, "native_facing_direction") != ReadInt(animation, "facing_direction") ||
            ReadIntParameter(action, "native_animation_duration_ms") != ReadInt(animation, "animation_duration_ms") ||
            ReadIntParameter(action, "native_totem_callback_delay_ms") != ReadInt(animation, "totem_callback_delay_ms") ||
            ReadIntParameter(action, "native_initial_item_sprite_count") != ReadInt(animation, "initial_item_sprite_count") ||
            ReadIntParameter(action, "native_sprinkle_sprite_count") != ReadInt(animation, "sprinkle_sprite_count") ||
            ReadIntParameter(action, "native_poof_sprite_count") != ReadInt(animation, "poof_sprite_count") ||
            ReadIntParameter(action, "native_trail_sprite_count") != ReadInt(animation, "trail_sprite_count") ||
            !string.Equals(ReadParameter(action, "native_initial_sound"), ReadString(animation, "initial_sound"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_warp_sound"), ReadString(animation, "warp_sound"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_glow_color_rgba"), ReadString(row.Value, "glow_color_rgba"), StringComparison.Ordinal))
            reasons.Add("use_warp_totem_animation_contract_drifted");
        if (!string.Equals(ReadParameter(action, "native_contract"), WarpTotemNativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadString(context.Value, "native_contract"), WarpTotemNativeContract, StringComparison.Ordinal))
            reasons.Add("use_warp_totem_native_contract_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
