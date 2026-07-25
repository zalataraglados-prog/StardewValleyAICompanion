using System;
using StardewAI.Contracts.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[] CompileMiningNativePrimitiveStep(SmallModelAction action)
        {
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var targetIdentity = ReadParameter(action, "target_runtime_identity");
            var slotIndex = ReadIntParameter(action, "slot_index");
            var target = action.OptionId switch
            {
                "executor.consume_food" => "inventory_slot=" + (slotIndex?.ToString() ?? "missing"),
                "executor.exit_mine" => "current_loaded_mine",
                "executor.break_resource_clump" =>
                    "clump(" +
                    (ReadIntParameter(action, "resource_clump_tile_x")?.ToString() ?? "missing") + "," +
                    (ReadIntParameter(action, "resource_clump_tile_y")?.ToString() ?? "missing") + ")",
                "executor.combat_monster" or "executor.shoot_monster" =>
                    "monster=" + (string.IsNullOrWhiteSpace(targetIdentity) ? "missing" : targetIdentity) +
                    ":tile(" + (targetX?.ToString() ?? "missing") + "," + (targetY?.ToString() ?? "missing") + ")",
                _ => "tile(" + (targetX?.ToString() ?? "missing") + "," + (targetY?.ToString() ?? "missing") + ")"
            };

            var (kind, effect) = action.OptionId switch
            {
                "executor.mine_stone" => ("mine_stone", "transparent_mine_stone_removed_by_native_pickaxe_lifecycle"),
                "executor.break_container" => ("break_container", "transparent_mine_container_removed_by_native_weapon_lifecycle"),
                "executor.break_resource_clump" => ("break_resource_clump", "transparent_mine_resource_clump_removed_by_native_tool_lifecycle"),
                "executor.combat_monster" => ("combat_monster", "transparent_monster_reaches_native_terminal_state"),
                "executor.shoot_monster" => ("shoot_monster", "transparent_monster_reaches_native_terminal_state_by_projectile"),
                "executor.place_bomb" => ("place_bomb", "native_bomb_explodes_and_player_reaches_verified_escape_tile"),
                "executor.consume_food" => ("consume_food", "native_food_use_consumes_one_item_and_applies_observed_recovery"),
                "executor.descend_ladder" => ("descend_ladder", "native_ladder_interaction_changes_mine_level_by_one"),
                "executor.descend_shaft" => ("descend_shaft", "native_shaft_dialogue_changes_mine_level_and_health_by_verified_deltas"),
                "executor.exit_mine" => ("exit_mine", "native_exit_dialogue_leaves_loaded_mine"),
                _ => (string.Empty, string.Empty)
            };

            if (string.IsNullOrWhiteSpace(kind))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    kind,
                    target,
                    effect,
                    Math.Max(1, (ReadIntParameter(action, "estimated_minutes") ?? 1) * 60))
            };
        }

        private static CompiledActionStep[] CompileShippingBinStep(SmallModelAction action)
        {
            var slotIndex = ReadIntParameter(action, "slot_index");
            var quantity = ReadIntParameter(action, "quantity") ?? 1;
            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            return new[]
            {
                Step(
                    "ship_inventory_item_to_bin",
                    "inventory_slot=" + (slotIndex?.ToString() ?? "missing") +
                    ":item=" + (string.IsNullOrWhiteSpace(qualifiedItemId) ? "missing" : qualifiedItemId) +
                    ":quantity=" + quantity,
                    "native_shipping_bin_receives_exact_verified_inventory_quantity",
                    Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 1) * 60))
            };
        }
    }
}
