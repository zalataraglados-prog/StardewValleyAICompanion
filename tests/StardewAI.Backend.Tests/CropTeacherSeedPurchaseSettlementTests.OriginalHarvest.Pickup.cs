using System.Reflection;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void OriginalHarvestPickupReplaysAndOffersSaleClaimsOnlyAtTheSettledNativeFrame()
    {
        using var f = new HarvestFixture();
        f.SetStartingDebris(0);
        var start = CropTeacherHarvest.Preparation.OpenRetainingSaleStock(f.Shared.Inputs.PlanPath, f.SourcePath,
            f.LedgerPath, [Id], Id, "harvest.pickup.1", f.Day, f.Batch.BatchIndex, f.ModelPath, f.QueuePath);
        f.Emit(outputs: new[] { (0, 1), (1, 1) });
        var batch = start.ReadNativeBatch(f.Transition);
        Assert.True(batch.NeedsPickup);
        Assert.Throws<InvalidDataException>(() => start.VerifyTransition(f.Transition));
        var (pickup, files, manifest, after) = f.EmitPickup(batch);
        batch.ObservePickup(pickup, files);
        var live = batch.Complete(manifest);
        var replay = start.VerifyTransition(f.Transition, manifest);

        Assert.Equal(2, live.AppliedTileCount);
        Assert.Equal(2, live.RetainedQuantityIncrease);
        Assert.Equal(live.InputHashes.OrderBy(pair => pair.Key), replay.InputHashes.OrderBy(pair => pair.Key));
        Assert.Equal(after.StateHash, replay.RebindRequest.StateHash);
        Assert.Equal(2, replay.RebindRequest.MaterialClaims.Sum(row => row.Quantity));
        Assert.NotEqual(f.Transition.AfterSnapshotPath, replay.AfterSnapshotPath);
        var reply = new ReservationPortfolioLedgerService().Commit(replay.BaseLedger, after, replay.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        Assert.Equal(after.StateHash, replay.VerifyRebindReply(reply).SourceStateHash);

        File.AppendAllText(files.ReceiptPath, " ");
        Assert.Throws<InvalidDataException>(() => replay.ValidateInputsUnchanged());
    }

    [Theory]
    [InlineData("wrong-candidate")]
    [InlineData("quantity")]
    [InlineData("old-frame")]
    public void OriginalHarvestPickupRejectsReceiptAndPhysicalDrift(string fault)
    {
        using var f = new HarvestFixture();
        f.SetStartingDebris(0);
        var start = f.Open();
        f.Emit(outputs: new[] { (0, 1), (1, 1) });
        var batch = start.ReadNativeBatch(f.Transition);
        var (pickup, files, _, _) = f.EmitPickup(batch, fault);
        Assert.Throws<InvalidDataException>(() => batch.ObservePickup(pickup, files));
        Assert.True(batch.NeedsPickup);
    }

    [Fact]
    public void OriginalHarvestPickupProductionReplayBindsBothClosingFrames()
    {
        using var f = new HarvestFixture();
        f.SetStartingDebris(0);
        var start = f.Open();
        f.Emit(outputs: new[] { (0, 1), (1, 1) });
        var (_, files, manifest, _) = f.EmitPickup(start.ReadNativeBatch(f.Transition));
        var reference = new MoneyRevenueCropNativeHarvestSettlementReference(f.Shared.Inputs.PlanPath, f.LedgerPath, [Id],
            Id, "harvest.step.1", f.Batch.BatchIndex, f.ModelPath, manifest, files.AfterSnapshotPath);
        var transition = f.Transition with { NativeHarvestSettlement = reference };
        Assert.Equal(f.Transition.AfterSnapshotPath, transition.AfterSnapshotPath);
        Assert.Equal(files.AfterSnapshotPath, transition.EffectiveAfterSnapshotPath);
        var replay = typeof(CropTeacherHarvest).GetMethod("ReplaySettlement", BindingFlags.Static | BindingFlags.NonPublic)!;
        var result = Assert.IsType<CropTeacherHarvest>(replay.Invoke(null, [transition, f.Shared.Inputs.Plan.CropProductionPlanPath]));
        Assert.Equal(transition.EffectiveAfterSnapshotPath, result.AfterSnapshotPath);
        Assert.False(result.NativeCropLifecycleVerified);
        var substituted = transition with { NativeHarvestSettlement = reference with { SettledAfterSnapshotPath = transition.AfterSnapshotPath } };
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
            replay.Invoke(null, [substituted, f.Shared.Inputs.Plan.CropProductionPlanPath])).InnerException);
    }

    private sealed partial class HarvestFixture
    {
        internal (CropTeacherHarvest.NativeBatch.Pickup, CropTeacherHarvestPickupFiles, string, SnapshotEnvelope) EmitPickup(
            CropTeacherHarvest.NativeBatch batch, string? fault = null)
        {
            var root = Shared.Runtime.LiveOptions.SnapshotDir;
            var before = Copy(After); before.GameTick++;
            var files = new CropTeacherHarvestPickupFiles(Path.Combine(root, "pickup-before.json"), Path.Combine(root, "pickup-queue.json"),
                Path.Combine(root, "pickup-receipt.json"), Path.Combine(root, "pickup-after.json"));
            Fixture.Save(files.BeforeSnapshotPath, before);
            var pickup = batch.PreparePickup(files.BeforeSnapshotPath);
            var after = Copy(before); after.GameTick++;
            AddHeldOutput(after, Output, fault == "quantity" ? 2 : 1);
            Put(after, "current_location", "debris", HarvestDebris(0));
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            Fixture.Save(files.QueuePath, pickup.Queue);
            Fixture.Save(files.AfterSnapshotPath, after);
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("SeedAcquisitionReceipt", pickup.Queue,
                before, after, new[] { after }, new[] { files.AfterSnapshotPath }, Shared.Files.Options.RunId, pickup.CandidateId);
            var step = Assert.Single(receipt.StepResults);
            step.PrimitiveKind = "pickup_debris";
            step.PrimitiveVerificationReasons = ["fixture_verified_pickup"];
            if (fault == "wrong-candidate") receipt.SelectedCandidateId = "wrong";
            if (fault == "old-frame") step.AfterSnapshotPath = Transition.AfterSnapshotPath;
            Fixture.Save(files.ReceiptPath, receipt);
            var manifest = Path.Combine(root, "pickup-settlement.json");
            Fixture.Save(manifest, new[] { files });
            return (pickup, files, manifest, after);
        }
    }
}
