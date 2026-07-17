using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class NativeShippingSourceGuardTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string RuntimeHarnessSource = RuntimeHarnessSources.All;
    private static readonly string ShippingSmokeSource = File.ReadAllText(
        FindRepositoryFile("scripts", "Invoke-RuntimeShipInventorySmoke.ps1"));

    [Fact]
    public void ShipExecutorSourceHasDayStartedSubscription()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("DayStarted += OnDayStartedForShippingReceipts", source, StringComparison.Ordinal);
        Assert.Contains("private void OnDayStartedForShippingReceipts", source, StringComparison.Ordinal);
        Assert.Contains("ReconcileShippingReceipts", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSourceHasReceiptLifecycleMethods()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("basicShipped_incremented_by_expected_quantity", source, StringComparison.Ordinal);
        Assert.Contains("basicShipped_did_not_increment", source, StringComparison.Ordinal);
        Assert.Contains("\"pending\"", source, StringComparison.Ordinal);
        Assert.Contains("\"completed\"", source, StringComparison.Ordinal);
        Assert.Contains("\"failed\"", source, StringComparison.Ordinal);
        Assert.Contains("AppendDelayedFeedback", source, StringComparison.Ordinal);
        Assert.Contains("delayed_shipping_feedback.jsonl", source, StringComparison.Ordinal);
        Assert.Contains("ReconcileShippingReceipts", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasNoSetCursorPositionReflection()
    {
        var source = RuntimeHarnessSource;
        Assert.DoesNotContain("SetCursorPosition", source, StringComparison.Ordinal);
        Assert.Contains("Game1.setMousePosition", source, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseX(", source, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseY(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasCursorPositionVerification()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("cursor_position_mismatch", source, StringComparison.Ordinal);
        Assert.Contains("active.PositionTarget.X", source, StringComparison.Ordinal);
        Assert.Contains("active.PositionTarget.Y", source, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(actualX", source, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(actualY", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasExplicitPerClickPhases()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("ShipPhase.BinPosition", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.BinPositionVerify", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.BinPress", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.BinRelease", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.WaitForShippingMenu", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.SlotPosition", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.SlotPositionVerify", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.SlotPress", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.SlotRelease", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.WaitForSlotDispatch", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.VerifyAndClose", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorDoesNotCheckButtonPressedEarlyReturnAtTopOfApplyPhase()
    {
        var applyPhaseSource = Slice(RuntimeHarnessSources.All,
            "private void ApplyShipPhaseInput", "private void CleanupAndBlock");
        var lines = applyPhaseSource.Split('\n');
        var firstSwitchCase = string.Join("\n", lines.Take(Math.Min(15, lines.Length)));
        Assert.DoesNotContain("if (active.ButtonPressed)", firstSwitchCase, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasReleaseRetryAndFailureCheck()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("ReleaseRetries", source, StringComparison.Ordinal);
        Assert.Contains("ReleaseShipRightButton", source, StringComparison.Ordinal);
        Assert.Contains("_failed_after_retries", source, StringComparison.Ordinal);
        Assert.Contains("for (var i = 0; i < 3; i++)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorRequiresStandTileBothFieldsAndExactEquality()
    {
        var source = RuntimeHarnessSource;
        var startSlice = Slice(source, "private void StartShipInventoryItemToBin", "private void TickShipInventoryToBin");
        Assert.Contains("stand_tile_required", startSlice, StringComparison.Ordinal);
        Assert.Contains("player_not_on_exact_stand_tile", startSlice, StringComparison.Ordinal);
        Assert.Contains("!pending.Request.StandTileX.HasValue || !pending.Request.StandTileY.HasValue", startSlice, StringComparison.Ordinal);
        Assert.Contains("distance > 2.0f", startSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHandlesNullSlotWhenStackWasOne()
    {
        var source = RuntimeHarnessSource;
        var slotPhaseSlice = Slice(source, "private void TickShipSlotClickPhase", "private void TickShipVerifyAndClose");
        Assert.Contains("BeforeSlotStack > 1", slotPhaseSlice, StringComparison.Ordinal);
        Assert.Contains("slotItem is null", slotPhaseSlice, StringComparison.Ordinal);
        Assert.Contains("slotStackNow == 0", slotPhaseSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasAtomicReceiptWrite()
    {
        var source = RuntimeHarnessSource;
        var receiptSlice = Slice(source, "private string WriteShipPendingReceipt", "private static string SanitizeFileName");
        Assert.Contains(".tmp", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("File.Move", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("SanitizeFileName", receiptSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorFailClosedOnEmptyReceiptPath()
    {
        var source = RuntimeHarnessSource;
        var verifySlice = Slice(source, "private void TickShipVerifyAndClose", "private void ApplyShipPhaseInput");
        Assert.Contains("receipt_write_failed", verifySlice, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(receiptPath)", verifySlice, StringComparison.Ordinal);
        Assert.Contains("CleanupAndBlock(active, \"receipt_write_failed\")", verifySlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipResultHasItemIdAndSlotBeforeAfterFields()
    {
        var contractsSource = File.ReadAllText(FindRepositoryFile("src", "StardewAI.Contracts", "Training", "TrainingExecutionContracts.cs"));
        Assert.Contains("ship_item_id", contractsSource, StringComparison.Ordinal);
        Assert.Contains("ship_before_slot_stack", contractsSource, StringComparison.Ordinal);
        Assert.Contains("ship_after_slot_stack", contractsSource, StringComparison.Ordinal);
        Assert.Contains("ship_before_slot_qualified_id", contractsSource, StringComparison.Ordinal);
        Assert.Contains("ship_after_slot_qualified_id", contractsSource, StringComparison.Ordinal);
        Assert.Contains("ship_source_date", contractsSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptPollsExactReceiptByPathAndVerifiesRunId()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("-LiteralPath $receiptPath -PathType Leaf", script, StringComparison.Ordinal);
        Assert.Contains("receipt.run_id -ne $RunId", script, StringComparison.Ordinal);
        Assert.Contains("receipt.queue_id", script, StringComparison.Ordinal);
        Assert.Contains("receipt.queue_item_id", script, StringComparison.Ordinal);
        Assert.Contains("receipt.request_nonce", script, StringComparison.Ordinal);
        Assert.Contains("receipt.status -in", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorProductionRegionHasNoProhibitedMutationCalls()
    {
        var source = RuntimeHarnessSource;
        var shipSlice = Slice(source, "private void StartShipInventoryItemToBin", "private static string ShipRequestedEffect");
        Assert.DoesNotContain("Farm.shipItem", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("shipItem(", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("leftClicked", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("removeItemFromInventory", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsumeStack", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("IInventory.Add", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("IInventory.Remove", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("IInventory.Clear", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("basicShipped[", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("shippedBasic", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveItem =", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("lastItemShipped", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCursorPosition", shipSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSourceContainsShippingBinDoActionPath()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("Game1.setMousePosition", source, StringComparison.Ordinal);
        Assert.Contains("OverrideButton", source, StringComparison.Ordinal);
        Assert.Contains("SButton.MouseRight", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorUsesUnqualifiedItemIdForBasicShipped()
    {
        var source = RuntimeHarnessSource;
        var startSlice = Slice(source, "private void StartShipInventoryItemToBin", "private void TickShipInventoryToBin");
        Assert.Contains("slotItem.ItemId", startSlice, StringComparison.Ordinal);
        Assert.Contains("GetBasicShippedCount(Game1.player, unqualifiedItemId)", startSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasExactTargetTileResolution()
    {
        var source = RuntimeHarnessSource;
        var startSlice = Slice(source, "private void StartShipInventoryItemToBin", "private void TickShipInventoryToBin");
        Assert.Contains("no_completed_bin_at_target_tile", startSlice, StringComparison.Ordinal);
        Assert.Contains("target_tile_required", startSlice, StringComparison.Ordinal);
        Assert.Contains("TargetTileX.Value == b.tileX.Value", startSlice, StringComparison.Ordinal);
        Assert.Contains("TargetTileY.Value == b.tileY.Value", startSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetTileX.Value >= b.tileX.Value", startSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipReceiptContainsAllRequiredFields()
    {
        var source = RuntimeHarnessSource;
        var receiptSlice = Slice(source, "private sealed class ShippingReceipt", "internal static class SavesFolderPatch");
        Assert.Contains("ReceiptId", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("RunId", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("UnqualifiedItemId", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("QualifiedItemId", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("PreBasicShippedCount", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("SettledBasicShippedCount", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("SettledAt", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("SettlementReason", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("RequestNonce", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("FeedbackAppended", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("QueueItemId", receiptSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipReceiptIdConstructionIncludesRequestNonce()
    {
        var source = RuntimeHarnessSource;
        var receiptSlice = Slice(source, "private string WriteShipPendingReceipt", "private static string SanitizeFileName");
        Assert.Contains("safeNonce", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("receiptFileName", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("ship_\" + safeRunId + \"_\" + safeQueueItemId + \"_\" + safeNonce", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("ship_\" + safeRunId + \"_\" + safeQueueItemId + \"_\" + safeNonce + \".json\"", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("request nonce is empty", receiptSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasRequestNonceValidation()
    {
        var source = RuntimeHarnessSource;
        var startSlice = Slice(source, "private void StartShipInventoryItemToBin", "private void TickShipInventoryToBin");
        Assert.Contains("request_nonce_required", startSlice, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(pending.Request.RequestNonce)", startSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorReconciliationHandlesTerminalUnappendedReceipts()
    {
        var source = RuntimeHarnessSource;
        var reconcileSlice = Slice(source, "private void ReconcileShippingReceipts", "private static void AtomicWriteReceipt");
        Assert.Contains("isTerminal", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("!receipt.FeedbackAppended", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("completed\" || receipt.Status == \"failed\"", reconcileSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorAppendDelayedFeedbackIsIdempotentByReceiptId()
    {
        var source = RuntimeHarnessSource;
        var feedbackSlice = Slice(source, "private bool AppendDelayedFeedback", "private void StartShipInventoryItemToBin");
        Assert.Contains("File.ReadAllLines", feedbackSlice, StringComparison.Ordinal);
        Assert.Contains("receipt_id", feedbackSlice, StringComparison.Ordinal);
        Assert.Contains("TryGetProperty(\"receipt_id\"", feedbackSlice, StringComparison.Ordinal);
        Assert.Contains("return true", feedbackSlice, StringComparison.Ordinal);
        Assert.Contains("return false", feedbackSlice, StringComparison.Ordinal);
        Assert.Contains("File.AppendAllText", feedbackSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasRequestNonceInReceiptCreation()
    {
        var source = RuntimeHarnessSource;
        var receiptSlice = Slice(source, "private string WriteShipPendingReceipt", "private static string SanitizeFileName");
        Assert.Contains("RequestNonce", receiptSlice, StringComparison.Ordinal);
        Assert.Contains("active.Pending.Request.RequestNonce", receiptSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasSameDateGuardInSettlementHelper()
    {
        var source = RuntimeHarnessSource;
        var helperSlice = Slice(source, "private void TrySettleActiveRunPendingShippingReceipts", "private void StartShipInventoryItemToBin");
        Assert.Contains("SourceDate", helperSlice, StringComparison.Ordinal);
        Assert.Contains("currentGameDate", helperSlice, StringComparison.Ordinal);
        Assert.Contains("currentGameDate <= sourceDate", helperSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSettlementHelperAccumulatesPerReceiptErrors()
    {
        var source = RuntimeHarnessSource;
        var helperSlice = Slice(source, "private void TrySettleActiveRunPendingShippingReceipts", "private void StartShipInventoryItemToBin");
        Assert.Contains("new List<Exception>()", helperSlice, StringComparison.Ordinal);
        Assert.Contains("errors.Add(ex)", helperSlice, StringComparison.Ordinal);
        Assert.Contains("throw new AggregateException", helperSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasAtomicReceiptWriteMethod()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("private static void AtomicWriteReceipt", source, StringComparison.Ordinal);
        Assert.Contains("File.Move(tempPath, receiptPath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasFeedbackAppendedGuard()
    {
        var source = RuntimeHarnessSource;
        var feedbackSlice = Slice(source, "private bool AppendDelayedFeedback", "private void StartShipInventoryItemToBin");
        Assert.Contains("FeedbackAppended", feedbackSlice, StringComparison.Ordinal);
        Assert.Contains("if (receipt.FeedbackAppended) return true;", feedbackSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorReconciliationOnlySetsFeedbackAppendedOnAppendSuccess()
    {
        var source = RuntimeHarnessSource;
        var reconcileSlice = Slice(source, "private void ReconcileShippingReceipts", "private static void AtomicWriteReceipt");
        Assert.Contains("if (AppendDelayedFeedback(receipt))", reconcileSlice, StringComparison.Ordinal);
        var appendCalls = CountOccurrences(reconcileSlice, "if (AppendDelayedFeedback(receipt))");
        Assert.True(appendCalls >= 2, $"Expected >=2 conditional AppendDelayedFeedback calls in ReconcileShippingReceipts, found {appendCalls}");
    }

    [Fact]
    public void ShipExecutorAppendDelayedFeedbackReturnsBool()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("private bool AppendDelayedFeedback", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorResolveReceiptDirectoryFailClosedInTrainingMode()
    {
        var source = RuntimeHarnessSource;
        var resolveSlice = Slice(source, "private static string ResolveReceiptDirectory", "private void AppendDelayedFeedback");
        Assert.Contains("STARDEWAI_TRAINING_MODE", resolveSlice, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_TRAINING_OUTPUT_DIR is required", resolveSlice, StringComparison.Ordinal);
        var trainingCheckIdx = resolveSlice.IndexOf("STARDEWAI_TRAINING_MODE", StringComparison.Ordinal);
        var fallbackIdx = resolveSlice.IndexOf("GetDirectoryName", StringComparison.Ordinal);
        Assert.True(trainingCheckIdx < fallbackIdx,
            "Training mode fail-closed check must appear before assembly-location fallback");
    }

    [Fact]
    public void SmokeScriptHasPreLaunchCollisionGuard()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("Get-NetTCPConnection", script, StringComparison.Ordinal);
        Assert.Contains("Port $port is already listening", script, StringComparison.Ordinal);
        Assert.Contains("Get-Process -Name", script, StringComparison.Ordinal);
        Assert.Contains("Refusing to attach", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptPassesOnlyOnCompletedStatus()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("-ne \"completed\"", script, StringComparison.Ordinal);
        Assert.Contains("non-completed status", script, StringComparison.Ordinal);
        Assert.Contains("\"failed\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasFullReceiptCorrelation()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("receipt.request_nonce -ne", script, StringComparison.Ordinal);
        Assert.Contains("receipt.queue_item_id -ne", script, StringComparison.Ordinal);
        Assert.Contains("receipt.quantity -ne", script, StringComparison.Ordinal);
        Assert.Contains("receipt.qualified_item_id -ne", script, StringComparison.Ordinal);
        Assert.Contains("receipt.unqualified_item_id -ne", script, StringComparison.Ordinal);
        Assert.Contains("receipt.source_date -ne", script, StringComparison.Ordinal);
        Assert.Contains("shipResult.ship_source_date", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptChecksNonceInReceiptFilename()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("Split-Path -Leaf", script, StringComparison.Ordinal);
        Assert.Contains("receiptFileName", script, StringComparison.Ordinal);
        Assert.Contains("shipRequest.request_nonce", script, StringComparison.Ordinal);
        Assert.Contains("receipt filename does not contain request nonce", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorReconciliationWritesWithAtomicMethod()
    {
        var source = RuntimeHarnessSource;
        var reconcileSlice = Slice(source, "private void ReconcileShippingReceipts", "private static void AtomicWriteReceipt");
        Assert.Contains("AtomicWriteReceipt", reconcileSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText", reconcileSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void DailyPlanCompilerShipStepsHaveStandTileParameters()
    {
        var source = DailyPlanCompilerSources.All;
        var shipSlice = Slice(source, "private static IEnumerable<SmallModelPlanStep> ShipInventoryItemToBinSteps", "private static IEnumerable<SmallModelPlanStep> SocialInteractionSteps");
        Assert.Contains("route_stand_tile_x", shipSlice, StringComparison.Ordinal);
        Assert.Contains("route_stand_tile_y", shipSlice, StringComparison.Ordinal);
        Assert.Contains("\"stand_tile_x\"", shipSlice, StringComparison.Ordinal);
        Assert.Contains("\"stand_tile_y\"", shipSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptSelectsFarmWarpFromTransparentWarps()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("target_name -eq \"Farm\"", script, StringComparison.Ordinal);
        Assert.Contains("current_location.identity", script, StringComparison.Ordinal);
        Assert.Contains("current_location.warps", script, StringComparison.Ordinal);
        Assert.Contains("name_or_unique_name", script, StringComparison.Ordinal);
        Assert.Contains("multiple warps to Farm", script, StringComparison.Ordinal);
        Assert.Contains("no warp to Farm", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesProductionTraverseConnectorForPreflight()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("executor.traverse_connector", script, StringComparison.Ordinal);
        Assert.Contains("connector_kind = \"warp\"", script, StringComparison.Ordinal);
        Assert.Contains("expected_target_location = \"Farm\"", script, StringComparison.Ordinal);
        Assert.Contains("expected_arrival_tile_x = [int]$farmWarp.target_x", script, StringComparison.Ordinal);
        Assert.Contains("expected_arrival_tile_y = [int]$farmWarp.target_y", script, StringComparison.Ordinal);
        Assert.Contains("target_tile_x = [int]$farmWarp.x", script, StringComparison.Ordinal);
        Assert.Contains("queue_item_id = \"runtime-ship-inventory-smoke.preflight-warp\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesPostRouteFarmLocation()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("post-warp location is not Farm", script, StringComparison.Ordinal);
        Assert.Contains("expected Farm after warp", script, StringComparison.Ordinal);
        Assert.Contains("$afterLocationName -ne \"Farm\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesPostRouteStateHashForFixture()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("$connectorSnapshot.state_hash", script, StringComparison.Ordinal);
        Assert.Contains("$connectorSnapshot = $initialSnapshot", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeHarnessHasNoDirectMovementCompatibilitySwitch()
    {
        var script = ShippingSmokeSource;
        var source = RuntimeHarnessSource;
        var config = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "HarnessConfig.cs"));

        Assert.DoesNotContain("STARDEWAI_USE_DIRECT_VALIDATED_MOVEMENT", script, StringComparison.Ordinal);
        Assert.DoesNotContain("STARDEWAI_USE_DIRECT_VALIDATED_MOVEMENT", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UseDirectValidatedMovement", config, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.MovePosition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position +=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasDeferredCursorVerificationPhases()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("BinPositionVerify", source, StringComparison.Ordinal);
        Assert.Contains("SlotPositionVerify", source, StringComparison.Ordinal);
        Assert.Contains("PositionVerified", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSeparatesCursorDispatchFromVerification()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("Game1.setMousePosition(pos.X, pos.Y, ui_scale: false)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseX(ui_scale: false)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseY(ui_scale: false)", source, StringComparison.Ordinal);
        Assert.Contains("active.PositionVerified = true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorUsesUiScaleForSlotCursorPosition()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("Game1.setMousePosition(slotPos.Value.X, slotPos.Value.Y, ui_scale: true)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseX(ui_scale: true)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseY(ui_scale: true)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorBinPhaseAdvancementRequiresPositionVerified()
    {
        var source = RuntimeHarnessSource;
        var binOpenSlice = Slice(source, "private void TickShipBinOpenPhase", "private void TickShipSlotClickPhase");
        Assert.Contains("active.PositionSet && !active.PositionVerified", binOpenSlice, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.BinPositionVerify", binOpenSlice, StringComparison.Ordinal);
        Assert.Contains("active.PositionVerified && !active.ButtonPressed", binOpenSlice, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.BinPress", binOpenSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSlotPhaseAdvancementRequiresPositionVerified()
    {
        var source = RuntimeHarnessSource;
        var slotClickSlice = Slice(source, "private void TickShipSlotClickPhase", "private void TickShipVerifyAndClose");
        Assert.Contains("active.PositionSet && !active.PositionVerified", slotClickSlice, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.SlotPositionVerify", slotClickSlice, StringComparison.Ordinal);
        Assert.Contains("active.PositionVerified && !active.ButtonPressed", slotClickSlice, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.SlotPress", slotClickSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorResetsPositionVerifiedOnBinToSlotTransition()
    {
        var source = RuntimeHarnessSource;
        var binOpenSlice = Slice(source, "private void TickShipBinOpenPhase", "private void TickShipSlotClickPhase");
        Assert.Contains("active.PositionVerified = false", binOpenSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void FarmBuildingsTransparentRowHasDoorTraversalData()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "FarmReadAdapter.cs"));
        var buildingSlice = Slice(source, "private static object ReadBuildingRow(Building building)", "private static object[] ReadShippingBins");
        Assert.Contains("human_door_relative_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("human_door_absolute_tile_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("exterior_entry_tile_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("exterior_stand_tile_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_location_id", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_arrival_tile_x", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("has_door_access_resolved", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("door_resolution_status", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("source_label", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("Building.humanDoor", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("Building.GetIndoors()", buildingSlice, StringComparison.Ordinal);
        Assert.Contains("is_locked_by_construction", buildingSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteConnectorsAndWallGraphHaveBuildingDoorEdges()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "ShopAccessReadAdapter.cs"));
        Assert.Contains("ReadBuildingDoorConnectors", source, StringComparison.Ordinal);
        Assert.Contains("ReadBuildingDoorGraphEdge", source, StringComparison.Ordinal);
        Assert.Contains("\"building_door\"", source, StringComparison.Ordinal);
        Assert.Contains("kind = \"building_door\"", source, StringComparison.Ordinal);
        Assert.Contains("Building.humanDoor; Building.GetIndoors()", source, StringComparison.Ordinal);
        Assert.Contains("Building.GetIndoors().warps[0]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasBuildingDoorInActionConnectorKinds()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("building_door", source, StringComparison.Ordinal);
        Assert.Contains("ValidateBuildingDoorConnector", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorBuildingDoorUsesNativeCheckActionNotDirectWarp()
    {
        var source = RuntimeHarnessSource;
        var doorSlice = Slice(source, "private bool TryTriggerConnectorAction", "private static int? ParseIntPart");
        Assert.Contains("Game1.currentLocation.checkAction", doorSlice, StringComparison.Ordinal);
        Assert.Contains("ValidateBuildingDoorConnector", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("building.doAction", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("performAction", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectSetPlayerLocation", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.currentLocation =", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.currentLocation =", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", doorSlice, StringComparison.Ordinal);

        var validationSlice = Slice(source, "private bool ValidateBuildingDoorConnector", "private static Point? FindConnectorActionStandTile");
        Assert.Contains(".humanDoor", validationSlice, StringComparison.Ordinal);
        Assert.Contains("building.GetIndoors()", validationSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("building.doAction", validationSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectSetPlayerLocation", validationSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.warpFarmer", validationSlice, StringComparison.Ordinal);

        var tickSlice = Slice(source, "private void TickTileMove", "private bool TryTriggerConnectorAction");
        Assert.Contains("CompleteConnectorMoveAfterLocationChange", tickSlice, StringComparison.Ordinal);
        Assert.Contains("AllowsLocationChange", tickSlice, StringComparison.Ordinal);
        Assert.Contains("IsStepOntoConnectorKind", tickSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectorTraversalHasNoDirectLocationMutationPath()
    {
        var source = RuntimeHarnessSource;
        var connectorSlice = Slice(source, "private void StartTileMove", "private void StartSleep");
        Assert.DoesNotContain("DirectSetPlayerLocation", connectorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteDirectConnectorTraversal", connectorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.currentLocation =", connectorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.currentLocation =", connectorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", connectorSlice, StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.checkAction", connectorSlice, StringComparison.Ordinal);
        Assert.Contains("MovePlayerForTick", connectorSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptResolvesFarmhouseBuildingDoorConnector()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("has_door_access_resolved", script, StringComparison.Ordinal);
        Assert.Contains("human_door_absolute_tile_x", script, StringComparison.Ordinal);
        Assert.Contains("indoor_location_id", script, StringComparison.Ordinal);
        Assert.Contains("indoor_arrival_tile_x", script, StringComparison.Ordinal);
        Assert.Contains("connector_kind = \"building_door\"", script, StringComparison.Ordinal);
        Assert.Contains("runtime-ship-inventory-smoke.home-connector", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesHomeLocationAfterConnector()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("post-connector location is not Farmhouse interior", script, StringComparison.Ordinal);
        Assert.Contains("expected $homeIndoorId", script, StringComparison.Ordinal);
        Assert.Contains("$homeLocationName -ne $homeIndoorId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptSleepUsesPostHomeConnectorStateHash()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("$homeConnectorSnapshot.state_hash", script, StringComparison.Ordinal);
        Assert.Contains("runtime-ship-inventory-smoke.sleep", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSettlementHelperScopedToActiveRunId()
    {
        var source = RuntimeHarnessSource;
        var helperSlice = Slice(source, "private void TrySettleActiveRunPendingShippingReceipts", "private void StartShipInventoryItemToBin");
        Assert.Contains("STARDEWAI_TRAINING_RUN_ID", helperSlice, StringComparison.Ordinal);
        Assert.Contains("activeRunId", helperSlice, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(activeRunId)", helperSlice, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(receipt.RunId, activeRunId", helperSlice, StringComparison.Ordinal);
        Assert.Contains("continue;", helperSlice, StringComparison.Ordinal);
        Assert.Contains("IsNullOrWhiteSpace(activeRunId) ||", helperSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("IsNullOrWhiteSpace(activeRunId) &&", helperSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("!string.IsNullOrWhiteSpace(activeRunId)", helperSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorReconciliationScopesTimeoutToActiveRunId()
    {
        var source = RuntimeHarnessSource;
        var reconcileSlice = Slice(source, "private void ReconcileShippingReceipts", "private static void AtomicWriteReceipt");
        Assert.Contains("STARDEWAI_TRAINING_RUN_ID", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("activeRunId", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(activeRunId)", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(receipt.RunId, activeRunId", reconcileSlice, StringComparison.Ordinal);
        Assert.Contains("IsNullOrWhiteSpace(activeRunId) ||", reconcileSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("IsNullOrWhiteSpace(activeRunId) &&", reconcileSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("!string.IsNullOrWhiteSpace(activeRunId)", reconcileSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildingDoorStandTileEnforcesExactDecompileBackedTile()
    {
        var source = RuntimeHarnessSource;
        var startSlice = Slice(source, "private void StartTileMove", "private void TickTileMove");
        Assert.Contains("connector_building_door_building_not_found", startSlice, StringComparison.Ordinal);
        Assert.Contains("connector_building_door_stand_tile_blocked", startSlice, StringComparison.Ordinal);
        Assert.Contains("requestedTargetTile.X, requestedTargetTile.Y + 1", startSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildingDoorTriggerVerifiesExactStandTileBeforeAction()
    {
        var source = RuntimeHarnessSource;
        var validationSlice = Slice(source, "private bool ValidateBuildingDoorConnector", "private static Point? FindConnectorActionStandTile");
        Assert.Contains("building_door_player_not_on_stand_tile", validationSlice, StringComparison.Ordinal);
        Assert.Contains("actionTile.X, actionTile.Y + 1", validationSlice, StringComparison.Ordinal);

        var triggerSlice = Slice(source, "private bool TryTriggerConnectorAction", "private static int? ParseIntPart");
        Assert.Contains("Game1.player.faceDirection(DirectionTo", triggerSlice, StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.checkAction", triggerSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildingDoorCheckActionReturnValueChecked()
    {
        var source = RuntimeHarnessSource;
        var triggerSlice = Slice(source, "private bool TryTriggerConnectorAction", "private static int? ParseIntPart");
        Assert.Contains("var handled = Game1.currentLocation.checkAction", triggerSlice, StringComparison.Ordinal);
        Assert.Contains("connector_action_not_handled", triggerSlice, StringComparison.Ordinal);
        Assert.Contains("!handled", triggerSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("building.doAction", triggerSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadBuildingDoorGraphEdgeEmitsUnresolvedRows()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "ShopAccessReadAdapter.cs"));
        var doorSlice = Slice(source, "private static object ReadBuildingDoorGraphEdge", "private static string ClassifyRouteActionBranch");
        Assert.Contains("human_door_unavailable", doorSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_location_unavailable", doorSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_entry_warp_unavailable", doorSlice, StringComparison.Ordinal);
        Assert.Contains("target_location_not_loaded", doorSlice, StringComparison.Ordinal);
        Assert.Contains("building_under_construction", doorSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("return null", doorSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSingleBuildingDoorConnectorEmitsUnresolvedRows()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "ShopAccessReadAdapter.cs"));
        var connSlice = Slice(source, "private static object ReadSingleBuildingDoorConnector", "private static object ReadCollisionGrid");
        Assert.Contains("human_door_unavailable", connSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_location_unavailable", connSlice, StringComparison.Ordinal);
        Assert.Contains("indoor_entry_warp_unavailable", connSlice, StringComparison.Ordinal);
        Assert.Contains("building_under_construction", connSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("return null", connSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesRouteGraphForFarmhouseEdge()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("locations.route_graph", script, StringComparison.Ordinal);
        Assert.Contains("route_graph Farmhouse edge disagrees", script, StringComparison.Ordinal);
        Assert.Contains("no resolved Farmhouse building_door edge in route_graph", script, StringComparison.Ordinal);
        Assert.Contains("route_graph.edges", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCrossChecksRouteGraphAgainstFarmBuildings()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("route_graph Farmhouse edge disagrees with farm.buildings transparent row", script, StringComparison.Ordinal);
        Assert.Contains("graph_door", script, StringComparison.Ordinal);
        Assert.Contains("building_door", script, StringComparison.Ordinal);
        Assert.Contains("graph_indoor", script, StringComparison.Ordinal);
        Assert.Contains("building_indoor", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesExactLocationEqualityOnly()
    {
        var script = ShippingSmokeSource;
        Assert.DoesNotContain("StartsWith", script, StringComparison.Ordinal);
        Assert.Contains("$homeLocationName -ne $homeIndoorId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCrossCheckFailsClosedWhenFarmBuildingsAbsent()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("farm.buildings data unavailable", script, StringComparison.Ordinal);
        Assert.Contains("cannot cross-check route_graph Farmhouse edge", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCrossCheckFailsClosedOnNonSingleFarmhouseCount()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("expected exactly one resolved Farmhouse row", script, StringComparison.Ordinal);
        Assert.Contains("resolved_farmhouse_count", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesPlayerTileEqualsHomeArrivalTileAfterConnector()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("post-connector player tile does not match expected arrival tile", script, StringComparison.Ordinal);
        Assert.Contains("$homeConnectorSnapshot.state.player.tile_x.value", script, StringComparison.Ordinal);
        Assert.Contains("$homeConnectorSnapshot.state.player.tile_y.value", script, StringComparison.Ordinal);
        Assert.Contains("$homePlayerTileX -ne [int]$homeArrivalTileX", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFarmCapabilityDescribesBuildingDoorTraversal()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "ModEntry.cs"));
        Assert.Contains("live building-door/indoor traversal reads", source, StringComparison.Ordinal);
        Assert.Contains("Building.humanDoor, Building.GetIndoors()", source, StringComparison.Ordinal);
        Assert.Contains("transparent building-door connectors and indoor warp arrival tiles", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorHasShipSummaryClosePhaseEnum()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("ShipSummaryClosePhase", source, StringComparison.Ordinal);
        Assert.Contains("WaitReady", source, StringComparison.Ordinal);
        Assert.Contains("PositionVerify", source, StringComparison.Ordinal);
        Assert.Contains("WaitClose", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuUsesTickStateMachine()
    {
        var source = RuntimeHarnessSource;
        var summarySlice = Slice(source, "private void TickShipSummaryClosePhase", "private void ApplyShipSummaryInput");
        Assert.Contains("case ShipSummaryClosePhase.WaitReady", summarySlice, StringComparison.Ordinal);
        Assert.Contains("case ShipSummaryClosePhase.Position", summarySlice, StringComparison.Ordinal);
        Assert.Contains("case ShipSummaryClosePhase.PositionVerify", summarySlice, StringComparison.Ordinal);
        Assert.Contains("case ShipSummaryClosePhase.Press", summarySlice, StringComparison.Ordinal);
        Assert.Contains("case ShipSummaryClosePhase.Release", summarySlice, StringComparison.Ordinal);
        Assert.Contains("case ShipSummaryClosePhase.WaitClose", summarySlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuChecksCanReceiveInputAndCurrentPage()
    {
        var source = RuntimeHarnessSource;
        var summarySlice = Slice(source, "private void TickShipSummaryClosePhase", "private void ApplyShipSummaryInput");
        Assert.Contains("CanReceiveInput", summarySlice, StringComparison.Ordinal);
        Assert.Contains("currentPage", summarySlice, StringComparison.Ordinal);
        Assert.Contains("CanReceiveInput() && shippingMenu.currentPage == -1", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetMethod", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetField", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetProperty", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("BindingFlags", summarySlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuUsesUiScaleCursorPosition()
    {
        var source = RuntimeHarnessSource;
        var inputSlice = Slice(source, "private void ApplyShipSummaryInput", "private void CompleteSleep");
        Assert.Contains("Game1.setMousePosition(target.X, target.Y, ui_scale: true)", inputSlice, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseX(ui_scale: true)", inputSlice, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseY(ui_scale: true)", inputSlice, StringComparison.Ordinal);
        Assert.Contains("shippingMenu.okButton", inputSlice, StringComparison.Ordinal);
        Assert.Contains("okButton.bounds", inputSlice, StringComparison.Ordinal);
        Assert.Contains("is not ShippingMenu", inputSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetProperty", inputSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetField", inputSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetMethod", inputSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("BindingFlags", inputSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetType().Name", inputSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuHasDeferredCursorVerification()
    {
        var source = RuntimeHarnessSource;
        var summarySlice = Slice(source, "private void TickShipSummaryClosePhase", "private void ApplyShipSummaryInput");
        Assert.Contains("!sleep.SummaryPositionVerified", summarySlice, StringComparison.Ordinal);
        Assert.Contains("ShipSummaryClosePhase.PositionVerify", summarySlice, StringComparison.Ordinal);
        Assert.Contains("SummaryPositionVerified", summarySlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuHasCursorMismatchCheck()
    {
        var source = RuntimeHarnessSource;
        var inputSlice = Slice(source, "private void ApplyShipSummaryInput", "private void CompleteSleep");
        Assert.Contains("shipping_summary_cursor_position_mismatch", inputSlice, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(ax - sleep.SummaryPositionTarget.X)", inputSlice, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(ay - sleep.SummaryPositionTarget.Y)", inputSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuHasPressReleasePhases()
    {
        var source = RuntimeHarnessSource;
        var inputSlice = Slice(source, "private void ApplyShipSummaryInput", "private void CompleteSleep");
        Assert.Contains("ShipSummaryClosePhase.Press", inputSlice, StringComparison.Ordinal);
        Assert.Contains("ShipSummaryClosePhase.Release", inputSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryButtonPressed", inputSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryButtonReleased", inputSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryReleaseRetries", inputSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuUsesMouseLeftNotRight()
    {
        var source = RuntimeHarnessSource;
        var inputSlice = Slice(source, "private void ApplyShipSummaryInput", "private void CompleteSleep");
        Assert.Contains("TryApplySmapiLeftButtonOverride", inputSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("SButton.MouseRight", inputSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuHasNoProhibitedClosureCalls()
    {
        var source = RuntimeHarnessSource;
        var summarySlice = Slice(source, "private void TickShipSummaryClosePhase", "private void CompleteSleep");
        Assert.DoesNotContain("receiveLeftClick", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("okClicked", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("exitThisMenu", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.activeClickableMenu = null", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetProperty", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetField", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetMethod", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("BindingFlags", summarySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetType().Name", summarySlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuUsesDirectTypedAccessNotReflection()
    {
        var source = RuntimeHarnessSource;
        var summarySlice = Slice(source, "private void TickShipSummaryClosePhase", "private void CompleteSleep");
        Assert.Contains("ShippingMenu shippingMenu", summarySlice, StringComparison.Ordinal);
        Assert.Contains("shippingMenu.okButton", summarySlice, StringComparison.Ordinal);
        Assert.Contains("okButton.bounds", summarySlice, StringComparison.Ordinal);
        Assert.Contains("shippingMenu.CanReceiveInput()", summarySlice, StringComparison.Ordinal);
        Assert.Contains("shippingMenu.currentPage", summarySlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuCompletionOnlyAfterMenuNull()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("menu is null", source, StringComparison.Ordinal);
        Assert.Contains("\"post_sleep_menu_closed\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorShippingMenuReleasesLeftButtonOnAllPaths()
    {
        var source = RuntimeHarnessSource;
        var summarySlice = Slice(source, "private void ApplyShipSummaryInput", "private void CompleteSleep");
        Assert.Contains("ReleaseSmapiLeftButtonOverride()", summarySlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorOnUpdateTickingHasShipSummaryInputDispatch()
    {
        var source = RuntimeHarnessSource;
        var tickingSlice = Slice(source, "private void OnExecutorUpdateTicking", "private void StartTileMove");
        Assert.Contains("ApplyShipSummaryInput", tickingSlice, StringComparison.Ordinal);
        Assert.Contains("WaitForPostSleepStable", tickingSlice, StringComparison.Ordinal);
        Assert.Contains("is ShippingMenu", tickingSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ShippingMenu\"", tickingSlice, StringComparison.Ordinal);
        Assert.Contains("shipping_summary_input_dispatch_exception", tickingSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorNonShippingMenuPostSleepFailsClosed()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("post_sleep_menu_not_closed", source, StringComparison.Ordinal);
        Assert.Contains("PostSleepWaitTicks", source, StringComparison.Ordinal);
        Assert.Contains("PostSleepWaitTicks > 600", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorCompleteSleepAndBlockedSleepReleaseLeftButton()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("private void CompleteSleep(ActiveSleep sleep", source, StringComparison.Ordinal);
        Assert.Contains("private void CompleteBlockedSleep(ActiveSleep sleep", source, StringComparison.Ordinal);
        var completeSlice = Slice(source, "private void CompleteSleep(ActiveSleep sleep", "private void CompleteBlockedSleep(ActiveSleep sleep");
        Assert.Contains("ReleaseSmapiLeftButtonOverride()", completeSlice, StringComparison.Ordinal);
        var blockedSlice = Slice(source, "private void CompleteBlockedSleep(ActiveSleep sleep", "private static TrainingExecutionResult CompletedSleep");
        Assert.Contains("ReleaseSmapiLeftButtonOverride()", blockedSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void SettlementHelperIsCalledFromBothDayStartedAndPostSleep()
    {
        var source = RuntimeHarnessSource;
        var dayStartedSlice = Slice(source, "private void OnDayStartedForShippingReceipts", "private void ReconcileShippingReceipts");
        Assert.Contains("TrySettleActiveRunPendingShippingReceipts()", dayStartedSlice, StringComparison.Ordinal);

        var tickSleepSlice = Slice(source, "private void TickSleep", "private bool TickSleepMoveToStand");
        Assert.Contains("TrySettleActiveRunPendingShippingReceipts()", tickSleepSlice, StringComparison.Ordinal);

        var calls = CountOccurrences(source, "TrySettleActiveRunPendingShippingReceipts()");
        Assert.True(calls >= 2, $"Expected >=2 calls to TrySettleActiveRunPendingShippingReceipts, found {calls}");
    }

    [Fact]
    public void PostSleepSettlementOccursAfterMenuNullAndBeforeCompleteSleep()
    {
        var source = RuntimeHarnessSource;
        var tickSleepSlice = Slice(source, "private void TickSleep", "private bool TickSleepMoveToStand");

        var menuNullIdx = tickSleepSlice.IndexOf("menu is null", StringComparison.Ordinal);
        var settlementIdx = tickSleepSlice.IndexOf("TrySettleActiveRunPendingShippingReceipts", StringComparison.Ordinal);
        var completeSleepIdx = tickSleepSlice.IndexOf("CompleteSleep(sleep, \"verified\"", StringComparison.Ordinal);

        Assert.True(menuNullIdx >= 0, "menu is null check not found in TickSleep");
        Assert.True(settlementIdx >= 0, "TrySettleActiveRunPendingShippingReceipts call not found in TickSleep");
        Assert.True(completeSleepIdx >= 0, "CompleteSleep call not found in TickSleep");
        Assert.True(menuNullIdx < settlementIdx,
            $"menu is null (pos {menuNullIdx}) must appear before TrySettleActiveRunPendingShippingReceipts (pos {settlementIdx})");
        Assert.True(settlementIdx < completeSleepIdx,
            $"TrySettleActiveRunPendingShippingReceipts (pos {settlementIdx}) must appear before CompleteSleep (pos {completeSleepIdx})");

        Assert.Contains("post_sleep_receipt_settlement_threw", tickSleepSlice, StringComparison.Ordinal);
        var threwLine = tickSleepSlice.Split('\n').First(line => line.Contains("post_sleep_receipt_settlement_threw"));
        Assert.Contains("CompleteBlockedSleep", threwLine, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptNeverWritesReceiptStatusMutation()
    {
        var script = ShippingSmokeSource;
        Assert.DoesNotContain("AtomicWriteReceipt", script, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteAllText", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Out-File", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$receipt.status =", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$receipt.Status =", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Content -LiteralPath $receiptPath", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Content -Path $receiptPath", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSettlementHelperExistsWithExactName()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("private void TrySettleActiveRunPendingShippingReceipts", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepExecutorActiveSleepHasSummaryPhaseFields()
    {
        var source = RuntimeHarnessSource;
        var sleepClassSlice = Slice(source, "private sealed class ActiveSleep", "private enum ShipSummaryClosePhase");
        Assert.Contains("SummaryPhase", sleepClassSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryPositionSet", sleepClassSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryPositionVerified", sleepClassSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryPositionTarget", sleepClassSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryButtonPressed", sleepClassSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryButtonReleased", sleepClassSlice, StringComparison.Ordinal);
        Assert.Contains("SummaryReleaseRetries", sleepClassSlice, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int idx = 0;
        while ((idx = source.IndexOf(value, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += value.Length;
        }
        return count;
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var startIdx = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIdx < 0) return string.Empty;
        var endIdx = source.IndexOf(endMarker, startIdx + startMarker.Length, StringComparison.Ordinal);
        if (endIdx < 0) return source.Substring(startIdx);
        return source.Substring(startIdx, endIdx - startIdx);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "StardewValleyAICompanion.sln")))
            dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("Cannot find repository root from " + baseDir);
        return Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
    }
}
