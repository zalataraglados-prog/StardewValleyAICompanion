using System.Collections;
using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string MultiplayerWalletRuntimeNativeContract =
        "ManorHouse_LedgerBook_checkAction_then_native_DialogueBox_response_clicks_then_optional_DigitEntryMenu_digit_clicks_then_changeWalletTypeTonight_or_sendMoney_receipt_then_Game1_newDay_player_wallets_barrier_settlement";

    private static readonly FieldInfo? WalletNumberMinimumField =
        typeof(NumberSelectionMenu).GetField("minValue", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? WalletNumberMaximumField =
        typeof(NumberSelectionMenu).GetField("maxValue", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? WalletNumberCurrentField =
        typeof(NumberSelectionMenu).GetField("currentValue", BindingFlags.Instance | BindingFlags.NonPublic);

    private void StartMultiplayerWallet(PendingExecution pending)
    {
        var request = pending.Request;
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, genericReasons.ToArray()));
            return;
        }
        var reasons = ValidateMultiplayerWalletRequest(request);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(MultiplayerWalletBlocked(request, reasons));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(MultiplayerWalletBlocked(request, "multiplayer_wallet_player_or_menu_not_ready"));
            return;
        }
        if (Game1.currentLocation is not ManorHouse manor ||
            !string.Equals(request.LocationId, manor.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(MultiplayerWalletBlocked(request, "multiplayer_wallet_target_location_mismatch"));
            return;
        }

        var target = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        if (!AreAdjacent(target, stand) || manor.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") != "LedgerBook" ||
            !IsTileOnMap(manor, stand) || !IsTileWalkable(manor, stand) || IsTileOccupiedByCharacter(manor, stand))
        {
            pending.Completion.SetResult(MultiplayerWalletBlocked(request, "multiplayer_wallet_ledger_or_stand_drifted"));
            return;
        }
        var liveReasons = ValidateMultiplayerWalletLiveState(request);
        if (liveReasons.Length > 0)
        {
            pending.Completion.SetResult(MultiplayerWalletBlocked(request, liveReasons));
            return;
        }
        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(manor, Game1.player.TilePoint, stand, maxMovement,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(MultiplayerWalletBlocked(request,
                "multiplayer_wallet_path_unavailable:" + pathReason));
            return;
        }
        activeMultiplayerWallet = new ActiveMultiplayerWallet(pending, manor, target, stand, path, maxMovement);
    }

    private static string[] ValidateMultiplayerWalletRequest(TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        var operation = request.WalletOperation;
        if (operation is not ("schedule_separate" or "cancel_separate" or "schedule_merge" or "cancel_merge" or "transfer") ||
            string.IsNullOrWhiteSpace(request.WalletReason) || request.ConfirmWalletOperation != true)
            reasons.Add("multiplayer_wallet_exact_operation_reason_and_confirmation_required");
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.WalletChangeTonightBefore.HasValue || !request.WalletChangeTonightAfter.HasValue ||
            !request.WalletActorIsHost.HasValue || !request.WalletParticipantCount.HasValue ||
            !request.WalletSharedMoneyBefore.HasValue || !request.WalletSeparationEachBalance.HasValue ||
            !request.WalletSeparationResultingTotal.HasValue || !request.WalletSeparationDiscardedRemainder.HasValue ||
            !request.WalletMergeResultingSharedMoney.HasValue || !request.WalletSenderMoneyBefore.HasValue ||
            !request.WalletSenderMoneyAfter.HasValue || !request.WalletRecipientMoneyBefore.HasValue ||
            !request.WalletRecipientMoneyAfter.HasValue || !request.WalletTotalMoneyGiftedBefore.HasValue ||
            !request.WalletTotalMoneyGiftedAfter.HasValue || request.WalletProjectionFingerprint.Length != 64 ||
            request.WalletLedgerActionRaw != "LedgerBook" || request.NativeContract != MultiplayerWalletRuntimeNativeContract)
            reasons.Add("multiplayer_wallet_complete_typed_projection_required");
        if (operation == "transfer" &&
            (request.ConfirmWalletTransfer != true || string.IsNullOrWhiteSpace(request.WalletRecipientPlayerId) ||
             string.IsNullOrWhiteSpace(request.WalletRecipientResponseKey) || request.WalletTransferAmount is not > 0))
            reasons.Add("multiplayer_wallet_transfer_exact_recipient_amount_and_confirmation_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] ValidateMultiplayerWalletLiveState(TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        var separate = Game1.player.team.useSeparateWallets.Value;
        var pending = Game1.player.changeWalletTypeTonight.Value;
        if (request.WalletModeBefore != (separate ? "separate" : "shared") ||
            request.WalletChangeTonightBefore != pending ||
            request.WalletPendingTransitionBefore != WalletRuntimePendingTransition(separate, pending) ||
            request.WalletLocalPlayerId != Game1.player.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture) ||
            request.WalletActorIsHost != Game1.IsMasterGame ||
            request.WalletParticipantCount != MultiplayerWalletClaimedFarmers().Length ||
            request.WalletSharedMoneyBefore != Game1.player.team.money.Value ||
            request.WalletIndividualBalancesBeforeCsv != MultiplayerWalletRuntimeBalancesCsv() ||
            request.WalletTotalMoneyGiftedBefore != Game1.player.stats.Get("totalMoneyGifted"))
            reasons.Add("multiplayer_wallet_live_projection_drifted");

        switch (request.WalletOperation)
        {
            case "schedule_separate" when !Game1.IsMasterGame || separate || pending || MultiplayerWalletClaimedFarmers().Count(f => !f.IsMainPlayer) == 0:
            case "cancel_separate" when !Game1.IsMasterGame || separate || !pending:
            case "schedule_merge" when !Game1.IsMasterGame || !separate || pending:
            case "cancel_merge" when !Game1.IsMasterGame || !separate || !pending:
                reasons.Add("multiplayer_wallet_mode_command_not_ready");
                break;
            case "transfer":
                var recipient = MultiplayerWalletRecipient(request.WalletRecipientPlayerId, out var responseKey);
                var amount = request.WalletTransferAmount ?? 0;
                if (!separate || recipient is null || responseKey != request.WalletRecipientResponseKey ||
                    amount < 1 || amount > Game1.player.Money ||
                    request.WalletSenderMoneyBefore != Game1.player.Money ||
                    request.WalletSenderMoneyAfter != Game1.player.Money - amount ||
                    request.WalletRecipientMoneyBefore != MultiplayerWalletEffectiveBalance(recipient) ||
                    request.WalletRecipientMoneyAfter != MultiplayerWalletEffectiveBalance(recipient) + amount ||
                    request.WalletExpectedIndividualBalancesAfterCsv != MultiplayerWalletRuntimeTransferredBalancesCsv(recipient, amount) ||
                    request.WalletTotalMoneyGiftedAfter != request.WalletTotalMoneyGiftedBefore + (uint)amount)
                    reasons.Add("multiplayer_wallet_transfer_projection_drifted");
                break;
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickMultiplayerWallet()
    {
        var active = activeMultiplayerWallet;
        if (active is null)
            return;
        try
        {
            active.ElapsedTicks++;
            active.StageTicks++;
            if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Manor) ||
                active.ElapsedTicks > active.MaxTicks)
            {
                CompleteMultiplayerWallet(active, false, "multiplayer_wallet_world_location_or_timeout");
                return;
            }
            switch (active.Stage)
            {
                case MultiplayerWalletStage.Move:
                    TickMultiplayerWalletMove(active);
                    break;
                case MultiplayerWalletStage.WaitInitialDialogue:
                    TickMultiplayerWalletInitialDialogue(active);
                    break;
                case MultiplayerWalletStage.WaitSecondaryDialogue:
                    TickMultiplayerWalletSecondaryDialogue(active);
                    break;
                case MultiplayerWalletStage.WaitRecipientDialogue:
                    TickMultiplayerWalletRecipientDialogue(active);
                    break;
                case MultiplayerWalletStage.EnterTransferAmount:
                    TickMultiplayerWalletAmount(active);
                    break;
                case MultiplayerWalletStage.WaitReceipt:
                    TickMultiplayerWalletReceipt(active);
                    break;
            }
        }
        catch (Exception ex)
        {
            CompleteMultiplayerWallet(active, false,
                "multiplayer_wallet_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private void TickMultiplayerWalletMove(ActiveMultiplayerWallet active)
    {
        var movement = AdvanceNativeObjectInteractionMovement(active, "multiplayer_wallet", out var failure);
        if (movement == NativeObjectMovementStatus.Failed)
        {
            CompleteMultiplayerWallet(active, false, failure);
            return;
        }
        if (movement == NativeObjectMovementStatus.Moving)
            return;
        var liveReasons = ValidateMultiplayerWalletLiveState(active.Pending.Request);
        if (liveReasons.Length > 0)
        {
            CompleteMultiplayerWallet(active, false, liveReasons);
            return;
        }
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Target));
        active.NativeActionHandled = active.Manor.checkAction(
            new xTile.Dimensions.Location(active.Target.X, active.Target.Y), Game1.viewport, Game1.player);
        active.Stage = MultiplayerWalletStage.WaitInitialDialogue;
        active.StageTicks = 0;
    }

    private void TickMultiplayerWalletInitialDialogue(ActiveMultiplayerWallet active)
    {
        var request = active.Pending.Request;
        var separate = Game1.player.team.useSeparateWallets.Value;
        var key = request.WalletOperation switch
        {
            "schedule_separate" => "separateWallets",
            "cancel_separate" => "cancelSeparateWallets",
            "transfer" when !Game1.IsMasterGame => "chooseRecipient",
            _ when separate => "ledgerOptions",
            _ => string.Empty
        };
        var response = request.WalletOperation switch
        {
            "schedule_separate" or "cancel_separate" => "Yes",
            "schedule_merge" => "MergeWallets",
            "cancel_merge" => "CancelMerge",
            "transfer" when Game1.IsMasterGame => "SendMoney",
            "transfer" => request.WalletRecipientResponseKey,
            _ => string.Empty
        };
        if (!TryClickMultiplayerWalletDialogue(active, key, response))
            return;
        active.Stage = request.WalletOperation switch
        {
            "schedule_separate" or "cancel_separate" => MultiplayerWalletStage.WaitReceipt,
            "schedule_merge" or "cancel_merge" => MultiplayerWalletStage.WaitSecondaryDialogue,
            "transfer" when Game1.IsMasterGame => MultiplayerWalletStage.WaitRecipientDialogue,
            "transfer" => MultiplayerWalletStage.EnterTransferAmount,
            _ => MultiplayerWalletStage.WaitReceipt
        };
        active.StageTicks = 0;
    }

    private void TickMultiplayerWalletSecondaryDialogue(ActiveMultiplayerWallet active)
    {
        var operation = active.Pending.Request.WalletOperation;
        var key = operation == "schedule_merge" ? "mergeWallets" : "cancelMergeWallets";
        if (!TryClickMultiplayerWalletDialogue(active, key, "Yes"))
            return;
        active.Stage = MultiplayerWalletStage.WaitReceipt;
        active.StageTicks = 0;
    }

    private void TickMultiplayerWalletRecipientDialogue(ActiveMultiplayerWallet active)
    {
        if (!TryClickMultiplayerWalletDialogue(active, "chooseRecipient",
                active.Pending.Request.WalletRecipientResponseKey))
            return;
        active.Stage = MultiplayerWalletStage.EnterTransferAmount;
        active.StageTicks = 0;
    }

    private bool TryClickMultiplayerWalletDialogue(ActiveMultiplayerWallet active, string expectedKey, string responseKey)
    {
        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            if (active.StageTicks > 180)
                CompleteMultiplayerWallet(active, false, "multiplayer_wallet_dialogue_open_timeout:" + expectedKey);
            return false;
        }
        if (active.Manor.lastQuestionKey != expectedKey)
        {
            CompleteMultiplayerWallet(active, false,
                "multiplayer_wallet_dialogue_key_mismatch:expected=" + expectedKey + ":actual=" + active.Manor.lastQuestionKey);
            return false;
        }
        var index = Array.FindIndex(menu.responses, response => response.responseKey == responseKey);
        if (index < 0)
        {
            CompleteMultiplayerWallet(active, false, "multiplayer_wallet_response_missing:" + responseKey);
            return false;
        }
        if (menu.transitioning || menu.safetyTimer > 0 || menu.responseCC is null || index >= menu.responseCC.Count)
        {
            if (active.StageTicks > 180)
                CompleteMultiplayerWallet(active, false, "multiplayer_wallet_dialogue_not_clickable:" + expectedKey);
            return false;
        }
        var bounds = menu.responseCC[index].bounds;
        menu.performHoverAction(bounds.Center.X, bounds.Center.Y);
        if (menu.selectedResponse != index)
        {
            CompleteMultiplayerWallet(active, false, "multiplayer_wallet_response_hover_rejected:" + responseKey);
            return false;
        }
        menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
        return true;
    }

    private void TickMultiplayerWalletAmount(ActiveMultiplayerWallet active)
    {
        if (Game1.activeClickableMenu is not NumberSelectionMenu menu || menu.GetType().Name != "DigitEntryMenu")
        {
            if (active.StageTicks > 180)
                CompleteMultiplayerWallet(active, false, "multiplayer_wallet_digit_entry_menu_timeout");
            return;
        }
        var request = active.Pending.Request;
        var amount = request.WalletTransferAmount!.Value;
        if (ReadWalletNumber(menu, WalletNumberMinimumField) != 1 ||
            ReadWalletNumber(menu, WalletNumberMaximumField) != request.WalletSenderMoneyBefore)
        {
            CompleteMultiplayerWallet(active, false, "multiplayer_wallet_digit_entry_bounds_drifted");
            return;
        }
        var field = menu.GetType().GetField("digits", BindingFlags.Instance | BindingFlags.Public);
        if (field?.GetValue(menu) is not IEnumerable rawDigits)
        {
            CompleteMultiplayerWallet(active, false, "multiplayer_wallet_native_digit_components_unavailable");
            return;
        }
        var digits = rawDigits.Cast<object>().OfType<ClickableComponent>().ToArray();
        var amountText = amount.ToString(CultureInfo.InvariantCulture);
        var wanted = active.DigitIndex < 0 ? "c" :
            active.DigitIndex < amountText.Length ? amountText[active.DigitIndex].ToString() : string.Empty;
        if (!string.IsNullOrEmpty(wanted))
        {
            var component = digits.FirstOrDefault(row => row.name == wanted);
            if (component is null)
            {
                CompleteMultiplayerWallet(active, false, "multiplayer_wallet_digit_component_missing:" + wanted);
                return;
            }
            menu.performHoverAction(component.bounds.Center.X, component.bounds.Center.Y);
            menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
            active.DigitIndex++;
            active.StageTicks = 0;
            return;
        }
        if (ReadWalletNumber(menu, WalletNumberCurrentField) != amount)
        {
            CompleteMultiplayerWallet(active, false, "multiplayer_wallet_digit_entry_value_mismatch");
            return;
        }
        menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
        active.Stage = MultiplayerWalletStage.WaitReceipt;
        active.StageTicks = 0;
    }

    private void TickMultiplayerWalletReceipt(ActiveMultiplayerWallet active)
    {
        if (MultiplayerWalletImmediateReceiptMatches(active.Pending.Request))
        {
            CompleteMultiplayerWallet(active, true);
            return;
        }
        if (active.StageTicks > 180)
            CompleteMultiplayerWallet(active, false, "multiplayer_wallet_native_immediate_receipt_mismatch");
    }

    private static bool MultiplayerWalletImmediateReceiptMatches(TrainingExecutionRequest request)
    {
        var separate = Game1.player.team.useSeparateWallets.Value;
        var pending = Game1.player.changeWalletTypeTonight.Value;
        if (request.WalletModeBefore != (separate ? "separate" : "shared") ||
            request.WalletChangeTonightAfter != pending ||
            request.WalletPendingTransitionAfter != WalletRuntimePendingTransition(separate, pending) ||
            request.WalletSharedMoneyBefore != Game1.player.team.money.Value)
            return false;
        if (request.WalletOperation != "transfer")
        {
            return request.WalletIndividualBalancesBeforeCsv == MultiplayerWalletRuntimeBalancesCsv() &&
                request.WalletTotalMoneyGiftedBefore == Game1.player.stats.Get("totalMoneyGifted");
        }
        return request.WalletExpectedIndividualBalancesAfterCsv == MultiplayerWalletRuntimeBalancesCsv() &&
            request.WalletSenderMoneyAfter == Game1.player.Money &&
            request.WalletTotalMoneyGiftedAfter == Game1.player.stats.Get("totalMoneyGifted");
    }

    private void CompleteMultiplayerWallet(ActiveMultiplayerWallet active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activeMultiplayerWallet = null;
        var request = active.Pending.Request;
        var verificationReasons = verified
            ? request.WalletOperation == "transfer"
                ? new[] { "shared_bfs_reached_live_ledger", "native_dialogue_recipient_and_digit_entry_input_used", "individual_balance_conservation_and_gifted_stat_receipt_verified" }
                : new[] { "shared_bfs_reached_live_ledger", "native_dialogue_command_and_confirmation_input_used", "pending_transition_changed_without_immediate_mode_or_balance_mutation" }
            : reasons.Length == 0 ? new[] { "multiplayer_wallet_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "player_command_only_executor_calibration",
            PrimitiveKind = "manage_multiplayer_wallet",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = MultiplayerWalletRequestedEffect(request),
            ObservedEffect = MultiplayerWalletObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "team.change_wallet_type_tonight", Before = request.WalletChangeTonightBefore.ToString()!, After = Game1.player.changeWalletTypeTonight.Value.ToString() },
                new SimulatedFactChange { Path = "team.wallet_mode", Before = request.WalletModeBefore, After = Game1.player.team.useSeparateWallets.Value ? "separate" : "shared" },
                new SimulatedFactChange { Path = "team.individual_balances", Before = request.WalletIndividualBalancesBeforeCsv, After = MultiplayerWalletRuntimeBalancesCsv() }
            }
        });
    }

    private static TrainingExecutionResult MultiplayerWalletBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "manage_multiplayer_wallet", MultiplayerWalletRequestedEffect(request),
            MultiplayerWalletObservedEffect(), reasons);

    private static string MultiplayerWalletRequestedEffect(TrainingExecutionRequest request) =>
        "wallet_operation=" + request.WalletOperation + ";pending_transition=" + request.WalletPendingTransitionAfter +
        ";recipient=" + request.WalletRecipientPlayerId + ";amount=" + request.WalletTransferAmount;

    private static string MultiplayerWalletObservedEffect() =>
        "wallet_mode=" + (Game1.player.team.useSeparateWallets.Value ? "separate" : "shared") +
        ";pending_transition=" + WalletRuntimePendingTransition(Game1.player.team.useSeparateWallets.Value,
            Game1.player.changeWalletTypeTonight.Value) +
        ";shared_money=" + Game1.player.team.money.Value +
        ";individual_balances=" + MultiplayerWalletRuntimeBalancesCsv() +
        ";total_money_gifted=" + Game1.player.stats.Get("totalMoneyGifted");

    private static Farmer[] MultiplayerWalletClaimedFarmers() =>
        Game1.getAllFarmers().Where(farmer => !farmer.isUnclaimedFarmhand).ToArray();

    private static int MultiplayerWalletEffectiveBalance(Farmer farmer)
    {
        if (!Game1.player.team.useSeparateWallets.Value)
            return Game1.player.team.money.Value;
        return Game1.player.team.individualMoney.TryGetValue(farmer.UniqueMultiplayerID, out var money)
            ? money.Value
            : 500;
    }

    private static string MultiplayerWalletRuntimeBalancesCsv() =>
        string.Join(",", MultiplayerWalletClaimedFarmers()
            .OrderBy(farmer => farmer.UniqueMultiplayerID)
            .Select(farmer => farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture) + ":" +
                MultiplayerWalletEffectiveBalance(farmer).ToString(CultureInfo.InvariantCulture)));

    private static string MultiplayerWalletRuntimeTransferredBalancesCsv(Farmer recipient, int amount) =>
        string.Join(",", MultiplayerWalletClaimedFarmers()
            .OrderBy(farmer => farmer.UniqueMultiplayerID)
            .Select(farmer =>
            {
                var balance = MultiplayerWalletEffectiveBalance(farmer);
                if (farmer.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID) balance -= amount;
                if (farmer.UniqueMultiplayerID == recipient.UniqueMultiplayerID) balance += amount;
                return farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture) + ":" +
                    balance.ToString(CultureInfo.InvariantCulture);
            }));

    private static Farmer? MultiplayerWalletRecipient(string playerId, out string responseKey)
    {
        responseKey = string.Empty;
        var index = 0;
        foreach (var farmer in Game1.getAllFarmers())
        {
            if (farmer.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID || farmer.isUnclaimedFarmhand)
                continue;
            index++;
            if (farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture) != playerId)
                continue;
            responseKey = "Transfer" + index.ToString(CultureInfo.InvariantCulture);
            return farmer;
        }
        return null;
    }

    private static string WalletRuntimePendingTransition(bool separate, bool pending) =>
        !pending ? "none" : separate ? "merge_tonight" : "separate_tonight";

    private static int? ReadWalletNumber(NumberSelectionMenu menu, FieldInfo? field) =>
        field?.GetValue(menu) is int value ? value : null;
}
