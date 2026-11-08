using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("incomplete")]
    [InlineData("bad_marker")]
    [InlineData("queue")]
    [InlineData("journal")]
    [InlineData("proof_without_claim")]
    [InlineData("legacy")]
    [InlineData("legacy_start_v1")]
    public async Task PreparedRestoreRejectsIncompleteChangedLegacyOrUnclaimedExecution(string fault)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        if (fault == "legacy")
        {
            var standalone = await runtime.Prepare(transition, selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native);
            await owner.PinPreparedAsync(operation, standalone);
        }
        else await owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native));
        await owner.DisposeAsync();
        if (fault == "legacy_start_v1")
        {
            var path = Path.Combine(files.OwnerRoot, "operation-000001-started.json");
            var value = JsonNode.Parse(File.ReadAllText(path))!;
            value["schema_version"] = "crop_teacher_managed_seed_start.v1";
            value.AsObject().Remove("dispatch_protocol");
            File.WriteAllText(path, value.ToJsonString());
            path = Path.Combine(files.OwnerRoot, "operation-000001-prepared.json");
            value = JsonNode.Parse(File.ReadAllText(path))!;
            value.AsObject().Remove("dispatch_protocol");
            File.WriteAllText(path, value.ToJsonString());
        }
        if (fault is "incomplete" or "bad_marker")
        {
            var path = Path.Combine(files.Native, fault == "incomplete" ? "preparation-summary.json" : "marker-reply.json");
            var value = JsonNode.Parse(File.ReadAllText(path))!;
            if (fault == "incomplete") value["status"] = "incomplete";
            else value["accepted"] = false;
            File.WriteAllText(path, value.ToJsonString());
        }
        if (fault == "queue") File.AppendAllText(transition.QueuePath, " ");
        if (fault == "journal")
        {
            var path = Directory.EnumerateFiles(runtime.LiveOptions.SnapshotDir, "portfolio-mutation-*-rpc-request.json").Single();
            File.AppendAllText(path, " ");
        }
        if (fault == "proof_without_claim") File.WriteAllText(Path.Combine(files.Native, "support-proof.json"), "{}");
        // Index rejects pinned queue drift itself; the other cases reach the shared preparation verifier or protocol gate.
        if (fault == "queue") await Assert.ThrowsAsync<InvalidDataException>(() => files.Open());
        else
        {
            await using var resumed = await files.Open();
            if (fault is "legacy" or "legacy_start_v1" or "proof_without_claim")
                await Assert.ThrowsAsync<InvalidOperationException>(() => RestorePrepared(files, resumed, runtime));
            else await Assert.ThrowsAsync<InvalidDataException>(() => RestorePrepared(files, resumed, runtime));
            Assert.NotNull(resumed.Pending);
            Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-dispatch.json")));
        }
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(0, runtime.Handler.Lookups);
    }

    [Fact]
    public async Task PreparedRestoreGuardIsRecheckedBeforeClaimingTheExecutionLoop()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        await owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native));
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        await RestorePrepared(files, resumed, runtime);
        File.AppendAllText(Path.Combine(files.Native, "marker-reply.json"), " ");
        var calls = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => resumed.ExecuteSeedAsync(resumed.Pending!, _ => Task.FromResult(++calls)));
        Assert.Equal(0, calls);
        Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-dispatch.json")));
    }

    [Theory]
    [InlineData("prepared_sha256")]
    [InlineData("support_step_id")]
    [InlineData("native_request_performed")]
    public async Task OwnedDispatchIndexRejectsIdentityDriftAndFalseZeroClassification(string field)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        await owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native));
        await owner.ExecuteSeedAsync(operation, _ => Task.FromResult(true));
        await owner.DisposeAsync();
        var path = Path.Combine(files.OwnerRoot, "operation-000001-dispatch.json");
        var value = JsonNode.Parse(File.ReadAllText(path))!;
        if (field == "native_request_performed") value[field] = false;
        else value[field] = "changed";
        File.WriteAllText(path, value.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => files.Open());
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
    }

    [Fact]
    public async Task OwnedExecutionHoldsItsSingleOwnerLeaseUntilTheOriginalLoopReturns()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = await owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native));
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executing = owner.ExecuteSeedAsync(operation, async original =>
        {
            original.ValidateDispatchQueue(transition.QueuePath);
            entered.SetResult(true);
            await release.Task;
            original.ValidateDispatchQueue(transition.QueuePath);
            return true;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disposing = owner.DisposeAsync().AsTask();
        try { Assert.False(disposing.IsCompleted); }
        finally { release.TrySetResult(true); }
        await executing;
        await disposing;
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateDispatchQueue(transition.QueuePath));
    }
}
