using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("bad_marker")]
    [InlineData("bad_failed_summary")]
    [InlineData("missing_intent")]
    [InlineData("missing_journal")]
    [InlineData("lookup_missing")]
    [InlineData("bad_queue")]
    [InlineData("legacy")]
    [InlineData("authority_drift")]
    public async Task OwnedPartialPreparationRejectsUnknownOrChangedEvidenceBeforeCallingTheCompiler(string fault)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f, fault: fault == "lookup_missing" ? "missing_recovery_lookup" : "");
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        Task<CropTeacherSeedPurchaseRuntime> Prepare() => runtime.Prepare(transition,
            compile: () => throw new IOException("Original compilation interrupted."), selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native);
        if (fault == "legacy") await Assert.ThrowsAsync<IOException>(Prepare);
        else await Assert.ThrowsAsync<IOException>(() => owner.PrepareSeedAsync(operation, Prepare));
        await owner.DisposeAsync();
        var markerPath = Path.Combine(files.Native, "marker-reply.json");
        if (fault == "bad_marker")
        {
            var marker = JsonNode.Parse(File.ReadAllText(markerPath))!;
            marker["accepted"] = false;
            File.WriteAllText(markerPath, marker.ToJsonString());
        }
        if (fault == "bad_failed_summary")
        {
            var path = Path.Combine(files.Native, "preparation-summary.json");
            var failed = JsonNode.Parse(File.ReadAllText(path))!;
            failed["run_id"] = "different-run";
            File.WriteAllText(path, failed.ToJsonString());
        }
        if (fault == "missing_intent") File.Delete(Path.Combine(files.Native, "support-intent.json"));
        if (fault == "missing_journal") File.Delete(Directory.EnumerateFiles(runtime.LiveOptions.SnapshotDir, "portfolio-mutation-*-rpc-journal.json").Single());
        if (fault == "lookup_missing") File.Delete(markerPath);
        if (fault == "bad_queue") File.WriteAllText(Path.Combine(files.Native, "compiled-seed-queue.json"), "{}");
        if (fault == "authority_drift")
        {
            var result = runtime.Repository.CommitReservationPortfolio(f.Before, new ReservationPortfolioCommitRequest
            {
                StateHash = f.Before.StateHash, ExpectedLedgerRevision = runtime.Repository.Get(f.Before).Revision,
                PortfolioId = "other-root.partial-prepare", SourceDecisionId = "other-root.partial-prepare", GoalId = "earn_money"
            });
            Assert.True(result.Accepted, string.Join(",", result.Errors));
        }
        await using var resumed = await files.Open();
        var calls = 0;
        Task<ICropTeacherSeedRuntime> Continue() => ContinuePartial(files, resumed, runtime, _ => Task.FromResult((++calls).ToString()));
        if (fault == "legacy") await Assert.ThrowsAsync<InvalidOperationException>(Continue);
        else if (fault.StartsWith("missing_", StringComparison.Ordinal)) await Assert.ThrowsAnyAsync<IOException>(Continue);
        else await Assert.ThrowsAsync<InvalidDataException>(Continue);
        Assert.Equal(0, calls);
        Assert.NotNull(resumed.Pending);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-dispatch.json")));
    }

    [Theory]
    [InlineData("reference_path")]
    [InlineData("view_hash")]
    [InlineData("earlier_failure")]
    public async Task OwnedPartialPreparationPinsTheAppendOnlyViewAndOriginalFailedArtifacts(string fault)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition();
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        await Assert.ThrowsAsync<IOException>(() => owner.PrepareSeedAsync(operation, () => runtime.Prepare(transition,
            compile: () => throw new IOException("Original compilation interrupted."), selectedScope: operation.Scopes.Purchase, runtimeDirectory: files.Native)));
        await ContinuePartial(files, owner, runtime, async context =>
        {
            var path = Path.Combine(context.OutputDirectory, "compiled-seed-queue.json");
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, File.ReadAllText(transition.QueuePath));
            return path;
        });
        if (fault == "earlier_failure")
        {
            File.AppendAllText(Path.Combine(files.Native, "preparation-summary.json"), " ");
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.ExecuteSeedAsync(operation, _ => Task.FromResult(true)));
            Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-dispatch.json")));
        }
        else
        {
            await owner.DisposeAsync();
            var path = Path.Combine(files.OwnerRoot, "operation-000001-prepared.json");
            var record = JsonNode.Parse(File.ReadAllText(path))!;
            var reference = record["preparation_reference"]!;
            if (fault == "reference_path")
            {
                reference["path"] = Path.Combine(files.Native, "preparation-summary.json");
                reference["sha256"] = CropTeacherSeedRecoveryTestSupport.Hash(reference["path"]!.GetValue<string>());
                File.WriteAllText(path, record.ToJsonString());
            }
            else File.AppendAllText(reference["path"]!.GetValue<string>(), " ");
            await Assert.ThrowsAsync<InvalidDataException>(() => files.Open());
        }
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
    }
}
