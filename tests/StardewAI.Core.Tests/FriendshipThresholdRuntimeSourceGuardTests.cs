namespace StardewAI.Core.Tests;

public sealed class FriendshipThresholdRuntimeSourceGuardTests
{
    [Fact]
    public void NativeProbeKeepsOriginalQueueExecutionAndDeclaredFixturesSeparate()
    {
        var source = ReadScript();
        Assert.Contains("Invoke-RuntimePrecompiledQueue", source);
        Assert.Contains("-BeforeSnapshotProfile social_future -ExecutionTarget runtime_harness", source);
        Assert.Contains("'independent_pairs'", source);
        Assert.Contains("$preference.selectedCandidate.candidate_id", source);
        Assert.Contains("$_.candidateId", source);
        Assert.Contains("declared_local_story_prerequisites = $true", source);
        Assert.Contains("'fixture-time-result.json'", source);
        Assert.Contains("prior_friendship_acquisition_verified = $false", source);
        Assert.Contains("formal_product_training_authorized = $false", source);
        Assert.DoesNotContain("debug.setup_social", source);
        Assert.DoesNotContain("skipEvent", source);
        Assert.True(source.IndexOf("'fixture-time-result.json'", StringComparison.Ordinal) <
            source.IndexOf("'build-current-friendship-teacher-preference'", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeProbeUsesExplicitGamePathQuietOwnershipAndOriginalSaveHash()
    {
        var source = ReadScript();
        Assert.Contains("E:\\StardewValleyAICompanion-runtime", source);
        Assert.Contains("\"-p:GamePath=$gameDirectory\"", source);
        Assert.Contains("--disable-build-servers", source);
        Assert.Contains("Assert-RuntimePortsUnused", source);
        Assert.Contains("Get-RuntimeExactModAllowlist", source);
        Assert.Contains("SMAPI_MODS_PATH = $isolatedMods", source);
        Assert.Contains("STARDEWAI_SUPPRESS_LOCAL_RENDER = '1'", source);
        Assert.Contains("SDL_AUDIODRIVER = 'dummy'", source);
        Assert.Contains("Stop-RuntimeEvidenceProcesses -Processes $processes.ToArray()", source);
        Assert.Contains("Restore-RuntimeProcessEnvironment", source);
        Assert.Contains("$summary.source_save_unchanged", source);
        Assert.DoesNotContain("Remove-Item", source);
        Assert.DoesNotContain("Stop-Process -Name", source);
        Assert.DoesNotContain("F:\\", source);
    }

    private static string ReadScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
            !File.Exists(Path.Combine(directory.FullName, ".git"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, "scripts", "Invoke-RuntimeFriendshipThresholdSmoke.ps1"));
    }
}
