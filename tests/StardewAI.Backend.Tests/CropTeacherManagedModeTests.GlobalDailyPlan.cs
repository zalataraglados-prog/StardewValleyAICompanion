using System.Text.Json;
using StardewAI.Contracts.Execution;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedModeTests
{
    [Fact]
    public void GlobalDailyPlanContinuationDoesNotEnableCollectionControlOrRequireItsAuthorities()
    {
        var options = LiveTrainingOptions.Parse(GlobalDailyArgs());

        Assert.True(options.UseManagedCropTeacher && options.UseJointDailyCalendar);
        Assert.True(options.ContinueGlobalDailyPlanAfterCropTeacherRun && options.ShouldContinueAfterCropTeacherRun);
        Assert.False(options.ContinueAfterCropTeacherRun);
        Assert.False(options.UseStageOneCollectionTeacher);
        Assert.True(options.RequireNativeSaveBoundary && options.EmitQueueExecutionReceipt && options.SkipTraining);
        Assert.Empty(options.StageOneCollectionRequirementInventoryPath);
        Assert.Empty(options.StageOneCollectionAcquisitionLoweringPath);
        Assert.Empty(options.MasterAnglerStageOneWindowIndexPath);
        Assert.Empty(options.RouteTimingCalibrationPath);
    }

    [Theory]
    [InlineData("collection_continuation")]
    [InlineData("standalone_collection")]
    [InlineData("full_source_schedule")]
    [InlineData("gourmand_admission")]
    [InlineData("candidate_options")]
    [InlineData("candidate_parameters")]
    [InlineData("candidate_kind")]
    [InlineData("candidate_id")]
    [InlineData("rolling")]
    [InlineData("unmanaged")]
    public void GlobalDailyPlanContinuationRejectsAnotherSelectionMode(string change)
    {
        var options = LiveTrainingOptions.Parse(GlobalDailyArgs());
        switch (change)
        {
            case "collection_continuation": options.ContinueAfterCropTeacherRun = true; break;
            case "standalone_collection": options.StandaloneCollectionTeacher = true; break;
            case "full_source_schedule": options.RequireGoldenWalnutSourceScheduleReady = true; break;
            case "gourmand_admission": options.RequireGourmandContinuationReady = true; break;
            case "candidate_options": options.DailyPlanCandidateOptionIds = new[] { "farm.maintain_crops" }; break;
            case "candidate_parameters": options.DailyPlanCandidateParameters.Add(new SmallModelActionParameter { Name = "declared", Value = "fixture" }); break;
            case "candidate_kind": options.DailyPlanCandidateKind = "fixture"; break;
            case "candidate_id": options.DailyPlanCandidateId = "fixture"; break;
            case "rolling": options.ArtifactRetentionMode = "rolling"; break;
            case "unmanaged": options.UseManagedCropTeacher = false; break;
        }

        Assert.Contains("--continue-global-daily-plan-after-crop-teacher-run",
            Assert.Throws<ArgumentException>(options.ValidateGlobalDailyPlanContinuationMode).Message);
    }

    [Theory]
    [InlineData("--skip-training")]
    [InlineData("--emit-queue-execution-receipt")]
    [InlineData("--use-product-executor")]
    [InlineData("--require-native-save-boundary")]
    public void GlobalDailyPlanContinuationKeepsOriginalNativeGuards(string missing)
    {
        var args = GlobalDailyArgs().Where(value => value != missing).ToArray();
        Assert.ThrowsAny<Exception>(() => LiveTrainingOptions.Parse(args));
    }

    [Fact]
    public void GlobalDailyPlanContinuationRejectsRollingRetentionThroughTheActualParser()
    {
        var args = GlobalDailyArgs().ToList();
        args[args.IndexOf("--artifact-retention-mode") + 1] = "rolling";

        Assert.Contains("--artifact-retention-mode stop",
            Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(args.ToArray())).Message);
    }

    [Fact]
    public void GlobalDailyPlanContinuationMarkerDoesNotChangeOldReportDefaults()
    {
        var report = new LiveTrainingLoopReport();
        var old = JsonSerializer.SerializeToElement(report);
        Assert.False(old.TryGetProperty("continue_global_daily_plan_after_crop_teacher_run", out _));
        report.ContinueGlobalDailyPlanAfterCropTeacherRun = true;
        Assert.True(JsonSerializer.SerializeToElement(report)
            .GetProperty("continue_global_daily_plan_after_crop_teacher_run").GetBoolean());
        Assert.False(new LiveTrainingOptions().ShouldContinueAfterCropTeacherRun);
        Assert.True(new LiveTrainingOptions { ContinueAfterCropTeacherRun = true }.ShouldContinueAfterCropTeacherRun);
    }

    private static string[] GlobalDailyArgs() => JointArgs().Concat(new[]
    {
        "--continue-global-daily-plan-after-crop-teacher-run", "--artifact-retention-mode", "stop"
    }).ToArray();
}
