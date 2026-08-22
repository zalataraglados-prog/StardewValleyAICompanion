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
    private const string CrabPotQualifiedItemId = "(O)710";
    private const string CrabPotNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)710)->CrabPot.placementAction(owner=current_player)";

    private static CompiledActionStep[] CompilePlaceCrabPotStep(SmallModelAction action)
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
                "place_crab_pot",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + CrabPotQualifiedItemId,
                "current_location.objects[" + x.Value + "," + y.Value + "].runtime_type=StardewValley.Objects.CrabPot;" +
                    "current_location.objects[" + x.Value + "," + y.Value + "].owner=current_player;" +
                    "current_location.objects[" + x.Value + "," + y.Value + "].ready_for_harvest=false;" +
                    "player.inventory[" + slot.Value + "].stack_decreases=1",
                60)
        };
    }

    private static string[] ValidatePlaceCrabPotPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.place_crab_pot")
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
        var expectedOwner = ReadLongParameterExact(action, "expected_owner_player_id");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            !expectedStack.HasValue || expectedStack.Value < 1 || !expectedOwner.HasValue || string.IsNullOrWhiteSpace(location))
        {
            reasons.Add("place_crab_pot_typed_target_fields_required");
            return reasons.ToArray();
        }
        var targetX = x.GetValueOrDefault();
        var targetY = y.GetValueOrDefault();
        var exactStandX = standX.GetValueOrDefault();
        var exactStandY = standY.GetValueOrDefault();
        var exactSlot = slot.GetValueOrDefault();
        var exactStack = expectedStack.GetValueOrDefault();
        var exactOwner = expectedOwner.GetValueOrDefault();
        var exactLocation = location!;
        if (!string.Equals(ReadParameter(action, "qualified_item_id"), CrabPotQualifiedItemId, StringComparison.Ordinal))
        {
            reasons.Add("place_crab_pot_exact_item_identity_required");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "crab_pot_placement_reason")))
        {
            reasons.Add("place_crab_pot_reason_required");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "production_signature")))
        {
            reasons.Add("place_crab_pot_production_signature_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), CrabPotNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("place_crab_pot_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("place_crab_pot_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("place_crab_pot_requires_loaded_target_location");
        }
        if (Math.Abs(exactStandX - targetX) + Math.Abs(exactStandY - targetY) != 1 ||
            PlacementCollisionGridBlocks(snapshot, exactStandX, exactStandY))
        {
            reasons.Add("place_crab_pot_adjacent_stand_geometry_invalid");
        }

        var context = ReadStateFieldValue(snapshot, "player", "crab_pot_placement");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("place_crab_pot_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (!string.Equals(
                ReadParameter(action, "placement_projection_fingerprint"),
                ReadString(context.Value, "static_projection_fingerprint"),
                StringComparison.Ordinal))
        {
            reasons.Add("place_crab_pot_projection_fingerprint_drifted");
        }
        if (ReadInt64(context.Value, "owner_player_id") != exactOwner)
        {
            reasons.Add("place_crab_pot_owner_identity_drifted");
        }

        var row = PlacementInventoryRow(context.Value, exactSlot, CrabPotQualifiedItemId);
        if (!row.HasValue || ReadInt(row.Value, "stack") != exactStack)
        {
            reasons.Add("place_crab_pot_inventory_identity_drifted");
        }
        else
        {
            var locationRow = PlacementLocationRow(row.Value, exactLocation);
            var range = locationRow.HasValue ? PlacementRangeAt(locationRow.Value, targetX, targetY) : null;
            if (!locationRow.HasValue ||
                !string.Equals(ReadString(locationRow.Value, "placement_probe_status"), "native_legal_water_tiles_available", StringComparison.Ordinal) ||
                !range.HasValue)
            {
                reasons.Add("place_crab_pot_exact_water_tile_not_native_legal");
            }
            else if (!string.Equals(
                         ReadParameter(action, "production_signature"),
                         ReadString(range.Value, "production_signature"),
                         StringComparison.Ordinal))
            {
                reasons.Add("place_crab_pot_production_context_drifted");
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? PlacementRangeAt(JsonElement locationRow, int x, int y)
    {
        if (!locationRow.TryGetProperty("static_legal_tile_ranges", out var ranges) || ranges.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var range in ranges.EnumerateArray())
        {
            if (range.ValueKind == JsonValueKind.Object && ReadInt(range, "y", -1) == y &&
                x >= ReadInt(range, "start_x", int.MaxValue) && x <= ReadInt(range, "end_x", int.MinValue))
            {
                return range;
            }
        }
        return null;
    }

    private static long? ReadLongParameterExact(SmallModelAction action, string name) =>
        long.TryParse(ReadParameter(action, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static long ReadInt64(JsonElement element, string property, long fallback = 0) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var parsed)
            ? parsed
            : fallback;
}
