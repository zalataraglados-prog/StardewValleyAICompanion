using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("missing_stock")]
    [InlineData("downgrade")]
    [InlineData("missing_variants")]
    public void ReservedTerminalCannotBorrowUnreservedStockOrSilentlyUseTheOldConsumer(string fault)
    {
        using var f = new TerminalOutcomeFixture();
        var proof = JsonSerializer.Deserialize<MoneyRevenueCropOutputRetentionProof>(File.ReadAllText(f.ProofPath), JsonOptions)!;
        proof.OutputRetentionKey = "declared-native-leaf-wave"; Fixture.Save(f.ProofPath, proof);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f.ProofPath))).ToLowerInvariant();
        var target = MarkTerminalVariant(f.Target, f.Before) with { RetentionProofSha256 = hash, RequireSaleStockReservations = true };
        if (fault == "missing_variants") target = target with { NativeVariants = null };
        Assert.Throws<InvalidDataException>(() => fault == "downgrade"
            ? CropTeacherTerminalShipping.Preparation.OpenNativeVariants(f.Shared.Inputs.Plan.RequestPath, f.Shared.Source.SourcePath,
                f.Shared.Source.LedgerPath, [Id], "reserved-leaf", target, f.ModelPath, f.QueuePath)
            : CropTeacherTerminalShipping.Preparation.OpenNativeVariantsConsumingSaleStock(f.Shared.Inputs.Plan.RequestPath,
                f.Shared.Source.SourcePath, f.Shared.Source.LedgerPath, [Id], "reserved-leaf", target, f.ModelPath, f.QueuePath));
    }

    [Fact]
    public async Task ReservedSaleStockConsumesSixNativeVariantUnitsAtomicallyAndRestoresTheOriginalOwner()
    {
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        using var f = new NativeVariantTerminalFixture();
        var target = f.Target with { RequireSaleStockReservations = true };
        var proof = JsonSerializer.Deserialize<MoneyRevenueCropOutputRetentionProof>(File.ReadAllText(target.RetentionProofPath), JsonOptions)!;
        proof.OutputRetentionKey = "declared-native-leaf-sale-wave";
        Fixture.Save(target.RetentionProofPath, proof);
        target = target with { RetentionProofSha256 = Hash(target.RetentionProofPath) };
        const string producer = "declared-native-leaf-producer";
        var initial = f.Shared.Source.Before;
        var rows = initial.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().ToArray();
        var setupRequest = new ReservationPortfolioCommitRequest
        {
            StateHash = initial.StateHash, ExpectedLedgerRevision = f.Shared.Source.Ledger.Revision,
            PortfolioId = producer, SourceDecisionId = producer, GoalId = "earn_money",
            MaterialClaims = target.ReadNativeVariants().Select(variant => new MaterialReservationUpsertRequest
            {
                StateHash = initial.StateHash, ExpectedLedgerRevision = f.Shared.Source.Ledger.Revision,
                ReservationId = CropSaleStockReservationContract.ReservationId(producer, variant.SlotIndex),
                SourceDecisionId = producer, GoalId = "earn_money", NodeId = "player:" + target.PlayerId,
                SlotIndex = variant.SlotIndex, QualifiedItemId = target.QualifiedItemId, Quantity = variant.PlannedQuantity,
                Purpose = CropSaleStockReservationContract.Purpose(Id, proof.OutputRetentionKey,
                    MoneyRevenueCropRetentionAccount.InventoryIdentity(rows.Single(row => row.GetProperty("slot_index").GetInt32() == variant.SlotIndex)))
            }).ToArray()
        };
        // Declared calibration stock/leaf proof, not a whole production or real-game claim.
        var setup = await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Shared.Runtime.Client,
            f.Shared.Runtime.LiveOptions, new JsonObject(), setupRequest);
        var reserved = setup.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)!;
        Assert.True(reserved.Accepted, string.Join(",", reserved.Errors));
        var ledgerPath = Path.Combine(f.Root, "sale-stock-initial-ledger.json"); Fixture.Save(ledgerPath, reserved.Ledger);
        var oldTarget = target with { RequireSaleStockReservations = false };
        var oldModel = CropTeacherTerminalShipping.Preparation.BuildOriginalAction(initial, oldTarget);
        var oldQueue = new ActionQueueCompiler().Compile(oldModel, initial, reserved.Ledger);
        var oldModelPath = Path.Combine(f.Root, "downgrade-model.json"); var oldQueuePath = Path.Combine(f.Root, "downgrade-queue.json");
        Fixture.Save(oldModelPath, oldModel); Fixture.Save(oldQueuePath, oldQueue);
        Assert.Contains("cannot downgrade", Assert.Throws<InvalidDataException>(() =>
            CropTeacherTerminalShipping.Preparation.OpenNativeVariants(f.Shared.Inputs.Plan.RequestPath, f.Shared.Source.SourcePath,
                ledgerPath, [Id], "downgrade-leaf", oldTarget, oldModelPath, oldQueuePath)).Message);
        using var files = new OwnerFiles(f.Shared.Runtime.LiveOptions, f.Shared.Runtime.Client,
            f.Shared.Source.SourcePath, ledgerPath, f.Shared.Inputs);
        var owner = await files.Open();
        try
        {
            var source = initial;
            for (var index = 0; index < 6; index++)
            {
                var next = target with { AcceptedDepositCount = index };
                var model = CropTeacherTerminalShipping.Preparation.BuildOriginalAction(source, next);
                var queue = new ActionQueueCompiler().Compile(model, source, ReadLedger(owner.CurrentLedgerPath));
                var prefix = Path.Combine(f.Root, "reserved-unit-" + index);
                var modelPath = prefix + "-model.json"; var queuePath = prefix + "-queue.json";
                var receiptPath = prefix + "-receipt.json"; var afterPath = prefix + "-after.json";
                Fixture.Save(modelPath, model); Fixture.Save(queuePath, queue);
                var after = f.Deposit(source, index);
                var candidate = model.ModelOutputId["model-output:".Length..];
                var receipt = f.Receipt(queue, source, after, candidate, index);
                var operation = await owner.CompileAndAdmitOriginalTerminalAsync(next, () => Task.FromResult(
                    new CropTeacherOriginalTerminalCompilation(modelPath, queuePath, receiptPath, afterPath, files.Native + "-stock-" + index)));
                await owner.ExecuteOriginalTerminalAsync(operation, f.Shared.Runtime.Client, () =>
                {
                    Fixture.Save(receiptPath, receipt); Fixture.Save(afterPath, after);
                    f.Shared.Runtime.Store.Snapshots[after.StateHash] = after;
                    return Task.FromResult(new JsonObject { ["execution_path"] = receiptPath, ["after_snapshot_path"] = afterPath });
                });
                await owner.RunTerminalShippingAsync(operation, f.Shared.Runtime.Client, (diagnostic, request) =>
                {
                    Assert.NotNull(request.MaterialConsumption);
                    Assert.Equal(Hash(receiptPath), request.MaterialConsumption!.SupportingTransitionReceiptSha256);
                    Assert.Equal(1, request.MaterialConsumption.Consumption.ConsumedQuantity);
                    Assert.Empty(request.ReleaseReservationIds);
                    return InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Shared.Runtime.Client,
                        f.Shared.Runtime.LiveOptions, diagnostic, request);
                }, files.Post);
                var ledger = ReadLedger(owner.CurrentLedgerPath);
                Assert.Equal(reserved.Ledger!.Revision + index + 1, ledger.Revision);
                Assert.Equal(5 - index, ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                    CropSaleStockReservationContract.TryRead(row, out _)).Sum(row => row.Quantity));
                Assert.Equal(index + 1, owner.AcceptedTerminalDepositCount);
                if (index == 0)
                {
                    await owner.DisposeAsync(); owner = await files.Open();
                    await owner.RestoreHeadAsync(f.Shared.Runtime.Client, files.Recovery, files.Post);
                    Assert.Equal(1, owner.AcceptedTerminalDepositCount);
                }
                source = after;
            }
            Assert.Equal(7, f.Shared.Runtime.Handler.Commits);
            Assert.Null(owner.Pending);
        }
        finally { await owner.DisposeAsync(); }
    }
}
