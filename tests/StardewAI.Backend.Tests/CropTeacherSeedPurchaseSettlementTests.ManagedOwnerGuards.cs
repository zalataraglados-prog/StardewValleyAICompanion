using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedOwnerExclusiveLeaseRejectsAnotherInstanceWithoutBackendCalls()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open();
        await Assert.ThrowsAsync<IOException>(() => files.Open());
        Assert.Null(owner.Pending);
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.LedgerReads);
    }

    [Fact]
    public async Task ManagedOwnerSerializesConcurrentAdmissionsToOnePendingPhase()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        async Task<Exception?> Admit(string path)
        {
            try { await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, path); return null; }
            catch (Exception error) { return error; }
        }
        var results = await Task.WhenAll(Admit(files.Native), Admit(files.Native + "-other"));
        Assert.Single(results, error => error is null);
        Assert.IsType<InvalidOperationException>(Assert.Single(results, error => error is not null));
        Assert.NotNull(owner.Pending);
        Assert.Equal(1, owner.Pending.Sequence);
        Assert.Single(Directory.EnumerateFiles(files.OwnerRoot, "operation-*-intent.json"));
        Assert.Equal(0, runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("executor")]
    [InlineData("orphan")]
    public async Task ManagedOwnerRejectsChangedIdentityOrOrphanIndexBeforeAdmittingWork(string fault)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using (await files.Open()) { }
        if (fault == "orphan") File.WriteAllText(Path.Combine(files.OwnerRoot, "operation-000001-accepted.json"), "{}");
        var options = fault switch
        {
            "run" => files.Options with { RunId = "another-owner-run" },
            "executor" => files.Options with { ExecutorVersion = "another-executor" },
            _ => files.Options
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => files.Open(options));
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.LedgerReads);
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("current_record")]
    [InlineData("future_record")]
    [InlineData("header_child")]
    public async Task ManagedOwnerRejectsOutsideOrReservedRuntimeDirectoryBeforeCreatingAnAdmission(string fault)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var output = fault switch
        {
            "current_record" => Path.Combine(files.OwnerRoot, "operation-000001-intent.json"),
            "future_record" => Path.Combine(files.OwnerRoot, "operation-000002-intent.json"),
            "header_child" => Path.Combine(files.OwnerRoot, "owner-intent.json", "child"),
            _ => files.Recovery
        };
        await Assert.ThrowsAsync<IOException>(() => owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, output));
        Assert.Null(owner.Pending);
        Assert.Empty(Directory.EnumerateFiles(files.OwnerRoot, "operation-*.json"));
    }

    [Fact]
    public async Task ManagedOwnerLostAdmissionWriteAcknowledgementIsRetainedAsPendingAfterRestart()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (Path.GetFileName(path) == "operation-000001-intent.json") throw new IOException("Durable admission written; acknowledgement lost.");
        });
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        await Assert.ThrowsAsync<IOException>(() => owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native + "-retry"));
        await owner.DisposeAsync();
        await using var resumed = await files.Open();
        Assert.NotNull(resumed.Pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native + "-retry"));
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Lookups);
        Assert.Equal(0, runtime.Handler.LedgerReads);
    }

    [Fact]
    public async Task ManagedOwnerCannotAcceptOrForgetAnUnfinishedOriginalRuntime()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = await runtime.Prepare(transition, selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory);
        await owner.PinPreparedAsync(operation, prepared);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.AcceptLiveAsync(operation, prepared));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native + "-next"));
        Assert.Same(operation, owner.Pending);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
    }

    [Fact]
    public async Task ManagedOwnerRejectsChangedPendingRecordWithoutClearingItsOwnership()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        File.AppendAllText(Path.Combine(files.OwnerRoot, "operation-000001-intent.json"), "\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native + "-next"));
        Assert.Same(operation, owner.Pending);
        Assert.Equal(0, runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("missing_sequence")]
    [InlineData("text_sequence")]
    [InlineData("relative_runtime")]
    [InlineData("relative_before")]
    public async Task ManagedOwnerRejectsMalformedRetainedAdmissionBeforeBackendAccess(string fault)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using (var owner = await files.Open())
        {
            var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
            await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        }
        var path = Path.Combine(files.OwnerRoot, "operation-000001-intent.json");
        var record = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        switch (fault)
        {
            case "missing_sequence": record.Remove("sequence"); break;
            case "text_sequence": record["sequence"] = "one"; break;
            case "relative_runtime": record["runtime_directory"] = "native-000001"; break;
            case "relative_before": record["before_snapshot_path"] = "source.json"; break;
        }
        File.WriteAllText(path, record.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => files.Open());
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.LedgerReads);
    }
}
