using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherManagedModeTests
{
    [Fact]
    public void ActualManagedJointArgumentsDoNotRequireAHardcodedCandidatePool()
    {
        var options = LiveTrainingOptions.Parse(JointArgs());
        Assert.True(options.UseManagedCropTeacher && options.UseJointDailyCalendar && options.UseProductExecutor);
        Assert.True(options.SkipTraining && options.EmitQueueExecutionReceipt && options.RequireNativeSaveBoundary);
        Assert.Equal(4, options.DailyPlanMaxCandidates);
        Assert.True(string.IsNullOrWhiteSpace(options.DailyPlanCandidateKind));
        Assert.True(string.IsNullOrWhiteSpace(options.DailyPlanCandidateId));
        Assert.Equal(12, options.MaxQueueItemAttempts);
        Assert.False(options.ContinueAfterCropTeacherRun);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("no_requirement_inventory")]
    [InlineData("no_acquisition_lowering")]
    [InlineData("no_window_index")]
    [InlineData("no_route_timing")]
    [InlineData("no_receipt")]
    [InlineData("no_save")]
    [InlineData("no_managed")]
    [InlineData("rolling")]
    public void ManagedGlobalContinuationIsExplicitAndPreservesOriginalAuthorities(string mode)
    {
        var root = Path.Combine(Path.GetTempPath(), "managed-global-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var authority = Path.Combine(root, "declared-authority.json");
        File.WriteAllText(authority, "{}");
        var extra = new[]
        {
            "--continue-after-crop-teacher-run",
            "--artifact-retention-mode", "stop",
            "--stage-one-collection-requirement-inventory", authority,
            "--stage-one-collection-acquisition-lowering", authority,
            "--master-angler-stage-one-window-index", authority,
            "--route-timing-calibration", authority
        };
        var args = JointArgs().Concat(extra).ToList();
        var missing = mode switch
        {
            "no_requirement_inventory" => "--stage-one-collection-requirement-inventory",
            "no_acquisition_lowering" => "--stage-one-collection-acquisition-lowering",
            "no_window_index" => "--master-angler-stage-one-window-index",
            "no_route_timing" => "--route-timing-calibration",
            _ => string.Empty
        };
        if (missing.Length > 0) args[args.IndexOf(missing) + 1] = authority + ".missing";
        if (mode == "no_receipt") args.Remove("--emit-queue-execution-receipt");
        if (mode == "no_save") args.Remove("--require-native-save-boundary");
        if (mode == "no_managed") args.Remove("--use-managed-crop-teacher");
        if (mode == "rolling") args[args.IndexOf("--artifact-retention-mode") + 1] = "rolling";
        if (mode != "valid") { Assert.ThrowsAny<Exception>(() => LiveTrainingOptions.Parse(args.ToArray())); return; }
        var options = LiveTrainingOptions.Parse(args.ToArray());
        Assert.True(options.UseManagedCropTeacher && options.ContinueAfterCropTeacherRun && options.UseStageOneCollectionTeacher);
        Assert.True(options.SkipTraining && options.RequireNativeSaveBoundary && options.EmitQueueExecutionReceipt);
        Assert.Empty(options.ResumeCropTeacherTransitionLedgerPath);
    }

    [Theory]
    [InlineData("--skip-training")]
    [InlineData("--emit-queue-execution-receipt")]
    [InlineData("--use-product-executor")]
    [InlineData("--require-native-save-boundary")]
    public void ManagedJointParsingRetainsItsExistingMandatoryGuards(string missing)
    {
        var args = JointArgs().Where(value => value != missing).ToArray();
        Assert.ThrowsAny<Exception>(() => LiveTrainingOptions.Parse(args));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    public void ManagedJointParsingKeepsTheOriginalNativeQueueAttemptNormalization(int attempts)
    {
        var options = LiveTrainingOptions.Parse(JointArgs()
            .Concat(new[] { "--max-queue-item-attempts", attempts.ToString() }).ToArray());
        Assert.Equal(Math.Max(1, attempts), options.MaxQueueItemAttempts);
    }

    [Fact]
    public void ManagedJointParsingKeepsTheOriginalNativeQueueAttemptUpperBound()
    {
        var error = Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(JointArgs()
            .Concat(new[] { "--max-queue-item-attempts", "13" }).ToArray()));
        Assert.Contains("--max-queue-item-attempts between 1 and 12", error.Message);
    }

    [Fact]
    public void StandaloneReceiptCalibrationStillRequiresOneExactCandidate()
    {
        var args = JointArgs().Where(value => value != "--use-managed-crop-teacher").ToArray();
        var error = Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(args));
        Assert.Contains("one exact daily-plan candidate kind and ID", error.Message);
        var exact = LiveTrainingOptions.Parse(args.Concat(new[] { "--daily-plan-max-candidates", "1",
            "--daily-plan-candidate-kind", "declared-calibration", "--daily-plan-candidate-id", "declared-frame" }).ToArray());
        Assert.False(exact.UseManagedCropTeacher);
        Assert.Equal("declared-frame", exact.DailyPlanCandidateId);
    }

    private static string[] JointArgs() => new[]
    {
        "--root", Path.Combine(Path.GetTempPath(), "managed-joint-mode", "artifacts"), "--run-id", "managed-joint-mode",
        "--no-manifest", "--skip-training", "--use-daily-plan", "--joint-daily-calendar", "--use-managed-crop-teacher",
        "--use-product-executor", "--emit-queue-execution-receipt", "--max-queue-item-attempts", "12",
        "--max-attempts", "1", "--required-verified-actions", "1", "--daily-plan-max-candidates", "4",
        "--require-native-save-boundary", "--save-isolation-path", Path.Combine(Path.GetTempPath(), "managed-joint-mode", "saves"),
        "--save-slot", "declared-joint-slot"
    };

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
