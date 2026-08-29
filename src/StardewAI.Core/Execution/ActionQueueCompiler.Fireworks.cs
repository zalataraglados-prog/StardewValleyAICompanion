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
    private const string FireworkNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)893|(O)894|(O)895)->broadcastSprites+netAudio(fuse)+DelayedAction.StopPlaying(fuse)";
    private const string FireworkRandomContract = "live_Game1.random_runtime_only_no_read_side_rng_advance";

    private static CompiledActionStep[] CompileUseFireworkStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var location = ReadParameter(action, "target_location");
        var qualifiedItemId = ReadParameter(action, "qualified_item_id");
        if (!slot.HasValue || !x.HasValue || !y.HasValue ||
            string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "use_firework",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + qualifiedItemId,
                "firework_type=" + ReadParameter(action, "firework_type") +
                    ";inventory_stack=" + ReadParameter(action, "inventory_stack_after") +
                    ";fuse_duration_ms=" + ReadParameter(action, "firework_fuse_duration_ms") +
                    ";rocket_id_domain=" + ReadParameter(action, "firework_rocket_id_min") + ".." + ReadParameter(action, "firework_rocket_id_max"),
                60)
        };
    }

    private static string[] ValidateUseFireworkPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.use_firework")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var stackBefore = ReadIntParameter(action, "inventory_stack_before");
        var stackAfter = ReadIntParameter(action, "inventory_stack_after");
        var location = ReadParameter(action, "target_location");
        var qualifiedItemId = ReadParameter(action, "qualified_item_id");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            !stackBefore.HasValue || stackBefore.Value < 1 || stackAfter != stackBefore - 1 ||
            string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return new[] { "use_firework_typed_target_fields_required" };
        }
        if (Math.Abs(standX.Value - x.Value) + Math.Abs(standY.Value - y.Value) != 1)
        {
            reasons.Add("use_firework_adjacent_stand_geometry_invalid");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("use_firework_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("use_firework_requires_loaded_target_location");
        }

        var context = ReadStateFieldValue(snapshot, "player", "firework_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("use_firework_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(ReadParameter(action, "firework_projection_fingerprint"),
                ReadString(context.Value, "projection_fingerprint"), StringComparison.Ordinal))
        {
            reasons.Add("use_firework_projection_fingerprint_drifted");
        }
        if (!string.Equals(ReadParameter(action, "firework_random_contract"), FireworkRandomContract, StringComparison.Ordinal) ||
            !string.Equals(ReadString(context.Value, "random_outcome_contract"), FireworkRandomContract, StringComparison.Ordinal))
        {
            reasons.Add("use_firework_random_contract_drifted");
        }

        var row = PlacementInventoryRow(context.Value, slot.Value, qualifiedItemId!);
        var expectedType = qualifiedItemId switch { "(O)893" => 0, "(O)894" => 1, "(O)895" => 2, _ => -1 };
        var expectedSourceX = expectedType < 0 ? -1 : 256 + expectedType * 16;
        if (!row.HasValue ||
            !string.Equals(ReadString(row.Value, "item_id"), ReadParameter(action, "item_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            ReadInt(row.Value, "stack_before", -1) != stackBefore.Value || ReadInt(row.Value, "stack_after", -1) != stackAfter.Value ||
            ReadIntParameter(action, "firework_type") != expectedType || ReadInt(row.Value, "firework_type", -1) != expectedType ||
            ReadIntParameter(action, "firework_source_rect_x") != expectedSourceX || ReadInt(row.Value, "source_rect_x", -1) != expectedSourceX ||
            ReadIntParameter(action, "firework_source_rect_y") != 397 || ReadInt(row.Value, "source_rect_y", -1) != 397)
        {
            reasons.Add("use_firework_inventory_or_variant_identity_drifted");
        }
        if (row.HasValue &&
            (ReadIntParameter(action, "firework_fuse_duration_ms") != ReadInt(row.Value, "fuse_duration_ms") ||
             ReadIntParameter(action, "firework_rocket_delay_ms") != ReadInt(row.Value, "rocket_delay_ms") ||
             ReadIntParameter(action, "firework_rocket_id_min") != ReadInt(row.Value, "rocket_id_min") ||
             ReadIntParameter(action, "firework_rocket_id_max") != ReadInt(row.Value, "rocket_id_max") ||
             !string.Equals(ReadParameter(action, "firework_acceleration_y_min"), ReadStringNumber(row.Value, "acceleration_y_min"), StringComparison.Ordinal) ||
             !string.Equals(ReadParameter(action, "firework_acceleration_y_max"), ReadStringNumber(row.Value, "acceleration_y_max"), StringComparison.Ordinal) ||
             !string.Equals(ReadParameter(action, "firework_acceleration_y_step"), ReadStringNumber(row.Value, "acceleration_y_step"), StringComparison.Ordinal) ||
             !string.Equals(ReadParameter(action, "native_contract"), FireworkNativeContract, StringComparison.Ordinal) ||
             !string.Equals(ReadString(row.Value, "native_contract"), FireworkNativeContract, StringComparison.Ordinal)))
        {
            reasons.Add("use_firework_native_effect_projection_drifted");
        }

        var locationRow = row.HasValue ? PlacementLocationRow(row.Value, location!) : null;
        if (!locationRow.HasValue ||
            !string.Equals(ReadString(locationRow.Value, "placement_probe_status"), "native_legal_tiles_available", StringComparison.Ordinal) ||
            !PlacementRangeAt(locationRow.Value, x.Value, y.Value).HasValue)
        {
            reasons.Add("use_firework_exact_tile_not_native_legal");
        }
        else if (ContainsTile(locationRow.Value, "temporary_sprite_blocked_tiles", x.Value, y.Value))
        {
            reasons.Add("use_firework_exact_tile_transiently_occupied");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool ContainsTile(JsonElement row, string property, int x, int y)
    {
        if (!row.TryGetProperty(property, out var tiles) || tiles.ValueKind != JsonValueKind.Array)
            return false;
        return tiles.EnumerateArray().Any(tile => tile.ValueKind == JsonValueKind.Object &&
            ReadInt(tile, "tile_x", int.MinValue) == x && ReadInt(tile, "tile_y", int.MinValue) == y);
    }

    private static string ReadStringNumber(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetRawText()
            : string.Empty;
}
