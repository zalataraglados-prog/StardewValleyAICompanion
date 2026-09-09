namespace StardewAI.Core.Tests;

public sealed class MovementTimingEvidenceSourceGuardTests
{
    [Fact]
    public void MovementAndConnectorResultsPersistTheirExistingTickCounter()
    {
        var root = RepositoryRoot();
        var movement = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.ResultsConnector.cs"));
        var connector = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs"));
        var result = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Sleep.cs"));

        Assert.Contains("move.Tick", movement, StringComparison.Ordinal);
        Assert.Contains("move.StartedAt", movement, StringComparison.Ordinal);
        Assert.Contains("ActualTicks = move.Tick", connector, StringComparison.Ordinal);
        Assert.Contains("ActualTicks = actualTicks", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCalibrationUsesAnIsolatedCloneAndExactNativeMovementEvidence()
    {
        var root = RepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeMovementTimingCalibrationSmoke.ps1"));

        Assert.Contains("Copy-Item -LiteralPath $sourceSavePath", script, StringComparison.Ordinal);
        Assert.Contains("Get-SaveFingerprint", script, StringComparison.Ordinal);
        Assert.Contains("source_save_untouched", script, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", script, StringComparison.Ordinal);
        Assert.Contains("SDL_AUDIODRIVER", script, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_DISABLE_MOVEMENT_TIMEOUTS = \"false\"", script, StringComparison.Ordinal);
        Assert.Contains("profile=", script, StringComparison.Ordinal);
        Assert.Contains("social_future", script, StringComparison.Ordinal);
        Assert.Contains("exact_current_date_static_native_walkability", script, StringComparison.Ordinal);
        Assert.Contains("before_state_hash = $before.StateHash", script, StringComparison.Ordinal);
        Assert.Contains("executor.move_to_tile", script, StringComparison.Ordinal);
        Assert.Contains("actual_ticks", script, StringComparison.Ordinal);
        Assert.Contains("conservative_upper_bound", script, StringComparison.Ordinal);
        Assert.Contains("connector_kind = $case.Kind", script, StringComparison.Ordinal);
        Assert.Contains("building_door", script, StringComparison.Ordinal);
        Assert.Contains("ConservativeConnectorGameMinutes = 2", script, StringComparison.Ordinal);
        Assert.Contains("runtime_proven_building_door_and_step_warp", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BridgePublishesTheNativeMovementTimingApplicabilityContext()
    {
        var root = RepositoryRoot();
        var adapter = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.MovementTiming.cs"));
        var registration = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.cs"));

        Assert.Contains("movement_timing_context", registration, StringComparison.Ordinal);
        Assert.Contains("player.Speed", adapter, StringComparison.Ordinal);
        Assert.Contains("player.addedSpeed", adapter, StringComparison.Ordinal);
        Assert.Contains("player.temporarySpeedBuff", adapter, StringComparison.Ordinal);
        Assert.Contains(
            "minimumVanillaTerrainTemporarySpeedBuff = -3f",
            adapter,
            StringComparison.Ordinal);
        Assert.Contains(
            "current_native_on_foot_speed_scalar",
            adapter,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Math.Min(0f, temporarySpeed)",
            adapter,
            StringComparison.Ordinal);
        Assert.Contains("player.hasBuff(\"19\")", adapter, StringComparison.Ordinal);
        Assert.Contains("Game1.CurrentEvent", adapter, StringComparison.Ordinal);
        Assert.Contains("Game1.realMilliSecondsPerGameMinute", adapter, StringComparison.Ordinal);
        Assert.Contains("runtime_calibration_compatible", adapter, StringComparison.Ordinal);
        Assert.Contains("route_timing_ready_now", adapter, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ??
            throw new InvalidOperationException("Cannot find repository root.");
    }
}
