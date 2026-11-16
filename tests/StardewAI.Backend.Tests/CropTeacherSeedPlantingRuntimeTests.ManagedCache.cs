using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Fact]
    public async Task ManagedPlantingCacheReimportsAcceptedSourceBeforeReadingItsLedger()
    {
        using var f = new RuntimeFixture();
        f.Files.AdvanceSourceWithMarkerOnly();
        f.Options.UseRuntimeTestHarnessExecutor = true;
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Options, f.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Files.Before,
            preparedCommitmentIds: new HashSet<string>(StringComparer.Ordinal) { f.Files.Id }, resourceLedger: f.Files.Ledger);
        Assert.Equal(CropTeacherRunStageKinds.ExecuteCropBatch, step.Stage);
        f.Store.Snapshots.Clear();
        using var missing = await f.Client.GetAsync(f.Options.BackendUrl +
            "/api/v1/strategy/commitments/latest?stateHash=" + f.Files.Before.StateHash);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var sourceBytes = File.ReadAllBytes(f.Files.SourcePath);
        var task = InvokeLive<Task>("BuildCropTeacherOwnedControlQueueAsync", f.Client, f.Options, 1, step,
            inputs.Plan, inputs.Request, owner);
        await task;
        var built = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var build = built.GetType().GetProperty("Build")!.GetValue(built)!;
        var operation = (CropTeacherManagedSeedOperation)built.GetType().GetProperty("Operation")!.GetValue(built)!;
        var queue = ((JsonObject)build.GetType().GetProperty("Queue")!.GetValue(build)!).Deserialize<ActionQueueEnvelope>(JsonOptions)!;
        Assert.Same(owner.Pending, operation);
        Assert.IsType<CropTeacherSeedPlantingRuntime>(build.GetType().GetProperty("SeedPlantingSupport")!.GetValue(build));
        Assert.Equal("pending", queue.Status); Assert.Equal(f.Files.Before.StateHash, queue.StateHash);
        Assert.All(queue.Items, row => Assert.Equal("executor.plant_seed", row.OptionId));
        Assert.Equal("full", build.GetType().GetProperty("SnapshotProfile")!.GetValue(build));
        Assert.True(f.Store.Snapshots.ContainsKey(f.Files.Before.StateHash));
        Assert.Equal(sourceBytes, File.ReadAllBytes(f.Files.SourcePath));
        Assert.Equal(1, f.Handler.Commits); Assert.Equal(0, f.Handler.Settlements); Assert.Equal(0, f.NativeRequests);
    }
}
