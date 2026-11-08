using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OwnedPartialPlantingLooksUpTheOriginalMarkerThenUsesOriginalCompilerPrefixAndLoop(bool retainedQueue, bool noDispatch)
    {
        using var f = new RuntimeFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Options, f.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Files.Before,
            preparedCommitmentIds: new HashSet<string>(StringComparer.Ordinal) { f.Files.Id }, resourceLedger: f.Files.Ledger)
            with { SeedReservationId = f.Files.OwnedSlotOne };
        var operation = await owner.AdmitAsync(step, f.Files.SourcePath, f.Files.LedgerPath, files.Native);
        f.BeforeWrite = path =>
        {
            if (retainedQueue && Path.GetFileName(path) == "preparation-summary.json")
                File.Copy(f.Files.QueuePath, Path.Combine(files.Native, "compiled-seed-queue.json"));
            CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, retainedQueue ? "preparation-summary.json" : "marker-reply.json");
        };
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSeedAsync(operation,
            () => f.Prepare(selectedScope: operation.Scopes.Planting, runtimeDirectory: files.Native, selectedStep: operation.ReadStep())));
        var compiles = 0;
        var prepared = await owner.ContinueSeedPreparationAsync(f.Client, files.Recovery, files.Post,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Client, f.Options, diagnostic, request),
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", f.Client, f.Options, diagnostic, request), context =>
            {
                compiles++;
                return f.CompileContinuation(context);
            });
        var planting = Assert.IsType<CropTeacherSeedPlantingRuntime>(prepared);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(retainedQueue ? 0 : 1, f.Handler.Lookups);
        Assert.Equal(retainedQueue ? 0 : 1, compiles);
        var receipt = await owner.ExecuteSeedAsync(operation, original =>
        {
            original.ValidateDispatchQueue(f.Files.QueuePath);
            return f.Execute(planting, attachOwner: !noDispatch);
        });
        Assert.Equal(noDispatch ? 0 : 8, f.NativeRequests);
        await prepared.CompleteAsync(f.Files.QueuePath, receipt["execution_path"]!.GetValue<string>(),
            receipt["after_snapshot_path"]!.GetValue<string>(), f.Transition.SelectedCandidateId);
        if (noDispatch) await owner.AcceptStoppedSeedAsync(operation, prepared);
        else await owner.AcceptLiveAsync(operation, prepared);
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        await resumed.RestoreHeadAsync(f.Client, files.Recovery + "-native", files.Post);
        Assert.False(resumed.HeadRecoveryRequired);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(noDispatch ? 0 : 1, f.Handler.Settlements);
    }
}
