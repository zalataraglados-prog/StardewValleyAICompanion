using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SupportingRouteOwnerCannotEnterAnySeedPreparationCallback()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = CreateOwnedSupportingRoute(f, runtime, files);
        await using var owner = await files.Open();
        var route = await owner.AdmitSupportingRouteAsync(transition, files.Native);
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.PrepareSeedAsync<CropTeacherSeedPurchaseRuntime>(route, () =>
        { calls++; throw new InvalidOperationException("Wrong callback must not run."); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteSeedAsync(route, _ =>
        { calls++; return Task.FromResult(0); }));
        Assert.Equal(0, calls);
        Assert.False(File.Exists(Path.Combine(files.OwnerRoot, "operation-000001-started.json")));
    }

    [Theory]
    [InlineData("foreign_run")]
    [InlineData("foreign_executor")]
    [InlineData("resource_drift")]
    [InlineData("bad_reply")]
    [InlineData("missing_journal")]
    public async Task SupportingRouteOwnerRejectsDriftWithoutNativeReplayOrSecondMutation(string change)
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = CreateOwnedSupportingRoute(f, runtime, files);
        await using var owner = await files.Open();
        if (change is "foreign_run" or "foreign_executor")
        {
            transition = change == "foreign_run" ? transition with { RunId = "foreign-run" } :
                transition with { ExecutorVersion = "foreign-executor" };
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitSupportingRouteAsync(transition, files.Native));
            Assert.Null(owner.Pending);
            return;
        }
        var operation = await owner.AdmitSupportingRouteAsync(transition, files.Native);
        if (change == "resource_drift")
            Assert.True(runtime.Repository.CommitReservationPortfolio(f.Before, new ReservationPortfolioCommitRequest
            {
                StateHash = f.Before.StateHash, ExpectedLedgerRevision = f.Ledger.Revision,
                PortfolioId = "other-new-step", SourceDecisionId = "other-new-step", GoalId = "earn_money"
            }).Accepted);
        var error = await Record.ExceptionAsync(() => owner.RunSupportingRouteAsync(operation, runtime.Client, async (diagnostic, request) =>
        {
            if (change == "missing_journal")
                return JsonSerializer.SerializeToNode(new StardewAI.Core.Strategy.ReservationPortfolioLedgerService().Commit(
                    f.Ledger, runtime.Store.Snapshots[request.StateHash], request, RecordedAt), JsonOptions)!.AsObject();
            var reply = await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", runtime.Client, runtime.LiveOptions, diagnostic, request);
            if (change == "bad_reply") reply["ledger"]!["currency_reservations"]![0]!["amount"] = 999;
            return reply;
        }, files.Post));
        if (change == "missing_journal") Assert.IsAssignableFrom<IOException>(error);
        else Assert.IsType<InvalidDataException>(error);
        Assert.Same(operation, owner.Pending);
        Assert.Equal(change == "bad_reply" ? 1 : 0, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Lookups);
        Assert.Empty(Directory.GetFiles(files.OwnerRoot, "operation-*-accepted.json"));
    }
}
