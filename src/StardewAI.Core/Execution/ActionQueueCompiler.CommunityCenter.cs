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
    private static CompiledActionStep[] CompileDonateCommunityCenterItemStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var itemId = ReadParameter(action, "qualified_item_id");
        var bundleId = ReadIntParameter(action, "bundle_id");
        var ingredientIndex = ReadIntParameter(action, "bundle_ingredient_index");
        if (!slot.HasValue || string.IsNullOrWhiteSpace(itemId) || !bundleId.HasValue || !ingredientIndex.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "donate_community_center_item",
                "community_center:bundle=" + bundleId.Value + ":ingredient=" + ingredientIndex.Value + ":slot=" + slot.Value + ":item=" + itemId,
                "world_progress.community_center.bundle_rows[" + bundleId.Value + "].ingredients[" + ingredientIndex.Value + "].completed=true" +
                    ";player.inventory[" + slot.Value + "].stack=" + ReadParameter(action, "expected_stack_after") +
                    ";bundle_complete=" + ReadParameter(action, "expected_bundle_complete_after"),
                300)
        };
    }

    private static string[] ValidateCommunityCenterDonationPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.donate_community_center_item")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var noteX = ReadIntParameter(action, "note_tile_x");
        var noteY = ReadIntParameter(action, "note_tile_y");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var bundleId = ReadIntParameter(action, "bundle_id");
        var areaId = ReadIntParameter(action, "bundle_area_id");
        var ingredientIndex = ReadIntParameter(action, "bundle_ingredient_index");
        var quality = ReadIntParameter(action, "expected_item_quality");
        var requiredStack = ReadIntParameter(action, "required_stack");
        var inventoryTotalBefore = ReadIntParameter(action, "inventory_item_total_before");
        var inventoryTotalAfter = ReadIntParameter(action, "inventory_item_total_after");
        var stackBefore = ReadIntParameter(action, "expected_stack_before");
        var stackAfter = ReadIntParameter(action, "expected_stack_after");
        var requiredSlots = ReadIntParameter(action, "bundle_required_slot_count");
        var completedBefore = ReadIntParameter(action, "expected_bundle_completed_count_before");
        var completedAfter = ReadIntParameter(action, "expected_bundle_completed_count_after");
        if (!slot.HasValue || slot.Value < 0 || !noteX.HasValue || !noteY.HasValue ||
            !targetX.HasValue || !targetY.HasValue || targetX != noteX || targetY != noteY ||
            !standX.HasValue || !standY.HasValue || Math.Abs(noteX.Value - standX.Value) + Math.Abs(noteY.Value - standY.Value) != 1 ||
            !bundleId.HasValue || bundleId.Value < 0 || !areaId.HasValue || areaId.Value < 0 || !ingredientIndex.HasValue || ingredientIndex.Value < 0 ||
            !quality.HasValue || quality.Value < 0 || !requiredStack.HasValue || requiredStack.Value < 1 ||
            !stackBefore.HasValue || !stackAfter.HasValue || stackBefore.Value < requiredStack.Value || stackAfter.Value != stackBefore.Value - requiredStack.Value ||
            !inventoryTotalBefore.HasValue || !inventoryTotalAfter.HasValue || inventoryTotalBefore.Value < stackBefore.Value || inventoryTotalAfter.Value != inventoryTotalBefore.Value - requiredStack.Value ||
            !requiredSlots.HasValue || requiredSlots.Value < 1 || !completedBefore.HasValue || !completedAfter.HasValue || completedAfter.Value <= completedBefore.Value ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "bundle_data_key")) || string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) ||
            !TryBoolParameter(action, "expected_bundle_complete_after", out var completesBundle) || completesBundle != (completedAfter.Value >= requiredSlots.Value) ||
            ReadParameter(action, "native_contract") != "CommunityCenter.checkBundle_then_JunimoNoteMenu.receiveLeftClick_bundle_inventory_and_ingredient_slot_then_exitThisMenu")
        {
            return new[] { "community_center_donation_typed_projection_required" };
        }

        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("community_center_donation_menu_must_be_clear");
        }
        if (!string.Equals(ReadParameter(action, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("community_center_donation_target_location_mismatch");
        }

        var progress = ReadStateFieldValue(snapshot, "world_progress", "community_center");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
            ReadString(progress.Value, "route_state") != ReadParameter(action, "route_state") ||
            ReadString(progress.Value, "route_state") is not ("undecided" or "community_center_locked") ||
            ReadBool(progress.Value, "community_center_is_current_location") != true ||
            ReadInt(progress.Value, "bundle_data_row_count") != ReadInt(progress.Value, "projected_bundle_row_count") ||
            ReadInt(progress.Value, "unavailable_bundle_row_count") != 0 ||
            !TryFindCommunityCenterBundle(progress.Value, bundleId.Value, ReadParameter(action, "bundle_data_key"), out var bundle) ||
            ReadString(bundle, "projection_status") != "exact" || ReadInt(bundle, "area_id") != areaId.Value ||
            ReadString(bundle, "area_name") != ReadParameter(action, "bundle_area_name") ||
            NullableReadInt(bundle, "note_tile_x") != noteX || NullableReadInt(bundle, "note_tile_y") != noteY ||
            ReadBool(bundle, "note_appears") != true || ReadBool(bundle, "area_mutex_locked") == true ||
            ReadInt(bundle, "required_slot_count") != requiredSlots.Value || ReadInt(bundle, "completed_ingredient_count") != completedBefore.Value ||
            !bundle.TryGetProperty("ingredients", out var ingredients) || ingredients.ValueKind != JsonValueKind.Array ||
            completedAfter.Value != (completesBundle ? ingredients.GetArrayLength() : completedBefore.Value + 1) ||
            !TryFindCommunityCenterDonationCandidate(bundle, slot.Value, ingredientIndex.Value, ReadParameter(action, "qualified_item_id"), out var candidate) ||
            ReadString(candidate, "action_status") != "ready" || ReadString(candidate, "item_id") != ReadParameter(action, "item_id") ||
            ReadString(candidate, "runtime_type") != ReadParameter(action, "target_runtime_type") || ReadInt(candidate, "quality") != quality.Value ||
            ReadInt(candidate, "required_stack") != requiredStack.Value || ReadInt(candidate, "stack_before") != stackBefore.Value ||
            ReadInt(candidate, "stack_after") != stackAfter.Value || ReadInt(candidate, "inventory_item_total_before") != inventoryTotalBefore.Value ||
            ReadInt(candidate, "inventory_item_total_after") != inventoryTotalAfter.Value || ReadInt(candidate, "completed_ingredient_count_before") != completedBefore.Value ||
            ReadInt(candidate, "completed_ingredient_count_after") != completedAfter.Value || ReadBool(candidate, "completes_bundle") != completesBundle)
        {
            reasons.Add("community_center_donation_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool TryFindCommunityCenterBundle(JsonElement progress, int bundleId, string? dataKey, out JsonElement bundle)
    {
        bundle = default;
        if (!progress.TryGetProperty("bundle_rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "bundle_id") == bundleId && ReadString(row, "bundle_data_key") == dataKey)
            {
                bundle = row;
                return true;
            }
        }
        return false;
    }

    private static bool TryFindCommunityCenterDonationCandidate(JsonElement bundle, int slot, int ingredientIndex, string? qualifiedItemId, out JsonElement candidate)
    {
        candidate = default;
        if (!bundle.TryGetProperty("donation_candidates", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "inventory_slot_index") == slot &&
                ReadInt(row, "ingredient_index") == ingredientIndex && ReadString(row, "qualified_item_id") == qualifiedItemId)
            {
                candidate = row;
                return true;
            }
        }
        return false;
    }
}
