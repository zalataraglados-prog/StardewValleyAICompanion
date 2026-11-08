using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void SupportingRouteRetainsTheOriginalExactControlObservationAndSevenRejections()
    {
        using var f = new Fixture();
        _ = MoneyCropTeacherFixture.Create<object>("VerifyCropNativeControlRoutes", f.Before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SupportingRouteRebindPreservesActiveCropAndAllResourceOwners(bool sleepRoute)
    {
        using var f = new Fixture();
        var (transition, after) = CreateSupportingRoute(f, sleepRoute);
        var route = OpenRoute(f, transition);
        var request = route.RebindRequest;
        Assert.Empty(request.MaterialClaims);
        Assert.Empty(request.CurrencyClaims);
        Assert.Empty(request.ReleaseReservationIds);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Ledger, after, request, RecordedAt);
        var rebound = route.VerifyRebindReply(reply);
        Assert.Equal(after.StateHash, rebound.SourceStateHash);
        Assert.Equal(f.Ledger.Revision + 1, rebound.Revision);
        Assert.Equal(JsonSerializer.Serialize(f.Ledger.CropPlantingCommitments, JsonOptions),
            JsonSerializer.Serialize(rebound.CropPlantingCommitments, JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(f.Ledger.MaterialReservations, JsonOptions),
            JsonSerializer.Serialize(rebound.MaterialReservations, JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(f.Ledger.CurrencyReservations, JsonOptions),
            JsonSerializer.Serialize(rebound.CurrencyReservations, JsonOptions));
        Assert.Equal(StrategyCommitmentStatuses.Active, rebound.CropPlantingCommitments.Single().Status);
        Assert.False(route.NativeCropLifecycleVerified);
        Assert.False(route.FormalProductTrainingAuthorized);
        request.SourceDecisionId = "caller-mutation";
        rebound.CropPlantingCommitments.Single().Status = StrategyCommitmentStatuses.Cancelled;
        Assert.Equal("route.step.1", route.RebindRequest.SourceDecisionId);
        Assert.Equal(StrategyCommitmentStatuses.Active, route.VerifyRebindReply(reply).CropPlantingCommitments.Single().Status);
    }

    [Fact]
    public void SupportingRouteReboundSourceFeedsOriginalPurchaseScopeWithoutIdleOverride()
    {
        using var f = new Fixture();
        var (transition, after) = CreateSupportingRoute(f);
        var route = OpenRoute(f, transition);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Ledger, after, route.RebindRequest, RecordedAt);
        var path = RoutePath(f, "rebound-ledger.json");
        Fixture.Save(path, route.VerifyRebindReply(reply));
        var original = CropTeacherSeedPurchaseSupport.Open(f.RequestPath, transition.AfterSnapshotPath, path, Id, "next.purchase");
        Assert.Equal(after.StateHash, original.MarkerRequest.StateHash);
        Assert.Equal(reply.CommittedLedgerRevision, original.BaseLedger.Revision);
        Assert.Equal(1, original.MaximumPurchaseQuantity);
        Assert.Throws<InvalidDataException>(() => CropTeacherLedgerSourceBinder.BuildRefreshRequest(
            transition.AfterSnapshotPath, path, "do-not-relax-old-binder"));
    }

    [Fact]
    public void SupportingRouteRebindReplyRejectsDifferentFullLedger()
    {
        using var f = new Fixture();
        var (transition, after) = CreateSupportingRoute(f);
        var route = OpenRoute(f, transition);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Ledger, after, route.RebindRequest, RecordedAt);
        reply.Ledger!.CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount--;
        Assert.Throws<InvalidDataException>(() => route.VerifyRebindReply(reply));
    }

    private static CropTeacherSupportingRoute OpenRoute(Fixture f, MoneyRevenueCropNativeLifecycleTransitionRequest transition) =>
        CropTeacherSupportingRoute.Open(f.RequestPath, f.SourcePath, f.LedgerPath, new[] { Id }, transition, "route.step.1");

    private static string RoutePath(Fixture f, string name) => Path.Combine(Path.GetDirectoryName(f.RequestPath)!, "route-" + name);

    private static (MoneyRevenueCropNativeLifecycleTransitionRequest Transition, SnapshotEnvelope After) CreateSupportingRoute(
        Fixture f, bool sleepRoute = false)
    {
        var target = sleepRoute ? "FarmHouse" : "Farm";
        if (sleepRoute)
        {
            f.Before.State["current_location"] = JsonSerializer.SerializeToElement(new
            {
                home_context = new { status = "available", value = new { home_location_id = "FarmHouse" } }
            });
            f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State);
            f.Ledger.SourceStateHash = f.Before.StateHash;
            Fixture.Save(f.SourcePath, f.Before);
            Fixture.Save(f.LedgerPath, f.Ledger);
        }
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(f.Before, JsonOptions), JsonOptions)!;
        var player = JsonNode.Parse(after.State["player"].GetRawText())!;
        player["location_id"]!["value"] = target;
        player["tile_x"] = JsonSerializer.SerializeToNode(new { status = "available", value = 5 });
        player["tile_y"] = JsonSerializer.SerializeToNode(new { status = "available", value = 6 });
        after.State["player"] = JsonSerializer.SerializeToElement(player);
        var farm = JsonNode.Parse(after.State["farm"].GetRawText())!;
        farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["location_id"] = target;
        after.State["farm"] = JsonSerializer.SerializeToElement(farm);
        after.GameTick++;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var option = sleepRoute ? "executor.traverse_connector" : "executor.move_to_tile";
        var queue = MoneyCropTeacherFixture.Create<ActionQueueEnvelope>("LifecycleQueue", "route", option, f.Before, new[] { 5 }, target);
        var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt",
            queue, f.Before, after, "route.run", "route.candidate", option["executor.".Length..]);
        var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest(
            sleepRoute ? "supporting_route_to_sleep" : "supporting_route_to_crop",
            f.Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
            RoutePath(f, "queue.json"), f.SourcePath, RoutePath(f, "receipt.json"), RoutePath(f, "after.json"),
            receipt.RunId, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, receipt.SelectedCandidateId);
        Fixture.Save(transition.QueuePath, queue);
        Fixture.Save(transition.ExecutionReceiptPath, receipt);
        Fixture.Save(transition.AfterSnapshotPath, after);
        return (transition, after);
    }
}
