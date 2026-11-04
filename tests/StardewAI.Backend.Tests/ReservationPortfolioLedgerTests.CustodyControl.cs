using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustodyControlUsesOriginalCandidateCompilerDispatchAndAtomicSettlementForBothDirections(bool persisted)
    {
        var root = persisted ? NewPortfolioReplyRoot() : null;
        var fixture = new CustodyTransferFixture(root is not null ? new FileStrategyCommitmentRepository(root) : null);
        foreach (var withdraw in new[] { false, true })
        {
            var attempt = PrepareCustodyControl(fixture, withdraw);
            var queue = attempt.Compilation.Queue!;
            Assert.Equal("deterministic_output_custody_control", attempt.Compilation.Plan!.SourceModel);
            Assert.Equal("pending", queue.Status);
            Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, attempt.Transfer, fixture.Ledger, attempt.Before.StateHash).Ready);
            Assert.True(MuseumOutputCustodyTransferBinding.IsBound(attempt.Transfer, fixture.Ledger));
            Assert.DoesNotContain(attempt.Transfer.NormalizedCommand.Parameters, row => row.Name.StartsWith("acquisition_", StringComparison.Ordinal));
            Assert.Equal(JsonSerializer.Serialize(attempt.Compilation), JsonSerializer.Serialize(
                MuseumOutputCustodyTransferRequestBuilder.Compile(attempt.Before, attempt.SourceLedger, fixture.CustodyId,
                    withdraw ? "player:123" : CustodyTransferFixture.Chest, attempt.Commit)));
            var built = MuseumOutputCustodyTransferSettlementRequestBuilder.Build(queue, attempt.Transfer, attempt.Receipt(),
                attempt.Before, attempt.After, fixture.Ledger);
            Assert.Empty(built.Errors);
            Assert.NotNull(built.Request);
            Assert.Empty(built.Request.MaterialConsumptions);
            var settled = fixture.Repository?.SettleReservationPortfolioSupportingTransition(attempt.After, built.Request) ??
                fixture.Service.SettleSupportingTransition(fixture.Ledger, attempt.After, built.Request, "control-settled");
            Assert.True(settled.Accepted, string.Join(",", settled.Errors));
            Assert.Empty(MuseumOutputCustodyTransferSettlementRequestBuilder.Verify(queue, attempt.Transfer, attempt.Receipt(),
                attempt.Before, attempt.After, fixture.Ledger, settled));
            if (!persisted && !withdraw) ExportCustodyControlIfRequested(attempt, built.Request, settled);
            fixture.Ledger = settled.Ledger!;
            fixture.Snapshot = attempt.After;
            Assert.Null(fixture.Ledger.PreparedMaterialTransfer);
            var custody = fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId);
            Assert.Equal(withdraw ? "player:123" : CustodyTransferFixture.Chest, custody.NodeId);
            Assert.True(MaterialOutputCustodyContract.TryGetPositionBinding(custody, fixture.Ledger, out _));
            Assert.Equal(withdraw, MaterialOutputCustodyContract.IsAuthorizedForConsumption(custody, fixture.Ledger,
                MaterialOutputCustodyContract.MuseumConsumer));
            Assert.All(fixture.Ledger.MaterialReservations.Where(row => row.ReservationId is "other-goal-stone" or "other-goal-spare"),
                row => Assert.Equal(StrategyCommitmentStatuses.Active, row.Status));
            if (persisted)
            {
                fixture.Repository = new FileStrategyCommitmentRepository(root!);
            }
        }
        var museum = fixture.AtMuseum();
        var item = CustodyDonationItem(new CustodyFixture(museum, fixture.Ledger, fixture.Service));
        var after = AfterCustodyDonation(museum);
        var donation = MuseumDonationCustodySettlementRequestBuilder.Build(item, CustodyReceipt(item, museum, after), museum, after, fixture.Ledger);
        Assert.Empty(donation.Errors);
        var consumed = fixture.Repository?.SettleReservationPortfolioSupportingTransition(after, donation.Request!) ??
            fixture.Service.SettleSupportingTransition(fixture.Ledger, after, donation.Request!, "donated");
        Assert.True(consumed.Accepted, string.Join(",", consumed.Errors));
        Assert.Equal(StrategyCommitmentStatuses.Completed, consumed.Ledger!.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId).Status);
    }
}
