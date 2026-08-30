using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private static readonly string[] WalletOperations =
    {
        "schedule_separate", "cancel_separate", "schedule_merge", "cancel_merge", "transfer"
    };

    private EventCandidate[] MultiplayerWalletCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var operation = WalletIntent(intent, "wallet_operation");
        var reason = WalletIntent(intent, "wallet_reason");
        var confirmed = WalletIntent(intent, "confirm_wallet_operation") == "true";
        if (!WalletOperations.Contains(operation, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(reason) || !confirmed)
            return Array.Empty<EventCandidate>();
        if (operation == "transfer" && WalletIntent(intent, "confirm_wallet_transfer") != "true")
            return Array.Empty<EventCandidate>();

        var wallet = ReadStateFieldValue(snapshot, "player", "multiplayer_wallet");
        if (!wallet.HasValue || wallet.Value.ValueKind != JsonValueKind.Object ||
            ReadString(wallet.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(wallet.Value, "invocation_policy") != "player_command_only")
            return Array.Empty<EventCandidate>();
        var command = WalletCommand(wallet.Value, operation);
        if (!command.HasValue || ReadString(command.Value, "gate_status") != "ready")
            return Array.Empty<EventCandidate>();
        if (!WalletIntentIsValid(wallet.Value, operation, intent))
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(wallet.Value, "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return MultiplayerWalletRouteCandidates(snapshot, wallet.Value, operation, reason, intent, currentLocation, targetLocation);

        var endpoint = WalletLedgerTiles(wallet.Value)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(row => row.stand is not null)
            .OrderBy(row => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - row.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - row.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (ReadString(wallet.Value, "service_status") != "ready")
            reasons.Add("wallet_ledger_service_not_ready:" + ReadString(wallet.Value, "service_status"));
        if (endpoint is null)
            reasons.Add("wallet_ledger_has_no_reachable_stand");
        var parameters = endpoint is null
            ? Array.Empty<SmallModelActionParameter>()
            : WalletCandidateParameters(wallet.Value, operation, reason, intent, endpoint.tile, endpoint.stand!);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "multiplayer.manage_wallet",
            Parameters = parameters,
            InvocationSource = OptionInvocationSource.PlayerCommand,
            ExplicitConfirmationGranted = true
        }));

        return new[]
        {
            new EventCandidate
            {
                CandidateId = "multiplayer-wallet:" + operation + ":" +
                    (ReadString(wallet.Value, "projection_fingerprint") is { Length: >= 12 } fingerprint
                        ? fingerprint[..12]
                        : "invalid"),
                Kind = "manage_multiplayer_wallet",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = endpoint?.tile.X,
                TileY = endpoint?.tile.Y,
                EstimatedTicks = operation == "transfer" ? 900 : 420,
                EnergyCost = 0,
                AvailabilityClass = "explicit_player_command_native_multiplayer_wallet",
                ExpectedEffect = WalletExpectedEffect(wallet.Value, operation, intent),
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private EventCandidate[] MultiplayerWalletRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement wallet,
        string operation,
        string reason,
        SmallModelActionParameter[] intent,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(wallet, "service_status") != "route_to_manor_house_required")
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstConnectorCandidate is null)
            return Array.Empty<EventCandidate>();
        var continuation = new List<SmallModelActionParameter>
        {
            Parameter("continuation.option_id", "multiplayer.manage_wallet"),
            Parameter("continuation.wallet_operation", operation),
            Parameter("continuation.wallet_reason", reason),
            Parameter("continuation.confirm_wallet_operation", "true"),
            Parameter("continuation.confirm_wallet_transfer", WalletIntent(intent, "confirm_wallet_transfer")),
            Parameter("continuation.wallet_recipient_player_id", WalletIntent(intent, "wallet_recipient_player_id")),
            Parameter("continuation.wallet_transfer_amount", WalletIntent(intent, "wallet_transfer_amount"))
        };
        return new[]
        {
            CloneCandidate(route.FirstConnectorCandidate,
                candidateId: "multiplayer-wallet-route:" + operation + ":" + currentLocation,
                expectedEffect: route.FirstConnectorCandidate.ExpectedEffect + ";wallet_operation_continuation=" + operation,
                parameters: route.FirstConnectorCandidate.Parameters.Concat(continuation).ToArray(),
                availabilityClass: "multiplayer_wallet_player_command_rolling_route")
        };
    }

    private static SmallModelActionParameter[] WalletCandidateParameters(
        JsonElement wallet,
        string operation,
        string reason,
        SmallModelActionParameter[] intent,
        WalletLedgerTile tile,
        CandidateTile stand)
    {
        var beforeCsv = WalletParticipantBalancesCsv(wallet);
        var afterCsv = operation == "transfer"
            ? WalletTransferredBalancesCsv(wallet, WalletIntent(intent, "wallet_recipient_player_id"),
                ParseWalletIntentInt(intent, "wallet_transfer_amount") ?? 0)
            : beforeCsv;
        var changeBefore = ReadBool(wallet, "change_wallet_type_tonight") == true;
        var changeAfter = operation is "schedule_separate" or "schedule_merge"
            ? true
            : operation is "cancel_separate" or "cancel_merge" ? false : changeBefore;
        var recipient = operation == "transfer"
            ? WalletRecipient(wallet, WalletIntent(intent, "wallet_recipient_player_id"))
            : null;
        var amount = operation == "transfer" ? ParseWalletIntentInt(intent, "wallet_transfer_amount") ?? 0 : 0;
        var senderBefore = ReadInt(wallet, "local_effective_money");
        var recipientBefore = recipient.HasValue ? ReadInt(recipient.Value, "balance") : 0;
        var giftedBefore = ReadInt(wallet, "total_money_gifted");
        return new[]
        {
            Parameter("wallet_operation", operation),
            Parameter("wallet_reason", reason),
            Parameter("confirm_wallet_operation", "true"),
            Parameter("confirm_wallet_transfer", operation == "transfer" ? "true" : "false"),
            Parameter("wallet_projection_fingerprint", ReadString(wallet, "projection_fingerprint")),
            Parameter("wallet_mode_before", ReadString(wallet, "wallet_mode")),
            Parameter("wallet_change_tonight_before", changeBefore.ToString().ToLowerInvariant()),
            Parameter("wallet_change_tonight_after", changeAfter.ToString().ToLowerInvariant()),
            Parameter("wallet_pending_transition_before", ReadString(wallet, "pending_transition")),
            Parameter("wallet_pending_transition_after", WalletPendingAfter(wallet, operation)),
            Parameter("wallet_local_player_id", ReadString(wallet, "local_player_id")),
            Parameter("wallet_actor_is_host", (ReadBool(wallet, "is_host") == true).ToString().ToLowerInvariant()),
            Parameter("wallet_participant_count", ReadInt(wallet, "claimed_participant_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_shared_money_before", ReadInt(wallet, "shared_money").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_individual_balances_before_csv", beforeCsv),
            Parameter("wallet_expected_individual_balances_after_csv", afterCsv),
            Parameter("wallet_separation_each_balance", ReadNestedWalletInt(wallet, "separation_settlement", "each_balance").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_separation_resulting_total", ReadNestedWalletInt(wallet, "separation_settlement", "resulting_total").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_separation_discarded_remainder", ReadNestedWalletInt(wallet, "separation_settlement", "discarded_integer_remainder").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_merge_resulting_shared_money", ReadNestedWalletInt(wallet, "merge_settlement", "resulting_shared_money").ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_recipient_player_id", recipient.HasValue ? ReadString(recipient.Value, "player_id") : string.Empty),
            Parameter("wallet_recipient_response_key", recipient.HasValue ? ReadString(recipient.Value, "response_key") : string.Empty),
            Parameter("wallet_transfer_amount", amount.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_sender_money_before", senderBefore.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_sender_money_after", (senderBefore - amount).ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_recipient_money_before", recipientBefore.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_recipient_money_after", (recipientBefore + amount).ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_total_money_gifted_before", giftedBefore.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_total_money_gifted_after", (giftedBefore + amount).ToString(CultureInfo.InvariantCulture)),
            Parameter("target_location", ReadString(wallet, "location_id")),
            Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("wallet_ledger_action_raw", tile.ActionRaw),
            Parameter("native_contract", ReadString(wallet, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static bool WalletIntentIsValid(JsonElement wallet, string operation, SmallModelActionParameter[] intent)
    {
        if (operation != "transfer")
            return true;
        var amount = ParseWalletIntentInt(intent, "wallet_transfer_amount");
        var recipient = WalletRecipient(wallet, WalletIntent(intent, "wallet_recipient_player_id"));
        return amount is > 0 && amount <= ReadInt(wallet, "local_effective_money") && recipient.HasValue;
    }

    private static string WalletExpectedEffect(JsonElement wallet, string operation, SmallModelActionParameter[] intent) =>
        operation == "transfer"
            ? "wallet_transfer_recipient=" + WalletIntent(intent, "wallet_recipient_player_id") +
              ";amount=" + WalletIntent(intent, "wallet_transfer_amount") +
              ";individual_total_conserved=true;totalMoneyGifted_incremented=true"
            : "wallet_mode=" + ReadString(wallet, "wallet_mode") +
              ";pending_transition=" + WalletPendingAfter(wallet, operation) +
              ";native_new_day_settlement_projected=true";

    private static string WalletPendingAfter(JsonElement wallet, string operation) => operation switch
    {
        "schedule_separate" => "separate_tonight",
        "schedule_merge" => "merge_tonight",
        "cancel_separate" or "cancel_merge" => "none",
        _ => ReadString(wallet, "pending_transition")
    };

    private static JsonElement? WalletCommand(JsonElement wallet, string operation)
    {
        if (!wallet.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array)
            return null;
        var command = commands.EnumerateArray().FirstOrDefault(row => row.ValueKind == JsonValueKind.Object &&
            ReadString(row, "operation") == operation);
        return command.ValueKind == JsonValueKind.Object ? command : null;
    }

    private static JsonElement? WalletRecipient(JsonElement wallet, string playerId)
    {
        if (!wallet.TryGetProperty("recipients", out var recipients) || recipients.ValueKind != JsonValueKind.Array)
            return null;
        var recipient = recipients.EnumerateArray().FirstOrDefault(row => row.ValueKind == JsonValueKind.Object &&
            ReadString(row, "player_id") == playerId);
        return recipient.ValueKind == JsonValueKind.Object ? recipient : null;
    }

    private static WalletLedgerTile[] WalletLedgerTiles(JsonElement wallet) =>
        wallet.TryGetProperty("ledger_action_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => new WalletLedgerTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y"), ReadString(row, "action_raw")))
                .ToArray()
            : Array.Empty<WalletLedgerTile>();

    private static string WalletParticipantBalancesCsv(JsonElement wallet)
    {
        if (!wallet.TryGetProperty("participants", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return string.Empty;
        return string.Join(",", rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
            .OrderBy(row => ReadString(row, "player_id"), StringComparer.Ordinal)
            .Select(row => ReadString(row, "player_id") + ":" + ReadInt(row, "effective_balance")));
    }

    private static string WalletTransferredBalancesCsv(JsonElement wallet, string recipientId, int amount)
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

    private static int ReadNestedWalletInt(JsonElement wallet, string objectName, string valueName) =>
        wallet.TryGetProperty(objectName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? ReadInt(nested, valueName)
            : 0;

    private static string WalletIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => parameter.Name == name)?.Value ??
        intent.FirstOrDefault(parameter => parameter.Name == "continuation." + name)?.Value ??
        string.Empty;

    private static int? ParseWalletIntentInt(SmallModelActionParameter[] intent, string name) =>
        int.TryParse(WalletIntent(intent, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private sealed record WalletLedgerTile(int X, int Y, string ActionRaw);
}
