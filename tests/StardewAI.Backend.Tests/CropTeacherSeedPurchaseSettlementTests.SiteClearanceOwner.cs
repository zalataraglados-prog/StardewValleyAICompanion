using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("clear")]
    [InlineData("access")]
    public async Task SiteClearanceOwnerAdmissionReopensTheSamePreparedPhaseWithoutClaimOrDispatch(string frontier)
    {
        var f = new SiteTillFiles(frontier: frontier); using var host = f.F.Runtime;
        await using var owner = await f.F.Files.Open();
        var operation = await owner.CompileAndAdmitOriginalClearanceAsync(() => Task.FromResult(ClearanceOwnerCompilation(f)));
        Assert.True(operation.IsSiteClearance); Assert.True(operation.IsSupportingTransition);
        Assert.False(operation.IsSiteTilling); Assert.False(operation.IsSupportingRoute); Assert.False(operation.IsWateringMaintenance);
        Assert.Throws<InvalidOperationException>(operation.ReadStep);
        Assert.False(File.Exists(Path.Combine(f.F.Files.Native, "clearance-native-claim.json")));
        Assert.False(File.Exists(Path.Combine(f.F.Files.Native, "till-native-claim.json")));
        Assert.False(File.Exists(Path.Combine(f.F.Files.Native, "route-native-claim.json")));
        await owner.DisposeAsync();
        await using var reopened = await f.F.Files.Open();
        Assert.True(reopened.Pending!.IsSiteClearance); Assert.False(reopened.Pending.IsSiteTilling);
        Assert.Equal(operation.SupportStepId, reopened.Pending.SupportStepId);
        Assert.Equal(f.F.Source.SourcePath, reopened.CurrentSnapshotPath);
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("commitment")]
    [InlineData("observation")]
    [InlineData("request")]
    [InlineData("plan")]
    [InlineData("original_model")]
    [InlineData("model")]
    [InlineData("queue")]
    [InlineData("output_input_alias")]
    [InlineData("outputs_alias")]
    public async Task SiteClearanceOwnerRejectsChangedCompilationBeforeNativeClaim(string fault)
    {
        var f = new SiteTillFiles(frontier: "clear"); using var host = f.F.Runtime;
        await using var owner = await f.F.Files.Open();
        var value = ClearanceOwnerCompilation(f);
        value = fault switch
        {
            "commitment" => value with { CommitmentId = "foreign" },
            "observation" => value with { ObservationPath = f.F.Source.SourcePath },
            "request" => value with { SiteRequestPath = f.Plan },
            "plan" => value with { SitePlanPath = f.Request },
            "original_model" => value with { OriginalModelActionPath = f.Full },
            "model" => value with { ModelActionPath = f.Narrow },
            "queue" => value with { QueuePath = f.Narrow },
            "output_input_alias" => value with { ReceiptPath = f.Plan },
            "outputs_alias" => value with { AfterSnapshotPath = value.ReceiptPath },
            _ => throw new InvalidDataException("Unknown fixture fault.")
        };
        if (fault is "output_input_alias" or "outputs_alias")
            await Assert.ThrowsAsync<IOException>(() => owner.CompileAndAdmitOriginalClearanceAsync(() => Task.FromResult(value)));
        else await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalClearanceAsync(() => Task.FromResult(value)));
        Assert.Null(owner.Pending); Assert.Equal(0, f.F.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(f.F.Files.Native, "clearance-native-claim.json")));
    }

    [Fact]
    public async Task SiteClearanceOwnerRejectsTillFirstFrontier()
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        await using var owner = await f.F.Files.Open();
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalClearanceAsync(() => Task.FromResult(ClearanceOwnerCompilation(f))));
        Assert.Null(owner.Pending); Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SiteClearanceOwnerUnknownClaimNeverRedispatchesAfterReopen(bool lostClaimAcknowledgement)
    {
        var f = new SiteTillFiles(frontier: "clear"); using var host = f.F.Runtime;
        await using var owner = await f.F.Files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (lostClaimAcknowledgement && Path.GetFileName(path) == "clearance-native-claim.json")
                throw new IOException("Durable clearance claim acknowledgement lost before dispatch.");
        });
        var operation = await owner.CompileAndAdmitOriginalClearanceAsync(() => Task.FromResult(ClearanceOwnerCompilation(f)));
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalClearanceAsync(operation, f.F.Runtime.Client, () =>
        { calls++; throw new IOException("Unknown native clearance boundary."); }));
        await owner.DisposeAsync();
        await using var reopened = await f.F.Files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ExecuteOriginalClearanceAsync(reopened.Pending!, f.F.Runtime.Client, () =>
        { calls++; throw new InvalidOperationException("Must not redispatch."); }));
        Assert.Equal(lostClaimAcknowledgement ? 0 : 1, calls);
        Assert.Equal(0, f.F.Runtime.Handler.Commits); Assert.NotNull(reopened.Pending);
        var claim = JsonNode.Parse(File.ReadAllText(Path.Combine(f.F.Files.Native, "clearance-native-claim.json")))!;
        Assert.Equal("crop_teacher_owned_clearance_native_claim.v1", claim["schema_version"]!.GetValue<string>());
        Assert.Null(claim["native_request_performed"]);
        Assert.False(File.Exists(Path.Combine(f.F.Files.Native, "till-native-claim.json")));
    }

    private static CropTeacherOriginalClearanceCompilation ClearanceOwnerCompilation(SiteTillFiles f) => new(Id, f.Observation,
        f.Request, f.Plan, f.Narrow, f.Full, f.Queue, f.F.ReceiptPath, f.F.AfterPath, f.F.Files.Native);
}
