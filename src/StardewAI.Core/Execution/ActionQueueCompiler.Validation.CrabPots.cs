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
    private static string[] ValidateCollectCrabPotPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.collect_crab_pot")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var outputId = ReadParameter(action, "qualified_item_id");
        var quantity = ReadIntParameter(action, "quantity");
        var outputItemsJson = ReadParameter(action, "expected_output_items_json");
        var expectedExperience = ReadIntParameter(action, "expected_skill_experience_delta");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            string.IsNullOrWhiteSpace(outputId) || !quantity.HasValue || string.IsNullOrWhiteSpace(outputItemsJson) ||
            !expectedExperience.HasValue ||
            !string.Equals(ReadParameter(action, "expected_skill_id"), "fishing", StringComparison.Ordinal))
        {
            return new[] { "collect_crab_pot_typed_projection_required" };
        }
        if (Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
        {
            reasons.Add("collect_crab_pot_stand_tile_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("collect_crab_pot_menu_must_be_clear");
        }
        var targetLocation = ReadParameter(action, "target_location");
        if (!string.IsNullOrWhiteSpace(targetLocation) &&
            !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("collect_crab_pot_target_location_mismatch");
        }

        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        var target = objects.HasValue && objects.Value.ValueKind == JsonValueKind.Array
            ? objects.Value.EnumerateArray().FirstOrDefault(item =>
                ReadInt(item, "tile_x") == targetX.Value && ReadInt(item, "tile_y") == targetY.Value)
            : default;
        if (target.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(target, "type"), ReadParameter(action, "target_runtime_type"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(target, "crab_pot_collect_status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("collect_crab_pot_target_not_ready_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        if (!string.Equals(ReadString(target, "crab_pot_output_qualified_item_id"), outputId, StringComparison.OrdinalIgnoreCase) ||
            ReadInt(target, "crab_pot_output_stack_on_collect") != quantity.Value ||
            !string.Equals(ReadString(target, "crab_pot_expected_output_items_json"), outputItemsJson, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "expected_output_state_context"), "post_inventory_receive", StringComparison.Ordinal) ||
            !string.Equals(ReadString(target, "crab_pot_output_state_context"), "post_inventory_receive", StringComparison.Ordinal))
        {
            reasons.Add("collect_crab_pot_output_projection_drifted");
        }
        if (!string.Equals(ReadString(target, "crab_pot_bait_qualified_item_id"), ReadParameter(action, "expected_container_bait_qualified_item_id") ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("collect_crab_pot_bait_projection_drifted");
        }
        if (ReadString(target, "crab_pot_experience_projection_status") != "exact" ||
            ReadInt(target, "crab_pot_fishing_experience_on_success_min") != expectedExperience.Value ||
            ReadInt(target, "crab_pot_fishing_experience_on_success_max") != expectedExperience.Value)
        {
            reasons.Add("collect_crab_pot_experience_projection_drifted");
        }

        CompareIntProjection(action, target, reasons, "expected_fish_collection_eligible", "crab_pot_fish_collection_eligible");
        CompareIntProjection(action, target, reasons, "book_double_roll_succeeded", "crab_pot_book_double_roll_succeeded");
        CompareIntProjection(action, target, reasons, "book_crabbing_owned", "crab_pot_book_crabbing_owned");
        CompareIntProjection(action, target, reasons, "book_double_applied", "crab_pot_book_double_applied");
        CompareIntProjection(action, target, reasons, "expected_fish_caught_count_before", "crab_pot_fish_caught_count_before");
        CompareIntProjection(action, target, reasons, "expected_fish_caught_count_after", "crab_pot_fish_caught_count_after");
        CompareIntProjection(action, target, reasons, "expected_fish_caught_max_size_before", "crab_pot_fish_caught_max_size_before");
        CompareIntProjection(action, target, reasons, "expected_catch_size_min", "crab_pot_catch_size_min");
        CompareIntProjection(action, target, reasons, "expected_catch_size_max", "crab_pot_catch_size_max");
        if (!string.Equals(ReadParameter(action, "catch_size_projection_status"), ReadString(target, "crab_pot_catch_size_projection_status"), StringComparison.Ordinal))
        {
            reasons.Add("collect_crab_pot_catch_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void CompareIntProjection(
        SmallModelAction action,
        JsonElement target,
        ICollection<string> reasons,
        string parameterName,
        string fieldName)
    {
        var expected = ReadIntParameter(action, parameterName);
        var actual = fieldName is "crab_pot_fish_collection_eligible" or
                "crab_pot_book_double_roll_succeeded" or
                "crab_pot_book_crabbing_owned" or
                "crab_pot_book_double_applied"
            ? ReadBool(target, fieldName) == true ? 1 : 0
            : ReadInt(target, fieldName);
        if (!expected.HasValue || expected.Value != actual)
        {
            reasons.Add("collect_crab_pot_catch_projection_drifted");
        }
    }
}
