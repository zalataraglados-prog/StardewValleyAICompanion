using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedPlantingPrefix
{
    private readonly object sync = new();
    private readonly CropTeacherSeedPlantingSupport scope;
    private readonly StrategyCommitmentLedger ledger;
    private readonly MoneyRevenueCropNativeProductionActionBatch batch;
    private readonly ActionQueueEnvelope queue;
    private readonly string queuePath;
    private readonly string queueHash;
    private readonly List<(QueueExecutionStepReceipt Step, string Path, string Hash)> observations = new();
    private SnapshotEnvelope last;
    private bool dispatched;
    private bool closed;
    private bool broken;
    private int verified;

    internal CropTeacherSeedPlantingPrefix(CropTeacherSeedPlantingSupport scope, StrategyCommitmentLedger ledger,
        MoneyRevenueCropNativeProductionActionBatch batch, ActionQueueEnvelope queue, string queuePath, string queueHash)
    {
        Require(queue.ExecutionMode == ExecutionTargetProfiles.TrainingSingleplayer,
            "Owned planting runtime prefix requires the isolated singleplayer execution profile.");
        this.scope = scope;
        this.ledger = CropTeacherSeedSupportSource.Copy(ledger);
        this.batch = CropTeacherSeedSupportSource.Copy(batch);
        this.queue = CropTeacherSeedSupportSource.Copy(queue);
        this.queuePath = queuePath;
        this.queueHash = queueHash;
        last = scope.Source.Before;
    }

    public int VerifiedCount { get { lock (sync) return verified; } }
    public string CurrentStateHash { get { lock (sync) return last.StateHash; } }

    public void ValidateBeforeDispatch(int index, ActionQueueItem item, SnapshotEnvelope current, StrategyCommitmentLedger observed)
    {
        lock (sync)
        {
            scope.ValidateInputsUnchanged();
            Require(!closed && !broken && !dispatched && index == verified && index < queue.Items.Length &&
                    EqualJson(item, queue.Items[index]) && EqualJson(observed, ledger) &&
                    CurrentTeacherFrontierSupport.HashFile(queuePath) == queueHash,
                "Owned planting dispatch does not match its locked queue/authority/prefix.");
            MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(current, "owned planting current dispatch");
            Require(current.StateHash == last.StateHash && current.GameTick == last.GameTick &&
                    current.SaveId.Value == last.SaveId.Value && current.PlayerId.Value == last.PlayerId.Value &&
                    current.GameVersion == last.GameVersion && current.InGameTime.Value == last.InGameTime.Value &&
                    FieldEnvelopeValidator.IsReadableStatus(current.InGameTime.Status),
                "Owned planting current source is not the actual verified prefix boundary.");
            var crop = ledger.CropPlantingCommitments.Single(row => row.CommitmentId == scope.CommitmentId);
            Require(CropSeedPlantingBinding.TryInventory(item.NormalizedCommand.Parameters, out var inventory),
                "Owned planting next item lost its exact inventory binding.");
            CropSeedPlantingGrowthPreflight.Validate(current, batch, inventory!.InventorySlotIndex!.Value, crop.SeedId, crop.BaseGrowDays, index);
            dispatched = true;
        }
    }

    public void ObserveNativeStep(QueueExecutionStepReceipt step, string afterSnapshotPath)
    {
        lock (sync)
        {
            try
            {
                scope.ValidateInputsUnchanged();
                Require(dispatched && !closed && !broken && step.QueueItemIndex == verified &&
                        CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(afterSnapshotPath), Path.GetFullPath(step.AfterSnapshotPath)),
                    "Owned planting observation has no matching dispatched item/artifact.");
                var hash = CurrentTeacherFrontierSupport.HashFile(afterSnapshotPath);
                var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(afterSnapshotPath, "owned planting observed after");
                var successful = step.Status == "applied" && step.PrimitiveVerificationStatus == "verified";
                CropTeacherSeedPlantingReceipt.ValidateStep(queue, step, verified, last.StateHash, last.GameTick, successful,
                    completed: successful && verified == queue.Items.Length - 1);
                CropTeacherSeedPlantingNativeOutcome.VerifyStepBoundary(last, after, scope.Source.Before, step, scope.TotalDay);
                if (successful)
                {
                    var item = queue.Items[verified];
                    Require(CropSeedPlantingBinding.TryInventory(item.NormalizedCommand.Parameters, out var inventory), "Owned planting inventory binding is missing.");
                    var node = item.NormalizedCommand.Parameters.Single(row => row.Name == "crop_seed_planting_node_id").Value;
                    var crop = ledger.CropPlantingCommitments.Single(row => row.CommitmentId == scope.CommitmentId);
                    var tile = batch.Tiles[verified];
                    CropSeedResourceBoundary.VerifyPlanting(last, after, inventory!, node, crop, batch.LocationId, tile.TileX, tile.TileY);
                    verified++;
                }
                else CropSeedResourceBoundary.VerifyUnconsumedPlanting(last, after);
                Require(CurrentTeacherFrontierSupport.HashFile(afterSnapshotPath) == hash, "Owned planting after artifact changed during observation.");
                observations.Add((CropTeacherSeedSupportSource.Copy(step), Path.GetFullPath(afterSnapshotPath), hash));
                last = after;
                dispatched = false;
                closed = !successful || verified == queue.Items.Length;
            }
            catch { broken = true; throw; }
        }
    }

    public void SealObservedReceipt(QueueExecutionReceiptEnvelope receipt)
    {
        lock (sync)
        {
            Require(!broken && !dispatched && observations.Count > 0 && receipt.StepResults.Length == observations.Count &&
                    receipt.AfterStateHash == last.StateHash && receipt.AfterGameTick == last.GameTick,
                "Owned planting completion differs from its actual observed terminal boundary.");
            for (var index = 0; index < observations.Count; index++)
            {
                var observed = observations[index];
                Require(EqualJson(receipt.StepResults[index], observed.Step) &&
                        CurrentTeacherFrontierSupport.HashFile(observed.Path) == observed.Hash,
                    "Owned planting completion replaced an observed native step/artifact.");
            }
            closed = true;
        }
    }
}
