using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed partial class NativeShippingSourceGuardTests
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
    public void ShipExecutorDoesNotMoveTheOperatingSystemCursor()
    {
        var source = RuntimeHarnessSource;
        var shipSlice = Slice(source, "private void StartShipInventoryItemToBin", "private static string ShipRequestedEffect");
        Assert.DoesNotContain("SetCursorPosition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.setMousePosition", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.getMouseX(", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.getMouseY(", shipSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorRequiresCardinalNativeActionStandTile()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("TryResolveShippingActionTile", source, StringComparison.Ordinal);
        Assert.Contains("stand_tile_not_cardinal_to_shipping_bin", source, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(candidate.X - standTile.X) + Math.Abs(candidate.Y - standTile.Y) == 1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasExplicitPerClickPhases()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("ShipPhase.BinFace", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.BinPress", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.BinRelease", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.WaitForShippingMenu", source, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.SlotDispatch", source, StringComparison.Ordinal);
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
        Assert.Contains("ReleaseShipInputOverrides", source, StringComparison.Ordinal);
        Assert.Contains("_failed_after_retries", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorTimeoutReportsExactStateMachinePhase()
    {
        var source = RuntimeHarnessSource;
        var tickSlice = Slice(source, "private void TickShipInventoryToBin", "private void TickShipBinOpenPhase");
        Assert.Contains("ship_timeout_phase=", tickSlice, StringComparison.Ordinal);
        Assert.Contains("ship_timeout_state=", tickSlice, StringComparison.Ordinal);
        Assert.Contains("active.Phase", tickSlice, StringComparison.Ordinal);
        Assert.Contains("active.SawShippingMenu", tickSlice, StringComparison.Ordinal);
        Assert.Contains("active.SlotClickDispatched", tickSlice, StringComparison.Ordinal);
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
    public void ShipExecutorUsesVanillaShippingBinDistanceCoordinates()
    {
        var source = RuntimeHarnessSource;
        var startSlice = Slice(source, "private void StartShipInventoryItemToBin", "private void TickShipInventoryToBin");
        Assert.Contains("Game1.player.Tile", startSlice, StringComparison.Ordinal);
        Assert.Contains("new Vector2(bin.tileX.Value + 0.5f, bin.tileY.Value)", startSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.TilePoint.X + 0.5f", startSlice, StringComparison.Ordinal);
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
    public void ShipExecutorUsesNativeActionAndMenuDispatchWithoutMouseOwnership()
    {
        var source = RuntimeHarnessSource;
        var shipSlice = Slice(source, "private void StartShipInventoryItemToBin", "private static string ShipRequestedEffect");
        Assert.Contains("OverrideButton", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.X", shipSlice, StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.checkAction", shipSlice, StringComparison.Ordinal);
        Assert.Contains("TryOpenNativeShippingMenu", shipSlice, StringComparison.Ordinal);
        Assert.Contains("menu.receiveRightClick", shipSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.setMousePosition", shipSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorFallbackReconstructsExactNativeShippingMenuBranch()
    {
        var source = RuntimeHarnessSource;
        var menuSlice = Slice(source, "private static bool TryOpenNativeShippingMenu", "private static Point? InventorySlotScreenPosition");
        Assert.Contains("AccessTools.Method(typeof(ShippingBin), \"shipItem\"", menuSlice, StringComparison.Ordinal);
        Assert.Contains("Utility.highlightShippableObjects", menuSlice, StringComparison.Ordinal);
        Assert.Contains("reverseGrab: true", menuSlice, StringComparison.Ordinal);
        Assert.Contains("showReceivingMenu: false", menuSlice, StringComparison.Ordinal);
        Assert.Contains("menu.initializeShippingBin()", menuSlice, StringComparison.Ordinal);
        Assert.Contains("Game1.activeClickableMenu = menu", menuSlice, StringComparison.Ordinal);
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

}
