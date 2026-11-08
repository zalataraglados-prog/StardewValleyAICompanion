using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Fact]
    public async Task ManagedOwnerRestoresOriginalEightTilePrefixAndRunsTheSameExecutionLoop()
    {
        using var f = new RuntimeFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Options, f.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Files.Before,
            preparedCommitmentIds: new HashSet<string>(StringComparer.Ordinal) { f.Files.Id }, resourceLedger: f.Files.Ledger);
        step = step with { SeedReservationId = f.Files.OwnedSlotOne };
        var operation = await owner.AdmitAsync(step, f.Files.SourcePath, f.Files.LedgerPath, files.Native);
        await owner.PrepareSeedAsync(operation, () => f.Prepare(selectedScope: operation.Scopes.Planting,
            runtimeDirectory: operation.RuntimeDirectory, selectedStep: operation.ReadStep()));
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        var restored = await resumed.RestorePreparedSeedAsync(f.Client, files.Recovery, files.Post,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Client, f.Options, diagnostic, request),
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", f.Client, f.Options, diagnostic, request));
        var planting = Assert.IsType<CropTeacherSeedPlantingRuntime>(restored);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(0, f.NativeRequests);
        var pending = resumed.Pending!;
        var receipt = await resumed.ExecuteSeedAsync(pending, original =>
        {
            original.ValidateDispatchQueue(f.Files.QueuePath);
            return f.Execute(planting);
        });
        Assert.Equal(8, f.NativeRequests);
        Assert.Equal(8, f.FullSnapshotReads);
        await restored.CompleteAsync(f.Files.QueuePath, receipt["execution_path"]!.GetValue<string>(),
            receipt["after_snapshot_path"]!.GetValue<string>(), f.Transition.SelectedCandidateId);
        await resumed.AcceptLiveAsync(pending, restored);
        Assert.Null(resumed.Pending);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(1, f.Handler.Settlements);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ManagedOwnerUsesOriginalPlantingSelectionAndPartialConsumption(bool explicitClaim, bool managedPreparation)
    {
        using var f = new RuntimeFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Options, f.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Files.Before,
            preparedCommitmentIds: new HashSet<string>(StringComparer.Ordinal) { f.Files.Id }, resourceLedger: f.Files.Ledger);
        Assert.Equal(CropTeacherRunStageKinds.ExecuteCropBatch, step.Stage);
        if (explicitClaim) step = step with { SeedReservationId = f.Files.OwnedSlotOne };
        var operation = await owner.AdmitAsync(step, f.Files.SourcePath, f.Files.LedgerPath, files.Native);
        Task<CropTeacherSeedPlantingRuntime> Prepare() => f.Prepare(consumed: explicitClaim ? 3 : 1, failedSuffix: explicitClaim,
            selectedScope: operation.Scopes.Planting, runtimeDirectory: operation.RuntimeDirectory, selectedStep: operation.ReadStep());
        var runtime = managedPreparation ? await owner.PrepareSeedAsync(operation, Prepare) : await Prepare();
        if (!managedPreparation) await owner.PinPreparedAsync(operation, runtime);
        async Task<bool> Execute(ICropTeacherSeedRuntime original)
        {
            original.ValidateDispatchQueue(f.Transition.QueuePath);
            await f.Observe(runtime);
            await runtime.CompleteAsync(f.Transition);
            return true;
        }
        if (managedPreparation) await owner.ExecuteSeedAsync(operation, Execute);
        else await Execute(runtime);
        var reads = f.Handler.LedgerReads;
        await owner.AcceptLiveAsync(operation, runtime);
        Assert.Null(owner.Pending);
        Assert.Equal(reads, f.Handler.LedgerReads);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(1, f.Handler.Settlements);
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
        Assert.Equal(explicitClaim ? 6 : 9, ledger.MaterialReservations.Single(row => row.ReservationId == f.Files.OwnedSlotOne).Quantity);
        Assert.Equal("active", ledger.CropPlantingCommitments.Single().Status);
    }
}
