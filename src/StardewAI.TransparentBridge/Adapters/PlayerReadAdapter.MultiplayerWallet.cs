using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string MultiplayerWalletNativeContract =
        "ManorHouse_LedgerBook_checkAction_then_native_DialogueBox_response_clicks_then_optional_DigitEntryMenu_digit_clicks_then_changeWalletTypeTonight_or_sendMoney_receipt_then_Game1_newDay_player_wallets_barrier_settlement";

    private static object ReadMultiplayerWallet(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
        {
            return new
            {
                schema_version = "multiplayer_wallet.v1",
                projection_status = "unavailable_world_or_player",
                commands = Array.Empty<object>(),
                participants = Array.Empty<object>(),
                recipients = Array.Empty<object>(),
                ledger_action_tiles = Array.Empty<object>()
            };
        }

        var team = player.team!;
        var separate = team.useSeparateWallets.Value;
        var allFarmers = Game1.getAllFarmers().ToArray();
        var claimed = allFarmers.Where(farmer => !farmer.isUnclaimedFarmhand).ToArray();
        var onlineIds = Game1.getOnlineFarmers().Select(farmer => farmer.UniqueMultiplayerID).ToHashSet();
        var participants = claimed.Select(farmer =>
        {
            var hasIndividual = team.individualMoney.TryGetValue(farmer.UniqueMultiplayerID, out var individual);
            var individualBalance = hasIndividual ? individual.Value : (int?)null;
            var effectiveBalance = separate ? individualBalance ?? 500 : team.money.Value;
            return new
            {
                player_id = farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                name = farmer.Name,
                display_name = farmer.displayName,
                is_main_player = farmer.IsMainPlayer,
                is_local_player = farmer.UniqueMultiplayerID == player.UniqueMultiplayerID,
                is_online = onlineIds.Contains(farmer.UniqueMultiplayerID),
                is_unclaimed_farmhand = farmer.isUnclaimedFarmhand,
                individual_entry_exists = hasIndividual,
                individual_balance = individualBalance,
                native_default_if_missing = separate && !hasIndividual ? 500 : (int?)null,
                effective_balance = effectiveBalance
            };
        }).ToArray();

        var recipients = new List<object>();
        var nativeRecipientNumber = 0;
        foreach (var farmer in allFarmers)
        {
            if (farmer.UniqueMultiplayerID == player.UniqueMultiplayerID || farmer.isUnclaimedFarmhand)
                continue;
            nativeRecipientNumber++;
            var hasIndividual = team.individualMoney.TryGetValue(farmer.UniqueMultiplayerID, out var individual);
            recipients.Add(new
            {
                player_id = farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                name = farmer.Name,
                display_name = farmer.displayName,
                is_online = onlineIds.Contains(farmer.UniqueMultiplayerID),
                response_key = "Transfer" + nativeRecipientNumber,
                individual_entry_exists = hasIndividual,
                balance = separate ? hasIndividual ? individual.Value : 500 : team.money.Value,
                transfer_eligible = separate
            });
        }

        var manor = Game1.getLocationFromName("ManorHouse") as ManorHouse;
        var actionTiles = ReadWalletLedgerActionTiles(manor);
        var farmhandCount = claimed.Count(farmer => !farmer.IsMainPlayer);
        var changeTonight = player.changeWalletTypeTonight.Value;
        var sharedMoney = team.money.Value;
        var effectiveIndividualTotal = separate
            ? participants.Sum(row => row.effective_balance)
            : 0;
        var separateShare = sharedMoney / Math.Max(claimed.Length, 1);
        var separatedTotal = separateShare * claimed.Length;
        var commands = new[]
        {
            WalletCommand("schedule_separate", !Game1.IsMasterGame
                ? "blocked_host_required"
                : farmhandCount == 0
                    ? "blocked_no_claimed_farmhand"
                    : separate
                        ? "blocked_wallets_already_separate"
                        : changeTonight ? "blocked_separation_already_scheduled" : "ready"),
            WalletCommand("cancel_separate", !Game1.IsMasterGame
                ? "blocked_host_required"
                : separate
                    ? "blocked_wallets_not_shared"
                    : changeTonight ? "ready" : "blocked_no_separation_scheduled"),
            WalletCommand("schedule_merge", !Game1.IsMasterGame
                ? "blocked_host_required"
                : !separate
                    ? "blocked_wallets_already_shared"
                    : changeTonight ? "blocked_merge_already_scheduled" : "ready"),
            WalletCommand("cancel_merge", !Game1.IsMasterGame
                ? "blocked_host_required"
                : !separate
                    ? "blocked_wallets_not_separate"
                    : changeTonight ? "ready" : "blocked_no_merge_scheduled"),
            WalletCommand("transfer", !separate
                ? "blocked_transfer_requires_separate_wallets"
                : recipients.Count == 0
                    ? "blocked_no_recipient"
                    : player.Money < 1 ? "blocked_no_transferable_money" : "ready")
        };
        var pendingTransition = !changeTonight
            ? "none"
            : separate ? "merge_tonight" : "separate_tonight";
        var projectionBody = new
        {
            local_player_id = player.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
            is_host = Game1.IsMasterGame,
            separate,
            changeTonight,
            pendingTransition,
            sharedMoney,
            localMoney = player.Money,
            gifted = player.stats.Get("totalMoneyGifted"),
            participants,
            recipients,
            actionTiles
        };

        return new
        {
            schema_version = "multiplayer_wallet.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = WalletSha256(JsonSerializer.Serialize(projectionBody)),
            invocation_policy = "player_command_only",
            location_id = "ManorHouse",
            is_current_location = ReferenceEquals(Game1.currentLocation, manor),
            service_status = manor is null
                ? "blocked_manor_house_missing"
                : actionTiles.Length == 0
                    ? "blocked_ledger_book_action_missing"
                    : ReferenceEquals(Game1.currentLocation, manor) ? "ready" : "route_to_manor_house_required",
            local_player_id = projectionBody.local_player_id,
            is_host = projectionBody.is_host,
            wallet_mode = separate ? "separate" : "shared",
            use_separate_wallets = separate,
            change_wallet_type_tonight = changeTonight,
            pending_transition = pendingTransition,
            claimed_participant_count = claimed.Length,
            claimed_farmhand_count = farmhandCount,
            shared_money = sharedMoney,
            local_effective_money = player.Money,
            current_individual_total = effectiveIndividualTotal,
            total_money_gifted = projectionBody.gifted,
            participants,
            recipients = recipients.ToArray(),
            commands,
            separation_settlement = new
            {
                participant_count = claimed.Length,
                each_balance = separateShare,
                resulting_total = separatedTotal,
                discarded_integer_remainder = sharedMoney - separatedTotal,
                formula = "shared_money / max(claimed_participant_count,1) assigned_to_every_claimed_farmer"
            },
            merge_settlement = new
            {
                resulting_shared_money = effectiveIndividualTotal,
                formula = "sum_current_claimed_individual_balances"
            },
            ledger_action_tiles = actionTiles,
            native_contract = MultiplayerWalletNativeContract,
            settlement_policy = "mode_change_is_scheduled_by_ledger_and_applied_only_by_host_in_Game1_newDay_player.wallets_barrier",
            transfer_policy = "separate_wallets_only;amount_1_to_sender_balance;recipient_may_be_offline;native_sendMoney_conserves_individual_total_and_increments_totalMoneyGifted",
            direct_mutation_policy = "production_executor_must_not_write_useSeparateWallets_changeWalletTypeTonight_money_individualMoney_or_stats"
        };
    }

    private static object WalletCommand(string operation, string gateStatus) => new
    {
        operation,
        gate_status = gateStatus,
        available = gateStatus == "ready",
        requires_explicit_confirmation = true
    };

    private static object[] ReadWalletLedgerActionTiles(ManorHouse? manor)
    {
        if (manor?.map?.Layers is null)
            return Array.Empty<object>();
        var buildings = manor.map.GetLayer("Buildings");
        if (buildings is null)
            return Array.Empty<object>();
        var rows = new List<object>();
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                var action = manor.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.Equals(action, "LedgerBook", StringComparison.Ordinal))
                {
                    rows.Add(new { tile_x = x, tile_y = y, action_raw = action, action_token = "LedgerBook" });
                }
            }
        }
        return rows.ToArray();
    }

    private static string WalletSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
