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
    private const string GrangeNativeContract =
        "Event.checkAction(festival_fall16_buildings_349_350_351)->FarmerTeam.grangeMutex->StorageContainer(9x3,Event.onGrangeChange,Utility.highlightSmallObjects)->one_native_remove_or_place_click_pair->okButton->mutex_release";

    private static CompiledActionStep[] CompileManageGrangeDisplayStep(SmallModelAction action)
    {
        var displaySlot = ReadIntParameter(action, "display_slot_index");
        var operation = ReadParameter(action, "operation");
        if (!displaySlot.HasValue || operation is not ("place" or "remove"))
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "manage_grange_display",
                "grange:" + operation + ":display_slot=" + displaySlot.Value + ":item=" + ReadParameter(action, "qualified_item_id"),
                "grange_score=" + ReadParameter(action, "score_after") +
                    ";occupied_slots=" + ReadParameter(action, "occupied_slots_after") +
                    ";objective=" + ReadParameter(action, "objective"),
                240)
        };
    }

    private static string[] ValidateGrangeDisplayPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.manage_grange_display")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var operation = ReadParameter(action, "operation");
        var objective = ReadParameter(action, "objective");
        var displaySlot = ReadIntParameter(action, "display_slot_index");
        var inventorySlot = ReadIntParameter(action, "inventory_slot_index");
        var sinkSlot = ReadIntParameter(action, "sink_inventory_slot_index");
        var before = ReadIntParameter(action, "inventory_stack_before");
        var after = ReadIntParameter(action, "inventory_stack_after");
        var scoreBefore = ReadIntParameter(action, "score_before");
        var scoreAfter = ReadIntParameter(action, "score_after");
        var occupiedBefore = ReadIntParameter(action, "occupied_slots_before");
        var occupiedAfter = ReadIntParameter(action, "occupied_slots_after");
        var interactionX = ReadIntParameter(action, "interaction_tile_x");
        var interactionY = ReadIntParameter(action, "interaction_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        if (operation is not ("place" or "remove") ||
            objective is not ("prepare_best_available_display" or "retrieve_after_judging") ||
            !displaySlot.HasValue || displaySlot is < 0 or > 8 ||
            !scoreBefore.HasValue || !scoreAfter.HasValue ||
            !occupiedBefore.HasValue || !occupiedAfter.HasValue ||
            !interactionX.HasValue || !interactionY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(interactionX.Value - standX.Value) + Math.Abs(interactionY.Value - standY.Value) != 1 ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "runtime_type")) ||
            ReadParameter(action, "festival_id") != "festival_fall16" ||
            ReadParameter(action, "native_contract") != GrangeNativeContract ||
            !TryBoolParameter(action, "grange_judged", out var judged) ||
            operation == "place" && (!inventorySlot.HasValue || inventorySlot.Value < 0 || !before.HasValue || before.Value < 1 || after != before - 1 || occupiedAfter != occupiedBefore + 1) ||
            operation == "remove" && (!sinkSlot.HasValue || sinkSlot.Value < 0 || occupiedAfter != occupiedBefore - 1) ||
            objective == "retrieve_after_judging" && !judged ||
            objective == "prepare_best_available_display" && judged)
            return new[] { "grange_display_typed_projection_required" };

        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("grange_display_menu_must_be_clear");
        if (!string.Equals(ReadParameter(action, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal))
            reasons.Add("grange_display_location_mismatch");

        var projection = ReadStateFieldValue(snapshot, "player", "grange_display");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_fingerprint") != ReadParameter(action, "grange_projection_fingerprint") ||
            ReadString(projection.Value, "gate_status") != "ready" ||
            ReadBool(projection.Value, "mutex_locked_by_other") == true ||
            ReadString(projection.Value, "festival_id") != "festival_fall16" ||
            ReadBool(projection.Value, "grange_judged") != judged ||
            ReadString(projection.Value, "native_contract") != GrangeNativeContract ||
            !projection.Value.TryGetProperty("next_operation", out var projected) || projected.ValueKind != JsonValueKind.Object ||
            ReadString(projected, "status") != "ready" ||
            ReadString(projected, "operation") != operation ||
            ReadString(projected, "objective") != objective ||
            ReadInt(projected, "display_slot_index") != displaySlot.Value ||
            ReadInt(projected, "inventory_slot_index") != inventorySlot.GetValueOrDefault(-1) ||
            ReadInt(projected, "sink_inventory_slot_index") != sinkSlot.GetValueOrDefault(-1) ||
            ReadInt(projected, "inventory_stack_before") != before.GetValueOrDefault(-1) ||
            ReadInt(projected, "inventory_stack_after") != after.GetValueOrDefault(-1) ||
            ReadString(projected, "qualified_item_id") != ReadParameter(action, "qualified_item_id") ||
            ReadString(projected, "item_id") != ReadParameter(action, "item_id") ||
            ReadString(projected, "runtime_type") != ReadParameter(action, "runtime_type") ||
            ReadInt(projected, "quality") != ReadIntParameter(action, "quality") ||
            ReadInt(projected, "actual_sell_price") != ReadIntParameter(action, "actual_sell_price") ||
            ReadInt(projected, "item_points") != ReadIntParameter(action, "item_points") ||
            ReadString(projected, "scoring_group") != ReadParameter(action, "scoring_group") ||
            ReadInt(projected, "score_before") != scoreBefore.Value ||
            ReadInt(projected, "score_after") != scoreAfter.Value ||
            ReadInt(projected, "occupied_slots_before") != occupiedBefore.Value ||
            ReadInt(projected, "occupied_slots_after") != occupiedAfter.Value)
            reasons.Add("grange_display_projection_drifted");

        if (!GrangeProjectionContainsInteractionTile(projection, interactionX.Value, interactionY.Value))
            reasons.Add("grange_display_interaction_tile_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool GrangeProjectionContainsInteractionTile(JsonElement? projection, int x, int y)
    {
        if (!projection.HasValue || !projection.Value.TryGetProperty("interaction_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return false;
        return rows.EnumerateArray().Any(row => ReadInt(row, "tile_x", -1) == x && ReadInt(row, "tile_y", -1) == y && ReadInt(row, "tile_index", -1) is 349 or 350 or 351);
    }
}
