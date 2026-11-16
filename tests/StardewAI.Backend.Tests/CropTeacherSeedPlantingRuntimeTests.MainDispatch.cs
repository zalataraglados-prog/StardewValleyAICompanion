using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Fact]
    public async Task MainOwnedPlantingUsesOriginalBatchIdentityWithoutChangingItsMechanicalQueue()
    {
        using var f = new RuntimeFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Options, f.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Files.Before,
            preparedCommitmentIds: new HashSet<string>(StringComparer.Ordinal) { f.Files.Id }, resourceLedger: f.Files.Ledger);
        step = step with { SeedReservationId = f.Files.OwnedSlotOne };
        var operation = await owner.AdmitAsync(step, f.Files.SourcePath, f.Files.LedgerPath, files.Native);
        var runtime = await owner.PrepareSeedAsync(operation, () => f.Prepare(selectedScope: operation.Scopes.Planting,
            runtimeDirectory: operation.RuntimeDirectory, selectedStep: operation.ReadStep()));
        var queue = JsonSerializer.SerializeToNode(f.Queue, JsonOptions)!.AsObject();
        var original = queue.ToJsonString();
        Assert.All(f.Queue.Items, item => Assert.DoesNotContain(item.NormalizedCommand.Parameters,
            parameter => parameter.Name == "precondition" && parameter.Value.StartsWith("candidate_id:", StringComparison.Ordinal)));
        var candidate = InvokeLive<string>("ValidateCropTeacherSeedQueueBeforeDispatch", runtime, owner, operation,
            queue, f.Files.QueuePath);
        Assert.Equal(operation.SupportStepId, candidate);
        Assert.Equal(original, queue.ToJsonString());
        Assert.Equal(0, f.NativeRequests);
        Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-dispatch.json")));
        var receipt = await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherManagedOperationAsync", f.Client, f.Client,
            f.Options, 1, owner, operation, f.Files.QueuePath,
            (Func<Task<JsonObject>>)(() => f.Execute(runtime, selectedCandidateId: candidate)));
        Assert.Equal(candidate, receipt["selected_candidate_id"]!.GetValue<string>());
        Assert.Equal(8, f.NativeRequests);
        Assert.Equal(8, f.FullSnapshotReads);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(1, f.Handler.Settlements);
        Assert.Null(owner.Pending);
        Assert.Equal(receipt["after_snapshot_path"]!.GetValue<string>(), owner.CurrentSnapshotPath);
    }

    [Fact]
    public async Task MainUnmanagedPlantingKeepsItsExistingExplicitSelectionRequirement()
    {
        using var f = new RuntimeFixture();
        var runtime = await f.Prepare();
        var queue = JsonSerializer.SerializeToNode(f.Queue, JsonOptions)!.AsObject();
        var error = Assert.Throws<TargetInvocationException>(() => InvokeLive<string>(
            "ValidateCropTeacherSeedQueueBeforeDispatch", runtime, null!, null!, queue, f.Files.QueuePath));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(0, f.NativeRequests);
    }
}
