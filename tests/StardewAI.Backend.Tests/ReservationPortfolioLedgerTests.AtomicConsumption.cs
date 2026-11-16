using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public async Task AtomicConsumptionLostHttpReplyRecoversWithoutAnotherConsumptionOrMarker()
    {
        var root = NewPortfolioReplyRoot();
        await using var app = PortfolioReplyApplication(root);
        using var setup = app.CreateClient();
        var store = app.Services.GetRequiredService<StateStore>();
        var snapshot = Snapshot(); store.Snapshots[snapshot.StateHash] = snapshot;
        await PostPortfolioAccepted(setup, "commit", Request(snapshot, 2, 300));
        var request = AtomicConsumption(snapshot);
        using var transport = new LostPortfolioReplyHandler(app.Server.CreateHandler(), "commit");
        using var client = new HttpClient(transport) { BaseAddress = new Uri("http://localhost") };
        await Assert.ThrowsAsync<HttpRequestException>(() => PostPortfolioAccepted(client, "commit", request));
        var path = Assert.Single(Directory.GetFiles(root)); var bytes = File.ReadAllBytes(path);
        using var lookup = await client.PostAsync(PortfolioEndpoint + "lookup-mutation", ReplyContent(new { operation = "commit", request }));
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var found = JsonNode.Parse(await lookup.Content.ReadAsStringAsync())!;
        Assert.Equal("found", found["status"]!.GetValue<string>());
        var ledger = found["reply"]!["ledger"]!.Deserialize<StrategyCommitmentLedger>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(2, ledger.Revision);
        Assert.Equal(1, Assert.Single(ledger.MaterialReservations).Quantity);
        Assert.Single(ledger.History.Where(row => row.Operation == "material_reservation_partially_consumed"));
        Assert.Equal(1, transport.MutationCalls); Assert.Equal(1, transport.LookupCalls);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ExplicitAtomicConsumptionReusesOriginalSettlementWithOnePublicRevision(int quantity)
    {
        var snapshot = Snapshot(); var service = new ReservationPortfolioLedgerService();
        var before = service.Commit(null, snapshot, Request(snapshot, quantity, 300), "2026-11-15T00:00:00Z").Ledger!;
        var original = JsonSerializer.Serialize(before);
        var request = AtomicConsumption(snapshot);
        var result = service.Commit(before, snapshot, request, "2026-11-15T00:01:00Z");
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        Assert.Equal(2, result.CommittedLedgerRevision);
        Assert.Equal(2, result.Ledger!.Revision);
        var claim = Assert.Single(result.Ledger.MaterialReservations);
        Assert.Equal(quantity == 1 ? StrategyCommitmentStatuses.Completed : StrategyCommitmentStatuses.Active, claim.Status);
        Assert.Equal(quantity == 1 ? 1 : quantity - 1, claim.Quantity);
        Assert.Equal(2, claim.Revision);
        Assert.Equal(original, JsonSerializer.Serialize(before));
        Assert.Equal(JsonSerializer.Serialize(before.CurrencyReservations), JsonSerializer.Serialize(result.Ledger.CurrencyReservations));
        var history = result.Ledger.History.Skip(before.History.Length).ToArray();
        Assert.Equal(3, history.Length);
        Assert.All(history, row => Assert.Equal(2, row.LedgerRevision));
        Assert.Contains(history, row => row.Operation == "reservation_portfolio_commit" && row.CommitmentId == request.PortfolioId);
        Assert.Contains(history, row => row.Operation == "reservation_portfolio_supporting_transition_complete" &&
            row.SourceDecisionId == "route:a" && row.Reason == request.MaterialConsumption!.SupportingTransitionReceiptSha256);
        Assert.Contains(history, row => row.Operation == (quantity == 1 ? "material_reservation_consumed" : "material_reservation_partially_consumed"));
        var repeated = service.Commit(result.Ledger, snapshot, request, "2026-11-15T00:02:00Z");
        Assert.False(repeated.Accepted);
        Assert.Same(result.Ledger, repeated.Ledger);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("receipt")]
    [InlineData("source")]
    [InlineData("goal")]
    [InlineData("quantity")]
    [InlineData("slot")]
    [InlineData("claim")]
    [InlineData("null")]
    [InlineData("marker")]
    [InlineData("mixed_claim")]
    [InlineData("mixed_release")]
    public void ExplicitAtomicConsumptionRejectsBeforePublishingItsMarkerOrMutatingAnyClaim(string fault)
    {
        var snapshot = Snapshot(); var service = new ReservationPortfolioLedgerService();
        var before = service.Commit(null, snapshot, Request(snapshot, 1, 300), "2026-11-15T00:00:00Z").Ledger!;
        var original = JsonSerializer.Serialize(before); var request = AtomicConsumption(snapshot);
        var value = request.MaterialConsumption!;
        if (fault == "version") value.SchemaVersion += ".unknown";
        if (fault == "receipt") value.SupportingTransitionReceiptSha256 = "missing";
        if (fault == "source") value.RouteSourceDecisionId = "another-owner";
        if (fault == "goal") request.GoalId = "another-goal";
        if (fault == "quantity") value.Consumption.ConsumedQuantity = 2;
        if (fault == "slot") value.Consumption.SlotIndex++;
        if (fault == "claim") value.Consumption.MaterialReservationId = "missing-claim";
        if (fault == "null") value.Consumption = null!;
        if (fault == "marker") request.SourceDecisionId = "not-this-marker";
        if (fault == "mixed_claim") request.MaterialClaims = Request(snapshot, 1, 0, 1).MaterialClaims;
        if (fault == "mixed_release") request.ReleaseReservationIds = ["route-a-money"];
        var result = service.Commit(before, snapshot, request, "2026-11-15T00:01:00Z");
        Assert.False(result.Accepted); Assert.Same(before, result.Ledger);
        Assert.Equal(original, JsonSerializer.Serialize(before));
    }

    [Fact]
    public void OldPortfolioRequestsDoNotSerializeOrExecuteNewConsumption()
    {
        var snapshot = Snapshot(); var request = Request(snapshot, 2, 300);
        Assert.DoesNotContain("material_consumption", JsonSerializer.Serialize(request));
        var result = new ReservationPortfolioLedgerService().Commit(null, snapshot, request, "2026-11-15T00:00:00Z");
        Assert.True(result.Accepted);
        Assert.DoesNotContain(result.Ledger!.History, row => row.Operation.Contains("consumed", StringComparison.Ordinal));
    }

    private static ReservationPortfolioCommitRequest AtomicConsumption(SnapshotEnvelope snapshot) => new()
    {
        StateHash = snapshot.StateHash, ExpectedLedgerRevision = 1, PortfolioId = "support:atomic-consumption",
        SourceDecisionId = "support:atomic-consumption", GoalId = "goal.grandpa_21",
        MaterialConsumption = new()
        {
            RouteSourceDecisionId = "route:a", ConsumerOptionId = "executor.ship_inventory_item_to_bin",
            SupportingTransitionReceiptSha256 = new string('b', 64), Reason = "verified_native_terminal_consumption",
            Consumption = new() { MaterialReservationId = "route-a-material", NodeId = "player:123", SlotIndex = 0,
                QualifiedItemId = "(O)388", ConsumedQuantity = 1 }
        }
    };
}
