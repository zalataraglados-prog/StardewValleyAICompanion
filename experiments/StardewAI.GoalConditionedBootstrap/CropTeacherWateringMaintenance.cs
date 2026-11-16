using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherWateringMaintenance : ICropTeacherSupportingTransition
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly CropTeacherSupportingRebind rebind;
    private readonly CropTeacherRefillApproachNativeTrace? refillTrace;

    private CropTeacherWateringMaintenance(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId, bool refillControl = false)
    {
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, transition.QueuePath, transition.BeforeSnapshotPath,
            transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Select(Path.GetFullPath).Distinct(comparer).ToArray();
        using var reads = InvocationReadSession.Start(paths);
        var originalInputs = new CropTeacherRetainedInputs(paths);
        var source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
        var crops = source.Ledger.CropPlantingCommitments.Where(row => row.CommitmentId == commitmentId &&
            row.Status == StrategyCommitmentStatuses.Active && commitmentIds.Contains(row.CommitmentId, StringComparer.Ordinal)).ToArray();
        Require(crops.Length == 1 && transition.TransitionKind == "crop_actions" &&
                transition.TotalDay == MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days") &&
                crops[0].RequiredWateringTotalDays.Contains(transition.TotalDay),
            "Crop watering maintenance is outside its original active obligation/day.");
        var layout = source.RunRequest.LayoutIntents.Single(row => row.CommitmentId == commitmentId);
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(transition.BeforeSnapshotPath, "watering maintenance before");
        Require(EqualJson(source.Snapshot, before), "Crop watering maintenance does not continue its accepted original source.");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(transition.QueuePath, "watering maintenance queue");
        Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is { Length: >= 1 and <= 8 } &&
                queue.Items.All(item => item is { OptionId: "executor.water_crop", NormalizedCommand: { Parameters: not null, Steps.Length: > 0 } }),
            "Crop watering maintenance requires the original bounded homogeneous watering batch.");
        var tiles = queue.Items.Select(item => MoneyRevenueCropNativeLifecycleProofBuilder.ReadMaintenanceTarget(
            item, source.Snapshot, layout.LocationId, commitmentId)).ToArray();
        Require(tiles.Distinct().Count() == tiles.Length && tiles.All(tile => layout.Tiles.Contains(tile)),
            "Crop watering queue repeats a tile or leaves its original layout.");
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath, "watering maintenance receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(transition.AfterSnapshotPath, "watering maintenance after");
        if (refillControl)
        {
            CropTeacherAppliedPrefixReceipt.ValidateRefillControl(queue, before, receipt, after, transition);
            CropSeedResourceBoundary.VerifyRefillControlWaterDelta(before, after);
        }
        AppliedTileCount = refillControl ? 0 : CropTeacherAppliedPrefixReceipt.Validate(queue, before, receipt, after, transition, "water_crop", "Crop watering");
        RefillControlVerified = refillControl;
        CompleteBatch = AppliedTileCount == tiles.Length;
        var stepPaths = CropTeacherNativeStepPaths.Read(transition, receipt, paths, "Crop watering");
        inputs = new(paths.Concat(stepPaths).Distinct(comparer).ToArray());
        originalInputs.Validate();
        var current = before;
        for (var index = 0; index < receipt.StepResults.Length; index++)
        {
            var next = comparer.Equals(stepPaths[index], Path.GetFullPath(transition.AfterSnapshotPath)) ? after :
                CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(stepPaths[index], "watering maintenance step after");
            CropTeacherNativeStepBoundary.Verify(current, next, before, receipt.StepResults[index], transition.TotalDay, "Crop watering");
            MoneyRevenueCropNativeLifecycleProofBuilder.VerifyMaintenanceLocation(current, next, layout.LocationId);
            if (index < AppliedTileCount)
                MoneyRevenueCropNativeLifecycleProofBuilder.VerifyWateringMaintenanceTile(current, next, layout.LocationId, tiles[index], crops[0]);
            CropSeedResourceBoundary.VerifyWatering(current, next, layout.LocationId, tiles[index], index < AppliedTileCount);
            current = next;
        }
        Require(EqualJson(current, after), "Crop watering maintenance terminal snapshot differs from its last native step.");
        AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
        AppliedTiles = Array.AsReadOnly(tiles.Take(AppliedTileCount).ToArray());
        rebind = new(after, source.Ledger, stepId, inputs.Validate, "Crop watering maintenance");
        originalInputs.Validate();
        inputs.Validate();
    }

    public static CropTeacherWateringMaintenance Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, transition, stepId);

    public static CropTeacherWateringMaintenance OpenRefillControl(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, transition, stepId, refillControl: true);

    public int AppliedTileCount { get; }
    public bool RefillControlVerified { get; }
    public bool RefillApproachVerified => refillTrace?.ResourceNeutralBoundaryVerified == true || RefillResourceEffectsVerified || RefillNativeEffectsObserved;
    public bool RefillNativeEffectsObserved => refillTrace?.NativeEngineEffectsObserved == true;
    public bool RefillResourceEffectsVerified => refillTrace?.SelectedResourceEffectsVerified == true;
    public bool CompleteBatch { get; }
    public IReadOnlyList<MoneyRevenueCropPlantingTile> AppliedTiles { get; }
    public string AfterSnapshotPath { get; }
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => refillTrace?.InputHashes ?? inputs.ReadHashes();
    public void ValidateInputsUnchanged() { inputs.Validate(); refillTrace?.ValidateInputsUnchanged(); }

}
