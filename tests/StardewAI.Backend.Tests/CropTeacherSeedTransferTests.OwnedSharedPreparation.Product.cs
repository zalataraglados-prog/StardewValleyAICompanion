using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductSharedPreparationKeepsControllerVersionAndRequiresProductNativeWire(bool productWire)
    {
        using var f = new SharedTransferReturnFixture();
        var (decision, request) = PreviewSharedPreparation(f);
        f.Runtime.Options.UseProductExecutor = true;
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs) { RetainArtifacts = true };
        await using var owner = await files.Open();
        var receipt = files.Native + "-receipt.json"; var after = files.Native + "-after.json";
        var operation = await owner.AdmitSharedTransferPreparationAsync(decision, request, receipt, after, files.Native);
        var selected = await owner.PrepareSharedTransferAsync(operation, f.Runtime.Client,
            (diagnostic, actual) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, actual),
            (ledger, root) => Task.FromResult(PrepareSharedTransferSelection(f, ledger, root, decision, updateTransition: false)));
        f.PreparationReference = selected.MaterialTransferPreparation!;
        f.Transition = f.Transition with { QueuePath = selected.QueuePath, ExecutionReceiptPath = receipt, AfterSnapshotPath = after,
            ExecutorVersion = PolicyTrajectoryVersionPins.ProductExecutor };
        Task<JsonObject> Execute() => InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Runtime.Client,
            f.Runtime.Options, owner, operation, selected.QueuePath, (Func<Task<JsonObject>>)(async () =>
            {
                await f.Build(product: productWire, includePreparation: true, settle: false, alreadyPrepared: true);
                return new() { ["execution_path"] = receipt, ["after_snapshot_path"] = after };
            }));
        if (!productWire)
        {
            await Assert.ThrowsAsync<InvalidDataException>(Execute);
            Assert.Same(operation, owner.Pending);
            Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(0, f.Runtime.Handler.Settlements);
            return;
        }
        await Execute();
        Assert.Null(owner.Pending); Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
        var shared = Assert.Single((await owner.ReadJointProductionTraceAsync()).SharedSourceReturns);
        var transition = shared.NativeTransition with { NativeSharedSource = shared.SourceReference };
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithPreparedTransferYields(transition));
        Assert.Equal(f.Files.After.StateHash, MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithPreparedProductTransferYields(transition).AfterStateHash);
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, files.Recovery, files.Post);
        Assert.Equal(after, reopened.CurrentSnapshotPath); Assert.Equal(2, f.Runtime.Handler.Commits);
    }
}
