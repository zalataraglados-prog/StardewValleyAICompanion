using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherWateringMaintenance
{
    private CropTeacherWateringMaintenance(CropTeacherRefillApproachCompilation compilation,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId)
    {
        Require(transition.TransitionKind == "crop_actions" && transition.TotalDay == compilation.TotalDay &&
                transition.ExecutorVersion == PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor &&
                transition.SelectedCandidateId == compilation.CandidateId &&
                CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.QueuePath), compilation.QueuePath) &&
                CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), compilation.SourcePath),
            "Crop refill approach changed its pinned original transition.");
        refillTrace = CropTeacherRefillApproachNativeTrace.OpenResourceNeutral(compilation,
            transition.ExecutionReceiptPath, transition.AfterSnapshotPath, transition.RunId);
        // The original trace owns all retained artifacts, including compressed requests.
        // Do not decode/hash those same snapshots again in a second retainer.
        inputs = new(Array.Empty<string>());
        AppliedTiles = Array.AsReadOnly(Array.Empty<MoneyRevenueCropPlantingTile>());
        AfterSnapshotPath = refillTrace.AfterSnapshotPath;
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            AfterSnapshotPath, "original resource-neutral refill approach after");
        rebind = new(after, compilation.BaseLedger, stepId, ValidateInputsUnchanged, "Crop refill approach");
        ValidateInputsUnchanged();
    }
}
