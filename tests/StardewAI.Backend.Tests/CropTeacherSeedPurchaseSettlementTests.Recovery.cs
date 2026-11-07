using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedRecoveryRebuildsOriginalPurchaseAndLostFinalReplyWithoutReplay(bool missingReply)
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        using var recovery = new CropTeacherSeedRecoveryTestSupport();
        var transition = f.Transition();
        runtime.LiveOptions.RunId = transition.RunId;
        var prepared = await runtime.Prepare(transition);
        runtime.ObserveAfter();
        if (missingReply) runtime.BeforeWrite = path => CropTeacherSeedRecoveryTestSupport.FailBeforeWrite(path, "acquisition-reply.json");
        if (missingReply) await Assert.ThrowsAsync<IOException>(() => prepared.CompleteAsync(transition));
        else await prepared.CompleteAsync(transition);
        var original = f.Open();
        var intent = original.AcquisitionIntent;
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.AcquireSeed, original.TotalDay, original.CommitmentId,
            "", "FarmHouse", intent.SeedQualifiedItemId, intent.ShopId, intent.MaxUnitPrice, 1, null, "owned_supply");
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step, original.MarkerRequest.PortfolioId);
        var before = runtime.Repository.Get(f.After);
        var recovered = await recovery.Recover(runtime.Client, runtime.LiveOptions, f.RuntimePath, scopes, step);
        CropTeacherSeedRecoveryTestSupport.AssertSameLedger(recovered, before);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(missingReply ? 1 : 0, runtime.Handler.Lookups);
        Assert.Equal(transition.AfterSnapshotPath, recovered.AfterSnapshotPath);
        AssertOtherRootsUnchanged(f.Ledger, recovered.ReadLedger());
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("reply")]
    [InlineData("request")]
    [InlineData("preparation")]
    [InlineData("before")]
    public async Task ManagedRecoveryRejectsDriftInsteadOfFallingBackToAnotherMutation(string fault)
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        using var recovery = new CropTeacherSeedRecoveryTestSupport();
        var transition = f.Transition();
        runtime.LiveOptions.RunId = transition.RunId;
        var prepared = await runtime.Prepare(transition);
        runtime.ObserveAfter();
        await prepared.CompleteAsync(transition);
        var original = f.Open();
        var intent = original.AcquisitionIntent;
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.AcquireSeed, original.TotalDay, original.CommitmentId,
            "", "FarmHouse", intent.SeedQualifiedItemId, intent.ShopId, intent.MaxUnitPrice, 1, null, "owned_supply");
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step, original.MarkerRequest.PortfolioId);
        if (fault == "before") File.AppendAllText(transition.BeforeSnapshotPath, "\n");
        if (fault == "reply")
        {
            var path = Path.Combine(f.RuntimePath, "marker-reply.json");
            var reply = JsonNode.Parse(File.ReadAllText(path))!;
            reply["ledger"]!["revision"] = 999;
            File.WriteAllText(path, reply.ToJsonString());
        }
        if (fault == "request")
        {
            var journal = runtime.Summary()["marker"]!["crop_teacher_ledger_commit_request_journal_path"]!.GetValue<string>();
            var request = JsonNode.Parse(File.ReadAllText(journal))!["request_file"]!.GetValue<string>();
            File.AppendAllText(Path.Combine(Path.GetDirectoryName(journal)!, request), "\n");
        }
        if (fault == "preparation")
        {
            var path = Path.Combine(f.RuntimePath, "preparation-summary.json");
            var summary = JsonNode.Parse(File.ReadAllText(path))!;
            summary["run_id"] = "another-run";
            File.WriteAllText(path, summary.ToJsonString());
        }
        var reads = runtime.Handler.LedgerReads;
        await Assert.ThrowsAsync<InvalidDataException>(() => recovery.Recover(runtime.Client, runtime.LiveOptions,
            f.RuntimePath, scopes, step, fault == "intent" ? new string('f', 64) : null));
        Assert.Equal(reads, runtime.Handler.LedgerReads);
        Assert.Equal(0, runtime.Handler.Lookups);
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        if (fault == "intent") Assert.False(Directory.Exists(recovery.Output));
    }
}
