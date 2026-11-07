using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Theory]
    [InlineData(8, false, false)]
    [InlineData(8, false, true)]
    [InlineData(3, false, false)]
    [InlineData(3, true, true)]
    public async Task OriginalTransactionSettlesOnlyObservedNativePrefixAndRecoversLostMutationReplies(int consumed, bool failedSuffix, bool loseReplies)
    {
        using var f = new RuntimeFixture(loseReplies);
        var runtime = await f.Prepare(consumed, failedSuffix);
        runtime.ValidateDispatchQueue(f.Transition.QueuePath);
        await f.Observe(runtime);
        var final = await runtime.CompleteAsync(f.Transition);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(1, f.Handler.Settlements);
        Assert.Equal(loseReplies ? 2 : 0, f.Handler.Lookups);
        Assert.Equal(f.Files.Ledger.Revision + 2, final.Ledger.Revision);
        var claim = final.Ledger.MaterialReservations.Single(row => row.ReservationId == f.Files.OwnedSlotOne);
        Assert.Equal(9 - consumed, claim.Quantity);
        Assert.Equal(StrategyCommitmentStatuses.Active, claim.Status);
        Assert.Equal(StrategyCommitmentStatuses.Active, final.Ledger.CropPlantingCommitments.Single().Status);
        Assert.Equal(JsonSerializer.Serialize(f.Marker.Ledger.MaterialReservations.Single(row => row.ReservationId == "outside"), JsonOptions),
            JsonSerializer.Serialize(final.Ledger.MaterialReservations.Single(row => row.ReservationId == "outside"), JsonOptions));
        Assert.True(f.Summary()["resource_settlement_verified"]!.GetValue<bool>());
        Assert.False(f.Summary()["formal_product_training_authorized"]!.GetValue<bool>());
        Assert.Null(f.Summary()["acquisition"]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.CompleteAsync(f.Transition));
        Assert.Equal(1, f.Handler.Settlements);
    }

    [Theory]
    [InlineData("marker_reply")]
    [InlineData("marker_readback")]
    public async Task WholeLedgerDriftStopsBeforeCompiledDispatch(string fault)
    {
        using var f = new RuntimeFixture(fault: fault);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Prepare());
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(0, f.Handler.Settlements);
        Assert.Equal(0, f.NativeRequests);
    }

    [Fact]
    public async Task CompletionCannotSkipRealObservedPrefixEvenWithValidStandaloneProof()
    {
        using var f = new RuntimeFixture();
        var runtime = await f.Prepare();
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.CompleteAsync(f.Transition));
        Assert.Equal(0, f.Handler.Settlements);
        await f.Observe(runtime);
        await runtime.CompleteAsync(f.Transition);
        Assert.Equal(1, f.Handler.Settlements);
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task SameOriginalLoopDispatchesFreshOwnedSlotsAndObservesBeforeEarlyStop(int consumed, bool failedSuffix)
    {
        using var f = new RuntimeFixture();
        var runtime = await f.Prepare(consumed, failedSuffix);
        f.Options.MaxQueueItemAttempts = failedSuffix ? 8 : consumed;
        var receipt = await f.Execute(runtime);
        var attempted = consumed + (failedSuffix ? 1 : 0);
        Assert.Equal(attempted, f.NativeRequests);
        Assert.Equal(attempted, f.FullSnapshotReads);
        Assert.Equal(attempted, receipt["executed_item_count"]!.GetValue<int>());
        Assert.Equal(attempted, receipt["step_results"]!.AsArray().Count);
        Assert.Equal(consumed == 8, receipt["success"]!.GetValue<bool>());
        Assert.All(receipt["step_results"]!.AsArray(), row => Assert.True(row!["owned_seed_prefix_verified"]!.GetValue<bool>(), row.ToJsonString()));
        var final = await runtime.CompleteAsync(f.Files.QueuePath, receipt["execution_path"]!.GetValue<string>(),
            receipt["after_snapshot_path"]!.GetValue<string>(), f.Transition.SelectedCandidateId);
        Assert.Equal(9 - consumed, final.Ledger.MaterialReservations.Single(row => row.ReservationId == f.Files.OwnedSlotOne).Quantity);
        Assert.Equal(1, f.Handler.Settlements);
    }

    [Fact]
    public async Task MissingPrefixOwnerProducesZeroNativeAttemptsAndHonestUnchangedPreparation()
    {
        using var f = new RuntimeFixture();
        var runtime = await f.Prepare();
        var receipt = await f.Execute(runtime, attachOwner: false);
        Assert.Equal(0, f.NativeRequests);
        Assert.Empty(receipt["step_results"]!.AsArray());
        Assert.Equal(0, receipt["executed_item_count"]!.GetValue<int>());
        Assert.False(receipt["success"]!.GetValue<bool>());
        Assert.NotNull(receipt["owned_seed_controller_stop"]);
        var final = await runtime.CompleteAsync(f.Files.QueuePath, receipt["execution_path"]!.GetValue<string>(),
            receipt["after_snapshot_path"]!.GetValue<string>(), f.Transition.SelectedCandidateId);
        Assert.Equal(f.Marker.Ledger.Revision, final.Ledger.Revision);
        Assert.Equal(0, f.Handler.Settlements);
        Assert.False(f.Summary()["support_slice_verified"]!.GetValue<bool>());
        Assert.False(f.Summary()["resource_settlement_verified"]!.GetValue<bool>());
        Assert.Equal("crop_seed_support_prepared_no_native_dispatch", f.Summary()["status"]!.GetValue<string>());
    }
}
