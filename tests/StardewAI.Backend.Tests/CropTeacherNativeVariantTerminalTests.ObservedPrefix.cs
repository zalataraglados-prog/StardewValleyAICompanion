using System.Reflection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void ObservedShippingFrameReusesTheOriginalInventoryBinAndPersistentResourceKernels()
    {
        using var f = new NativeVariantTerminalFixture(interleaved: true);
        var source = f.Shared.Source.Before;
        for (var count = 0; count < 6; count++)
        {
            var next = f.Deposit(source, count);
            var frame = DeclaredShippingFrame(f, source, next, count);
            var deposit = VerifyDeclaredShippingFrame(frame.Frame, f.Target.QualifiedItemId);
            Assert.True((bool)DepositProperty(deposit, "Verified")!, string.Join(",", (string[])DepositProperty(deposit, "BlockingReasons")!));
            Assert.Equal(count == 1 ? f.SilverSlot : f.NormalSlot, DepositProperty(deposit, "SlotIndex"));
            Assert.Equal(count == 1 ? 218 : 175, DepositProperty(deposit, "UnitPrice"));
            var boundary = typeof(CropTeacherTerminalShippingTarget).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!;
            foreach (var method in new[] { "VerifyShippingStock", "VerifyShippingBinsAndPersistentState" })
                boundary.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [source, next, deposit]);
            source = next;
        }
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("price")]
    [InlineData("item")]
    [InlineData("bin")]
    [InlineData("status")]
    [InlineData("verification")]
    [InlineData("native_option")]
    [InlineData("facts")]
    public void ObservedShippingFrameDoesNotCreditAnInvalidPhysicalDeposit(string fault)
    {
        using var f = new NativeVariantTerminalFixture(interleaved: true);
        var source = f.Shared.Source.Before;
        var after = f.Deposit(source, 0);
        var frame = DeclaredShippingFrame(f, source, after, 0);
        if (fault is "quantity" or "price" or "item" or "bin")
        {
            var parameter = frame.Item.NormalizedCommand.Parameters.Single(row => row.Name == (fault switch
            { "price" => "expected_unit_price", "item" => "qualified_item_id", "bin" => "target_tile_x", _ => "quantity" }));
            parameter.Value = fault == "item" ? "(O)388" : fault == "quantity" ? "2" : "999";
        }
        if (fault == "status") frame.Result.Status = "blocked";
        if (fault == "verification") frame.Result.PrimitiveVerificationStatus = "unverified";
        if (fault == "native_option") frame.Result.OptionId = "executor.wait_ticks";
        if (fault == "facts") frame.Result.ChangedFacts = [];
        Assert.False((bool)DepositProperty(VerifyDeclaredShippingFrame(frame.Frame, f.Target.QualifiedItemId), "Verified")!);
    }

    [Fact]
    public void ObservedShippingPrefixDoesNotReplaceOriginalNativeWireWithDeclaredFrameFixtures()
    {
        using var f = new NativeVariantTerminalFixture(interleaved: true);
        var initial = LifecycleRead<MoneyRevenueCropOutputRetentionProof>(f.Target.RetentionProofPath).AfterSnapshotPath;
        var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("shared_source_return", f.Target.TotalDay,
            Path.Combine(f.Root, "not-a-real-wire.json"), initial, Path.Combine(f.Root, "not-a-real-receipt.json"),
            initial, "declared-only", PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, "declared-only")
        { NativeSharedSource = new(f.Shared.Inputs.Plan.RequestPath, f.Shared.Source.LedgerPath, [Id], "declared-only",
            new Dictionary<string, string>()) { AppliedNativeWorkQueue = true } };
        var prefix = new CropTeacherObservedShippingPrefix("crop_teacher_observed_shipping_prefix.v1",
            f.Target.RetentionProofPath, initial, [transition]);
        Assert.Throws<InvalidDataException>(() => CropTeacherObservedShippingPrefixVerifier.Verify(prefix, f.Target, initial));
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    private static object VerifyDeclaredShippingFrame(object frame, string itemId) =>
        typeof(CropTeacherTerminalShippingTarget).Assembly.GetType("StardewAI.GoalConditionedBootstrap.NativeShippingDepositEvidenceVerifier")!
            .GetMethod("VerifyNativeWorkFrame", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [frame, itemId])!;

    private static object? DepositProperty(object deposit, string name) => deposit.GetType()
        .GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(deposit);

    private static (object Frame, ActionQueueItem Item, TrainingExecutionResult Result) DeclaredShippingFrame(NativeVariantTerminalFixture f,
        SnapshotEnvelope before, SnapshotEnvelope after, int count)
    {
        // Trusted-frame unit fixture only, never an original-wire or owner/production certificate.
        var target = f.Target with { AcceptedDepositCount = count };
        var model = CropTeacherTerminalShipping.Preparation.BuildOriginalAction(before, target);
        var queue = new ActionQueueCompiler().Compile(model, before, ReadLedger(f.Shared.Source.LedgerPath));
        var item = Assert.Single(queue.Items);
        var result = new TrainingExecutionResult
        {
            OptionId = item.OptionId, QueueItemId = item.QueueItemId, Status = "applied", PrimitiveKind = "ship_inventory_item_to_bin",
            PrimitiveVerificationStatus = "verified", ChangedFacts =
            [
                new() { Path = "player.inventory." + target.QualifiedItemId + ".count", Before = (6 - count).ToString(), After = (5 - count).ToString() },
                new() { Path = "farm.shipping_bin." + target.QualifiedItemId + ".count", Before = count.ToString(), After = (count + 1).ToString() }
            ]
        };
        var type = typeof(CropTeacherTerminalShippingTarget).Assembly.GetType("StardewAI.GoalConditionedBootstrap.NativeExecutionActionFrame")!;
        var frame = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 10).Invoke(
                [queue, item, result, new TrainingExecutionRequest(), before, after, "declared-only", new Dictionary<string, string>(), "declared-before", "declared-after"]);
        return (frame, item, result);
    }
}
