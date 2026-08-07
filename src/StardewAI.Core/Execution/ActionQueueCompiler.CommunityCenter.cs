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
                    ";bundle_complete=" + ReadParameter(action, "expected_bundle_complete_after") +
                    ";bundle_reward_available=" + ReadParameter(action, "expected_bundle_reward_available_after") +
                    ";area_complete=" + ReadParameter(action, "expected_area_complete_after") +
                    ";new_note_areas=" + ReadParameter(action, "newly_appearing_note_area_ids_json"),
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
        var noteX = ReadIntParameter(action, "community_center_note_tile_x");
        var noteY = ReadIntParameter(action, "community_center_note_tile_y");
        var interactionX = ReadIntParameter(action, "interaction_tile_x");
        var interactionY = ReadIntParameter(action, "interaction_tile_y");
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
        if (!slot.HasValue || slot.Value < 0 || !noteX.HasValue || !noteY.HasValue || !interactionX.HasValue || !interactionY.HasValue ||
            !targetX.HasValue || !targetY.HasValue || targetX != interactionX || targetY != interactionY ||
            !standX.HasValue || !standY.HasValue || Math.Abs(interactionX.Value - standX.Value) + Math.Abs(interactionY.Value - standY.Value) != 1 ||
            !bundleId.HasValue || bundleId.Value < 0 || !areaId.HasValue || areaId.Value < 0 || !ingredientIndex.HasValue || ingredientIndex.Value < 0 ||
            !quality.HasValue || quality.Value < 0 || !requiredStack.HasValue || requiredStack.Value < 1 ||
            !stackBefore.HasValue || !stackAfter.HasValue || stackBefore.Value < requiredStack.Value || stackAfter.Value != stackBefore.Value - requiredStack.Value ||
            !inventoryTotalBefore.HasValue || !inventoryTotalAfter.HasValue || inventoryTotalBefore.Value < stackBefore.Value || inventoryTotalAfter.Value != inventoryTotalBefore.Value - requiredStack.Value ||
            !requiredSlots.HasValue || requiredSlots.Value < 1 || !completedBefore.HasValue || !completedAfter.HasValue || completedAfter.Value <= completedBefore.Value ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "bundle_data_key")) || string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) ||
            !TryBoolParameter(action, "expected_bundle_complete_after", out var completesBundle) || completesBundle != (completedAfter.Value >= requiredSlots.Value) ||
            !TryBoolParameter(action, "expected_bundle_reward_available_after", out _) ||
            !ReadIntParameter(action, "expected_complete_bundle_count_after").HasValue ||
            !TryBoolParameter(action, "completes_area", out _) ||
            !TryBoolParameter(action, "expected_area_complete_after", out _) ||
            !TryBoolParameter(action, "expected_area_completion_mail_pending_after", out _) ||
            !TryBoolParameter(action, "expected_bulletin_thank_you_pending_after", out _) ||
            !TryBoolParameter(action, "expected_all_areas_complete_after", out _) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "area_completion_mail_id")) ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "newly_appearing_note_area_ids_json")) ||
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
            ReadBool(progress.Value, "can_read_junimo_text") != true ||
            ReadInt(progress.Value, "bundle_data_row_count") != ReadInt(progress.Value, "projected_bundle_row_count") ||
            ReadInt(progress.Value, "unavailable_bundle_row_count") != 0 ||
            !TryFindCommunityCenterBundle(progress.Value, bundleId.Value, ReadParameter(action, "bundle_data_key"), out var bundle) ||
            ReadString(bundle, "projection_status") != "exact" || ReadInt(bundle, "area_id") != areaId.Value ||
            ReadString(bundle, "area_name") != ReadParameter(action, "bundle_area_name") ||
            NullableReadInt(bundle, "note_tile_x") != noteX || NullableReadInt(bundle, "note_tile_y") != noteY ||
            NullableReadInt(bundle, "interaction_tile_x") != interactionX || NullableReadInt(bundle, "interaction_tile_y") != interactionY ||
            ReadBool(bundle, "note_appears") != true || ReadBool(bundle, "area_mutex_locked") == true ||
            ReadInt(bundle, "required_slot_count") != requiredSlots.Value || ReadInt(bundle, "completed_ingredient_count") != completedBefore.Value ||
            ReadString(bundle, "area_completion_mail_id") != ReadParameter(action, "area_completion_mail_id") ||
            !bundle.TryGetProperty("ingredients", out var ingredients) || ingredients.ValueKind != JsonValueKind.Array ||
            completedAfter.Value != (completesBundle ? ingredients.GetArrayLength() : completedBefore.Value + 1) ||
            !TryFindCommunityCenterDonationCandidate(bundle, slot.Value, ingredientIndex.Value, ReadParameter(action, "qualified_item_id"), out var candidate) ||
            ReadString(candidate, "action_status") != "ready" || ReadString(candidate, "item_id") != ReadParameter(action, "item_id") ||
            ReadString(candidate, "runtime_type") != ReadParameter(action, "target_runtime_type") || ReadInt(candidate, "quality") != quality.Value ||
            ReadInt(candidate, "required_stack") != requiredStack.Value || ReadInt(candidate, "stack_before") != stackBefore.Value ||
            ReadInt(candidate, "stack_after") != stackAfter.Value || ReadInt(candidate, "inventory_item_total_before") != inventoryTotalBefore.Value ||
            ReadInt(candidate, "inventory_item_total_after") != inventoryTotalAfter.Value || ReadInt(candidate, "completed_ingredient_count_before") != completedBefore.Value ||
            ReadInt(candidate, "completed_ingredient_count_after") != completedAfter.Value || ReadBool(candidate, "completes_bundle") != completesBundle ||
            ReadBool(candidate, "expected_bundle_reward_available_after") != ReadBoolParameter(action, "expected_bundle_reward_available_after") ||
            ReadInt(candidate, "expected_complete_bundle_count_after") != ReadIntParameter(action, "expected_complete_bundle_count_after") ||
            ReadBool(candidate, "completes_area") != ReadBoolParameter(action, "completes_area") ||
            ReadBool(candidate, "expected_area_complete_after") != ReadBoolParameter(action, "expected_area_complete_after") ||
            ReadBool(candidate, "expected_area_completion_mail_pending_after") != ReadBoolParameter(action, "expected_area_completion_mail_pending_after") ||
            ReadBool(candidate, "expected_bulletin_thank_you_pending_after") != ReadBoolParameter(action, "expected_bulletin_thank_you_pending_after") ||
            ReadBool(candidate, "expected_all_areas_complete_after") != ReadBoolParameter(action, "expected_all_areas_complete_after") ||
            CommunityCenterRawJson(candidate, "newly_appearing_note_area_ids") != ReadParameter(action, "newly_appearing_note_area_ids_json"))
        {
            reasons.Add("community_center_donation_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string CommunityCenterRawJson(JsonElement row, string propertyName)
    {
        return row.TryGetProperty(propertyName, out var value) ? value.GetRawText() : string.Empty;
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
