namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherWateringMaintenance
{
    private CropTeacherWateringMaintenance(CropTeacherRefillApproachCompilation compilation,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId)
    {
        Require(transition.TransitionKind == "crop_actions" && transition.TotalDay == compilation.TotalDay &&
                transition.SelectedCandidateId == compilation.CandidateId &&
                CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.QueuePath), compilation.QueuePath) &&
                CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), compilation.SourcePath),
            "Crop refill approach changed its pinned original transition.");
        refillTrace = CropTeacherRefillApproachNativeTrace.OpenResourceNeutral(compilation,
            transition.ExecutionReceiptPath, transition.AfterSnapshotPath, transition.RunId);
        // The original trace retains and independently validates its compressed request blobs.
        inputs = new(refillTrace.InputHashes.Keys.Where(path => !path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)).ToArray());
        AppliedTiles = Array.AsReadOnly(Array.Empty<MoneyRevenueCropPlantingTile>());
        AfterSnapshotPath = refillTrace.AfterSnapshotPath;
        var after = CurrentTeacherFrontierSupport.Read<StardewAI.Contracts.State.SnapshotEnvelope>(
            AfterSnapshotPath, "original resource-neutral refill approach after");
        rebind = new(after, compilation.BaseLedger, stepId, ValidateInputsUnchanged, "Crop refill approach");
        ValidateInputsUnchanged();
    }
}
