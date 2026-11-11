using System.Text.Json;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task RefillApproachNeutralOutcomeRebindsSameOwnerWithoutWateringOrProductionCredit()
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true, resourceNeutral: true);
        await f.Build();
        var approach = f.Approach;
        var shared = approach.Watering.Shared;
        var preparation = CropTeacherWateringMaintenance.Preparation.OpenRefillApproach(shared.Inputs.Plan.RequestPath,
            shared.Source.SourcePath, shared.Source.LedgerPath, shared.Inputs.Plan.CoveredCommitmentIds, Id,
            "refill-neutral.original", approach.ModelPath, approach.QueuePath);
        var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("crop_actions", preparation.TotalDay,
            approach.QueuePath, shared.Source.SourcePath, f.ReceiptPath, f.AfterPath,
            shared.Runtime.LiveOptions.RunId, shared.Runtime.LiveOptions.PolicyTrajectoryExecutorVersion, preparation.CandidateId);
        var outcome = preparation.VerifyTransition(transition);
        Assert.True(outcome.RefillApproachVerified);
        Assert.False(outcome.RefillControlVerified);
        Assert.Equal(0, outcome.AppliedTileCount);
        Assert.Empty(outcome.AppliedTiles);
        Assert.False(outcome.CompleteBatch);
        Assert.False(outcome.NativeCropLifecycleVerified);
        Assert.False(outcome.FormalProductTrainingAuthorized);
        var after = JsonSerializer.Deserialize<StardewAI.Contracts.State.SnapshotEnvelope>(File.ReadAllText(f.AfterPath), JsonOptions)!;
        var reply = new ReservationPortfolioLedgerService().Commit(shared.Source.Ledger, after, outcome.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        AssertOtherRootsUnchanged(shared.Source.Ledger, outcome.VerifyRebindReply(reply));
        outcome.ValidateInputsUnchanged();
    }

    [Fact]
    public async Task RefillApproachNeutralPreparationRejectsCompiledRepairBeforeDispatch()
    {
        using var f = new RefillApproachFixture(repair: true);
        await f.Compile();
        var shared = f.Watering.Shared;
        var error = Assert.Throws<InvalidDataException>(() => CropTeacherWateringMaintenance.Preparation.OpenRefillApproach(
            shared.Inputs.Plan.RequestPath, shared.Source.SourcePath, shared.Source.LedgerPath,
            shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-neutral.original", f.ModelPath, f.QueuePath));
        Assert.Contains("exact resource settlement", error.Message);
    }
}
