using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalTerminalOwnerUsesOriginalLoopAndRetainsOnlyActualDepositCursor(bool approach)
    {
        using var f = new TerminalOutcomeFixture(approach);
        await using var owner = await f.Shared.Files.Open();
        var operation = await AdmitTerminal(owner, f);
        Assert.True(operation.IsTerminalShipping); Assert.False(operation.IsSupportingRoute);
        using var transport = new OriginalRouteTransport(f.Shared, (f.After,
            JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(f.ReceiptPath), JsonOptions)!));
        using var client = new HttpClient(transport);
        await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherManagedOperationAsync", client, client,
            f.Shared.Runtime.LiveOptions, 1, owner, operation, f.QueuePath,
            (Func<Task<JsonObject>>)(() => throw new InvalidOperationException("Terminal cannot use generic fallback.")));
        Assert.Equal(1, transport.NativeRequests); Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
        Assert.InRange(transport.FullSnapshotReads, 1, 3);
        Assert.Equal(approach ? 0 : 1, owner.AcceptedTerminalDepositCount);
        Assert.Null(owner.Pending);
        var accepted = OwnedControlAccepted(f.Shared, operation);
        Assert.True(accepted["native_terminal_shipping_step_verified"]!.GetValue<bool>());
        Assert.False(accepted["terminal_settlement_verified"]!.GetValue<bool>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.Null(accepted["applied_tile_count"]); Assert.Null(accepted["route_target_reached"]);
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(owner.AcceptedTerminalDepositCount, reopened.AcceptedTerminalDepositCount);
        Assert.Equal(1, transport.NativeRequests); Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("admitted_restart")]
    [InlineData("native_ack_lost")]
    [InlineData("native_result_ack_lost")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    public async Task OriginalTerminalOwnerRecoversOneNativeInvocationAndOneMarker(string mode)
    {
        using var f = new TerminalOutcomeFixture();
        var loseWrite = true;
        await using var owner = await f.Shared.Files.Open(writer: async (path, json) =>
        {
            if (mode == "marker_reply_lost" && loseWrite && Path.GetFileName(path) == "marker-reply.json")
            { loseWrite = false; throw new IOException("Terminal marker committed before reply."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (loseWrite && (mode == "native_result_ack_lost" && Path.GetFileName(path) == "terminal-native-result.json" ||
                mode == "acceptance_ack_lost" && Path.GetFileName(path) == "operation-000001-accepted.json"))
            { loseWrite = false; throw new IOException("Terminal durable record acknowledgement lost."); }
        });
        var operation = await AdmitTerminal(owner, f);
        var calls = 0;
        Task<JsonObject> Native()
        {
            calls++;
            var result = EmitTerminal(f);
            if (mode == "native_ack_lost") throw new IOException("Terminal files retained before callback reply.");
            return Task.FromResult(result);
        }
        if (mode is "native_ack_lost" or "native_result_ack_lost")
            await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalTerminalAsync(operation, f.Shared.Runtime.Client, Native));
        else if (mode != "admitted_restart")
        {
            await owner.ExecuteOriginalTerminalAsync(operation, f.Shared.Runtime.Client, Native);
            await Assert.ThrowsAsync<IOException>(() => RebindTerminal(owner, operation, f));
        }
        Assert.Equal(0, owner.AcceptedTerminalDepositCount);
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        if (mode == "admitted_restart")
            await reopened.ExecuteOriginalTerminalAsync(reopened.Pending!, f.Shared.Runtime.Client, Native);
        if (mode == "acceptance_ack_lost") await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        else if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        else await RebindTerminal(reopened, reopened.Pending!, f);
        Assert.Null(reopened.Pending); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, reopened.AcceptedTerminalDepositCount);
        Assert.Equal(1, calls); Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(mode == "marker_reply_lost" ? 1 : 0, f.Shared.Runtime.Handler.Lookups);
    }

    [Fact]
    public async Task OriginalTerminalOwnerRejectsModelCursorAndDoesNotRepeatUnknownNative()
    {
        using var f = new TerminalOutcomeFixture();
        await using var owner = await f.Shared.Files.Open();
        var compiles = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalTerminalAsync(
            f.Target with { AcceptedDepositCount = 1 }, () => { compiles++; throw new InvalidOperationException("No compile."); }));
        Assert.Equal(0, compiles);
        var operation = await AdmitTerminal(owner, f);
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalTerminalAsync(operation, f.Shared.Runtime.Client, () =>
        { calls++; throw new IOException("Unknown terminal native result."); }));
        await owner.DisposeAsync();
        await using var reopened = await f.Shared.Files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ExecuteOriginalTerminalAsync(reopened.Pending!, f.Shared.Runtime.Client, () =>
        { calls++; throw new InvalidOperationException("No redispatch."); }));
        await Assert.ThrowsAnyAsync<IOException>(() => RebindTerminal(reopened, reopened.Pending!, f));
        Assert.Equal(1, calls); Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(0, reopened.AcceptedTerminalDepositCount); Assert.NotNull(reopened.Pending);
    }

    private static Task<CropTeacherManagedSeedOperation> AdmitTerminal(CropTeacherManagedSeedOwner owner, TerminalOutcomeFixture f) =>
        owner.CompileAndAdmitOriginalTerminalAsync(f.Target, () => Task.FromResult(new CropTeacherOriginalTerminalCompilation(
            f.ModelPath, f.QueuePath, f.Shared.ReceiptPath, f.Shared.AfterPath, f.Shared.Files.Native)));

    private static JsonObject EmitTerminal(TerminalOutcomeFixture f)
    {
        File.Copy(f.ReceiptPath, f.Shared.ReceiptPath, overwrite: false);
        File.Copy(f.AfterPath, f.Shared.AfterPath, overwrite: false);
        f.Shared.Runtime.Store.Snapshots[f.After.StateHash] = f.After;
        return new() { ["execution_path"] = f.Shared.ReceiptPath, ["after_snapshot_path"] = f.Shared.AfterPath };
    }

    private static Task RebindTerminal(CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation, TerminalOutcomeFixture f) =>
        owner.RunTerminalShippingAsync(operation, f.Shared.Runtime.Client, (diagnostic, request) =>
            InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions,
                diagnostic, request), f.Shared.Files.Post);
}
