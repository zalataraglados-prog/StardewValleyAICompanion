using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceRefreshSurvivesHttpDispatchSettlementAndRetainedRecovery(bool loseReplies)
    {
        using var f = new RuntimeFixture(loseReplies);
        using var recovery = new CropTeacherSeedRecoveryTestSupport();
        var previousHash = f.Files.Before.StateHash;
        f.Files.AdvanceSourceWithMarkerOnly();
        Assert.NotEqual(previousHash, f.Files.Before.StateHash);
        f.Store.Snapshots[f.Files.Before.StateHash] = f.Files.Before;
        var priorClaims = f.Files.Ledger.MaterialReservations.Where(row =>
            row.Status == StrategyCommitmentStatuses.Active && row.NodeId == "player:123")
            .ToDictionary(row => row.ReservationId);
        Assert.NotEmpty(priorClaims);
        Assert.All(priorClaims.Values, row => Assert.NotEqual(f.Files.Before.StateHash, row.SourceStateHash));
        var outside = JsonSerializer.Serialize(f.Files.Ledger.MaterialReservations.Single(row => row.ReservationId == "outside"), JsonOptions);

        const int consumed = 3;
        var runtime = await f.Prepare(consumed);
        Assert.Equal("pending", f.Queue.Status);
        Assert.Empty(f.Queue.CompilerDiagnostics);
        runtime.ValidateDispatchQueue(f.Transition.QueuePath);
        Assert.All(priorClaims.Values, prior =>
        {
            var current = f.Marker.Ledger.MaterialReservations.Single(row => row.ReservationId == prior.ReservationId);
            Assert.Equal(f.Files.Before.StateHash, current.SourceStateHash);
            Assert.Equal(prior.Revision + 1, current.Revision);
            Assert.Equal(prior.SourceDecisionId, current.SourceDecisionId);
            Assert.Equal(prior.GoalId, current.GoalId);
            Assert.Equal(prior.NodeId, current.NodeId);
            Assert.Equal(prior.SlotIndex, current.SlotIndex);
            Assert.Equal(prior.QualifiedItemId, current.QualifiedItemId);
            Assert.Equal(prior.Quantity, current.Quantity);
            Assert.Equal(prior.Purpose, current.Purpose);
            Assert.Equal(prior.Status, current.Status);
        });
        Assert.Equal(outside, JsonSerializer.Serialize(f.Marker.Ledger.MaterialReservations.Single(row => row.ReservationId == "outside"), JsonOptions));

        f.Options.MaxQueueItemAttempts = consumed;
        var receipt = await f.Execute(runtime);
        Assert.Equal(consumed, f.NativeRequests);
        Assert.Equal(consumed, receipt["executed_item_count"]!.GetValue<int>());
        Assert.False(receipt["success"]!.GetValue<bool>());
        Assert.All(receipt["step_results"]!.AsArray(), row => Assert.True(row!["owned_seed_prefix_verified"]!.GetValue<bool>()));
        var final = await runtime.CompleteAsync(f.Files.QueuePath, receipt["execution_path"]!.GetValue<string>(),
            receipt["after_snapshot_path"]!.GetValue<string>(), f.Transition.SelectedCandidateId);
        Assert.Equal(f.Files.Ledger.Revision + 2, final.Ledger.Revision);
        Assert.Equal(9 - consumed, final.Ledger.MaterialReservations.Single(row => row.ReservationId == f.Files.OwnedSlotOne).Quantity);
        Assert.Equal(StrategyCommitmentStatuses.Active, final.Ledger.CropPlantingCommitments.Single().Status);
        Assert.Equal(outside, JsonSerializer.Serialize(final.Ledger.MaterialReservations.Single(row => row.ReservationId == "outside"), JsonOptions));
        Assert.True(f.Summary()["resource_settlement_verified"]!.GetValue<bool>());
        Assert.False(f.Summary()["formal_product_training_authorized"]!.GetValue<bool>());
        Assert.Null(f.Summary()["acquisition"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.CompleteAsync(f.Files.QueuePath,
            receipt["execution_path"]!.GetValue<string>(), receipt["after_snapshot_path"]!.GetValue<string>(),
            f.Transition.SelectedCandidateId));
        var step = RecoveryStep(f);
        var scopes = CropTeacherManagedSeedScopes.Open(f.Files.RequestPath, f.Files.SourcePath, f.Files.LedgerPath,
            step, f.Files.Open().MarkerRequest.PortfolioId);
        var recovered = await recovery.Recover(f.Client, f.Options, f.Output, scopes, step);
        CropTeacherSeedRecoveryTestSupport.AssertSameLedger(recovered, final.Ledger);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(1, f.Handler.Settlements);
        Assert.Equal(loseReplies ? 2 : 0, f.Handler.Lookups);
        Assert.Equal(consumed, f.NativeRequests);
    }
}
