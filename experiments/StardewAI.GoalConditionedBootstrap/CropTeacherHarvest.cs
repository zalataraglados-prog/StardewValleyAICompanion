using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherHarvest
{
    private readonly Preparation preparation;
    private readonly CropTeacherRetainedInputs inputs;
    private readonly CropTeacherSupportingRebind rebind;

    private CropTeacherHarvest(Preparation preparation, SnapshotEnvelope before, StardewAI.Contracts.Execution.ActionQueueEnvelope queue,
        string stepId, MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        this.preparation = preparation;
        preparation.ValidateInputsUnchanged();
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        Require(transition.TransitionKind == "crop_actions" && transition.TotalDay == preparation.TotalDay &&
                new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.All(Path.IsPathFullyQualified) &&
                transition.SelectedCandidateId == preparation.CandidateId && comparer.Equals(Path.GetFullPath(transition.QueuePath), preparation.QueuePath) &&
                comparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), preparation.SourcePath),
            "Owned harvest differs from its pinned original compilation/source/day.");
        var paths = preparation.InputHashes.Keys.Concat(new[] { transition.ExecutionReceiptPath, transition.AfterSnapshotPath })
            .Select(Path.GetFullPath).Distinct(comparer).ToArray();
        using var reads = InvocationReadSession.Start(paths);
        var originalInputs = new CropTeacherRetainedInputs(paths);
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath, "original harvest receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(transition.AfterSnapshotPath, "original harvest after");
        AppliedTileCount = CropTeacherAppliedPrefixReceipt.Validate(queue, before, receipt, after, transition, "harvest_crop", "Crop harvest");
        CompleteBatch = AppliedTileCount == preparation.Tiles.Count;
        var stepPaths = CropTeacherNativeStepPaths.Read(transition, receipt, paths, "Crop harvest");
        inputs = new(paths.Concat(stepPaths).Distinct(comparer).ToArray());
        originalInputs.Validate();
        var current = before;
        var quantity = 0L;
        for (var index = 0; index < receipt.StepResults.Length; index++)
        {
            var next = comparer.Equals(stepPaths[index], Path.GetFullPath(transition.AfterSnapshotPath)) ? after :
                CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(stepPaths[index], "original harvest step after");
            CropTeacherNativeStepBoundary.Verify(current, next, before, receipt.StepResults[index], preparation.TotalDay, "Crop harvest");
            MoneyRevenueCropNativeLifecycleProofBuilder.VerifyMaintenanceLocation(current, next, preparation.LocationId);
            if (index < AppliedTileCount)
            {
                var tile = preparation.Tiles[index];
                var effects = CropSeedResourceBoundary.VerifyHarvestResources(current, next, new TrainingExecutionRequest
                {
                    OptionId = "executor.harvest_crop", BeforeStateHash = current.StateHash,
                    LocationId = preparation.LocationId, TargetTileX = tile.TileX, TargetTileY = tile.TileY
                }, preparation.HarvestItemQualifiedId, preparation.MinimumUnitsPerTile);
                quantity = checked(quantity + effects.Stock.Inventory.QuantityIncrease!.Value);
            }
            else CropSeedResourceBoundary.VerifyUnconsumedCropAction(current, next, "Owned harvest", requireSeedInventory: false);
            current = next;
        }
        Require(EqualJson(current, after), "Owned harvest terminal snapshot differs from its last native step.");
        RetainedQuantityIncrease = quantity;
        AppliedTiles = Array.AsReadOnly(preparation.Tiles.Take(AppliedTileCount).ToArray());
        AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
        rebind = new(after, preparation.BaseLedger, stepId, ValidateInputsUnchanged, "Crop harvest");
        originalInputs.Validate();
        ValidateInputsUnchanged();
    }

    public int AppliedTileCount { get; }
    public bool CompleteBatch { get; }
    public long RetainedQuantityIncrease { get; }
    public IReadOnlyList<MoneyRevenueCropPlantingTile> AppliedTiles { get; }
    public string AfterSnapshotPath { get; }
    public int WaveIndex => preparation.WaveIndex;
    public string OutputRetentionKey => preparation.OutputRetentionKey;
    // An exact batch is not the original whole-wave output-retention/lifecycle proof.
    public bool WholeWaveOutputRetentionVerified => false;
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() { preparation.ValidateInputsUnchanged(); inputs.Validate(); }
}
