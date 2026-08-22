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
    private const string CookoutKitQualifiedItemId = "(O)926";
    private const string PlacedCookoutQualifiedItemId = "(BC)278";
    private const string CookoutKitNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)926)->Torch((BC)278,destroyOvernight:true)";

    private static CompiledActionStep[] CompilePlaceCookoutKitStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(location))
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "place_cookout_kit",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + CookoutKitQualifiedItemId,
                "current_location.objects[" + x.Value + "," + y.Value + "].qualified_item_id=" +
                    PlacedCookoutQualifiedItemId + ";current_location.objects[" + x.Value + "," + y.Value +
                    "].destroy_over_night=true;player.inventory[" + slot.Value + "].stack_decreases=1",
                60)
        };
    }

    private static string[] ValidatePlaceCookoutKitPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.place_cookout_kit")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var expectedStack = ReadIntParameter(action, "inventory_stack_before");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            !expectedStack.HasValue || expectedStack.Value < 1 || string.IsNullOrWhiteSpace(location))
        {
            reasons.Add("place_cookout_kit_typed_target_fields_required");
            return reasons.ToArray();
        }
        if (!string.Equals(ReadParameter(action, "qualified_item_id"), CookoutKitQualifiedItemId, StringComparison.Ordinal))
        {
            reasons.Add("place_cookout_kit_exact_item_identity_required");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "cookout_placement_reason")))
        {
            reasons.Add("place_cookout_kit_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), CookoutKitNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("place_cookout_kit_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("place_cookout_kit_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("place_cookout_kit_requires_loaded_target_location");
        }
        if (Math.Abs(standX.Value - x.Value) + Math.Abs(standY.Value - y.Value) != 1 ||
            PlacementCollisionGridBlocks(snapshot, standX.Value, standY.Value))
        {
            reasons.Add("place_cookout_kit_adjacent_stand_geometry_invalid");
        }

        var context = ReadStateFieldValue(snapshot, "player", "cookout_kit_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("place_cookout_kit_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(
                ReadParameter(action, "placement_projection_fingerprint"),
                ReadString(context.Value, "static_projection_fingerprint"),
                StringComparison.Ordinal))
        {
            reasons.Add("place_cookout_kit_projection_fingerprint_drifted");
        }

        var row = PlacementInventoryRow(context.Value, slot.Value, CookoutKitQualifiedItemId);
        if (!row.HasValue || ReadInt(row.Value, "stack") != expectedStack.Value)
        {
            reasons.Add("place_cookout_kit_inventory_identity_drifted");
        }
        else
        {
            var locationRow = PlacementLocationRow(row.Value, location);
            if (!locationRow.HasValue ||
                !string.Equals(ReadString(locationRow.Value, "placement_probe_status"), "native_legal_tiles_available", StringComparison.Ordinal) ||
                !MachinePlacementRangeContains(locationRow.Value, x.Value, y.Value))
            {
                reasons.Add("place_cookout_kit_exact_tile_not_native_legal");
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? PlacementInventoryRow(JsonElement context, int slot, string qualifiedItemId)
    {
        if (!context.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "inventory_slot_index", -1) == slot &&
                string.Equals(ReadString(row, "qualified_item_id"), qualifiedItemId, StringComparison.Ordinal))
            {
                return row;
            }
        }
        return null;
    }

    private static JsonElement? PlacementLocationRow(JsonElement row, string locationId)
    {
        if (!row.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var location in locations.EnumerateArray())
        {
            if (location.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(location, "location_id"), locationId, StringComparison.OrdinalIgnoreCase))
            {
                return location;
            }
        }
        return null;
    }
}
