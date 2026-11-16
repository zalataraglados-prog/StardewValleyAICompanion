using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("normal", true)]
    [InlineData("normal", false)]
    [InlineData("native_ack_lost", true)]
    [InlineData("marker_reply_lost", true)]
    public async Task SiteClearanceRecoveryReusesOneNativeInvocationAndOneMarker(string mode, bool product)
    {
        var f = new SiteClearanceNativeFixture(product); using var host = f.Files.F.Runtime;
        var files = f.Files.F.Files; var runtime = f.Files.F.Runtime;
        var lose = true;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (lose && mode == "marker_reply_lost" && Path.GetFileName(path) == "marker-reply.json")
            { lose = false; throw new IOException("Clearance marker committed before reply."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var operation = await owner.CompileAndAdmitOriginalClearanceAsync(() => Task.FromResult(f.OwnerCompilation));
        var calls = 0;
        async Task<JsonObject> Native()
        {
            calls++; await f.Build(); runtime.Store.Snapshots[f.After.StateHash] = f.After;
            if (mode == "native_ack_lost") throw new IOException("Original artifacts durable; acknowledgement lost.");
            return new JsonObject { ["execution_path"] = f.ReceiptPath, ["after_snapshot_path"] = f.AfterPath };
        }
        Task Rebind(CropTeacherManagedSeedOwner current, CropTeacherManagedSeedOperation pending) =>
            current.RunClearanceAsync(pending, runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), files.Post);
        if (mode == "native_ack_lost")
            await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalClearanceAsync(operation, runtime.Client, Native));
        else
        {
            await owner.ExecuteOriginalClearanceAsync(operation, runtime.Client, Native);
            if (mode == "normal") await Rebind(owner, operation);
            else await Assert.ThrowsAsync<IOException>(() => Rebind(owner, operation));
        }
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        if (mode == "native_ack_lost") await Rebind(reopened, reopened.Pending!);
        else if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post);
        else await reopened.RestoreHeadAsync(runtime.Client, files.Recovery, files.Post);
        Assert.Null(reopened.Pending); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, calls); Assert.Equal(1, runtime.Handler.Commits); Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal("site_clearance", accepted["operation_kind"]!.GetValue<string>());
        Assert.True(accepted["native_site_clearance_verified"]!.GetValue<bool>());
        Assert.Equal("native_engine", accepted["site_clearance_outcome_authority"]!.GetValue<string>());
        Assert.False(accepted["site_clearance_private_effects_predicted"]!.GetValue<bool>());
        Assert.False(accepted["site_clearance_resource_credit_authorized"]!.GetValue<bool>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
        var trace = await reopened.ReadProductionTraceAsync();
        Assert.Equal("site_clearance", Assert.Single(trace.Transitions).OperationKind);
        Assert.Contains(f.JournalPath + ".gz", trace.InputHashes.Keys); trace.ValidateInputsUnchanged();
    }
}
