namespace StardewAI.Core.Tests;

public sealed class FriendshipTeacherRolloutLauncherSourceGuardTests
{
    [Fact]
    public void LauncherUsesIsolatedSaveGoalScopedSnapshotsAndExactProcesses()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeFriendshipTeacherRollout.ps1"));

        Assert.Contains("profile=social_future&fresh=true", source, StringComparison.Ordinal);
        Assert.Contains("profile=social&fresh=true", source, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $sourceSavePath", source, StringComparison.Ordinal);
        Assert.Contains("$clonedSavesRoot", source, StringComparison.Ordinal);
        Assert.Contains("Get-SaveFingerprint", source, StringComparison.Ordinal);
        Assert.Contains("source_save_untouched", source, StringComparison.Ordinal);
        Assert.Contains("SMAPI_MODS_PATH", source, StringComparison.Ordinal);
        Assert.Contains("SDL_AUDIODRIVER", source, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", source, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Id $gameProcess.Id", source, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Id $backendProcess.Id", source, StringComparison.Ordinal);

        Assert.DoesNotContain("training_execution_request", source, StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/training/execute", source, StringComparison.Ordinal);
        Assert.DoesNotContain("profile=full", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Process | Stop-Process", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherMarksBoundedAdmissionAsNotFormalTraining()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeFriendshipTeacherRollout.ps1"));

        Assert.Contains("bounded_teacher_rollout_not_formal_training", source, StringComparison.Ordinal);
        Assert.Contains("bounded_calibration_not_grandpa_deadline_proof", source, StringComparison.Ordinal);
        Assert.Contains("formal_training_started = $false", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
