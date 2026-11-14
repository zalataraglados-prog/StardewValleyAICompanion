using StardewAI.LiveTrainingLoop;
using StardewAI.Contracts.Execution;

public sealed partial class LiveTrainingOptions
{
    public bool UseManagedCropTeacher { get; set; }
    internal bool UseOwnedSupportingQueueBoundary { get; private set; }

    internal LiveTrainingOptions WithOwnedSupportingQueueBoundary()
    {
        var copy = (LiveTrainingOptions)MemberwiseClone();
        copy.UseOwnedSupportingQueueBoundary = true;
        return copy;
    }

    public void ValidateManagedCropTeacherMode()
    {
        if (!UseManagedCropTeacher) return;
        if (!SkipTraining || !UseDailyPlan || !UseRuntimeTestHarnessExecutor || !UseProductExecutor || !RequireExecutorFeedback ||
            !EmitQueueExecutionReceipt || !RequireNativeSaveBoundary || string.IsNullOrWhiteSpace(SaveIsolationPath) ||
            string.IsNullOrWhiteSpace(SaveSlot) || TargetExecutionMode != ExecutionTargetProfiles.TrainingSingleplayer ||
            UseTeacherPreferenceQueue || UsePrecompiledQueue || UsePlanOutput || UseParameterizedAction)
            throw new ArgumentException("Managed crop Teacher calibration requires skip-training, original daily-plan/product execution, receipts, feedback and its isolated native save boundary.");
        if (!string.IsNullOrWhiteSpace(ResumeCropTeacherTransitionLedgerPath))
            throw new ArgumentException("Managed crop Teacher cannot fork a legacy unmanaged resume ledger; recover its original owner instead.");
        _ = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(BackendUrl);
        var binding = CropTeacherManagedSeedOwner.NormalizeNativeSaveBinding(new(SaveIsolationPath, SaveSlot));
        CropTeacherManagedSeedOwner.RequireOutsideNativeSaveSlot(new[] { Path.GetFullPath(SnapshotDir) }, binding);
    }
}
