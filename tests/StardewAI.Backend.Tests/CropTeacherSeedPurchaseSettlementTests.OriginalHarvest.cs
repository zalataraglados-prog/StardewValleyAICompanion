using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    public void OriginalHarvestBindsDueBatchWaveAndExactAppliedResources(int applied, bool failedSuffix)
    {
        using var f = new HarvestFixture();
        var start = f.Open(); f.Emit(applied, failedSuffix);
        ICropTeacherSupportingTransition outcome = start.VerifyTransition(f.Transition);
        var harvest = Assert.IsType<CropTeacherHarvest>(outcome);
        Assert.Equal(applied, harvest.AppliedTileCount); Assert.Equal(applied == 2, harvest.CompleteBatch);
        Assert.Equal(applied, harvest.RetainedQuantityIncrease); Assert.Equal(1, harvest.WaveIndex);
        Assert.Equal(start.OutputRetentionKey, harvest.OutputRetentionKey);
        Assert.False(harvest.WholeWaveOutputRetentionVerified); Assert.False(harvest.NativeCropLifecycleVerified);
        Assert.False(harvest.FormalProductTrainingAuthorized);
        Assert.Equal(f.After.StateHash, outcome.RebindRequest.StateHash);
        Assert.Equal(f.Ledger.Revision, outcome.RebindRequest.ExpectedLedgerRevision);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Ledger, f.After, outcome.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        var rebound = outcome.VerifyRebindReply(reply);
        Assert.Equal(f.After.StateHash, rebound.SourceStateHash);
        AssertOtherRootsUnchanged(f.Ledger, rebound);
    }

    [Fact]
    public void OriginalHarvestAllowsAnOrderedRemainingSubsetWithoutClaimingWholeWaveRetention()
    {
        using var f = new HarvestFixture(); f.Compile(count: 1);
        var start = f.Open(); f.Emit(applied: 1);
        var outcome = start.VerifyTransition(f.Transition);
        Assert.Single(outcome.AppliedTiles); Assert.True(outcome.CompleteBatch);
        Assert.Equal(2, start.WaveMinimumUnits); Assert.Equal(1, start.MinimumUnitsPerTile);
        Assert.False(outcome.WholeWaveOutputRetentionVerified);
    }

    [Fact]
    public void OriginalHarvestResultStillPinsAllInputsBeforeOfferingItsOriginalMarker()
    {
        using var f = new HarvestFixture(); var start = f.Open(); f.Emit();
        var outcome = start.VerifyTransition(f.Transition);
        File.AppendAllText(f.ModelPath, " ");
        Assert.Throws<InvalidDataException>(() => outcome.RebindRequest);
    }

    [Theory]
    [InlineData("day")]
    [InlineData("batch")]
    [InlineData("order")]
    [InlineData("quantity")]
    [InlineData("owner")]
    [InlineData("foreign-plan-save")]
    public void OriginalHarvestRefusesWrongOriginalDenominatorBeforeDispatch(string change)
    {
        using var f = new HarvestFixture();
        if (change == "order") f.Compile(reversed: true);
        if (change == "quantity") { f.Ledger.CropPlantingCommitments.Single().MinimumUnitsPerWave++;
            Fixture.Save(f.LedgerPath, f.Ledger); }
        if (change == "owner") { f.Ledger.CropPlantingCommitments.Single().SourceDecisionId = "foreign-owner";
            Fixture.Save(f.LedgerPath, f.Ledger); }
        if (change == "foreign-plan-save") { f.Before.SaveId.Value = "foreign-save"; f.Ledger.SaveId = "foreign-save";
            Fixture.Save(f.SourcePath, f.Before); Fixture.Save(f.LedgerPath, f.Ledger); }
        Assert.Throws<InvalidDataException>(() => f.Open(day: change == "day" ? f.Day + 1 : null, batch: change == "batch" ? 999 : null));
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("day")]
    [InlineData("input")]
    [InlineData("save")]
    [InlineData("run")]
    [InlineData("executor")]
    [InlineData("relative")]
    [InlineData("step-path")]
    public void OriginalHarvestRefusesUnboundOrChangedOutcome(string change)
    {
        using var f = new HarvestFixture(); var start = f.Open(); f.Emit();
        var transition = f.Transition;
        if (change == "candidate") transition = transition with { SelectedCandidateId = "other" };
        if (change == "day") transition = transition with { TotalDay = f.Day + 1 };
        if (change == "input") File.AppendAllText(f.ModelPath, " ");
        if (change == "save") { f.After.SaveId.Value = "other"; Fixture.Save(transition.AfterSnapshotPath, f.After); }
        if (change == "run") transition = transition with { RunId = "other" };
        if (change == "executor") transition = transition with { ExecutorVersion = "other" };
        if (change == "relative") transition = transition with { ExecutionReceiptPath = "receipt.json" };
        if (change == "step-path")
        {
            var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(transition.ExecutionReceiptPath), JsonOptions)!;
            receipt.StepResults[0].AfterSnapshotPath = f.SourcePath; Fixture.Save(transition.ExecutionReceiptPath, receipt);
        }
        Assert.Throws<InvalidDataException>(() => start.VerifyTransition(transition));
    }
}
