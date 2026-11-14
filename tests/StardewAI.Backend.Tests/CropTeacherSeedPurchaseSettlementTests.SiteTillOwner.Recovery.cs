using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("normal", false)]
    [InlineData("normal", true)]
    [InlineData("admitted_restart", false)]
    [InlineData("admitted_restart", true)]
    [InlineData("native_ack_lost", false)]
    [InlineData("native_ack_lost", true)]
    [InlineData("native_result_ack_lost", false)]
    [InlineData("native_result_ack_lost", true)]
    [InlineData("marker_reply_lost", false)]
    [InlineData("marker_reply_lost", true)]
    [InlineData("acceptance_ack_lost", false)]
    [InlineData("acceptance_ack_lost", true)]
    public async Task SiteTillOwnerReusesOriginalNativeAndMarkerRecoveryWithoutDoubleExecutionOrProductionCredit(string mode, bool productTransport)
    {
        var f = new SiteTillNativeFixture(productTransport: productTransport); using var host = f.Files.F.Runtime;
        var files = f.Files.F.Files; var runtime = f.Files.F.Runtime;
        var lose = true;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (lose && mode == "marker_reply_lost" && Path.GetFileName(path) == "marker-reply.json")
            { lose = false; throw new IOException("Till marker committed before local reply."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (lose && (mode == "native_result_ack_lost" && Path.GetFileName(path) == "till-native-result.json" ||
                mode == "acceptance_ack_lost" && Path.GetFileName(path) == "operation-000001-accepted.json"))
            { lose = false; throw new IOException("Till durable result/acceptance acknowledgement lost."); }
        });
        var compilation = TillOwnerCompilation(f.Files) with { ReceiptPath = f.ReceiptPath, AfterSnapshotPath = f.AfterPath };
        var operation = await owner.CompileAndAdmitOriginalTillingAsync(() => Task.FromResult(compilation));
        var calls = 0;
        async Task<JsonObject> Native()
        {
            calls++; await f.Build(); runtime.Store.Snapshots[f.After.StateHash] = f.After;
            if (mode == "native_ack_lost") throw new IOException("Till original native artifacts durable, acknowledgement lost.");
            return new JsonObject { ["execution_path"] = f.ReceiptPath, ["after_snapshot_path"] = f.AfterPath };
        }
        Task Rebind(CropTeacherManagedSeedOwner current, CropTeacherManagedSeedOperation pending) =>
            current.RunTillingAsync(pending, runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request), files.Post);
        if (mode is "native_ack_lost" or "native_result_ack_lost")
            await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalTillingAsync(operation, runtime.Client, Native));
        else if (mode != "admitted_restart")
        {
            await owner.ExecuteOriginalTillingAsync(operation, runtime.Client, Native);
            if (mode == "normal") await Rebind(owner, operation);
            else await Assert.ThrowsAsync<IOException>(() => Rebind(owner, operation));
        }
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        if (mode == "admitted_restart") await reopened.ExecuteOriginalTillingAsync(reopened.Pending!, runtime.Client, Native);
        if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post);
        else if (mode is "acceptance_ack_lost" or "normal") await reopened.RestoreHeadAsync(runtime.Client, files.Recovery, files.Post);
        else await Rebind(reopened, reopened.Pending!);
        Assert.Null(reopened.Pending); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, calls); Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-accepted.json")))!.AsObject();
        Assert.True(accepted["native_site_till_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["site_till_tile_x"]!.GetValue<int>()); Assert.Equal(0, accepted["site_till_tile_y"]!.GetValue<int>());
        Assert.Equal(0, accepted["site_till_observed_pickup_units"]!.GetValue<int>());
        Assert.False(accepted["site_till_resource_credit_authorized"]!.GetValue<bool>());
        Assert.False(accepted["site_till_producer_provenance_verified"]!.GetValue<bool>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
        var trace = await reopened.ReadProductionTraceAsync();
        Assert.Equal("site_till", Assert.Single(trace.Transitions).OperationKind);
        Assert.Contains(f.JournalPath + ".gz", trace.InputHashes.Keys); trace.ValidateInputsUnchanged();
        if (mode == "normal")
        {
            foreach (var fault in new[] { "site_till_tile_x", "site_till_observed_pickup_units", "site_till_resource_credit_authorized",
                         "site_till_producer_provenance_verified", "route_target_reached", "native_site_approach_verified" })
            {
                var forged = accepted.DeepClone().AsObject();
                forged[fault] = fault is "site_till_tile_x" or "site_till_observed_pickup_units" ? JsonValue.Create(99) : JsonValue.Create(true);
                var method = typeof(CropTeacherManagedSeedOwner).GetMethod("ValidateTillingAcceptance", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(reopened, new object[] { operation, forged }));
                Assert.IsType<InvalidDataException>(error.InnerException);
            }
        }
        await reopened.DisposeAsync();
        await using var restored = await files.Open();
        await restored.RestoreHeadAsync(runtime.Client, files.Recovery + "-second-readback", files.Post);
        Assert.Equal(1, calls); Assert.Equal(1, runtime.Handler.Commits); Assert.Equal(f.AfterPath, restored.CurrentSnapshotPath);
    }
}
