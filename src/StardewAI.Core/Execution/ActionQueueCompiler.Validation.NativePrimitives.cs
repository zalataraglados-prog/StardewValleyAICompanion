using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateNativeMiningPrimitivePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var reasons = new List<string>();
            switch (action.OptionId)
            {
                case "executor.mine_stone":
                case "executor.break_container":
                case "executor.descend_ladder":
                    RequireTile(action, reasons);
                    break;

                case "executor.break_resource_clump":
                    RequireTile(action, reasons);
                    RequireInt(action, reasons, "stand_tile_x");
                    RequireInt(action, reasons, "stand_tile_y");
                    RequireInt(action, reasons, "resource_clump_tile_x");
                    RequireInt(action, reasons, "resource_clump_tile_y");
                    RequireInt(action, reasons, "resource_clump_width");
                    RequireInt(action, reasons, "resource_clump_height");
                    RequireInt(action, reasons, "resource_clump_parent_sheet_index");
                    RequireInt(action, reasons, "tool_slot_index");
                    RequireText(action, reasons, "required_tool_kind");
                    break;

                case "executor.combat_monster":
                    RequireTile(action, reasons);
                    RequireMonsterIdentity(action, reasons);
                    break;

                case "executor.shoot_monster":
                    RequireTile(action, reasons);
                    RequireMonsterIdentity(action, reasons);
                    RequireInt(action, reasons, "slingshot_slot_index");
                    RequireText(action, reasons, "slingshot_ammo_qualified_item_id");
                    break;

                case "executor.place_bomb":
                    RequireTile(action, reasons);
                    RequireInt(action, reasons, "stand_tile_x");
                    RequireInt(action, reasons, "stand_tile_y");
                    RequireInt(action, reasons, "escape_tile_x");
                    RequireInt(action, reasons, "escape_tile_y");
                    RequireInt(action, reasons, "bomb_slot_index");
                    RequireInt(action, reasons, "bomb_radius_tiles");
                    RequireText(action, reasons, "bomb_qualified_item_id");
                    break;

                case "executor.place_staircase":
                    RequireTile(action, reasons);
                    RequireInt(action, reasons, "stand_tile_x");
                    RequireInt(action, reasons, "stand_tile_y");
                    RequireInt(action, reasons, "slot_index");
                    RequireText(action, reasons, "qualified_item_id");
                    RequireInt(action, reasons, "inventory_item_total_before");
                    RequireInt(action, reasons, "inventory_item_total_after");
                    if (!string.Equals(
                            ReadParameter(action, "qualified_item_id"),
                            "(BC)71",
                            StringComparison.Ordinal))
                    {
                        reasons.Add(
                            "staircase_qualified_item_id_must_equal_(BC)71");
                    }
                    break;

                case "executor.consume_food":
                    RequireInt(action, reasons, "slot_index");
                    RequireText(action, reasons, "qualified_item_id");
                    break;

                case "executor.descend_shaft":
                    RequireTile(action, reasons);
                    RequireInt(action, reasons, "expected_mine_level_delta");
                    RequireInt(action, reasons, "expected_mine_level_after");
                    RequireInt(action, reasons, "expected_health_cost");
                    RequireInt(action, reasons, "expected_health_after");
                    break;

                case "executor.exit_mine":
                    RequireTile(action, reasons);
                    RequireText(action, reasons, "expected_target_location");
                    RequireInt(action, reasons, "expected_arrival_tile_x");
                    RequireInt(action, reasons, "expected_arrival_tile_y");
                    RequireText(action, reasons, "mining_step_reason");
                    break;
            }

            ValidateAttachedSlayQuestPlan(action, snapshot, reasons);
            return reasons.ToArray();
        }

        private static string[] ValidateShippingBinPrimitivePlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.ship_inventory_item_to_bin")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            RequireTile(action, reasons);
            RequireInt(action, reasons, "stand_tile_x");
            RequireInt(action, reasons, "stand_tile_y");
            RequireInt(action, reasons, "slot_index");
            RequireText(action, reasons, "qualified_item_id");
            RequireInt(action, reasons, "quantity");
            RequireInt(action, reasons, "expected_unit_price");
            if (ReadIntParameter(action, "quantity") is int quantity && quantity != 1)
            {
                reasons.Add("executor_parameter_must_equal_one:quantity");
            }

            return reasons.ToArray();
        }

        private static void RequireTile(SmallModelAction action, ICollection<string> reasons)
        {
            RequireInt(action, reasons, "target_tile_x");
            RequireInt(action, reasons, "target_tile_y");
        }

        private static void RequireMonsterIdentity(SmallModelAction action, ICollection<string> reasons)
        {
            RequireText(action, reasons, "target_runtime_identity");
            RequireText(action, reasons, "target_runtime_type");
            RequireText(action, reasons, "target_name");
        }

        private static void RequireInt(SmallModelAction action, ICollection<string> reasons, string name)
        {
            if (!ReadIntParameter(action, name).HasValue)
            {
                reasons.Add("executor_parameter_required:" + name);
            }
        }

        private static void RequireText(SmallModelAction action, ICollection<string> reasons, string name)
        {
            if (string.IsNullOrWhiteSpace(ReadParameter(action, name)))
            {
                reasons.Add("executor_parameter_required:" + name);
            }
        }
    }
}
