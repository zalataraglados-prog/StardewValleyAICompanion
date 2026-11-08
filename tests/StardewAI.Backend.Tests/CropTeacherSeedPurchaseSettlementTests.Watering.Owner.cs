using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("live", false, false)]
    [InlineData("live", true, false)]
    [InlineData("live", false, true)]
    [InlineData("admitted_restart", false, false)]
    [InlineData("lost_reply", true, true)]
    [InlineData("lost_acceptance", false, true)]
    public async Task WateringOwnerUsesSameMarkerRecoveryAndKeepsPrefixDistinctFromRouteAndFullDay(string mode, bool partial, bool mirrors)
    {
        using var f = new WateringFixture(bowlMirrors: mirrors);
        await f.Emit(applied: partial ? 1 : 2, failedSuffix: partial);
        var files = f.Shared.Files;
        var runtime = f.Shared.Runtime;
        var loseWrite = true;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (mode == "lost_reply" && loseWrite && Path.GetFileName(path) == "marker-reply.json")
            { loseWrite = false; throw new IOException("Original maintenance marker committed; local reply not retained."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (mode == "lost_acceptance" && loseWrite && Path.GetFileName(path) == "operation-000001-accepted.json")
            { loseWrite = false; throw new IOException("Maintenance acceptance durable; acknowledgement lost."); }
        });
        var operation = await owner.AdmitWateringMaintenanceAsync(Id, f.Transition, files.Native);
        Assert.True(operation.IsWateringMaintenance);
        Assert.True(operation.IsSupportingTransition);
        Assert.False(operation.IsSupportingRoute);
        Assert.Throws<InvalidOperationException>(() => operation.ReadStep());
        if (mode == "live") await RunWateringOwner(f, owner, operation);
        else if (mode != "admitted_restart") await Assert.ThrowsAsync<IOException>(() => RunWateringOwner(f, owner, operation));
        await owner.DisposeAsync();
        await using var restarted = await files.Open();
        if (mode == "admitted_restart") await RunWateringOwner(f, restarted, restarted.Pending!);
        else if (mode == "lost_reply") await restarted.RecoverPendingAsync(runtime.Client, files.Recovery, files.Post);
        else await restarted.RestoreHeadAsync(runtime.Client, files.Recovery, files.Post);
        Assert.Null(restarted.Pending);
        Assert.False(restarted.HeadRecoveryRequired);
        Assert.Equal(f.Transition.AfterSnapshotPath, restarted.CurrentSnapshotPath);
        var final = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(restarted.CurrentLedgerPath), JsonOptions)!;
        AssertOtherRootsUnchanged(f.Shared.Source.Ledger, final);
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.Equal(mode == "lost_reply" ? 1 : 0, runtime.Handler.Lookups);
        var acceptance = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.Equal("watering_maintenance", acceptance["operation_kind"]!.GetValue<string>());
        Assert.Equal(partial ? 1 : 2, acceptance["applied_tile_count"]!.GetValue<int>());
        Assert.Equal(!partial, acceptance["complete_batch"]!.GetValue<bool>());
        Assert.False(acceptance["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(acceptance["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.Null(acceptance["route_target_reached"]);
        Assert.True(File.Exists(Path.Combine(files.Native, "maintenance-intent.json")));
        Assert.False(File.Exists(Path.Combine(files.Native, "route-intent.json")));
    }

    private static Task RunWateringOwner(WateringFixture f, CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>>? commit = null) =>
        owner.RunWateringMaintenanceAsync(operation, f.Shared.Runtime.Client, commit ?? ((diagnostic, request) =>
            InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Shared.Runtime.Client,
                f.Shared.Runtime.LiveOptions, diagnostic, request)), f.Shared.Files.Post);

    [Fact]
    public async Task WateringOwnerContinuesOriginalOwnedRouteOnSameSequenceAndRestoresTheMixedHead()
    {
        using var f = new WateringFixture(bowlMirrors: true);
        await f.Emit();
        await using var owner = await f.Shared.Files.Open();
        var watering = await owner.AdmitWateringMaintenanceAsync(Id, f.Transition, f.Shared.Files.Native);
        await RunWateringOwner(f, owner, watering);
        var waterHead = owner.CurrentSnapshotPath;
        var route = await f.Shared.Prepare(owner, 2);
        Assert.Equal(2, route.Sequence);
        var nativeCalls = 0;
        await owner.ExecuteOriginalRouteAsync(route, f.Shared.Runtime.Client, () =>
        { nativeCalls++; return f.Shared.EmitNative(2, waterHead); });
        await f.Shared.Rebind(owner, route);
        var last = owner.CurrentSnapshotPath;
        await owner.DisposeAsync();
        await using var restarted = await f.Shared.Files.Open();
        await restarted.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(last, restarted.CurrentSnapshotPath);
        Assert.False(restarted.HeadRecoveryRequired);
        Assert.Equal(1, nativeCalls);
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
        var accepted = Directory.GetFiles(f.Shared.Files.OwnerRoot, "operation-*-accepted.json").OrderBy(path => path).ToArray();
        Assert.Equal(2, accepted.Length);
        Assert.Equal("watering_maintenance", JsonNode.Parse(File.ReadAllText(accepted[0]))!["operation_kind"]!.GetValue<string>());
        var second = JsonNode.Parse(File.ReadAllText(accepted[1]))!;
        Assert.Equal("supporting_route", second["operation_kind"]!.GetValue<string>());
        Assert.Null(second["applied_tile_count"]);
        Assert.True(second["route_target_reached"]!.GetValue<bool>());
    }
}
