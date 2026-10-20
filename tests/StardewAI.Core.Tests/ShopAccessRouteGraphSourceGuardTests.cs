namespace StardewAI.Core.Tests;

public sealed class ShopAccessRouteGraphSourceGuardTests
{
    [Fact]
    public void NativeBusTransitionsAreNotAdvertisedAsGenericWarpRoutes()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.RouteGraph.cs"));

        Assert.Contains("IsNativeBusTransportWarp", source,
            StringComparison.Ordinal);
        Assert.Contains("location.NameOrUniqueName", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("location is Desert", source,
            StringComparison.Ordinal);
        Assert.Contains("native_bus_transport_executor_required", source,
            StringComparison.Ordinal);
        Assert.Contains("native_transport_transition = nativeBusTransport",
            source, StringComparison.Ordinal);
        Assert.Contains("transport_executor_supported = !nativeBusTransport",
            source, StringComparison.Ordinal);
        Assert.Contains("resolved = targetLoaded && !nativeBusTransport",
            source, StringComparison.Ordinal);
        Assert.Contains("native_transport_transitions_excluded", source,
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
