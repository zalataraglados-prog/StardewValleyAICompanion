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
    private static string[] ValidateMiningResourceClumpPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.break_resource_clump")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var anchorX = ReadIntParameter(action, "resource_clump_tile_x");
        var anchorY = ReadIntParameter(action, "resource_clump_tile_y");
        var width = ReadIntParameter(action, "resource_clump_width");
        var height = ReadIntParameter(action, "resource_clump_height");
        var index = ReadIntParameter(
            action,
            "resource_clump_parent_sheet_index");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var toolSlot = ReadIntParameter(action, "tool_slot_index");
        var maximumHits = ReadIntParameter(action, "max_tool_swings");
        var health = ReadDoubleParameter(action, "resource_clump_health");
        var minimumUpgrade = ReadIntParameter(
            action,
            "resource_clump_minimum_upgrade_level");
        var toolUpgrade = ReadIntParameter(
            action,
            "resource_clump_tool_upgrade_level");
        var toolAdditionalPower = ReadIntParameter(
            action,
            "resource_clump_tool_additional_power");
        var toolEffectiveUpgrade = ReadIntParameter(
            action,
            "resource_clump_tool_effective_upgrade_level");
        var damage = ReadDoubleParameter(
            action,
            "resource_clump_damage_per_hit");
        if (!anchorX.HasValue || !anchorY.HasValue ||
            !width.HasValue || !height.HasValue || !index.HasValue ||
            !targetX.HasValue || !targetY.HasValue ||
            !standX.HasValue || !standY.HasValue ||
            !toolSlot.HasValue || !maximumHits.HasValue ||
            !health.HasValue || !minimumUpgrade.HasValue ||
            !toolUpgrade.HasValue || !toolAdditionalPower.HasValue ||
            !toolEffectiveUpgrade.HasValue || !damage.HasValue ||
            string.IsNullOrWhiteSpace(
                ReadParameter(
                    action,
                    "resource_clump_tool_qualified_item_id")) ||
            string.IsNullOrWhiteSpace(
                ReadParameter(action, "expected_output_items_json")) ||
            string.IsNullOrWhiteSpace(
                ReadParameter(action, "target_runtime_type")))
        {
            return new[]
            {
                "mining_resource_clump_typed_projection_fields_required"
            };
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("mining_resource_clump_menu_must_be_clear");
        }
        if (width.Value < 1 || height.Value < 1 ||
            !TileInsideRectangle(
                targetX.Value,
                targetY.Value,
                anchorX.Value,
                anchorY.Value,
                width.Value,
                height.Value) ||
            TileInsideRectangle(
                standX.Value,
                standY.Value,
                anchorX.Value,
                anchorY.Value,
                width.Value,
                height.Value) ||
            Math.Abs(standX.Value - targetX.Value) +
                Math.Abs(standY.Value - targetY.Value) != 1)
        {
            reasons.Add(
                "mining_resource_clump_hit_or_stand_geometry_invalid");
        }

        var clumps = ReadStateFieldValue(
            snapshot,
            "mining",
            "resource_clumps");
        var clump = clumps.HasValue &&
            clumps.Value.ValueKind == JsonValueKind.Array
                ? clumps.Value.EnumerateArray().FirstOrDefault(row =>
                    ReadInt(row, "tile_x") == anchorX.Value &&
                    ReadInt(row, "tile_y") == anchorY.Value &&
                    ReadInt(row, "width") == width.Value &&
                    ReadInt(row, "height") == height.Value &&
                    ReadInt(row, "parent_sheet_index") == index.Value)
                : default;
        if (clump.ValueKind != JsonValueKind.Object)
        {
            reasons.Add(
                "mining_resource_clump_target_not_found_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        var status = ReadString(clump, "executor_status");
        if (!string.Equals(
                status,
                "native_executor_available",
                StringComparison.Ordinal))
        {
            reasons.Add(
                string.IsNullOrWhiteSpace(status)
                    ? "mining_resource_clump_projection_unavailable"
                    : status);
        }
        var expectedHits = NullableReadInt(
            clump,
            "expected_hits_remaining");
        if (!expectedHits.HasValue ||
            maximumHits.Value < expectedHits.Value)
        {
            reasons.Add(
                "mining_resource_clump_tool_swing_budget_insufficient");
        }
        if (NullableReadInt(clump, "selected_tool_slot_index") !=
                toolSlot.Value ||
            NullableReadInt(clump, "minimum_upgrade_level") !=
                minimumUpgrade.Value ||
            NullableReadInt(clump, "selected_tool_upgrade_level") !=
                toolUpgrade.Value ||
            NullableReadInt(clump, "selected_tool_additional_power") !=
                toolAdditionalPower.Value ||
            NullableReadInt(
                clump,
                "selected_tool_effective_upgrade_level") !=
                toolEffectiveUpgrade.Value ||
            !string.Equals(
                ReadString(
                    clump,
                    "selected_tool_qualified_item_id"),
                ReadParameter(
                    action,
                    "resource_clump_tool_qualified_item_id"),
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                ReadString(clump, "required_tool"),
                ReadParameter(action, "required_tool_kind"),
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(clump, "runtime_type"),
                ReadParameter(action, "target_runtime_type"),
                StringComparison.Ordinal) ||
            Math.Abs(ReadDouble(clump, "health") - health.Value) >
                1e-6 ||
            Math.Abs(ReadDouble(clump, "damage_per_hit") -
                damage.Value) > 1e-6)
        {
            reasons.Add(
                "mining_resource_clump_tool_or_health_projection_drifted");
        }
        if (!string.Equals(
                ReadString(
                    clump,
                    "expected_core_output_items_json"),
                ReadParameter(action, "expected_output_items_json"),
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(
                    clump,
                    "possible_secret_note_qualified_item_id"),
                ReadParameter(
                    action,
                    "possible_secret_note_qualified_item_id"),
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(
                "mining_resource_clump_output_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
