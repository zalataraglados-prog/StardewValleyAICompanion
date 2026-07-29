namespace StardewAI.Core.Tests;

public sealed class QuarryGoldenScytheRuntimeSourceGuardTests
{
    [Fact]
    public void RuntimeLoopUsesOnlyTheTwoRequiredSmokeMods()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeQuarryGoldenScytheLoop.ps1");

        Assert.Contains(
            "$env:SMAPI_MODS_PATH = $smokeModsPath",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "loaded_mod_allowlist = @(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"StardewAI.TransparentBridge\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"StardewAI.RuntimeTestHarness\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"JunimoTestClient\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--bridge-snapshot-url\", \"http://127.0.0.1:8765/api/v1/snapshot?profile=mining\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--executor-timeout-seconds\", \"600\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "continued Quarry Mine clearance after the reward had already been claimed",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Golden Scythe claim and native Quarry Mine exit were both verified",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LiveLoopPreservesPurposeProfileForBeforeAndAfterIngest()
    {
        var program = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs");
        var runtime = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs");
        var http = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.JsonHttp.cs");

        Assert.Contains("SnapshotIngestUrl(options)", program);
        Assert.Contains("SnapshotIngestUrl(options)", runtime);
        Assert.Contains(
            "ingestUrl + \"?profile=\"",
            http,
            StringComparison.Ordinal);
        Assert.Contains(
            "Timeout = TimeSpan.FromSeconds(options.ExecutorTimeoutSeconds)",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NativeExitPromptIsConfirmedBeforeMovementStateGates()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Mining.Traversal.cs");
        var promptIndex = source.IndexOf(
            "if (active.PromptOpened)",
            StringComparison.Ordinal);
        var movementGateIndex = source.IndexOf(
            "if (Game1.player.UsingTool ||",
            promptIndex,
            StringComparison.Ordinal);

        Assert.True(promptIndex >= 0);
        Assert.True(movementGateIndex > promptIndex);
        Assert.Contains(
            "answerDialogueAction(",
            source[promptIndex..movementGateIndex],
            StringComparison.Ordinal);
    }

    [Fact]
    public void MeleeReachDoesNotAssumeWeaponRangeCanCrossMineObstacles()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Mining.Combat.cs");
        var start = source.IndexOf(
            "private static bool IsMonsterWithinCombatReach",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "private static int TrackCombatProgress",
            start,
            StringComparison.Ordinal);
        var reachSource = source[start..end];

        Assert.Contains(
            "AreAdjacent(Game1.player.TilePoint, target.TilePoint)",
            reachSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "targetBox.Intersects(Game1.player.GetBoundingBox())",
            reachSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "addedAreaOfEffect",
            reachSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CombatReusesNativeHeavyHitterInputSemantics()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Mining.Combat.cs");

        Assert.Contains(
            "HeavyHitterInputButton(active.Weapon)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "HeavyHitterInputButton(weapon)",
            source,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(
                Path.Combine(
                    directory.FullName,
                    "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(
            Path.Combine(
                directory?.FullName ??
                    throw new InvalidOperationException(
                        "Cannot find repository root."),
                Path.Combine(segments)));
    }
}
