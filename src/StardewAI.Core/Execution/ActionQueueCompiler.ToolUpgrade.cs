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
    private const string ToolUpgradeCompilerNativeContract =
        "shared_route->Blacksmith_checkAction->answerDialogue(Upgrade)->ShopMenu(ClintUpgrade).receiveLeftClick_exact_row->Tool.actionWhenPurchased->two_day_wait->Blacksmith_checkAction->native_tool_claim";

    private static SmallModelActionParameter[] BuildToolUpgradeParameters(
        SmallModelAction action,
        SnapshotEnvelope _) => action.Parameters;

    private static CompiledActionStep[] CompileToolUpgradeStep(
        SmallModelAction action,
        SnapshotEnvelope _)
    {
        var claim = action.OptionId == "executor.claim_tool_upgrade";
        var qid = ReadParameter(
            action,
            claim
                ? "tool_upgrade_pending_qualified_item_id"
                : "tool_upgrade_target_qualified_item_id");
        return string.IsNullOrWhiteSpace(qid)
            ? Array.Empty<CompiledActionStep>()
            :
            [
                Step(
                    claim ? "claim_tool_upgrade" : "submit_tool_upgrade",
                    qid,
                    claim
                        ? "native_Blacksmith_counter_claim_clears_pending_tool_and_restores_exact_upgraded_axe"
                        : "native_ClintUpgrade_purchase_consumes_exact_source_axe_money_and_bars_and_sets_two_day_pending_tool",
                    claim ? 180 : 300)
            ];
    }

    private static string[] ValidateToolUpgradePlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not (
            "executor.submit_tool_upgrade" or "executor.claim_tool_upgrade"))
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var claim = action.OptionId == "executor.claim_tool_upgrade";
        var expectedStage = claim
            ? "claim_ready_axe_upgrade"
            : "submit_next_axe_upgrade";
        var projection = ReadStateFieldValue(snapshot, "player", "tool_upgrade");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_live_native_contract")
        {
            return ["tool_upgrade_complete_typed_projection_required"];
        }
        var value = projection.Value;
        if (ReadString(value, "lifecycle_stage") != expectedStage ||
            ReadString(value, "service_status") != "ready" ||
            ReadStateFieldString(snapshot, "player", "location_id") != "Blacksmith")
        {
            reasons.Add("tool_upgrade_native_blacksmith_service_not_ready");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("tool_upgrade_menu_must_be_clear");
        if (!ToolUpgradeTargetMatches(action, snapshot, value))
            reasons.Add("tool_upgrade_counter_endpoint_or_geometry_drifted");
        if (!ExactToolUpgradeParameter(
                action,
                "tool_upgrade_projection_fingerprint",
                ReadString(value, "projection_fingerprint")) ||
            !ExactToolUpgradeParameter(
                action,
                "tool_upgrade_lifecycle_stage",
                expectedStage) ||
            !ExactToolUpgradeParameter(action, "target_location", "Blacksmith") ||
            !ExactToolUpgradeParameter(
                action,
                "tool_upgrade_action_token",
                "Blacksmith") ||
            !ExactToolUpgradeParameter(
                action,
                "native_contract",
                ToolUpgradeCompilerNativeContract))
        {
            reasons.Add("tool_upgrade_complete_fresh_binding_required");
        }

        if (claim)
            ValidateToolUpgradeClaim(action, value, reasons);
        else
            ValidateToolUpgradeSubmission(action, value, reasons);
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void ValidateToolUpgradeSubmission(
        SmallModelAction action,
        JsonElement projection,
        List<string> reasons)
    {
        if (!projection.TryGetProperty("next_axe_upgrade", out var detail) ||
            detail.ValueKind != JsonValueKind.Object ||
            ReadBool(detail, "requirements_satisfied") != true)
        {
            reasons.Add("tool_upgrade_exact_next_axe_requirements_not_ready");
            return;
        }
        var exact =
            ExactToolUpgradeInt(action, "tool_upgrade_source_slot_index", ReadInt(detail, "source_slot_index")) &&
            ExactToolUpgradeParameter(action, "tool_upgrade_source_qualified_item_id", ReadString(detail, "source_qualified_item_id")) &&
            ExactToolUpgradeParameter(action, "tool_upgrade_source_item_id", ReadString(detail, "source_item_id")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_source_level", ReadInt(detail, "source_upgrade_level")) &&
            ExactToolUpgradeParameter(action, "tool_upgrade_target_qualified_item_id", ReadString(detail, "target_qualified_item_id")) &&
            ExactToolUpgradeParameter(action, "tool_upgrade_target_item_id", ReadString(detail, "target_item_id")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_target_level", ReadInt(detail, "target_upgrade_level")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_money_before", ReadInt(projection, "money_before")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_price_gold", ReadInt(detail, "price_gold")) &&
            ExactToolUpgradeParameter(action, "tool_upgrade_trade_qualified_item_id", ReadString(detail, "trade_item_qualified_item_id")) &&
            ExactToolUpgradeParameter(action, "tool_upgrade_trade_item_id", ReadString(detail, "trade_item_id")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_trade_amount", ReadInt(detail, "trade_item_amount")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_trade_available_before", ReadInt(detail, "trade_item_available")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_expected_days_left_after", 2);
        if (!exact)
            reasons.Add("tool_upgrade_submission_state_or_cost_drifted");
    }

    private static void ValidateToolUpgradeClaim(
        SmallModelAction action,
        JsonElement projection,
        List<string> reasons)
    {
        if (!projection.TryGetProperty("pending_tool", out var pending) ||
            pending.ValueKind != JsonValueKind.Object ||
            ReadBool(pending, "present") != true ||
            ReadBool(pending, "claim_ready") != true ||
            ReadBool(pending, "claim_capacity_satisfied") != true)
        {
            reasons.Add("tool_upgrade_pending_claim_not_ready");
            return;
        }
        var exact =
            ExactToolUpgradeParameter(action, "tool_upgrade_pending_qualified_item_id", ReadString(pending, "qualified_item_id")) &&
            ExactToolUpgradeParameter(action, "tool_upgrade_pending_item_id", ReadString(pending, "item_id")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_pending_level", ReadInt(pending, "upgrade_level")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_days_left_before", ReadInt(pending, "days_left")) &&
            ExactToolUpgradeInt(action, "tool_upgrade_free_slots_before", ReadInt(projection, "free_inventory_slots"));
        if (!exact)
            reasons.Add("tool_upgrade_claim_state_or_capacity_drifted");
    }

    private static bool ToolUpgradeTargetMatches(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        JsonElement projection)
    {
        if (!projection.TryGetProperty("counter_action_tiles", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue ||
            !standY.HasValue ||
            Math.Abs(targetX.Value - standX.Value) +
                Math.Abs(targetY.Value - standY.Value) != 1 ||
            !SleepStandTileReachable(snapshot, standX.Value, standY.Value))
        {
            return false;
        }
        return rows.EnumerateArray().Any(row =>
            row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == targetX.Value &&
            ReadInt(row, "tile_y") == targetY.Value &&
            ReadString(row, "action_token") == "Blacksmith" &&
            ReadString(row, "action_raw") ==
                ReadParameter(action, "tool_upgrade_action_raw"));
    }

    private static bool ExactToolUpgradeParameter(
        SmallModelAction action,
        string name,
        string expected) => string.Equals(
            ReadParameter(action, name),
            expected,
            StringComparison.Ordinal);

    private static bool ExactToolUpgradeInt(
        SmallModelAction action,
        string name,
        int expected) => string.Equals(
            ReadParameter(action, name),
            expected.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
}
