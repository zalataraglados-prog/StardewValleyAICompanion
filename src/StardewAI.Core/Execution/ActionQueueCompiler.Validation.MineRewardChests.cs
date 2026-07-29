using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateMineRewardChestPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.claim_mine_reward_chest")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var quantity = ReadIntParameter(action, "quantity");
            var quality = ReadIntParameter(action, "expected_output_quality");
            var luckXp = ReadIntParameter(action, "expected_skill_experience_delta");
            var nativeLuckCall = ReadIntParameter(action, "native_gain_experience_call_amount");
            var stardropMaxStaminaDelta = ReadIntParameter(action, "expected_stardrop_max_stamina_delta");
            if (!x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue || !quantity.HasValue || !quality.HasValue || !luckXp.HasValue || !nativeLuckCall.HasValue || !stardropMaxStaminaDelta.HasValue)
            {
                return new[] { "mine_reward_chest_typed_target_fields_required" };
            }
            if (Math.Abs(x.Value - standX.Value) + Math.Abs(y.Value - standY.Value) != 1)
            {
                reasons.Add("mine_reward_chest_stand_not_adjacent");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("mine_reward_chest_menu_must_be_clear");
            }
            if (!string.Equals(ReadParameter(action, "target_runtime_type"), "StardewValley.Objects.Chest", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "interaction_kind"), "overlay_object", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "expected_action_type"), "MineRewardChest", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "native_contract"), "one_reward_open_then_wait_dumpContents_then_empty_chest_cleanup_checkAction", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "expected_skill_id"), "luck", StringComparison.Ordinal))
            {
                reasons.Add("mine_reward_chest_native_contract_incomplete");
            }

            var chests = ReadStateFieldValue(snapshot, "mining", "reward_chests");
            var chest = chests.HasValue && chests.Value.ValueKind == JsonValueKind.Array
                ? chests.Value.EnumerateArray().FirstOrDefault(value => ReadInt(value, "tile_x") == x.Value && ReadInt(value, "tile_y") == y.Value)
                : default;
            if (chest.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("mine_reward_chest_target_not_found_or_drifted");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }
            var item = chest.TryGetProperty("item", out var itemValue) ? itemValue : default;
            if (!string.Equals(ReadString(chest, "status"), "ready", StringComparison.Ordinal) || ReadBool(chest, "contains_skull_key") == true)
            {
                reasons.Add("mine_reward_chest_not_ready_by_transparent_state");
            }
            if (!string.Equals(ReadString(chest, "runtime_type"), ReadParameter(action, "target_runtime_type"), StringComparison.Ordinal) ||
                !string.Equals(ReadString(chest, "reward_branch"), ReadParameter(action, "reward_branch"), StringComparison.Ordinal) ||
                !string.Equals(ReadString(item, "qualified_item_id"), ReadParameter(action, "qualified_item_id"), StringComparison.OrdinalIgnoreCase) ||
                ReadInt(item, "quantity") != quantity.Value || ReadInt(item, "quality") != quality.Value ||
                ReadInt(chest, "native_gain_experience_call_amount") != nativeLuckCall.Value ||
                ReadInt(chest, "expected_luck_experience_delta") != luckXp.Value ||
                ReadInt(chest, "expected_stardrop_max_stamina_delta") != stardropMaxStaminaDelta.Value ||
                !string.Equals(ReadString(chest, "expected_output_items_json"), ReadParameter(action, "expected_output_items_json"), StringComparison.Ordinal))
            {
                reasons.Add("mine_reward_chest_projection_drifted");
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
