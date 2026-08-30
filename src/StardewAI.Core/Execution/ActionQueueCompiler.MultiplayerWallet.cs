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
    private const string MultiplayerWalletCompilerNativeContract =
        "ManorHouse_LedgerBook_checkAction_then_native_DialogueBox_response_clicks_then_optional_DigitEntryMenu_digit_clicks_then_changeWalletTypeTonight_or_sendMoney_receipt_then_Game1_newDay_player_wallets_barrier_settlement";

    private static readonly string[] MultiplayerWalletBoundParameterNames =
    {
        "wallet_projection_fingerprint", "wallet_mode_before", "wallet_change_tonight_before",
        "wallet_change_tonight_after", "wallet_pending_transition_before", "wallet_pending_transition_after",
        "wallet_local_player_id", "wallet_actor_is_host", "wallet_participant_count", "wallet_shared_money_before",
        "wallet_individual_balances_before_csv", "wallet_expected_individual_balances_after_csv",
        "wallet_separation_each_balance", "wallet_separation_resulting_total",
        "wallet_separation_discarded_remainder", "wallet_merge_resulting_shared_money",
        "wallet_recipient_response_key", "wallet_sender_money_before", "wallet_sender_money_after",
        "wallet_recipient_money_before", "wallet_recipient_money_after", "wallet_total_money_gifted_before",
        "wallet_total_money_gifted_after", "target_location", "target_tile_x", "target_tile_y", "stand_tile_x",
        "stand_tile_y", "wallet_ledger_action_raw", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildMultiplayerWalletParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !MultiplayerWalletBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var projection = ReadStateFieldValue(snapshot, "player", "multiplayer_wallet");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var wallet = projection.Value;
        var operation = ReadParameter(action, "wallet_operation") ?? string.Empty;
        var recipientId = ReadParameter(action, "wallet_recipient_player_id") ?? string.Empty;
        var amount = ReadIntParameter(action, "wallet_transfer_amount") ?? 0;
        var target = ResolveMultiplayerWalletCompilerTarget(wallet, action, snapshot);
        var command = FindMultiplayerWalletRow(wallet, "commands", "operation", operation);
        if (target is null || !command.HasValue || ReadString(command.Value, "gate_status") != "ready")
            return parameters.ToArray();

        var recipient = operation == "transfer"
            ? FindMultiplayerWalletRow(wallet, "recipients", "player_id", recipientId)
            : null;
        var beforeCsv = MultiplayerWalletBalancesCsv(wallet);
        var afterCsv = operation == "transfer"
            ? MultiplayerWalletTransferredBalancesCsv(wallet, recipientId, amount)
            : beforeCsv;
        var changeBefore = ReadBool(wallet, "change_wallet_type_tonight") == true;
        var changeAfter = operation is "schedule_separate" or "schedule_merge"
            ? true
            : operation is "cancel_separate" or "cancel_merge" ? false : changeBefore;
        var senderBefore = ReadInt(wallet, "local_effective_money");
        var recipientBefore = recipient.HasValue ? ReadInt(recipient.Value, "balance") : 0;
        var giftedBefore = ReadInt(wallet, "total_money_gifted");
        parameters.AddRange(new[]
        {
            Parameter("wallet_projection_fingerprint", ReadString(wallet, "projection_fingerprint")),
            Parameter("wallet_mode_before", ReadString(wallet, "wallet_mode")),
            Parameter("wallet_change_tonight_before", changeBefore.ToString().ToLowerInvariant()),
            Parameter("wallet_change_tonight_after", changeAfter.ToString().ToLowerInvariant()),
            Parameter("wallet_pending_transition_before", ReadString(wallet, "pending_transition")),
            Parameter("wallet_pending_transition_after", MultiplayerWalletPendingAfter(wallet, operation)),
            Parameter("wallet_local_player_id", ReadString(wallet, "local_player_id")),
            Parameter("wallet_actor_is_host", (ReadBool(wallet, "is_host") == true).ToString().ToLowerInvariant()),
            Parameter("wallet_participant_count", ReadInt(wallet, "claimed_participant_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_shared_money_before", ReadInt(wallet, "shared_money").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_individual_balances_before_csv", beforeCsv),
            Parameter("wallet_expected_individual_balances_after_csv", afterCsv),
            Parameter("wallet_separation_each_balance", ReadMultiplayerWalletNestedInt(wallet, "separation_settlement", "each_balance").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_separation_resulting_total", ReadMultiplayerWalletNestedInt(wallet, "separation_settlement", "resulting_total").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_separation_discarded_remainder", ReadMultiplayerWalletNestedInt(wallet, "separation_settlement", "discarded_integer_remainder").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_merge_resulting_shared_money", ReadMultiplayerWalletNestedInt(wallet, "merge_settlement", "resulting_shared_money").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_recipient_response_key", recipient.HasValue ? ReadString(recipient.Value, "response_key") : string.Empty),
            Parameter("wallet_sender_money_before", senderBefore.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_sender_money_after", (senderBefore - amount).ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_recipient_money_before", recipientBefore.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_recipient_money_after", (recipientBefore + amount).ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_total_money_gifted_before", giftedBefore.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_total_money_gifted_after", (giftedBefore + amount).ToString(CultureInfo.InvariantCulture)),
            Parameter("target_location", ReadString(wallet, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_ledger_action_raw", target.ActionRaw),
            Parameter("native_contract", ReadString(wallet, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileMultiplayerWalletStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundMultiplayerWalletAction(action, snapshot);
        var operation = ReadParameter(bound, "wallet_operation");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (string.IsNullOrWhiteSpace(operation) || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        var transferTarget = operation == "transfer"
            ? ":recipient=" + ReadParameter(bound, "wallet_recipient_player_id") +
              ":amount=" + ReadParameter(bound, "wallet_transfer_amount")
            : string.Empty;
        return new[]
        {
            Step("manage_multiplayer_wallet",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):" + operation + transferTarget,
                "wallet_operation=" + operation + ";pending_transition=" +
                ReadParameter(bound, "wallet_pending_transition_after") + ";native_receipt_verified=true",
                operation == "transfer" ? 900 : 420)
        };
    }

    private static string[] ValidateMultiplayerWalletPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("multiplayer.manage_wallet" or "executor.manage_multiplayer_wallet"))
            return Array.Empty<string>();
        var reasons = new List<string>();
        var operation = ReadParameter(action, "wallet_operation") ?? string.Empty;
        if (operation is not ("schedule_separate" or "cancel_separate" or "schedule_merge" or "cancel_merge" or "transfer") ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "wallet_reason")) ||
            ReadParameter(action, "confirm_wallet_operation") != "true")
            reasons.Add("multiplayer_wallet_exact_operation_reason_and_confirmation_required");
        var amount = ReadIntParameter(action, "wallet_transfer_amount");
        if (operation == "transfer" &&
            (ReadParameter(action, "confirm_wallet_transfer") != "true" ||
             string.IsNullOrWhiteSpace(ReadParameter(action, "wallet_recipient_player_id")) || amount is not > 0))
            reasons.Add("multiplayer_wallet_transfer_exact_recipient_positive_amount_and_confirmation_required");

        var projection = ReadStateFieldValue(snapshot, "player", "multiplayer_wallet");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("multiplayer_wallet_projection_unavailable").ToArray();
        var wallet = projection.Value;
        var command = FindMultiplayerWalletRow(wallet, "commands", "operation", operation);
        var recipient = operation == "transfer"
            ? FindMultiplayerWalletRow(wallet, "recipients", "player_id", ReadParameter(action, "wallet_recipient_player_id") ?? string.Empty)
            : null;
        if (ReadString(wallet, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(wallet, "invocation_policy") != "player_command_only" ||
            ReadString(wallet, "service_status") != "ready" ||
            !string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "ManorHouse", StringComparison.OrdinalIgnoreCase))
            reasons.Add("multiplayer_wallet_native_service_not_ready");
        if (!command.HasValue || ReadString(command.Value, "gate_status") != "ready")
            reasons.Add("multiplayer_wallet_operation_not_ready:" + operation);
        if (operation != "transfer" && ReadBool(wallet, "is_host") != true)
            reasons.Add("multiplayer_wallet_mode_change_requires_host");
        if (operation == "transfer" &&
            (!recipient.HasValue || amount > ReadInt(wallet, "local_effective_money") ||
             ReadString(wallet, "wallet_mode") != "separate"))
            reasons.Add("multiplayer_wallet_transfer_projection_rejected");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("multiplayer_wallet_menu_must_be_clear");

        var bound = BoundMultiplayerWalletAction(action, snapshot);
        var target = ResolveMultiplayerWalletCompilerTarget(wallet, action, snapshot);
        var fingerprint = ReadParameter(bound, "wallet_projection_fingerprint") ?? string.Empty;
        if (target is null || fingerprint.Length != 64 ||
            fingerprint != ReadString(wallet, "projection_fingerprint") ||
            ReadParameter(bound, "wallet_mode_before") != ReadString(wallet, "wallet_mode") ||
            ReadParameter(bound, "wallet_pending_transition_before") != ReadString(wallet, "pending_transition") ||
            ReadIntParameter(bound, "wallet_shared_money_before") != ReadInt(wallet, "shared_money") ||
            ReadParameter(bound, "wallet_individual_balances_before_csv") != MultiplayerWalletBalancesCsv(wallet) ||
            ReadIntParameter(bound, "target_tile_x") != target?.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target?.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target?.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target?.StandY ||
            ReadParameter(bound, "wallet_ledger_action_raw") != "LedgerBook" ||
            ReadParameter(bound, "native_contract") != MultiplayerWalletCompilerNativeContract)
            reasons.Add("multiplayer_wallet_complete_fresh_typed_projection_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundMultiplayerWalletAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildMultiplayerWalletParameters(action, snapshot)
    };

    private static MultiplayerWalletCompilerTarget? ResolveMultiplayerWalletCompilerTarget(
        JsonElement wallet,
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (!wallet.TryGetProperty("ledger_action_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object && ReadString(row, "action_raw") == "LedgerBook")
            .Select(row =>
            {
                var x = ReadInt(row, "tile_x");
                var y = ReadInt(row, "tile_y");
                var requestedX = ReadIntParameter(action, "stand_tile_x");
                var requestedY = ReadIntParameter(action, "stand_tile_y");
                var stand = requestedX.HasValue && requestedY.HasValue &&
                    Math.Abs(x - requestedX.Value) + Math.Abs(y - requestedY.Value) == 1 &&
                    SleepStandTileReachable(snapshot, requestedX.Value, requestedY.Value)
                        ? new SleepStandTile(requestedX.Value, requestedY.Value)
                        : FindBestSleepStandTile(snapshot, x, y);
                return stand is null
                    ? null
                    : new MultiplayerWalletCompilerTarget(x, y, stand.X, stand.Y, ReadString(row, "action_raw"),
                        Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y));
            })
            .Where(target => target is not null)
            .OrderBy(target => target!.Distance)
            .ThenBy(target => target!.TargetY)
            .ThenBy(target => target!.TargetX)
            .FirstOrDefault();
    }

    private static JsonElement? FindMultiplayerWalletRow(JsonElement wallet, string arrayName, string keyName, string key)
    {
        if (!wallet.TryGetProperty(arrayName, out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        var row = rows.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object &&
            ReadString(value, keyName) == key);
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static string MultiplayerWalletBalancesCsv(JsonElement wallet)
    {
        if (!wallet.TryGetProperty("participants", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return string.Empty;
        return string.Join(",", rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
            .OrderBy(row => ReadString(row, "player_id"), StringComparer.Ordinal)
            .Select(row => ReadString(row, "player_id") + ":" + ReadInt(row, "effective_balance")));
    }

    private static string MultiplayerWalletTransferredBalancesCsv(JsonElement wallet, string recipientId, int amount)
    {
        if (!wallet.TryGetProperty("participants", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return string.Empty;
        var localId = ReadString(wallet, "local_player_id");
        return string.Join(",", rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
            .OrderBy(row => ReadString(row, "player_id"), StringComparer.Ordinal)
            .Select(row =>
            {
                var id = ReadString(row, "player_id");
                var balance = ReadInt(row, "effective_balance");
                if (id == localId) balance -= amount;
                if (id == recipientId) balance += amount;
                return id + ":" + balance;
            }));
    }

    private static string MultiplayerWalletPendingAfter(JsonElement wallet, string operation) => operation switch
    {
        "schedule_separate" => "separate_tonight",
        "schedule_merge" => "merge_tonight",
        "cancel_separate" or "cancel_merge" => "none",
        _ => ReadString(wallet, "pending_transition")
    };

    private static int ReadMultiplayerWalletNestedInt(JsonElement wallet, string objectName, string valueName) =>
        wallet.TryGetProperty(objectName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? ReadInt(nested, valueName)
            : 0;

    private sealed record MultiplayerWalletCompilerTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        string ActionRaw,
        int Distance);
}
