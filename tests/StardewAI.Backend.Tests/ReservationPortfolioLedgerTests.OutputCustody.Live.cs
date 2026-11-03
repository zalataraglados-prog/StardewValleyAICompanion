using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualLiveMuseumSettlementUsesTheSharedServiceAndFailsClosed(bool reject)
    {
        var fixture = MintMuseumCustody(protectSpare: true);
        var item = CustodyDonationItem(fixture);
        var after = AfterCustodyDonation(fixture.Snapshot);
        using var handler = new MuseumCustodyHandler(fixture, after, reject);
        using var http = new HttpClient(handler);
        var execution = JsonSerializer.SerializeToNode(CustodyReceipt(item, fixture.Snapshot, after))!.AsObject();
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("SettleMuseumOutputCustodyAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        await (Task)method.Invoke(null, new object[]
        {
            http, new LiveTrainingOptions
            {
                BackendUrl = "http://custody-fixture.invalid", Root = NewPortfolioReplyRoot(), RunId = "isolated-custody-test"
            },
            JsonSerializer.SerializeToNode(item)!.AsObject(), execution,
            JsonSerializer.SerializeToNode(fixture.Snapshot)!.AsObject(), JsonSerializer.SerializeToNode(after)!.AsObject()
        })!;
        Assert.Equal(1, handler.PostCount);
        Assert.Equal(reject ? "failed" : "completed", execution["material_reservation_settlement_status"]!.GetValue<string>());
        Assert.Equal(reject ? StrategyCommitmentStatuses.Active : StrategyCommitmentStatuses.Completed,
            handler.Ledger.MaterialReservations.Single(row => row.OutputCustody is not null).Status);
        Assert.Equal(StrategyCommitmentStatuses.Active, handler.Ledger.MaterialReservations.Single(row => row.ReservationId == "other-goal-spare").Status);
        if (reject)
        {
            Assert.True(execution["reservation_settlement_retry_required"]!.GetValue<bool>());
            Assert.Equal("reservation_settlement_failed", execution["primitive_verification_status"]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalMuseumOutputClaimsRequireVerifiedGainAndSafeStock(bool verified)
    {
        var snapshot = MuseumCustodySnapshot();
        var ledger = new ReservationPortfolioLedgerService().Commit(null, snapshot, Request(snapshot, 20, 300), "initial").Ledger!;
        var binding = new AcquisitionRouteExecutionBinding
        {
            RequirementSetId = "museum_collection", GoalId = "goal.grandpa_21", RouteOccurrenceId = "native-route",
            QualifiedItemId = "(O)96", RequiredAmount = 1, MinimumQuality = 0, DispatchBindingReady = true
        };
        var fresh = new AcquisitionRouteFreshTerminalReceiptAdmission
        {
            GoalId = binding.GoalId, RouteOccurrenceId = binding.RouteOccurrenceId, FreshTerminalReceiptVerified = verified,
            TerminalTransition = new() { QualifiedItemId = binding.QualifiedItemId, RequiredAmount = 1, Resolved = true, Verified = verified, QuantityIncrease = 1 }
        };
        var method = typeof(AcquisitionRoutePortfolioSettlementBuilder).GetMethod("BuildOutputCustodyClaims", BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = new object[] { binding, fresh, snapshot, ledger, Array.Empty<string>() };
        if (!verified)
        {
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, args)).InnerException);
            return;
        }
        var claim = Assert.Single((ReservationPortfolioOutputCustodyClaim[])method.Invoke(null, args)!);
        Assert.Equal("player:123", claim.NodeId);
        Assert.Equal(1, claim.SlotIndex);
        Assert.Equal("(O)96", claim.QualifiedItemId);
        Assert.Equal(1, claim.Quantity);
        binding.RequirementSetId = "full_shipment";
        Assert.Null(method.Invoke(null, args));
    }

    private sealed class MuseumCustodyHandler(CustodyFixture fixture, SnapshotEnvelope after, bool reject) : HttpMessageHandler
    {
        public StrategyCommitmentLedger Ledger { get; private set; } = fixture.Ledger;
        public int PostCount { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                Assert.Contains("/strategy/commitments/latest?stateHash=", request.RequestUri!.AbsoluteUri);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Ledger) };
            }
            Assert.EndsWith("/reservation-portfolios/settle-supporting-transition", request.RequestUri!.AbsolutePath);
            PostCount++;
            var body = await request.Content!.ReadFromJsonAsync<ReservationPortfolioSupportingTransitionSettlementRequest>(cancellationToken: cancellationToken);
            Assert.Equal(MaterialOutputCustodyContract.MuseumConsumer, body!.ConsumerOptionId);
            var result = reject ? new ReservationPortfolioSupportingTransitionSettlementResult { Accepted = false } :
                fixture.Service.SettleSupportingTransition(Ledger, after, body, "live-fixture-native-donation");
            if (result.Accepted) Ledger = result.Ledger!;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
        }
    }
}
