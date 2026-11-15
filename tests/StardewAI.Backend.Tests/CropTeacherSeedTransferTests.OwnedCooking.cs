using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;
using StardewAI.GoalConditionedBootstrap;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCookingOwnsPreparationBeforeRpcAndCompletesOriginalMainOnce(bool product)
    {
        using var f = new SharedCookingFixture(); f.PrepareSource(product);
        var decision = CookingDecision(f);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        await using var owner = await files.Open();
        var receipt = Path.Combine(files.Native + "-outputs", "receipt.json");
        var after = Path.Combine(files.Native + "-outputs", "after.json");
        var originals = new[] { f.Files.SourcePath, f.Files.LedgerPath }.ToDictionary(path => path, SharedCookingFixture.Hash);
        var operation = await owner.AdmitSharedCookingPreparationAsync(decision, f.PreparationRequest(), receipt, after, files.Native);
        Assert.Equal(0, f.Runtime.Handler.Commits); Assert.Same(operation, owner.Pending);
        var admission = Directory.GetFiles(Path.GetDirectoryName(files.Native)!, "operation-*-intent.json").Single();
        var admissionHash = SharedCookingFixture.Hash(admission);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Runtime.Client));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.PrepareSharedTransferAsync(operation, f.Runtime.Client,
            (_, _) => throw new Exception("wrong protocol must not commit"), (_, _) => throw new Exception("must not compile")));
        var selection = await owner.PrepareSharedCookingAsync(operation, f.Runtime.Client, async (diagnostic, request) =>
        {
            Assert.Same(operation, owner.Pending); Assert.Equal(admissionHash, SharedCookingFixture.Hash(admission));
            return await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request);
        }, (ledger, output) => Task.FromResult(CompileOwnedCookingSelection(f, ledger, output, decision)));
        Assert.NotNull(selection.InventoryConsumptionPreparation); Assert.Null(selection.MaterialTransferPreparation);
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(admissionHash, SharedCookingFixture.Hash(admission));
        var calls = 0;
        await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Runtime.Client, f.Runtime.Options,
            owner, operation, selection.QueuePath, (Func<Task<JsonObject>>)(async () =>
            {
                calls++; await f.WriteOwnedNative(selection, receipt, after, product);
                return new() { ["execution_path"] = receipt, ["after_snapshot_path"] = after };
            }));
        Assert.Equal(1, calls); Assert.Null(owner.Pending);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
        Assert.All(originals, pair => Assert.Equal(pair.Value, SharedCookingFixture.Hash(pair.Key)));
        var accepted = JsonNode.Parse(File.ReadAllText(Directory.GetFiles(Path.GetDirectoryName(files.Native)!, "operation-*-accepted.json").Single()))!;
        Assert.NotNull(accepted["shared_consumption_prepared_sha256"]); Assert.Null(accepted["shared_transfer_prepared_sha256"]);
        Assert.True(accepted["native_shared_inventory_consumption_settlement_verified"]!.GetValue<bool>());
        var nativeClaim = JsonNode.Parse(File.ReadAllText(Path.Combine(files.Native, "shared-source-native-claim.json")))!;
        Assert.NotNull(nativeClaim["shared_consumption_prepared_sha256"]); Assert.Null(nativeClaim["shared_transfer_prepared_sha256"]);
        var final = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
        Assert.Null(final.PreparedInventoryConsumption);
        Assert.Equal(0, final.MaterialReservations.Single(row => row.ReservationId == "cooking-wood").SlotIndex);
        Assert.Equal(1, final.MaterialReservations.Single(row => row.ReservationId == "cooking-stone").SlotIndex);
        var shared = Assert.Single((await owner.ReadJointProductionTraceAsync()).SharedSourceReturns);
        var transition = shared.NativeTransition with { NativeSharedSource = shared.SourceReference };
        Assert.NotNull(shared.SourceReference.SettledInventoryConsumption); Assert.Null(shared.SourceReference.SettledMaterialTransfer);
        Assert.NotNull(shared.SourceReference.SettledConsumptionReferencePath); Assert.Null(shared.SourceReference.SettledTransferReferencePath);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithTransferredSaleStock(transition));
        Assert.Equal(f.After.StateHash, MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithSharedInventoryEffects(transition).AfterStateHash);
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, files.Recovery, files.Post);
        Assert.Null(reopened.Pending); Assert.Equal(after, reopened.CurrentSnapshotPath);
        Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
    }
}
