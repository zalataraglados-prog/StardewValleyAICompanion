using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("wallet_operation")]
    public string WalletOperation { get; set; } = string.Empty;

    [JsonPropertyName("wallet_reason")]
    public string WalletReason { get; set; } = string.Empty;

    [JsonPropertyName("confirm_wallet_operation")]
    public bool? ConfirmWalletOperation { get; set; }

    [JsonPropertyName("confirm_wallet_transfer")]
    public bool? ConfirmWalletTransfer { get; set; }

    [JsonPropertyName("wallet_projection_fingerprint")]
    public string WalletProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("wallet_mode_before")]
    public string WalletModeBefore { get; set; } = string.Empty;

    [JsonPropertyName("wallet_change_tonight_before")]
    public bool? WalletChangeTonightBefore { get; set; }

    [JsonPropertyName("wallet_change_tonight_after")]
    public bool? WalletChangeTonightAfter { get; set; }

    [JsonPropertyName("wallet_pending_transition_before")]
    public string WalletPendingTransitionBefore { get; set; } = string.Empty;

    [JsonPropertyName("wallet_pending_transition_after")]
    public string WalletPendingTransitionAfter { get; set; } = string.Empty;

    [JsonPropertyName("wallet_local_player_id")]
    public string WalletLocalPlayerId { get; set; } = string.Empty;

    [JsonPropertyName("wallet_actor_is_host")]
    public bool? WalletActorIsHost { get; set; }

    [JsonPropertyName("wallet_participant_count")]
    public int? WalletParticipantCount { get; set; }

    [JsonPropertyName("wallet_shared_money_before")]
    public int? WalletSharedMoneyBefore { get; set; }

    [JsonPropertyName("wallet_individual_balances_before_csv")]
    public string WalletIndividualBalancesBeforeCsv { get; set; } = string.Empty;

    [JsonPropertyName("wallet_expected_individual_balances_after_csv")]
    public string WalletExpectedIndividualBalancesAfterCsv { get; set; } = string.Empty;

    [JsonPropertyName("wallet_separation_each_balance")]
    public int? WalletSeparationEachBalance { get; set; }

    [JsonPropertyName("wallet_separation_resulting_total")]
    public int? WalletSeparationResultingTotal { get; set; }

    [JsonPropertyName("wallet_separation_discarded_remainder")]
    public int? WalletSeparationDiscardedRemainder { get; set; }

    [JsonPropertyName("wallet_merge_resulting_shared_money")]
    public int? WalletMergeResultingSharedMoney { get; set; }

    [JsonPropertyName("wallet_recipient_player_id")]
    public string WalletRecipientPlayerId { get; set; } = string.Empty;

    [JsonPropertyName("wallet_recipient_response_key")]
    public string WalletRecipientResponseKey { get; set; } = string.Empty;

    [JsonPropertyName("wallet_transfer_amount")]
    public int? WalletTransferAmount { get; set; }

    [JsonPropertyName("wallet_sender_money_before")]
    public int? WalletSenderMoneyBefore { get; set; }

    [JsonPropertyName("wallet_sender_money_after")]
    public int? WalletSenderMoneyAfter { get; set; }

    [JsonPropertyName("wallet_recipient_money_before")]
    public int? WalletRecipientMoneyBefore { get; set; }

    [JsonPropertyName("wallet_recipient_money_after")]
    public int? WalletRecipientMoneyAfter { get; set; }

    [JsonPropertyName("wallet_total_money_gifted_before")]
    public uint? WalletTotalMoneyGiftedBefore { get; set; }

    [JsonPropertyName("wallet_total_money_gifted_after")]
    public uint? WalletTotalMoneyGiftedAfter { get; set; }

    [JsonPropertyName("wallet_ledger_action_raw")]
    public string WalletLedgerActionRaw { get; set; } = string.Empty;
}
