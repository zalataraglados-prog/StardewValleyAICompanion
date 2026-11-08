using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("admitted_restart")]
    [InlineData("native_ack_lost")]
    [InlineData("native_result_ack_lost")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    public async Task OriginalRouteRestartsAcrossNativeAndMarkerBoundariesWithoutRedispatch(string mode)
    {
        using var f = new OwnedRouteFixture();
        var loseWrite = true;
        await using var owner = await f.Files.Open(writer: async (path, json) =>
        {
            if (mode == "marker_reply_lost" && loseWrite && Path.GetFileName(path) == "marker-reply.json")
            { loseWrite = false; throw new IOException("Original marker committed before local reply."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (loseWrite && (mode == "native_result_ack_lost" && Path.GetFileName(path) == "route-native-result.json" ||
                mode == "acceptance_ack_lost" && Path.GetFileName(path) == "operation-000001-accepted.json"))
            { loseWrite = false; throw new IOException("Original durable record acknowledgement lost."); }
        });
        var operation = await f.Prepare(owner);
        var nativeCalls = 0;
        async Task<JsonObject> Native()
        {
            nativeCalls++;
            var result = await f.EmitNative();
            if (mode == "native_ack_lost") throw new IOException("Original native files retained before callback acknowledgement.");
            return result;
        }
        if (mode is "native_ack_lost" or "native_result_ack_lost")
            await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, Native));
        else if (mode != "admitted_restart")
        {
            await owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, Native);
            await Assert.ThrowsAsync<IOException>(() => f.Rebind(owner, operation));
        }
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        if (mode == "admitted_restart")
            await reopened.ExecuteOriginalRouteAsync(reopened.Pending!, f.Runtime.Client, () => { nativeCalls++; return f.EmitNative(); });
        if (mode == "acceptance_ack_lost") await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else await f.Rebind(reopened, reopened.Pending!);
        Assert.Null(reopened.Pending);
        Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, nativeCalls);
        Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.Equal(mode == "marker_reply_lost" ? 1 : 0, f.Runtime.Handler.Lookups);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalRouteUnknownNativeClaimNeverBecomesZeroOrAutomaticRedispatch(bool claimAckLost)
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (claimAckLost && Path.GetFileName(path) == "route-native-claim.json")
                throw new IOException("Native invocation claim durable; acknowledgement lost before dispatch.");
        });
        var operation = await f.Prepare(owner);
        var nativeCalls = 0;
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, () =>
        { nativeCalls++; throw new IOException("Unknown original native boundary."); }));
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ExecuteOriginalRouteAsync(reopened.Pending!, f.Runtime.Client, () =>
        { nativeCalls++; throw new InvalidOperationException("Must not redispatch."); }));
        await Assert.ThrowsAnyAsync<IOException>(() => f.Rebind(reopened, reopened.Pending!));
        Assert.Equal(claimAckLost ? 0 : 1, nativeCalls);
        Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.NotNull(reopened.Pending);
        var claim = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.Native, "route-native-claim.json")))!;
        Assert.Null(claim["native_request_performed"]);
    }
}
