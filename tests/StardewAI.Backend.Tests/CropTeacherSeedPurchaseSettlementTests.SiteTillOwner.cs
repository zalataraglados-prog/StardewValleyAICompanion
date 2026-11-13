using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SiteTillOwnerAdmissionReopensTheSamePreparedPhaseWithoutClaimOrDispatch()
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        await using var owner = await f.F.Files.Open();
        var operation = await owner.CompileAndAdmitOriginalTillingAsync(() => Task.FromResult(TillOwnerCompilation(f)));
        Assert.True(operation.IsSiteTilling); Assert.True(operation.IsSupportingTransition);
        Assert.False(operation.IsSupportingRoute); Assert.False(operation.IsWateringMaintenance);
        Assert.Throws<InvalidOperationException>(operation.ReadStep);
        Assert.False(File.Exists(Path.Combine(f.F.Files.Native, "till-native-claim.json")));
        await owner.DisposeAsync();
        await using var reopened = await f.F.Files.Open();
        Assert.True(reopened.Pending!.IsSiteTilling);
        Assert.Equal(operation.SupportStepId, reopened.Pending.SupportStepId);
        Assert.Equal(f.F.Source.SourcePath, reopened.CurrentSnapshotPath);
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("commitment")]
    [InlineData("observation")]
    [InlineData("original_model")]
    [InlineData("model")]
    [InlineData("queue")]
    [InlineData("output_input_alias")]
    [InlineData("outputs_alias")]
    public async Task SiteTillOwnerRejectsChangedCompilationBeforeNativeClaim(string fault)
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        await using var owner = await f.F.Files.Open();
        var value = TillOwnerCompilation(f);
        value = fault switch
        {
            "commitment" => value with { CommitmentId = "foreign" },
            "observation" => value with { ObservationPath = f.F.Source.SourcePath },
            "original_model" => value with { OriginalModelActionPath = f.Full },
            "model" => value with { ModelActionPath = f.Narrow },
            "queue" => value with { QueuePath = f.Narrow },
            "output_input_alias" => value with { ReceiptPath = f.Plan },
            "outputs_alias" => value with { AfterSnapshotPath = value.ReceiptPath },
            _ => throw new InvalidDataException("Unknown fixture fault.")
        };
        if (fault is "output_input_alias" or "outputs_alias")
            await Assert.ThrowsAsync<IOException>(() => owner.CompileAndAdmitOriginalTillingAsync(() => Task.FromResult(value)));
        else await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalTillingAsync(() => Task.FromResult(value)));
        Assert.Null(owner.Pending); Assert.Equal(0, f.F.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(f.F.Files.Native, "till-native-claim.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SiteTillOwnerUnknownClaimNeverRedispatchesAfterReopen(bool lostClaimAcknowledgement)
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        await using var owner = await f.F.Files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (lostClaimAcknowledgement && Path.GetFileName(path) == "till-native-claim.json")
                throw new IOException("Durable till claim acknowledgement lost before dispatch.");
        });
        var operation = await owner.CompileAndAdmitOriginalTillingAsync(() => Task.FromResult(TillOwnerCompilation(f)));
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalTillingAsync(operation, f.F.Runtime.Client, () =>
        { calls++; throw new IOException("Unknown native till boundary."); }));
        await owner.DisposeAsync();
        await using var reopened = await f.F.Files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ExecuteOriginalTillingAsync(reopened.Pending!, f.F.Runtime.Client, () =>
        { calls++; throw new InvalidOperationException("Must not redispatch."); }));
        Assert.Equal(lostClaimAcknowledgement ? 0 : 1, calls);
        Assert.Equal(0, f.F.Runtime.Handler.Commits); Assert.NotNull(reopened.Pending);
        var claim = JsonNode.Parse(File.ReadAllText(Path.Combine(f.F.Files.Native, "till-native-claim.json")))!;
        Assert.Null(claim["native_request_performed"]);
    }

    private static CropTeacherOriginalTillingCompilation TillOwnerCompilation(SiteTillFiles f) => new(Id, f.Observation,
        f.Request, f.Plan, f.Narrow, f.Full, f.Queue, f.F.ReceiptPath, f.F.AfterPath, f.F.Files.Native);
}
