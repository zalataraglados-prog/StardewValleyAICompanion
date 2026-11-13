using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Fact]
    public async Task ManagedSeedTransferCacheReimportsAcceptedSourceBeforePreparingOriginalTransfer()
    {
        using var f = new Fixture(nativeEvidence: true);
        using var runtime = new RuntimeFixture(f);
        runtime.Options.UseRuntimeTestHarnessExecutor = true;
        using var inputs = new OwnerInputs(f.SourcePath, f.LedgerPath, f.RequestPath);
        using var files = new OwnerFiles(runtime.Options, runtime.Client, f.SourcePath, f.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        Assert.Equal(CropTeacherRunStageKinds.TransferSeedSupply, step.Stage);
        runtime.Store.Snapshots.Clear();
        using var missing = await runtime.Client.GetAsync(runtime.Options.BackendUrl +
            "/api/v1/strategy/commitments/latest?stateHash=" + f.Before.StateHash);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var sourceBytes = File.ReadAllBytes(f.SourcePath);
        var task = InvokeLive<Task>("BuildCropTeacherOwnedControlQueueAsync", runtime.Client, runtime.Options, 1, step,
            inputs.Plan, inputs.Request, owner);
        await task;
        var built = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var build = built.GetType().GetProperty("Build")!.GetValue(built)!;
        var operation = (CropTeacherManagedSeedOperation)built.GetType().GetProperty("Operation")!.GetValue(built)!;
        var prepared = Assert.IsType<CropTeacherSeedTransferRuntime>(build.GetType().GetProperty("SeedTransferSupport")!.GetValue(build));
        var queue = ((JsonObject)build.GetType().GetProperty("Queue")!.GetValue(build)!).Deserialize<ActionQueueEnvelope>(JsonOptions)!;
        Assert.Same(owner.Pending, operation);
        Assert.Equal(CropTeacherRunStageKinds.TransferSeedSupply, operation.ReadStep().Stage);
        Assert.Equal("pending", queue.Status);
        Assert.Equal(f.Before.StateHash, queue.StateHash);
        Assert.Empty(queue.CompilerDiagnostics);
        Assert.Equal("executor.transfer_material", Assert.Single(queue.Items).OptionId);
        prepared.ValidateDispatchQueue(Path.Combine(operation.RuntimeDirectory, "compiled-seed-queue.json"));
        Assert.Equal("full", build.GetType().GetProperty("SnapshotProfile")!.GetValue(build));
        Assert.True(runtime.Store.Snapshots.ContainsKey(f.Before.StateHash));
        Assert.Equal(sourceBytes, File.ReadAllBytes(f.SourcePath));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Empty(Directory.EnumerateFiles(operation.RuntimeDirectory, "execution-*.json", SearchOption.AllDirectories));
    }
}
