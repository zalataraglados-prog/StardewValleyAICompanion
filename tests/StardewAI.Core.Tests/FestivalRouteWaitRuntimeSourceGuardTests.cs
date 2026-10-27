namespace StardewAI.Core.Tests;

public sealed class FestivalRouteWaitRuntimeSourceGuardTests
{
    [Fact]
    public void RouteWaitUsesBoundedNativeSlicesAndStopsAtKnownOpeningTime()
    {
        var runtime = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ExecutionCommon.cs"));
        var request = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecutionRequest.cs"));

        Assert.Contains("MaximumBoundedWaitSlices = 3", runtime,
            StringComparison.Ordinal);
        Assert.Contains("waitTicks > 600", runtime,
            StringComparison.Ordinal);
        Assert.Contains("Game1.timeOfDay >= activeWait.WaitUntilTime.Value", runtime,
            StringComparison.Ordinal);
        Assert.Contains("wait_until_time_clock_not_advanced", runtime,
            StringComparison.Ordinal);
        Assert.Contains("route_wait_until_time", request,
            StringComparison.Ordinal);
        Assert.Contains("route_wait_max_slices", request,
            StringComparison.Ordinal);
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
