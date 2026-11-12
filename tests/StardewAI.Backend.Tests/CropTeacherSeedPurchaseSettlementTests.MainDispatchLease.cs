using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MainSeedPreflightDefersOriginalQueueValidationUntilTheOwnedLease(bool tamperQueue)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        var queue = MainSeedQueue(transition);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = await owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory));
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateDispatchQueue(transition.QueuePath));
        if (tamperQueue)
        {
            queue["queue_id"] = "tampered-before-owned-dispatch";
            Fixture.Save(transition.QueuePath, queue);
        }
        var candidate = InvokeLive<string>("ValidateCropTeacherSeedQueueBeforeDispatch", prepared, owner, operation,
            queue, transition.QueuePath);
        Assert.Equal(transition.SelectedCandidateId, candidate);
        Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-dispatch.json")));
        var callbacks = 0;
        Task<JsonObject> Execute() => InvokeLive<Task<JsonObject>>("ExecuteCropTeacherManagedOperationAsync",
            runtime.Client, runtime.Client, runtime.LiveOptions, 1, owner, operation, transition.QueuePath,
            (Func<Task<JsonObject>>)(() =>
            {
                callbacks++;
                prepared.ValidateDispatchQueue(transition.QueuePath);
                runtime.ObserveAfter();
                return Task.FromResult(new JsonObject
                {
                    ["execution_path"] = transition.ExecutionReceiptPath,
                    ["after_snapshot_path"] = transition.AfterSnapshotPath,
                    ["selected_candidate_id"] = transition.SelectedCandidateId
                });
            }));
        if (tamperQueue)
        {
            await Assert.ThrowsAsync<InvalidDataException>(Execute);
            Assert.Equal(0, callbacks); Assert.Equal(0, runtime.Handler.Settlements);
            Assert.Same(operation, owner.Pending);
        }
        else
        {
            await Execute();
            Assert.Equal(1, callbacks); Assert.Equal(1, runtime.Handler.Settlements);
            Assert.Null(owner.Pending); Assert.Equal(transition.AfterSnapshotPath, owner.CurrentSnapshotPath);
        }
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateDispatchQueue(transition.QueuePath));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteSeedAsync(operation, _ => Task.FromResult(true)));
    }

    [Fact]
    public async Task MainSeedPreflightKeepsTheOriginalUnmanagedValidation()
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        var transition = f.Transition();
        var queue = MainSeedQueue(transition);
        var prepared = await runtime.Prepare(transition);
        Assert.Equal(transition.SelectedCandidateId, InvokeLive<string>("ValidateCropTeacherSeedQueueBeforeDispatch",
            prepared, null!, null!, queue, transition.QueuePath));
        queue["queue_id"] = "tampered-unmanaged-dispatch";
        Fixture.Save(transition.QueuePath, queue);
        var error = Assert.Throws<TargetInvocationException>(() => InvokeLive<string>("ValidateCropTeacherSeedQueueBeforeDispatch",
            prepared, null!, null!, queue, transition.QueuePath));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static JsonObject MainSeedQueue(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        var queue = JsonNode.Parse(File.ReadAllText(transition.QueuePath))!.AsObject();
        foreach (var item in queue["items"]!.AsArray())
            item!["normalized_command"]!["parameters"]!.AsArray().Add(new JsonObject
            {
                ["name"] = "precondition", ["value"] = "candidate_id:" + transition.SelectedCandidateId
            });
        Fixture.Save(transition.QueuePath, queue);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(transition.ExecutionReceiptPath), JsonOptions)!;
        foreach (var step in receipt.StepResults)
            step.EffectiveQueueItem = JsonSerializer.SerializeToElement(queue["items"]![step.QueueItemIndex], JsonOptions);
        Fixture.Save(transition.ExecutionReceiptPath, receipt);
        return queue;
    }
}
