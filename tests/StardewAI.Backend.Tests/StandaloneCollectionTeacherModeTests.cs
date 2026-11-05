using StardewAI.Contracts.Execution;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class StandaloneCollectionTeacherModeTests
{
    [Fact]
    public void ExplicitLocalModeUsesOriginalTeacherWithoutGlobalPrerequisites()
    {
        var options = LiveTrainingOptions.Parse(Arguments());
        Assert.True(options.StandaloneCollectionTeacher);
        Assert.True(options.UseStageOneCollectionTeacher);
        Assert.False(options.RequireGoldenWalnutSourceScheduleReady);
        Assert.False(options.RequireGourmandContinuationReady);
        Assert.False(options.ContinueAfterCropTeacherRun);
        Assert.True(options.SkipTraining);
        Assert.Empty(options.ManifestPath);
    }

    [Theory]
    [InlineData("training")]
    [InlineData("no_planner")]
    [InlineData("no_feedback")]
    [InlineData("no_native_executor")]
    [InlineData("coop")]
    [InlineData("manifest")]
    [InlineData("learner")]
    [InlineData("initialization")]
    [InlineData("filtered_candidates")]
    [InlineData("pinned_candidate")]
    [InlineData("parameters")]
    [InlineData("precompiled")]
    [InlineData("global_schedule")]
    [InlineData("crop_continuation")]
    [InlineData("missing_inventory")]
    [InlineData("missing_lowering")]
    [InlineData("missing_windows")]
    [InlineData("missing_calibration")]
    [InlineData("missing_isolation")]
    public void LocalModeRejectsInvalidSourceOrSupervisionBoundaries(string mode)
    {
        var options = LiveTrainingOptions.Parse(Arguments());
        switch (mode)
        {
            case "training": options.SkipTraining = false; break;
            case "no_planner": options.UseDailyPlan = false; break;
            case "no_feedback": options.RequireExecutorFeedback = false; break;
            case "no_native_executor": options.UseRuntimeTestHarnessExecutor = false; break;
            case "coop": options.TargetExecutionMode = "companion_multiplayer"; break;
            case "manifest": options.ManifestPath = "unrelated-training.json"; break;
            case "learner": options.PolicyCheckpointPath = "student.json"; break;
            case "initialization": options.PolicyInitializationCheckpointPath = "student.json"; break;
            case "filtered_candidates": options.DailyPlanCandidateOptionIds = new[] { "fishing.catch_fish" }; break;
            case "pinned_candidate": options.DailyPlanCandidateId = "preferred"; break;
            case "parameters": options.DailyPlanCandidateParameters.Add(new() { Name = "quantity", Value = "1" }); break;
            case "precompiled": options.PrecompiledQueuePath = "queue.json"; break;
            case "global_schedule": options.RequireGoldenWalnutSourceScheduleReady = true; break;
            case "crop_continuation": options.ContinueAfterCropTeacherRun = true; break;
            case "missing_inventory": options.StageOneCollectionRequirementInventoryPath = string.Empty; break;
            case "missing_lowering": options.StageOneCollectionAcquisitionLoweringPath = string.Empty; break;
            case "missing_windows": options.MasterAnglerStageOneWindowIndexPath = string.Empty; break;
            case "missing_calibration": options.RouteTimingCalibrationPath = string.Empty; break;
            case "missing_isolation": options.SaveIsolationPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); break;
        }
        Assert.Throws<ArgumentException>(() => StandaloneCollectionTeacherMode.Validate(options));
    }

    [Fact]
    public void LegacyTeacherModesKeepTheirOriginalActivationRules()
    {
        var options = new LiveTrainingOptions();
        Assert.False(options.UseStageOneCollectionTeacher);
        options.RequireGoldenWalnutSourceScheduleReady = true;
        Assert.True(options.UseStageOneCollectionTeacher);
        options.RequireGoldenWalnutSourceScheduleReady = false;
        options.RequireGourmandContinuationReady = true;
        Assert.True(options.UseStageOneCollectionTeacher);
        options.RequireGourmandContinuationReady = false;
        options.ContinueAfterCropTeacherRun = true;
        Assert.True(options.UseStageOneCollectionTeacher);
        StandaloneCollectionTeacherMode.Validate(options);
    }

    private static string[] Arguments()
    {
        var file = typeof(StandaloneCollectionTeacherModeTests).Assembly.Location;
        var directory = Path.GetDirectoryName(file)!;
        // This exercises CLI file-presence checks only; authority content is replayed by the original Teacher.
        return new[]
        {
            "--root", directory, "--run-id", "local-teacher-options", "--no-manifest",
            "--save-isolation-path", directory, "--skip-training", "--use-daily-plan", "--standalone-collection-teacher",
            "--stage-one-collection-requirement-inventory", file,
            "--stage-one-collection-acquisition-lowering", file,
            "--master-angler-stage-one-window-index", file,
            "--route-timing-calibration", file
        };
    }
}
