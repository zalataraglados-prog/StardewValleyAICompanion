using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed partial class NativeShippingSourceGuardTests
{
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
    public void StandaloneShippingSummaryRecoveryUsesNativeInputAndNeverDirectClosure()
    {
        var source = RuntimeHarnessSource;
        var recoverySlice = Slice(
            source,
            "private void StartShippingSummaryClose",
            "private static TrainingExecutionResult ShippingSummaryCloseResult");

        Assert.Contains("shippingMenu.CanReceiveInput()", recoverySlice, StringComparison.Ordinal);
        Assert.Contains("shippingMenu.currentPage == -1", recoverySlice, StringComparison.Ordinal);
        Assert.Contains("shippingMenu.okButton", recoverySlice, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiLeftButtonOverride", recoverySlice, StringComparison.Ordinal);
        Assert.Contains("Game1.setMousePosition", recoverySlice, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseX", recoverySlice, StringComparison.Ordinal);
        Assert.Contains("Game1.getMouseY", recoverySlice, StringComparison.Ordinal);
        Assert.Contains("shipping_summary_close_not_observed_after_retries", recoverySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.exitActiveMenu", recoverySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.activeClickableMenu = null", recoverySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("receiveLeftClick", recoverySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetMethod", recoverySlice, StringComparison.Ordinal);
        Assert.DoesNotContain("GetField", recoverySlice, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentMenuAdapterPublishesTypedShippingSummaryState()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "MenuReadAdapter.cs"));
        var shippingSlice = Slice(source, "private static object ReadShippingMenuState", "private static object ReadLevelUpMenuState");

        Assert.Contains("kind = \"shipping_summary\"", shippingSlice, StringComparison.Ordinal);
        Assert.Contains("menu.CanReceiveInput()", shippingSlice, StringComparison.Ordinal);
        Assert.Contains("menu.currentPage", shippingSlice, StringComparison.Ordinal);
        Assert.Contains("menu.okButton", shippingSlice, StringComparison.Ordinal);
        Assert.Contains("ready_for_native_ok", shippingSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("Reflection", shippingSlice, StringComparison.Ordinal);
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
    }}
