using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("admitted_restart")]
    [InlineData("native_ack_lost")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    public async Task SiteApproachOwnerReusesOriginalRecoveryWithoutRedispatchOrGeneralArrivalCredit(string mode)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f); var lose = true;
        await using var owner = await f.Files.Open(writer: async (path, json) =>
        {
            if (lose && mode == "marker_reply_lost" && Path.GetFileName(path) == "marker-reply.json")
            { lose = false; throw new IOException("Committed site marker reply lost."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (lose && mode == "acceptance_ack_lost" && Path.GetFileName(path) == "operation-000001-accepted.json")
            { lose = false; throw new IOException("Site acceptance durable; acknowledgement lost."); }
        });
        var operation = await AdmitSiteOwner(f, files, owner);
        var calls = 0;
        Task<JsonObject> Native()
        {
            calls++;
            var (after, receipt) = SiteOwnerNativeResult(f, files.Queue, new(2, 0));
            Fixture.Save(f.AfterPath, after); Fixture.Save(f.ReceiptPath, receipt);
            f.Runtime.Store.Snapshots[after.StateHash] = after;
            if (mode == "native_ack_lost") throw new IOException("Site native files durable before acknowledgement.");
            return Task.FromResult(new JsonObject { ["execution_path"] = f.ReceiptPath, ["after_snapshot_path"] = f.AfterPath });
        }
        if (mode == "native_ack_lost")
            await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, Native));
        else if (mode != "admitted_restart")
        {
            await owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client, Native);
            await Assert.ThrowsAsync<IOException>(() => f.Rebind(owner, operation));
        }
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        if (mode == "admitted_restart") await reopened.ExecuteOriginalRouteAsync(reopened.Pending!, f.Runtime.Client, Native);
        if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else if (mode == "acceptance_ack_lost") await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else await f.Rebind(reopened, reopened.Pending!);
        Assert.Null(reopened.Pending); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, calls); Assert.Equal(1, f.Runtime.Handler.Commits);
        AssertSiteOwnerAcceptance(f, new(2, 0));
        await reopened.DisposeAsync();
        await using var restored = await f.Files.Open();
        await restored.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery + "-second-readback", f.Files.Post);
        Assert.Equal(f.AfterPath, restored.CurrentSnapshotPath); Assert.Equal(1, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("commitment")]
    [InlineData("observation")]
    public async Task SiteApproachOwnerRejectsWrongAdmissionBeforeNativeClaim(string change)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f);
        await using var owner = await f.Files.Open();
        var compilation = SiteOwnerCompilation(f, files) with
        {
            Kind = change == "role" ? "supporting_route_home" : "supporting_route_to_crop",
            SiteApproachCommitmentId = change == "commitment" ? "foreign" : Id,
            SiteApproachObservationPath = change == "observation" ? string.Empty : files.Observation
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalRouteAsync(() => Task.FromResult(compilation)));
        Assert.Null(owner.Pending); Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(f.Files.Native, "route-native-claim.json")));
    }

    private static CropTeacherOriginalRouteCompilation SiteOwnerCompilation(OwnedRouteFixture f, SitePreparationFiles files) =>
        new("supporting_route_to_crop", files.Model, files.QueuePath, f.ReceiptPath, f.AfterPath, f.Files.Native)
        { SiteApproachObservationPath = files.Observation, SiteApproachCommitmentId = Id };

    private static Task<CropTeacherManagedSeedOperation> AdmitSiteOwner(OwnedRouteFixture f, SitePreparationFiles files,
        CropTeacherManagedSeedOwner owner) => owner.CompileAndAdmitOriginalRouteAsync(() => Task.FromResult(SiteOwnerCompilation(f, files)));

    private static (SnapshotEnvelope After, QueueExecutionReceiptEnvelope Receipt) SiteOwnerNativeResult(OwnedRouteFixture f,
        ActionQueueEnvelope queue, StardewAI.GoalConditionedBootstrap.MoneyRevenueCropPlantingTile target)
    {
        var after = ReadSiteSnapshot(f.Source.SourcePath);
        ChangeState(after, "player", player => { player["tile_x"]!["value"] = target.TileX; player["tile_y"]!["value"] = target.TileY; });
        after.GameTick++; after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var candidate = queue.Items.Single().NormalizedCommand.Parameters.Single(parameter => parameter.Name == "precondition" &&
            parameter.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Value["candidate_id:".Length..];
        return (after, MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue,
            f.Source.Before, after, f.Files.Options.RunId, candidate, "move_to_tile"));
    }

    private static void AssertSiteOwnerAcceptance(OwnedRouteFixture f, StardewAI.GoalConditionedBootstrap.MoneyRevenueCropPlantingTile target)
    {
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.True(accepted["native_site_approach_verified"]!.GetValue<bool>());
        Assert.False(accepted["route_target_reached"]!.GetValue<bool>());
        Assert.Equal(target.TileX, accepted["site_approach_tile_x"]!.GetValue<int>());
        Assert.Equal(target.TileY, accepted["site_approach_tile_y"]!.GetValue<int>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
    }
}
