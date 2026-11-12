using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false, "admitted_restart")]
    [InlineData(false, "native_ack_lost")]
    [InlineData(false, "save_ack_lost")]
    [InlineData(false, "native_result_ack_lost")]
    [InlineData(false, "marker_reply_lost")]
    [InlineData(false, "acceptance_ack_lost")]
    [InlineData(true, "admitted_restart")]
    [InlineData(true, "native_ack_lost")]
    [InlineData(true, "native_result_ack_lost")]
    [InlineData(true, "marker_reply_lost")]
    [InlineData(true, "acceptance_ack_lost")]
    public async Task OriginalOvernightAndFollowingHarvestRecoverTheirOwnBoundariesWithoutRedispatch(bool harvest, string mode)
    {
        using var f = new OvernightFixture();
        var armed = false;
        var loseWrite = true;
        var acceptedName = harvest ? "operation-000005-accepted.json" : "operation-000003-accepted.json";
        var resultName = harvest ? "harvest-native-result.json" : "overnight-native-result.json";
        await using var owner = await f.Open(async (path, json) =>
        {
            var name = Path.GetFileName(path);
            if (armed && loseWrite && mode == "marker_reply_lost" && name == "marker-reply.json")
            { loseWrite = false; throw new IOException("Current phase marker committed before local reply."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (armed && loseWrite && (mode == "native_result_ack_lost" && name == resultName ||
                mode == "save_ack_lost" && name == "overnight-save-boundary.json" || mode == "acceptance_ack_lost" && name == acceptedName))
            { loseWrite = false; throw new IOException("Current phase durable acknowledgement lost."); }
        });
        await f.Home(owner);
        if (harvest)
        {
            var night = await f.PrepareNight(owner);
            await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
            await f.RebindNight(owner, night);
            await f.Farm(owner);
        }
        armed = true;
        var operation = harvest ? await f.PrepareHarvest(owner) : await f.PrepareNight(owner);
        async Task<JsonObject> Native()
        {
            var result = harvest ? await f.EmitHarvest(owner) : await f.EmitNight(owner);
            if (mode == "native_ack_lost") throw new IOException("Native files retained before acknowledgement.");
            return result;
        }
        Task Execute(CropTeacherManagedSeedOwner target, CropTeacherManagedSeedOperation pending, Func<Task<JsonObject>> native) => harvest
            ? target.ExecuteOriginalHarvestAsync(pending, f.Shared.Runtime.Client, native)
            : target.ExecuteOriginalOvernightAsync(pending, f.Shared.Runtime.Client, native);
        Task Rebind(CropTeacherManagedSeedOwner target, CropTeacherManagedSeedOperation pending) => harvest
            ? f.RebindHarvest(target, pending) : f.RebindNight(target, pending);
        if (mode is "native_ack_lost" or "native_result_ack_lost" or "save_ack_lost")
            await Assert.ThrowsAsync<IOException>(() => Execute(owner, operation, Native));
        else if (mode != "admitted_restart")
        {
            await Execute(owner, operation, Native);
            await Assert.ThrowsAsync<IOException>(() => Rebind(owner, operation));
        }
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        if (mode == "admitted_restart")
            await Execute(reopened, reopened.Pending!, () => harvest ? f.EmitHarvest(reopened) : f.EmitNight(reopened));
        else if (mode != "acceptance_ack_lost")
            await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(reopened, reopened.Pending!, () => throw new InvalidOperationException("Never redispatch.")));
        if (mode == "acceptance_ack_lost") await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        else if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        else await Rebind(reopened, reopened.Pending!);
        Assert.Null(reopened.Pending); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, f.NativeNightCalls); Assert.Equal(harvest ? 1 : 0, f.NativeHarvestCalls);
        Assert.Equal(harvest ? 5 : 3, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(mode == "marker_reply_lost" ? 1 : 0, f.Shared.Runtime.Handler.Lookups);
        Assert.Equal(harvest ? f.HarvestAfter : f.NightAfter, reopened.CurrentSnapshotPath);
    }

    [Fact]
    public async Task OriginalOvernightIncompleteSaveCanOnlyRecoverReadOnlyUntilItsOriginalSaveChanges()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var before = owner.CurrentSnapshotPath;
        var night = await f.PrepareNight(owner);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner, save: false)));
        Assert.Contains("save is not complete", error.Message);
        Assert.Equal(before, owner.CurrentSnapshotPath); Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ExecuteOriginalOvernightAsync(reopened.Pending!, f.Shared.Runtime.Client, () => f.EmitNight(reopened)));
        await Assert.ThrowsAsync<InvalidDataException>(() => f.RebindNight(reopened, reopened.Pending!));
        f.CompleteSave();
        await f.RebindNight(reopened, reopened.Pending!);
        Assert.Equal(f.NightAfter, reopened.CurrentSnapshotPath); Assert.Null(reopened.Pending);
        Assert.Equal(1, f.NativeNightCalls); Assert.Equal(3, f.Shared.Runtime.Handler.Commits);
        File.AppendAllText(f.NativeSaveFile, "-later-original-native-save");
        await reopened.DisposeAsync();
        await using var historical = await f.Open();
        await historical.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(f.NightAfter, historical.CurrentSnapshotPath); Assert.Equal(3, f.Shared.Runtime.Handler.Commits);
    }
}
