using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleTests
{
    [Theory]
    [InlineData("pending_transfer")]
    [InlineData("stale_source")]
    [InlineData("seed_owner")]
    [InlineData("seed_item")]
    [InlineData("cash_owner")]
    [InlineData("cash_purpose")]
    [InlineData("used_step")]
    public void ReleaseScopeRejectsUnsettledOrForeignAllocationsBeforeAnyMutation(string fault)
    {
        using var f = new Fixture();
        var ledger = Fixture.Copy(f.Ledger);
        switch (fault)
        {
            case "pending_transfer": ledger.PreparedMaterialTransfer = new(); break;
            case "stale_source": ledger.SourceStateHash = new string('a', 64); break;
            case "seed_owner": ledger.MaterialReservations.Single(row => row.ReservationId == "owned-seeds").SourceDecisionId = "other-root"; break;
            case "seed_item": ledger.MaterialReservations.Single(row => row.ReservationId == "owned-seeds").QualifiedItemId = "(O)472"; break;
            case "cash_owner": ledger.CurrencyReservations[0].SourceDecisionId = "other-root"; break;
            case "cash_purpose": ledger.CurrencyReservations[0].Purpose = "unrelated"; break;
            case "used_step": ledger.History[0].CommitmentId = f.Step; break;
        }
        Fixture.Save(f.LedgerPath, ledger);
        Assert.Throws<InvalidDataException>(() => f.Open());
        Assert.Equal("active", f.Repository.Get(f.Snapshot).CropPlantingCommitments.Single().Status);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("history")]
    [InlineData("reason")]
    [InlineData("rejected")]
    public void CancellationReplyIsFullyReplayedNotJustAcceptedFlag(string fault)
    {
        using var f = new Fixture();
        var cancel = f.Open().BeginCancellation("explicit_cancel");
        var result = f.Repository.Cancel(f.Snapshot, f.Id, cancel.Request);
        switch (fault)
        {
            case "foreign": result.Ledger!.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity++; break;
            case "history": result.Ledger!.History[^1].Reason = "unrelated"; break;
            case "reason": result.Ledger!.CropPlantingCommitments[0].CancelReason = "unrelated"; break;
            case "rejected": result.Accepted = false; break;
        }
        Assert.Throws<InvalidDataException>(() => cancel.VerifyReply(result));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("release_list")]
    [InlineData("cash")]
    [InlineData("crop")]
    [InlineData("rejected")]
    public void PortfolioReleaseCannotAcknowledgeDifferentOwnersOrResults(string fault)
    {
        using var f = new Fixture();
        var cancel = f.Open().BeginCancellation("explicit_cancel");
        var release = cancel.VerifyReply(f.Repository.Cancel(f.Snapshot, f.Id, cancel.Request));
        Assert.Throws<InvalidDataException>(() => release.VerifyReadback(f.Repository.Get(f.Snapshot)));
        var result = f.Repository.CommitReservationPortfolio(f.Snapshot, release.Request);
        switch (fault)
        {
            case "foreign": result.Ledger!.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity++; break;
            case "release_list": result.ReleasedReservationIds = new[] { "outside" }; break;
            case "cash": result.Ledger!.CurrencyReservations[0].Status = "active"; break;
            case "crop": result.Ledger!.CropPlantingCommitments[0].Status = "active"; break;
            case "rejected": result.Accepted = false; break;
        }
        Assert.Throws<InvalidDataException>(() => release.VerifyReply(result));
    }

    [Theory]
    [InlineData("completion")]
    [InlineData("cancel_recovery")]
    [InlineData("final")]
    public void ReadbackRejectsAnyUnexplainedForeignRootChange(string stage)
    {
        using var f = new Fixture(due: stage == "completion");
        var scope = f.Open();
        var cancel = scope.BeginCancellation("explicit_cancel");
        if (stage == "completion")
        {
            var observed = f.Repository.Get(f.Snapshot);
            observed.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity++;
            Assert.Throws<InvalidDataException>(() => scope.VerifyCompletionReadback(observed));
            return;
        }
        var result = f.Repository.Cancel(f.Snapshot, f.Id, cancel.Request);
        var release = cancel.VerifyReply(result);
        if (stage == "final") release.VerifyReply(f.Repository.CommitReservationPortfolio(f.Snapshot, release.Request));
        var readback = Fixture.Copy(f.Repository.Get(f.Snapshot));
        readback.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity++;
        Assert.Throws<InvalidDataException>(() => stage == "final" ? release.VerifyReadback(readback) : cancel.ResumeFromReadback(readback).ClosedLedger);
    }

    [Fact]
    public void DetachedRequestsAndPhysicalSourceGuardPreventAccidentalRebinding()
    {
        using var f = new Fixture();
        var scope = f.Open();
        Assert.Throws<InvalidDataException>(() => scope.BeginCancellation(" "));
        var cancel = scope.BeginCancellation("explicit_cancel");
        cancel.Request.ExpectedLedgerRevision++;
        scope.BaseLedger.MaterialReservations[0].Quantity++;
        var release = cancel.VerifyReply(f.Repository.Cancel(f.Snapshot, f.Id, cancel.Request));
        release.Request.ReleaseReservationIds[0] = "outside";
        Assert.DoesNotContain("outside", release.Request.ReleaseReservationIds);
        File.AppendAllText(f.LedgerPath, " ");
        Assert.Throws<InvalidDataException>(() => release.Request);
        Assert.Throws<InvalidDataException>(() => cancel.Request);
    }
}
