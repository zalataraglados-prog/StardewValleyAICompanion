using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherHarvest
{
    public sealed partial class NativeBatch
    {
        private static readonly StringComparer Comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        private readonly ActionQueueEnvelope queue;
        private readonly MoneyRevenueCropNativeLifecycleTransitionRequest transition;
        private readonly List<CropTeacherRetainedInputs> inputs = new();
        private readonly List<CropTeacherHarvestPickupFiles> pickups = new();
        private readonly long harvestEndTick;
        private bool completed;

        internal NativeBatch(Preparation prepared, SnapshotEnvelope before, ActionQueueEnvelope queue,
            string stepId, MoneyRevenueCropNativeLifecycleTransitionRequest transition)
        {
            Prepared = prepared; Before = before; this.queue = queue; StepId = stepId; this.transition = transition;
            prepared.ValidateInputsUnchanged();
            Require(transition.TransitionKind == "crop_actions" && transition.TotalDay == prepared.TotalDay &&
                    new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.All(Path.IsPathFullyQualified) &&
                    transition.SelectedCandidateId == prepared.CandidateId && Comparer.Equals(Path.GetFullPath(transition.QueuePath), prepared.QueuePath) &&
                    Comparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), prepared.SourcePath),
                "Owned harvest differs from its pinned original compilation/source/day.");
            AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
            var paths = prepared.InputHashes.Keys.Concat(new[] { transition.ExecutionReceiptPath, AfterSnapshotPath })
                .Select(Path.GetFullPath).Distinct(Comparer).ToArray();
            using var reads = InvocationReadSession.Start(paths);
            var originalInputs = new CropTeacherRetainedInputs(paths);
            var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath, "original harvest receipt");
            After = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(AfterSnapshotPath, "original harvest after");
            harvestEndTick = After.GameTick;
            AppliedTileCount = CropTeacherAppliedPrefixReceipt.Validate(queue, before, receipt, After, transition, "harvest_crop", "Crop harvest");
            var stepPaths = CropTeacherNativeStepPaths.Read(transition, receipt, paths, "Crop harvest");
            inputs.Add(new(paths.Concat(stepPaths).Distinct(Comparer).ToArray()));
            originalInputs.Validate();
            var current = before;
            var acquired = new List<(SnapshotEnvelope After, long HeldIncrease)>(AppliedTileCount);
            for (var index = 0; index < receipt.StepResults.Length; index++)
            {
                var next = Comparer.Equals(stepPaths[index], AfterSnapshotPath) ? After :
                    CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(stepPaths[index], "original harvest step after");
                CropTeacherNativeStepBoundary.Verify(current, next, before, receipt.StepResults[index], prepared.TotalDay, "Crop harvest");
                MoneyRevenueCropNativeLifecycleProofBuilder.VerifyMaintenanceLocation(current, next, prepared.LocationId);
                if (index < AppliedTileCount)
                {
                    var tile = prepared.Tiles[index];
                    var effects = CropSeedResourceBoundary.VerifyHarvestResources(current, next, new TrainingExecutionRequest
                    {
                        OptionId = "executor.harvest_crop", BeforeStateHash = current.StateHash,
                        LocationId = prepared.LocationId, TargetTileX = tile.TileX, TargetTileY = tile.TileY
                    }, prepared.HarvestItemQualifiedId, minimumUnits: 0);
                    var held = effects.Stock.Inventory.QuantityIncrease!.Value;
                    acquired.Add((next, held));
                    HeldIncrease = checked(HeldIncrease + held);
                }
                else CropSeedResourceBoundary.VerifyUnconsumedCropAction(current, next, "Owned harvest", requireSeedInventory: false);
                current = next;
            }
            Require(EqualJson(current, After), "Owned harvest terminal snapshot differs from its last native step.");
            if (acquired.Any(row => row.HeldIncrease < prepared.MinimumUnitsPerTile))
                VerifyHarvestDropAccounting(before, acquired, prepared.HarvestItemQualifiedId, prepared.MinimumUnitsPerTile);
        }

        internal Preparation Prepared { get; }
        internal SnapshotEnvelope Before { get; }
        internal SnapshotEnvelope After { get; private set; }
        internal string StepId { get; }
        internal long HeldIncrease { get; private set; }
        internal int AppliedTileCount { get; }
        public string AfterSnapshotPath { get; private set; }
        internal string? PickupSettlementPath { get; private set; }
        public bool NeedsPickup => HeldIncrease < checked((long)AppliedTileCount * Prepared.MinimumUnitsPerTile);
        internal int PickupElapsedTicks => checked((int)(After.GameTick - harvestEndTick));
        internal IReadOnlyDictionary<string, string> InputHashes => inputs.SelectMany(row => row.ReadHashes())
            .ToDictionary(row => row.Key, row => row.Value, Comparer);

        internal void ValidateInputsUnchanged()
        {
            Prepared.ValidateInputsUnchanged();
            foreach (var input in inputs) input.Validate();
        }

        public CropTeacherHarvest Complete(string? pickupSettlementPath = null)
        {
            Require(!completed && !NeedsPickup, "Owned harvest needs verified pickup before its minimum output is in inventory.");
            Require(pickups.Count > 0 == (pickupSettlementPath is not null), "Owned harvest pickup has no retained settlement manifest.");
            if (pickupSettlementPath is not null)
            {
                var manifest = new CropTeacherRetainedInputs(new[] { pickupSettlementPath });
                Require(ReadPickupFiles(pickupSettlementPath).SequenceEqual(pickups), "Owned harvest pickup manifest differs from verified pickup steps.");
                inputs.Add(manifest);
                PickupSettlementPath = pickupSettlementPath;
            }
            completed = true;
            return new(this);
        }
    }
}
