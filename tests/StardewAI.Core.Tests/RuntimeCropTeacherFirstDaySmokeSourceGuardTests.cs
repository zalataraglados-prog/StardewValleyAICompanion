namespace StardewAI.Core.Tests;

public sealed class RuntimeCropTeacherFirstDaySmokeSourceGuardTests
{
    private static readonly string Source = ReadSource();

    [Fact]
    public void SmokeReusesNativeFreshSaveAndTheOnlyLiveTrainingLoop()
    {
        Assert.Contains(
            "Invoke-RuntimeStarterGiftSmoke.ps1",
            Source,
            StringComparison.Ordinal);
        Assert.Contains("-KeepGameRunning", Source, StringComparison.Ordinal);
        Assert.Contains(
            "StardewAI.LiveTrainingLoop.dll",
            Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "--use-runtime-test-harness-executor",
            Source,
            StringComparison.Ordinal);
        Assert.Contains("--use-daily-plan", Source, StringComparison.Ordinal);
        Assert.Contains(
            "goal.grandpa_max_score_year3",
            Source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("debug.setup", Source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "/api/v1/training/execute",
            Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeRequiresTeacherCompletionAndDurableSaveBoundary()
    {
        Assert.Contains(
            "--require-native-save-boundary",
            Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "--stop-after-crop-teacher-first-day",
            Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop_teacher_first_day_completed",
            Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop_teacher_control_attempts_started",
            Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_save_boundary_verified",
            Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "planning_handoff_native_run_plan_path",
            Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "afterCrops.Count -lt 1",
            Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeOwnsHiddenProcessesAndNeverDeletesEvidence()
    {
        Assert.Contains(
            "Start-RuntimeEvidenceProcess",
            Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Stop-RuntimeEvidenceProcesses",
            Source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", Source, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", Source, StringComparison.Ordinal);
    }

    private static string ReadSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "scripts",
                "Invoke-RuntimeCropTeacherFirstDaySmoke.ps1");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(
            "Runtime crop teacher first-day smoke script was not found.");
    }
}
