namespace StardewAI.Core.Tests;

public sealed class ExecutorInputLifecycleSourceGuardTests
{
    [Fact]
    public void MovementUsesSingleLeaseInsteadOfLooseDirectionField()
    {
        var movement = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.PathingInput.cs");
        var host = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs");

        Assert.Contains("executorMovementLease.Acquire(", movement, StringComparison.Ordinal);
        Assert.Contains("executorMovementLease.Direction == direction", movement, StringComparison.Ordinal);
        Assert.Contains("MovementLease executorMovementLease = new();", host, StringComparison.Ordinal);
        Assert.DoesNotContain("executorMovementDirection", host, StringComparison.Ordinal);
        Assert.DoesNotContain("executorMovementDirection", movement, StringComparison.Ordinal);

        var movementLoop = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs");
        Assert.Contains(
            "movedSinceLastTick &&",
            movementLoop,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenericClearanceUsesNativeLifecycleWithoutDirectMutation()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.ObstacleClearance.cs");

        Assert.Contains("private void StartClearObstacle(", source, StringComparison.Ordinal);
        Assert.Contains("private void TickClearObstacleCore(", source, StringComparison.Ordinal);
        Assert.Contains("active.Lifecycle.Advance(ObserveNativeToolAction())", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.BeginUsingTool();", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.EndUsingTool();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyClearanceTool", source, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("location.objects.Remove(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("location.terrainFeatures.Remove(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VolcanoStoneWaitsForNativeLifecycleAfterObjectRemoval()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Volcano.Obstacle.cs");

        Assert.Contains("TickRemovedVolcanoStone(active)", source, StringComparison.Ordinal);
        Assert.Contains("active.Lifecycle.Advance(", source, StringComparison.Ordinal);
        Assert.Contains("NativeToolActionCommand.CycleCompleted", source, StringComparison.Ordinal);
        Assert.Contains("active.Lifecycle.Reset();", source, StringComparison.Ordinal);
        Assert.Contains(
            "WriteExecutorDiagnosticDump(\"volcano_obstacle_timeout\")",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VolcanoMovementUsesSharedTurnAwarePathFollower()
    {
        var cooling = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Volcano.cs");
        var obstacle = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Volcano.Obstacle.cs");
        var follower = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.PathFollower.cs");

        Assert.Contains(
            "TryAdvanceExecutorPath(",
            cooling,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryAdvanceExecutorPath(",
            obstacle,
            StringComparison.Ordinal);
        Assert.Contains(
            "HasReachedTurnCenter(",
            follower,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "active.StuckTicks",
            cooling,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "active.StuckTicks",
            obstacle,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsAreBoundedAndDumpOnlyOnTrigger()
    {
        var host = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs");
        var diagnostics = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ExecutorInputDiagnostics.cs");

        Assert.Contains("new(600)", host, StringComparison.Ordinal);
        Assert.Contains("executorDiagnosticFrames.Snapshot()", diagnostics, StringComparison.Ordinal);
        Assert.Contains("lastExecutorDiagnosticDumpTick", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("AppendAllText", diagnostics, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(
            directory?.FullName ??
                throw new InvalidOperationException("Cannot find repository root."),
            Path.Combine(segments)));
    }
}
