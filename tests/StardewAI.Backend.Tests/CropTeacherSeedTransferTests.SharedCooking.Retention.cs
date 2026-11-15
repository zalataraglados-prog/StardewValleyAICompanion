using System.Reflection;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCookingRetentionAppliesPassivePositionsAtOriginalCookFrameWithoutCreditingItsOutput(bool product)
    {
        using var f = new SharedCookingFixture(); await f.Build(product);
        var scope = Assert.IsType<CropTeacherSharedSourceReturn>(f.Scope.VerifyTransition(f.Transition));
        var source = new MoneyRevenueCropNativeSharedSourceReference(f.Files.RequestPath, f.Files.LedgerPath, [Id],
            "declared.shared.cooking.return", scope.InputHashes)
        { SettledInventoryConsumption = f.Settlement, SettledConsumptionReferencePath = f.ReferencePath };
        var transition = f.Transition with { NativeSharedSource = source };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var account = Activator.CreateInstance(typeof(MoneyRevenueCropRetentionAccount), flags, null, [f.Files.Before, "(O)194"], null)!;
        var stockType = typeof(CropTeacherSaleStockInventory).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropTeacherTransferredSaleStock")!;
        object Stock() => Activator.CreateInstance(stockType, flags | BindingFlags.Public, null,
            [Id, "declared-no-owned-cooking-output", "(O)194"], null)!;
        var oldFailure = Assert.Throws<TargetInvocationException>(() => stockType.GetMethod("ObserveShared", flags)!.Invoke(Stock(), [account, transition]));
        Assert.IsType<InvalidDataException>(oldFailure.InnerException);
        var stock = Stock();
        Assert.Equal(1, stockType.GetMethod("ObserveSharedInventoryEffects", flags)!.Invoke(stock, [account, transition]));
        Assert.Equal(0, typeof(MoneyRevenueCropRetentionAccount).GetProperty("Quantity", flags)!.GetValue(account));
        var ledger = Assert.IsType<StrategyCommitmentLedger>(stockType.GetField("ledger", flags)!.GetValue(stock));
        Assert.Equal(0, ledger.MaterialReservations.Single(row => row.ReservationId == "cooking-wood").SlotIndex);
        Assert.Equal(1, ledger.MaterialReservations.Single(row => row.ReservationId == "cooking-stone").SlotIndex);
        foreach (var original in f.BeforeLedger.MaterialReservations)
        {
            var actual = ledger.MaterialReservations.Single(row => row.ReservationId == original.ReservationId);
            Assert.Equal(original.Quantity, actual.Quantity); Assert.Equal(original.SourceDecisionId, actual.SourceDecisionId);
            Assert.Equal(original.GoalId, actual.GoalId); Assert.Equal(original.Purpose, actual.Purpose);
        }
        var loader = typeof(MoneyRevenueCropNativeLifecycleProofBuilder).GetMethod("LoadSharedInventoryObservedTransition",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.NotNull(loader.Invoke(null, [transition]));
        var oldLoader = typeof(MoneyRevenueCropNativeLifecycleProofBuilder).GetMethod("LoadTransferredStockObservedTransition",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => oldLoader.Invoke(null, [transition])).InnerException);
    }
}
