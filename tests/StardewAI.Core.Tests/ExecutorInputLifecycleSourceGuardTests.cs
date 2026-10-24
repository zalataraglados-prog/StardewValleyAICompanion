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
        Assert.Contains(
            "TickMovementControlGate(move)",
            movementLoop,
            StringComparison.Ordinal);
        Assert.Contains(
            "movement_native_control_gate_settling",
            movementLoop,
            StringComparison.Ordinal);
        Assert.Contains(
            "movement_interrupted_by_story_event:",
            movementLoop,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (move.NeedsPathStartCentering)",
            movementLoop,
            StringComparison.Ordinal);
        Assert.Contains(
            "SettleFarmerOnTile(move, pathStartTile, \"path_start\")",
            movementLoop,
            StringComparison.Ordinal);
        Assert.Contains(
            "SettleFarmerOnTile(move, move.TargetTile, \"target_tile\")",
            movementLoop,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SettleFarmerOnTargetTile",
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
        Assert.Contains("BuildAdjacentToolPath(", source, StringComparison.Ordinal);
        Assert.Contains("TryAdvanceExecutorPath(", source, StringComparison.Ordinal);
        Assert.Contains("private void TickClearObstacleApproach(", source, StringComparison.Ordinal);
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
    public void AdjacentTileActionsUseSharedTurnAwarePathFollower()
    {
        var action = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.CropTileActions.cs");
        var state = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.State.cs");

        Assert.Contains("TryAdvanceExecutorPath(", action, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: false", action, StringComparison.Ordinal);
        Assert.Contains("ExecutorPathCursor PathCursor", state, StringComparison.Ordinal);
        Assert.DoesNotContain("action.PathIndex", action, StringComparison.Ordinal);
        Assert.DoesNotContain("action.StuckTicks", action, StringComparison.Ordinal);
    }

    [Fact]
    public void AdjacentPathObstaclePolicyFailsSafeUnlessExplicitlyEnabled()
    {
        var farming = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Farming.cs");
        var ginger = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Ginger.cs");
        var pets = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Pets.cs");
        var miningCombat = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Mining.Combat.cs");
        var normalizedFarming = farming.Replace("\r\n", "\n");
        var normalizedGinger = ginger.Replace("\r\n", "\n");
        var normalizedPets = pets.Replace("\r\n", "\n");

        Assert.Contains(
            "bool allowRemovableObstacles = false",
            farming,
            StringComparison.Ordinal);
        Assert.Contains("avoidSoftObstacles: true,\n            allowRemovableObstacles: false", normalizedFarming, StringComparison.Ordinal);
        Assert.Contains("avoidSoftObstacles: true,\n            allowRemovableObstacles: false", normalizedGinger, StringComparison.Ordinal);
        Assert.Contains("avoidSoftObstacles: true,\n            allowRemovableObstacles: false", normalizedPets, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: true", miningCombat, StringComparison.Ordinal);
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

    [Fact]
    public void NativeStoryEventInterruptsAndThenResumesTheSelectedQueue()
    {
        var runtime = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs");
        var queueBuilding = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.QueueBuilding.cs");

        Assert.Contains(
            "HasPriorityStoryEventInterrupt(currentBeforeSnapshot)",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildPriorityStoryEventQueueAsync(",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "runtime_priority_interrupt",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "priority_story_event_interrupt",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "selected_queue_lock_preserved",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "new[] { \"story.advance_event\" }",
            queueBuilding,
            StringComparison.Ordinal);
        Assert.Contains(
            "candidateKindOverride: string.Empty",
            queueBuilding,
            StringComparison.Ordinal);
        Assert.Contains(
            "candidateIdOverride: string.Empty",
            queueBuilding,
            StringComparison.Ordinal);
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
