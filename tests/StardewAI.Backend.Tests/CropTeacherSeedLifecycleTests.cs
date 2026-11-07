using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalClosureAndPortfolioReleasePreserveOtherRootsAndAllNativeState(bool completion)
    {
        using var f = new Fixture(due: completion);
        var scope = f.Open();
        var originalNative = JsonSerializer.Serialize(f.Snapshot, JsonOptions);
        var originalLedger = JsonSerializer.Serialize(scope.BaseLedger, JsonOptions);
        var cancel = scope.BeginCancellation("explicit_plan_cancelled");
        var release = completion
            ? scope.VerifyCompletionReadback(f.Repository.Get(f.Snapshot))
            : cancel.VerifyReply(f.Repository.Cancel(f.Snapshot, f.Id, cancel.Request));
        Assert.True(release.RequiresRelease);
        Assert.Equal(new[] { f.CashId, "owned-seeds" }.Order(), release.Request.ReleaseReservationIds);
        var closed = release.ClosedLedger;
        var final = release.VerifyReply(f.Repository.CommitReservationPortfolio(f.Snapshot, release.Request));
        Assert.Equal(completion ? "completed" : "cancelled", final.CropPlantingCommitments.Single().Status);
        Assert.All(final.MaterialReservations.Where(row => row.Purpose == "crop_seed_supply:" + f.Id), row => Assert.Equal("cancelled", row.Status));
        Assert.Equal("cancelled", final.CurrencyReservations.Single().Status);
        Assert.Equal(JsonSerializer.Serialize(closed.MaterialReservations.Where(row => row.SourceDecisionId == "other-root"), JsonOptions),
            JsonSerializer.Serialize(final.MaterialReservations.Where(row => row.SourceDecisionId == "other-root"), JsonOptions));
        var durable = new FileStrategyCommitmentRepository(f.RepositoryPath).Get(f.Snapshot);
        Assert.Equal(JsonSerializer.Serialize(final, JsonOptions), JsonSerializer.Serialize(release.VerifyReadback(durable), JsonOptions));
        Assert.Equal(originalNative, JsonSerializer.Serialize(f.Snapshot, JsonOptions));
        Assert.Equal(originalLedger, JsonSerializer.Serialize(scope.BaseLedger, JsonOptions));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoActiveClaimsIsAReadOnlyNoOpNotAnEmptyMarker(bool completion)
    {
        using var f = new Fixture(due: completion, noResources: true);
        var scope = f.Open();
        var cancel = scope.BeginCancellation("explicit_noop_cleanup");
        var release = completion ? scope.VerifyCompletionReadback(f.Repository.Get(f.Snapshot))
            : cancel.VerifyReply(f.Repository.Cancel(f.Snapshot, f.Id, cancel.Request));
        Assert.False(release.RequiresRelease);
        Assert.Throws<InvalidDataException>(() => release.Request);
        var prior = f.Repository.Get(f.Snapshot);
        Assert.Equal(prior.Revision, release.VerifyReadback(f.Repository.Get(f.Snapshot)).Revision);
        Assert.DoesNotContain(prior.History, row => row.CommitmentId == f.Step);
    }

    [Fact]
    public void LostCancelReplyRecoversFromExactOriginalDurableReadbackWithoutResending()
    {
        using var f = new Fixture();
        var cancellation = f.Open().BeginCancellation("explicit_lost_reply");
        Assert.True(f.Repository.Cancel(f.Snapshot, f.Id, cancellation.Request).Accepted);
        var readback = new FileStrategyCommitmentRepository(f.RepositoryPath).Get(f.Snapshot);
        var release = cancellation.ResumeFromReadback(readback);
        Assert.Equal(readback.Revision, f.Repository.Get(f.Snapshot).Revision);
        Assert.Single(readback.History.Where(row => row.CommitmentId == f.Id && row.Operation == "cancel"));
        Assert.Equal(readback.Revision, release.Request.ExpectedLedgerRevision);
    }

    [Fact]
    public void CompletionCannotBeInferredFromHavingSeedsOrAStillGrowingCrop()
    {
        using var f = new Fixture();
        Assert.Throws<InvalidDataException>(() => f.Open().VerifyCompletionReadback(f.Repository.Get(f.Snapshot)));
        Assert.Equal("active", f.Repository.Get(f.Snapshot).CropPlantingCommitments.Single().Status);
    }

    [Fact]
    public void ExtractedReadReconcilerKeepsOriginalRepositoryResultAndDoesNotMutateInput()
    {
        using var f = new Fixture(due: true);
        var before = JsonSerializer.Serialize(f.Ledger, JsonOptions);
        var actual = f.Repository.Get(f.Snapshot);
        var expected = StrategyCommitmentReadReconciler.Reconcile(f.Ledger, f.Snapshot, actual.UpdatedAt);
        Assert.Equal(JsonSerializer.Serialize(expected, JsonOptions), JsonSerializer.Serialize(actual, JsonOptions));
        Assert.Equal(before, JsonSerializer.Serialize(f.Ledger, JsonOptions));
    }

    [Fact]
    public void LostCancellationReplyAllowsOnlyOriginalConcurrentCalendarReconciliation()
    {
        using var f = new Fixture(otherExpiresDuringCancel: true);
        var scope = f.Open();
        var cancel = scope.BeginCancellation("explicit_cancel_before_readback");
        Assert.True(f.Repository.Cancel(f.Snapshot, f.Id, cancel.Request).Accepted);
        var restarted = new FileStrategyCommitmentRepository(f.RepositoryPath);
        var observed = restarted.Get(f.Snapshot);
        Assert.Equal("completed", observed.CropPlantingCommitments.Single(row => row.CommitmentId == "crop:other").Status);
        var release = cancel.ResumeFromReadback(observed);
        release.VerifyReply(restarted.CommitReservationPortfolio(f.Snapshot, release.Request));
        var final = release.VerifyReadback(restarted.Get(f.Snapshot));
        Assert.Equal("cancelled", final.CropPlantingCommitments.Single(row => row.CommitmentId == f.Id).Status);
        Assert.Equal("completed", final.CropPlantingCommitments.Single(row => row.CommitmentId == "crop:other").Status);
        Assert.All(final.MaterialReservations.Where(row => row.SourceDecisionId == "other-root"), row => Assert.Equal("active", row.Status));
    }

    [Fact]
    public void LostReleaseReplyUsesOriginalPortfolioLookupBeforeReconcilingReadback()
    {
        using var f = new Fixture();
        var cancel = f.Open().BeginCancellation("explicit_cancel_before_release");
        var release = cancel.VerifyReply(f.Repository.Cancel(f.Snapshot, f.Id, cancel.Request));
        var request = release.Request;
        var original = f.Repository.CommitReservationPortfolio(f.Snapshot, request);
        Assert.True(original.Accepted);
        var restarted = new FileStrategyCommitmentRepository(f.RepositoryPath);
        var lookup = restarted.LookupPortfolioMutationReply(f.Snapshot, "commit", JsonSerializer.SerializeToElement(request, JsonOptions));
        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        release.VerifyReply(lookup["reply"]!.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)!);
        var final = release.VerifyReadback(restarted.Get(f.Snapshot));
        Assert.Equal(original.Ledger!.Revision, final.Revision);
        Assert.Single(final.History.Where(row => row.CommitmentId == f.Step));
    }
}
