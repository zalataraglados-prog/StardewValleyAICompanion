using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    private sealed record CustodyControlAttempt(CustodyTransferFixture Fixture, SnapshotEnvelope Before, SnapshotEnvelope After,
        StrategyCommitmentLedger SourceLedger, MuseumOutputCustodyTransferPreparation Preparation,
        ReservationPortfolioCommitResult Commit, MuseumOutputCustodyTransferCompilation Compilation, ActionQueueItem Transfer)
    {
        internal QueueExecutionStepReceipt Receipt()
        {
            var receipt = CustodyReceipt(Transfer, Before, After);
            receipt.QueueId = Compilation.Queue!.QueueId;
            return receipt;
        }
    }

    private static CustodyControlAttempt PrepareCustodyControl(CustodyTransferFixture? fixture = null, bool withdraw = false)
    {
        fixture ??= new CustodyTransferFixture();
        var before = fixture.Snapshot;
        var sourceLedger = fixture.Ledger;
        var destination = withdraw ? "player:123" : CustodyTransferFixture.Chest;
        var original = JsonSerializer.Serialize(sourceLedger);
        var built = MuseumOutputCustodyTransferRequestBuilder.Build(before, sourceLedger, fixture.CustodyId, destination);
        Assert.Empty(built.Errors);
        Assert.NotNull(built.Request);
        Assert.NotNull(built.Candidate);
        Assert.Equal(original, JsonSerializer.Serialize(sourceLedger));
        Assert.Equal(JsonSerializer.Serialize(built), JsonSerializer.Serialize(
            MuseumOutputCustodyTransferRequestBuilder.Build(before, sourceLedger, fixture.CustodyId, destination)));
        var committed = fixture.Repository?.CommitReservationPortfolio(before, built.Request!) ??
            fixture.Service.Commit(sourceLedger, before, built.Request!, "control-commit");
        Assert.True(committed.Accepted, string.Join(",", committed.Errors));
        fixture.Ledger = committed.Ledger!;
        var compilation = MuseumOutputCustodyTransferRequestBuilder.Compile(before, sourceLedger, fixture.CustodyId, destination, committed);
        Assert.Empty(compilation.Errors);
        Assert.NotNull(compilation.Plan);
        Assert.NotNull(compilation.Queue);
        var transfer = Assert.Single(compilation.Queue.Items, row => row.OptionId == "executor.transfer_material");
        return new(fixture, before, fixture.AfterTransfer(withdraw), sourceLedger, built, committed, compilation, transfer);
    }
}
