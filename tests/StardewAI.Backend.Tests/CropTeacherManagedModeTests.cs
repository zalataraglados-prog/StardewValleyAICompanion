using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherManagedModeTests
{
    [Fact]
    public void ExistingUnmanagedDefaultsDoNotAcquireManagedRequirements()
    {
        var options = new LiveTrainingOptions();
        Assert.False(options.UseManagedCropTeacher);
        options.ValidateManagedCropTeacherMode();
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("training")]
    [InlineData("receipt")]
    [InlineData("save")]
    [InlineData("product")]
    [InlineData("legacy_resume")]
    [InlineData("mixed_queue")]
    [InlineData("slot_escape")]
    [InlineData("save_output_overlap")]
    public void ManagedModeRequiresItsOriginalExecutionAndSaveContract(string change)
    {
        var options = new LiveTrainingOptions
        {
            UseManagedCropTeacher = true, SkipTraining = true, UseDailyPlan = true, UseRuntimeTestHarnessExecutor = true,
            UseProductExecutor = true, EmitQueueExecutionReceipt = true, RequireExecutorFeedback = true,
            RequireNativeSaveBoundary = true, SaveIsolationPath = Path.GetTempPath(), SaveSlot = "declared-managed-mode-save",
            Root = Path.Combine(Path.GetTempPath(), "declared-managed-mode-artifacts"), RunId = "mode"
        };
        switch (change)
        {
            case "training": options.SkipTraining = false; break;
            case "receipt": options.EmitQueueExecutionReceipt = false; break;
            case "save": options.RequireNativeSaveBoundary = false; break;
            case "product": options.UseProductExecutor = false; break;
            case "legacy_resume": options.ResumeCropTeacherTransitionLedgerPath = "declared-legacy-ledger.json"; break;
            case "mixed_queue": options.PrecompiledQueuePath = "declared-queue.json"; break;
            case "slot_escape": options.SaveSlot = ".."; break;
            case "save_output_overlap": options.Root = Path.Combine(options.SaveIsolationPath, options.SaveSlot); break;
        }
        if (change == "valid") options.ValidateManagedCropTeacherMode();
        else Assert.ThrowsAny<Exception>(options.ValidateManagedCropTeacherMode);
    }
}
