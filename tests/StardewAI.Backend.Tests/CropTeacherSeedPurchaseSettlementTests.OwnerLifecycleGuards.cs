using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OwnerLifecycleNotDueCalendarRetainsItsOperationWithoutReleaseOrFalseAcceptance()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open();
        var operation = await owner.AdmitLifecycleAsync(Id, CropTeacherSeedLifecycleRuntime.CalendarCompletion, "",
            f.SourcePath, f.LedgerPath, files.Native);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.RunLifecycleAsync(operation, runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), files.Post));
        Assert.Same(operation, owner.Pending);
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Cancellations);
        Assert.Equal(1, runtime.Handler.LedgerReads);
        Assert.Empty(Directory.GetFiles(files.OwnerRoot, "*-accepted.json"));
    }

    [Fact]
    public async Task OwnerLifecycleChecksTheActualPortfolioJournalDirectoryBeforeAcceptance()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var wrong = Path.Combine(Path.GetDirectoryName(files.OwnerRoot)!, "wrong-journals");
        Directory.CreateDirectory(wrong);
        await using var owner = await files.Open(files.Options with { PortfolioJournalDirectory = wrong });
        var operation = await owner.AdmitLifecycleAsync(Id, CropTeacherSeedLifecycleRuntime.ExplicitCancellation, "owner_wrong_journal",
            f.SourcePath, f.LedgerPath, files.Native);
        await Assert.ThrowsAsync<FileNotFoundException>(() => owner.RunLifecycleAsync(operation, runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), files.Post));
        Assert.Same(operation, owner.Pending);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Cancellations);
        Assert.Equal(1, runtime.Handler.LedgerReads);
        Assert.Empty(Directory.GetFiles(files.OwnerRoot, "*-accepted.json"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerLifecycleFirstInvocationClaimSurvivesUncertainWriteWithoutResend(bool acknowledgementLost)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            var started = Path.GetFileName(path) == "operation-000001-started.json";
            if (started && !acknowledgementLost) throw new IOException("Invocation claim not written.");
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (started) throw new IOException("Invocation claim durable; acknowledgement lost.");
        });
        var operation = await owner.AdmitLifecycleAsync(Id, CropTeacherSeedLifecycleRuntime.ExplicitCancellation, "claim_test",
            f.SourcePath, f.LedgerPath, files.Native);
        await Assert.ThrowsAsync<IOException>(() => Run(owner, operation));
        Assert.Equal(0, runtime.Handler.Cancellations);
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.False(Directory.Exists(files.Native));
        await owner.DisposeAsync();
        await using var restarted = await files.Open();
        var pending = restarted.Pending!;
        if (acknowledgementLost)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(restarted, pending));
            var missing = await Assert.ThrowsAnyAsync<IOException>(() => restarted.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post));
            Assert.True(missing is FileNotFoundException or DirectoryNotFoundException);
            Assert.Same(pending, restarted.Pending);
            Assert.Equal(0, runtime.Handler.Cancellations);
            Assert.Equal(0, runtime.Handler.Commits);
        }
        else
        {
            await Run(restarted, pending);
            Assert.Null(restarted.Pending);
            Assert.Equal(1, runtime.Handler.Cancellations);
            Assert.Equal(1, runtime.Handler.Commits);
        }
        Task Run(CropTeacherManagedSeedOwner current, CropTeacherManagedSeedOperation item) => current.RunLifecycleAsync(item, runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), files.Post);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("mode")]
    [InlineData("intent")]
    [InlineData("prepared")]
    public async Task OwnerLifecycleRejectsMalformedRetainedPhaseWithoutBackendRequests(string fault)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using (var owner = await files.Open())
            await owner.AdmitLifecycleAsync(Id, CropTeacherSeedLifecycleRuntime.ExplicitCancellation, "owner_bad_record",
                f.SourcePath, f.LedgerPath, files.Native);
        var path = Path.Combine(files.OwnerRoot, "operation-000001-intent.json");
        var record = JsonNode.Parse(File.ReadAllText(path))!;
        switch (fault)
        {
            case "kind": record["operation_kind"] = "seed"; break;
            case "mode": record["mode"] = "unknown"; break;
            case "intent": record["original_intent_sha256"] = new string('a', 64); break;
            case "prepared": File.WriteAllText(Path.Combine(files.OwnerRoot, "operation-000001-prepared.json"), "{}"); break;
        }
        File.WriteAllText(path, record.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => files.Open());
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Cancellations);
        Assert.Equal(0, runtime.Handler.LedgerReads);
    }

    [Fact]
    public async Task OwnerLifecycleAndSeedAdmissionCompeteForTheSameSinglePendingSlot()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        async Task<Exception?> Attempt(Func<Task<CropTeacherManagedSeedOperation>> call)
        {
            try { await call(); return null; }
            catch (Exception error) { return error; }
        }
        var results = await Task.WhenAll(
            Attempt(() => owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native)),
            Attempt(() => owner.AdmitLifecycleAsync(Id, CropTeacherSeedLifecycleRuntime.ExplicitCancellation, "concurrent_lifecycle",
                f.SourcePath, f.LedgerPath, files.Native + "-lifecycle")));
        Assert.Single(results, value => value is null);
        Assert.IsType<InvalidOperationException>(Assert.Single(results, value => value is not null));
        Assert.Single(Directory.GetFiles(files.OwnerRoot, "operation-*-intent.json"));
        Assert.Equal(0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Cancellations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerLifecycleRequiresTheExactAcceptedSourceContentButNotItsOriginalPath(bool changedBytes)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        await using var owner = await files.Open();
        var copy = f.SourcePath + ".copy.json";
        File.Copy(f.SourcePath, copy);
        if (changedBytes) File.AppendAllText(copy, "\n");
        // Both files remain valid original scope inputs; the owner binds the accepted physical source.
        _ = CropTeacherSeedLifecycleSupport.Open(copy, f.LedgerPath, Id, "fixture.copy.check");
        Task<CropTeacherManagedSeedOperation> Admit() => owner.AdmitLifecycleAsync(Id,
            CropTeacherSeedLifecycleRuntime.ExplicitCancellation, "copy_source", copy, f.LedgerPath, files.Native);
        if (changedBytes)
        {
            await Assert.ThrowsAsync<InvalidDataException>(Admit);
            Assert.Null(owner.Pending);
        }
        else Assert.True((await Admit()).IsLifecycle);
        Assert.Equal(0, runtime.Handler.Cancellations);
        Assert.Equal(0, runtime.Handler.Commits);
    }
}
