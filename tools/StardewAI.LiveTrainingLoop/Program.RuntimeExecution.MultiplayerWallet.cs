using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyMultiplayerWalletRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.manage_multiplayer_wallet" or "multiplayer.manage_wallet" or
            "debug.setup_multiplayer_wallet" or "debug.settle_multiplayer_wallet"))
            return;

        request.WalletOperation = ReadQueueParameterString(item, "wallet_operation");
        request.WalletReason = ReadQueueParameterString(item, "wallet_reason");
        request.ConfirmWalletOperation = ReadQueueParameterBool(item, "confirm_wallet_operation");
        request.ConfirmWalletTransfer = ReadQueueParameterBool(item, "confirm_wallet_transfer");
        request.WalletProjectionFingerprint = ReadQueueParameterString(item, "wallet_projection_fingerprint");
        request.WalletModeBefore = ReadQueueParameterString(item, "wallet_mode_before");
        request.WalletChangeTonightBefore = ReadQueueParameterBool(item, "wallet_change_tonight_before");
        request.WalletChangeTonightAfter = ReadQueueParameterBool(item, "wallet_change_tonight_after");
        request.WalletPendingTransitionBefore = ReadQueueParameterString(item, "wallet_pending_transition_before");
        request.WalletPendingTransitionAfter = ReadQueueParameterString(item, "wallet_pending_transition_after");
        request.WalletLocalPlayerId = ReadQueueParameterString(item, "wallet_local_player_id");
        request.WalletActorIsHost = ReadQueueParameterBool(item, "wallet_actor_is_host");
        request.WalletParticipantCount = ReadQueueParameterInt(item, "wallet_participant_count");
        request.WalletSharedMoneyBefore = ReadQueueParameterInt(item, "wallet_shared_money_before");
        request.WalletIndividualBalancesBeforeCsv = ReadQueueParameterString(item, "wallet_individual_balances_before_csv");
        request.WalletExpectedIndividualBalancesAfterCsv = ReadQueueParameterString(item, "wallet_expected_individual_balances_after_csv");
        request.WalletSeparationEachBalance = ReadQueueParameterInt(item, "wallet_separation_each_balance");
        request.WalletSeparationResultingTotal = ReadQueueParameterInt(item, "wallet_separation_resulting_total");
        request.WalletSeparationDiscardedRemainder = ReadQueueParameterInt(item, "wallet_separation_discarded_remainder");
        request.WalletMergeResultingSharedMoney = ReadQueueParameterInt(item, "wallet_merge_resulting_shared_money");
        request.WalletRecipientPlayerId = ReadQueueParameterString(item, "wallet_recipient_player_id");
        request.WalletRecipientResponseKey = ReadQueueParameterString(item, "wallet_recipient_response_key");
        request.WalletTransferAmount = ReadQueueParameterInt(item, "wallet_transfer_amount");
        request.WalletSenderMoneyBefore = ReadQueueParameterInt(item, "wallet_sender_money_before");
        request.WalletSenderMoneyAfter = ReadQueueParameterInt(item, "wallet_sender_money_after");
        request.WalletRecipientMoneyBefore = ReadQueueParameterInt(item, "wallet_recipient_money_before");
        request.WalletRecipientMoneyAfter = ReadQueueParameterInt(item, "wallet_recipient_money_after");
        var giftedBefore = ReadQueueParameterInt(item, "wallet_total_money_gifted_before");
        var giftedAfter = ReadQueueParameterInt(item, "wallet_total_money_gifted_after");
        request.WalletTotalMoneyGiftedBefore = giftedBefore is >= 0 ? (uint)giftedBefore.Value : null;
        request.WalletTotalMoneyGiftedAfter = giftedAfter is >= 0 ? (uint)giftedAfter.Value : null;
        request.WalletLedgerActionRaw = ReadQueueParameterString(item, "wallet_ledger_action_raw");
    }
}
