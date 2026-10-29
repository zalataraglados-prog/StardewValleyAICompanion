namespace StardewAI.Core.Tests;

public sealed class GenericMovementClearanceBoundarySourceGuardTests
{
    [Fact]
    public void GenericMovementDoesNotReuseCropBatchLimitAsRouteLimit()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs"));

        Assert.Contains(
            "pending.Request.MaxMovementTiles ?? 512",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "pending.Request.MaxMovementTiles ?? pending.Request.MaxCrops",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenericMovementNeverPlansThroughAnObstacleThatRequiresASeparatePrimitive()
    {
        var movement = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs"));
        var clearance = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.ObstacleClearance.cs"));

        Assert.Contains("allowRemovableObstacles: false", movement, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: false", clearance, StringComparison.Ordinal);
        Assert.Contains(
            "Route repair is compiled as an explicit clear_obstacle primitive.",
            clearance,
            StringComparison.Ordinal);
        Assert.Contains("return false;", clearance, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericMovementRoutesAroundSoftObstaclesThenUsesNativePushThrough()
    {
        var movement = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs"));
        var softObstacles = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.SoftObstacles.cs"));

        Assert.Contains(
            "TickTileMoveSoftObstacle(move, currentTile, nextTile)",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "move.SoftObstacleTicks == 1 || move.SoftObstacleTicks % 30 == 0",
            softObstacles,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReplanTileMove(move, avoidSoftObstacles: true)",
            softObstacles,
            StringComparison.Ordinal);
        Assert.Contains(
            "StartMovingIfNeeded(move, direction)",
            softObstacles,
            StringComparison.Ordinal);
        Assert.Contains("MovePlayerForTick()", softObstacles, StringComparison.Ordinal);
        Assert.Contains(
            "movement_soft_obstacle_timeout",
            softObstacles,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryReleaseOccupiedPathStart(move, pathStartTile)",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (!IsTileOccupiedByCharacter(Game1.currentLocation, pathStartTile))",
            softObstacles,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (!ReplanTileMove(move, avoidSoftObstacles: true))",
            softObstacles,
            StringComparison.Ordinal);
        Assert.Contains(
            "move.NeedsPathStartCentering = false",
            softObstacles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Game1.player.Position =",
            softObstacles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".setTileLocation(",
            softObstacles,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IncidentalDialogueUsesBoundedStateChangePacing()
    {
        var movement = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs"));
        var state = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.State.cs"));

        Assert.Contains(
            "DialogueAdvanceStage.WaitAdvanceEffect",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "move.IncidentalDialogueAdvanceWaitTicks > 30",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "move.IncidentalDialoguePressAttempts >= 60",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "move.IncidentalDialogueTicks > 600",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "move.Pending.MovementIncidentalDialogues >= 8",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "move.MaxTicks += 600",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "IncidentalDialogueMenu",
            state,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "move.IncidentalDialoguePressAttempts >= 16",
            movement,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DialogueSnapshotPublishesReadOnlyCurrentText()
    {
        var menu = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MenuReadAdapter.cs"));

        Assert.Contains(
            "current_text = menu.getCurrentString()",
            menu,
            StringComparison.Ordinal);
        Assert.Contains(
            "DialogueBox public fields and read-only getCurrentString()",
            menu,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "dialogue_current_text_requires_method_call_not_read_in_this_slice",
            menu,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveFestivalTransitUsesOneSharedNativeProjection()
    {
        var projection = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ActiveFestivalTransitReader.cs"));
        var warpTotem = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.WarpTotem.cs"));
        var routeGates = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.GatesBlockers.cs"));

        Assert.Contains("Game1.weatherIcon == 1", projection, StringComparison.Ordinal);
        Assert.Contains("Game1.whereIsTodaysFest", projection, StringComparison.Ordinal);
        Assert.Contains("Game1.timeOfDay <= endTime", projection, StringComparison.Ordinal);
        Assert.Contains("Game1.timeOfDay < startTime", projection, StringComparison.Ordinal);
        Assert.Contains(
            "ActiveFestivalTransitReader.Read(destinationLocation)",
            warpTotem,
            StringComparison.Ordinal);
        Assert.Contains(
            "ActiveFestivalTransitReader.Read(",
            routeGates,
            StringComparison.Ordinal);
        Assert.Contains("warp.TargetName", routeGates, StringComparison.Ordinal);
        Assert.Contains(
            "ordinary_warp_active_festival",
            routeGates,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
}
