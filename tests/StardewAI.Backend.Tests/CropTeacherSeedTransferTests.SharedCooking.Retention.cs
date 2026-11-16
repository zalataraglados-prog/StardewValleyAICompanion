using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Fact]
    public async Task CookingCompactionPreservesOwnedCropAlreadyReservedForSale()
    {
        using var f = new SharedCookingFixture(includesReservedSaleStock: true);
        await f.Build(product: true);
        var returned = Assert.IsType<CropTeacherSharedSourceReturn>(f.Scope.VerifyTransition(f.Transition));
        var reference = new MoneyRevenueCropNativeSharedSourceReference(f.Files.RequestPath, f.Files.LedgerPath, [Id],
            "declared.shared.cooking.return", returned.InputHashes)
        { SettledInventoryConsumption = f.Settlement, SettledConsumptionReferencePath = f.ReferencePath };
        var transition = f.Transition with { NativeSharedSource = reference };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var beforeHarvest = CropInventory(0, tickOffset: -2);
        var afterHarvest = CropInventory(2, tickOffset: -1);
        var account = Activator.CreateInstance(typeof(MoneyRevenueCropRetentionAccount), flags, null,
            [beforeHarvest, SharedCookingFixture.SaleStockItem], null)!;
        typeof(MoneyRevenueCropRetentionAccount).GetMethod("Observe", flags)!
            .Invoke(account, [beforeHarvest, afterHarvest, true]);

        var stockType = typeof(CropTeacherSaleStockInventory).Assembly
            .GetType("StardewAI.GoalConditionedBootstrap.CropTeacherTransferredSaleStock")!;
        var stock = Activator.CreateInstance(stockType, flags | BindingFlags.Public, null,
            [Id, SharedCookingFixture.SaleStockWave, SharedCookingFixture.SaleStockItem], null)!;
        var stockRead = stockType.GetMethod("Read", flags)!;
        var reserved = stockRead.Invoke(stock, [f.Files.Before, f.BeforeLedger])!;
        var empty = Activator.CreateInstance(reserved.GetType(),
            [Array.Empty<MoneyRevenueCropRetainedVariant>(), Array.Empty<MoneyRevenueCropRetainedVariant>()])!;
        // The initial harvest and put-away observations are declared fixture inputs.
        typeof(MoneyRevenueCropRetentionAccount).GetMethod("ObserveTransferredStock", flags)!
            .Invoke(account, [afterHarvest, f.Files.Before, false, empty, reserved]);

        Assert.Equal(1, stockType.GetMethod("ObserveSharedInventoryEffects", flags)!.Invoke(stock, [account, transition]));
        Assert.Equal(2, typeof(MoneyRevenueCropRetentionAccount).GetProperty("Quantity", flags)!.GetValue(account));
        var ledger = Assert.IsType<StrategyCommitmentLedger>(stockType.GetField("ledger", flags)!.GetValue(stock));
        var crop = Assert.Single(ledger.MaterialReservations.Where(row => row.QualifiedItemId == SharedCookingFixture.SaleStockItem &&
            row.NodeId == "chest:FarmHouse:9,8"));
        Assert.Equal(2, crop.SlotIndex);
        Assert.Equal(1, crop.Quantity);
        var stored = Assert.Single(Assert.IsType<MoneyRevenueCropRetainedVariant[]>(reserved.GetType().GetProperty("Stored")!
            .GetValue(stockRead.Invoke(stock, [f.After, ledger]))));
        Assert.Equal(1, stored.RetainedQuantity);

        SnapshotEnvelope CropInventory(int quantity, int tickOffset)
        {
            var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(f.Files.Before, JsonOptions), JsonOptions)!;
            var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
            var rows = player["inventory"]!["value"]!.AsArray();
            var row = rows.Single(item => item?["qualified_item_id"]?.GetValue<string>() == SharedCookingFixture.SaleStockItem)!;
            if (quantity == 0) rows.Remove(row);
            else row["stack"] = quantity;
            snapshot.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            snapshot.GameTick += tickOffset;
            return snapshot;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionMergeHonorsTheExplicitCookingSourceMode(bool product)
    {
        using var f = new SharedCookingFixture();
        await f.Build(product);
        var returned = Assert.IsType<CropTeacherSharedSourceReturn>(f.Scope.VerifyTransition(f.Transition));
        const string stepId = "declared.shared.cooking.return";
        var reference = new MoneyRevenueCropNativeSharedSourceReference(f.Files.RequestPath, f.Files.LedgerPath, [Id],
            stepId, returned.InputHashes)
        { SettledInventoryConsumption = f.Settlement, SettledConsumptionReferencePath = f.ReferencePath };
        var shared = new CropTeacherManagedSharedSourceReturn(2, stepId, f.Transition) { SourceReference = reference };
        // The crop trace stands in for the already accepted neighboring stages; the inserted
        // cooking transition uses the original compiler, Backend and native artifact reader.
        var cropBefore = new CropTeacherManagedProductionTransition(1, "declared_original_transition",
            f.Transition with { AfterSnapshotPath = f.Transition.BeforeSnapshotPath });
        var cropAfter = new CropTeacherManagedProductionTransition(3, "declared_original_transition",
            f.Transition with { BeforeSnapshotPath = f.Transition.AfterSnapshotPath });
        var constructor = typeof(CropTeacherManagedProductionTrace)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var trace = (CropTeacherManagedProductionTrace)constructor.Invoke(
            [f.Files.RequestPath, f.Transition.AfterSnapshotPath, f.Files.LedgerPath,
                new[] { cropBefore, cropAfter }, returned.InputHashes]);
        var builder = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherProductionEvidence")!;
        var merge = builder.GetMethod("MergeSharedSources", BindingFlags.NonPublic | BindingFlags.Static)!;
        CropTeacherManagedProductionTransition[] Merge(bool inventoryEffects, CropTeacherManagedSharedSourceReturn source) =>
            (CropTeacherManagedProductionTransition[])merge.Invoke(null,
                [trace, new[] { source }, f.Files.SourcePath, f.Files.RequestPath, true, inventoryEffects])!;

        var merged = Merge(true, shared);
        Assert.Equal(new[] { 1, 2, 3 }, merged.Select(row => row.OwnerSequence));
        Assert.Same(reference, merged[1].NativeTransition.NativeSharedSource);
        Assert.Equal("shared_source_return", merged[1].NativeTransition.TransitionKind);
        Assert.IsType<InvalidDataException>(
            Assert.Throws<TargetInvocationException>(() => Merge(false, shared)).InnerException);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
            Merge(true, shared with { SourceReference = reference with { SettledConsumptionReferencePath = null } }))
            .InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCookingRetentionAppliesPassivePositionsAtOriginalCookFrameWithoutCreditingItsOutput(bool product)
    {
        using var f = new SharedCookingFixture(); await f.Build(product);
        var scope = Assert.IsType<CropTeacherSharedSourceReturn>(f.Scope.VerifyTransition(f.Transition));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var source = new MoneyRevenueCropNativeSharedSourceReference(f.Files.RequestPath, f.Files.LedgerPath, [Id],
            "declared.shared.cooking.return", scope.InputHashes)
        { SettledInventoryConsumption = f.Settlement, SettledConsumptionReferencePath = f.ReferencePath };
        var transition = f.Transition with { NativeSharedSource = source };
        object?[] verificationArguments = [transition, true, null];
        var verifiedScope = Assert.IsType<CropTeacherSharedSourceReturn>(typeof(MoneyRevenueCropNativeSharedSourceVerifier)
            .GetMethod("VerifyInventoryRetentionSource", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, verificationArguments));
        var observation = verificationArguments[2]!;
        var verifiedSource = Assert.IsType<StrategyCommitmentLedger>(observation.GetType().GetProperty("SourceLedger")!.GetValue(observation));
        var settled = Assert.IsType<StrategyCommitmentLedger>(observation.GetType().GetProperty("SettledLedger")!.GetValue(observation));
        Assert.Equal(f.BeforeLedger.Revision, verifiedSource.Revision);
        Assert.Equal(f.SettlementReply.Ledger!.Revision, settled.Revision);
        Assert.Equal(scope.AfterStateHash, verifiedScope.AfterStateHash);
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
