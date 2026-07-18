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
    private static CompiledActionStep[] CompilePurchaseFarmhouseUpgradeStep(SmallModelAction action)
    {
        var price = ReadIntParameter(action, "price");
        var moneyAfter = ReadIntParameter(action, "expected_money_after");
        var levelAfter = ReadIntParameter(action, "expected_house_upgrade_level_after_construction");
        if (!price.HasValue || !moneyAfter.HasValue || !levelAfter.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        var target = ReadParameter(action, "project_id") + ":price=" + price.Value;
        var effect = "player.money=" + moneyAfter.Value +
            ";player.days_until_farmhouse_upgrade=3" +
            ";eventual.player.farmhouse_upgrade_level=" + levelAfter.Value;
        return new[] { Step("purchase_farmhouse_upgrade", "housing:" + target, effect, 300) };
    }

    private static string[] ValidateFarmhouseUpgradePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.purchase_farmhouse_upgrade")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var actionX = ReadIntParameter(action, "carpenter_action_tile_x");
        var actionY = ReadIntParameter(action, "carpenter_action_tile_y");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var levelBefore = ReadIntParameter(action, "expected_house_upgrade_level_before");
        var levelAfter = ReadIntParameter(action, "expected_house_upgrade_level_after_construction");
        var daysBefore = ReadIntParameter(action, "expected_days_until_house_upgrade_before");
        var daysAfter = ReadIntParameter(action, "expected_days_until_house_upgrade_after");
        var moneyBefore = ReadIntParameter(action, "expected_money_before");
        var price = ReadIntParameter(action, "price");
        var moneyAfter = ReadIntParameter(action, "expected_money_after");
        var itemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
        var requiredCount = ReadIntParameter(action, "required_stack");
        var inventoryBefore = ReadIntParameter(action, "inventory_item_total_before");
        var inventoryAfter = ReadIntParameter(action, "inventory_item_total_after");
        if (!actionX.HasValue || !actionY.HasValue || targetX != actionX || targetY != actionY ||
            !standX.HasValue || !standY.HasValue || Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1 ||
            !levelBefore.HasValue || !levelAfter.HasValue || daysBefore != -1 || daysAfter != 3 ||
            !moneyBefore.HasValue || !price.HasValue || !moneyAfter.HasValue || moneyAfter != moneyBefore - price ||
            !requiredCount.HasValue || !inventoryBefore.HasValue || !inventoryAfter.HasValue || inventoryAfter != inventoryBefore - requiredCount ||
            ReadParameter(action, "target_location") != "ScienceHouse" || ReadParameter(action, "carpenter_action_raw") != "Carpenter" ||
            ReadParameter(action, "purchase_kind") != "farmhouse_upgrade" ||
            ReadParameter(action, "native_contract") != "GameLocation.checkAction_Carpenter_then_answerDialogue_carpenter_Upgrade_then_upgrade_Yes" ||
            !FarmhouseUpgradeParametersExact(levelBefore.Value, levelAfter.Value, price.Value, itemId, requiredCount.Value, daysAfter.Value))
        {
            return new[] { "farmhouse_upgrade_typed_projection_required" };
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("farmhouse_upgrade_menu_must_be_clear");
        }
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "ScienceHouse", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("farmhouse_upgrade_target_location_mismatch");
        }

        var progress = ReadStateFieldValue(snapshot, "world_progress", "marriage_house");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
            ReadBool(progress.Value, "is_current_location") != true ||
            NullableReadInt(progress.Value, "carpenter_action_tile_x") != actionX ||
            NullableReadInt(progress.Value, "carpenter_action_tile_y") != actionY ||
            ReadString(progress.Value, "carpenter_action_raw") != "Carpenter" ||
            ReadInt(progress.Value, "farmhouse_upgrade_level") != levelBefore.Value ||
            ReadInt(progress.Value, "days_until_farmhouse_upgrade") != daysBefore.Value ||
            ReadInt(progress.Value, "money") != moneyBefore.Value ||
            !progress.Value.TryGetProperty("house_upgrade", out var upgrade) || upgrade.ValueKind != JsonValueKind.Object ||
            ReadString(upgrade, "action_status") != "ready" ||
            ReadString(upgrade, "upgrade_id") != ReadParameter(action, "project_id") ||
            ReadInt(upgrade, "level_before") != levelBefore.Value || ReadInt(upgrade, "level_after") != levelAfter.Value ||
            ReadInt(upgrade, "price") != price.Value || ReadString(upgrade, "required_item_id") != itemId ||
            ReadInt(upgrade, "required_item_count") != requiredCount.Value || ReadInt(upgrade, "inventory_item_count") != inventoryBefore.Value ||
            ReadInt(upgrade, "construction_days") != daysAfter.Value)
        {
            reasons.Add("farmhouse_upgrade_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool FarmhouseUpgradeParametersExact(int before, int after, int price, string itemId, int count, int days) =>
        days == 3 && (before, after, price, itemId, count) switch
        {
            (0, 1, 10000, "(O)388", 450) => true,
            (1, 2, 65000, "(O)709", 100) => true,
            (2, 3, 100000, "", 0) => true,
            _ => false
        };
}
