using StardewAI.TransparentBridge.State;

namespace StardewAI.Backend.Tests;

public sealed class MapActionCaptureTests
{
    [Fact]
    public void SameCollectionReusesExactRowsAndRetainsMultipleLocations()
    {
        var firstLocation = new object(); var secondLocation = new object(); var map = new object();
        var reads = 0;
        string[] Read() { reads++; return new[] { "Buildings.Action", "Back.TouchAction" }; }
        using var capture = MapActionCapture<string>.Begin();
        var first = MapActionCapture<string>.Read(firstLocation, map, Read);
        var second = MapActionCapture<string>.Read(secondLocation, map, Read);
        Assert.NotSame(first, second);
        Assert.Same(first, MapActionCapture<string>.Read(firstLocation, map, Read));
        Assert.Same(second, MapActionCapture<string>.Read(secondLocation, map, Read));
        Assert.Equal(new[] { "Buildings.Action", "Back.TouchAction" }, first);
        Assert.Equal(2, reads);
    }

    [Fact]
    public void ReplacedMapRefreshesOnlyItsLocation()
    {
        var location = new object(); var otherLocation = new object(); var map = new object();
        using var capture = MapActionCapture<string>.Begin();
        var first = MapActionCapture<string>.Read(location, map, () => new[] { "old" });
        var other = MapActionCapture<string>.Read(otherLocation, map, () => new[] { "other" });
        var replacement = new object();
        var refreshed = MapActionCapture<string>.Read(location, replacement, () => new[] { "new" });
        Assert.NotSame(first, refreshed);
        Assert.Same(refreshed, MapActionCapture<string>.Read(location, replacement, () => throw new Exception("cached")));
        Assert.Same(other, MapActionCapture<string>.Read(otherLocation, map, () => throw new Exception("cached")));
    }

    [Fact]
    public void EqualButDistinctLocationAndMapReferencesNeverAlias()
    {
        var location = new Identity("Farm"); var equalLocation = new Identity("Farm");
        var map = new Identity("Map"); var equalMap = new Identity("Map");
        Assert.Equal(location, equalLocation); Assert.Equal(map, equalMap);
        using var capture = MapActionCapture<string>.Begin();
        var first = MapActionCapture<string>.Read(location, map, () => new[] { "first" });
        Assert.NotSame(first, MapActionCapture<string>.Read(equalLocation, map, () => new[] { "location" }));
        Assert.NotSame(first, MapActionCapture<string>.Read(location, equalMap, () => new[] { "map" }));
    }

    [Fact]
    public void SuccessiveCollectionsAtSameTickSeeMapMutation()
    {
        var location = new object(); var map = new object();
        var tick = 10461L; var action = "Warp Town 1 2"; var reads = 0;
        string[] Read() { reads++; return new[] { tick.ToString(), action }; }
        string[] before;
        using (MapActionCapture<string>.Begin())
        {
            before = MapActionCapture<string>.Read(location, map, Read);
            action = "Warp Forest 3 4";
            Assert.Same(before, MapActionCapture<string>.Read(location, map, Read));
        }
        using (MapActionCapture<string>.Begin())
        {
            var after = MapActionCapture<string>.Read(location, map, Read);
            Assert.NotSame(before, after);
            Assert.Equal(before[0], after[0]); Assert.NotEqual(before[1], after[1]);
            Assert.Same(after, MapActionCapture<string>.Read(location, map, Read));
        }
        Assert.Equal(2, reads);
    }

    [Fact]
    public void UnscopedCallsAlwaysReadFreshIncludingNullMap()
    {
        var location = new object(); var reads = 0;
        string[] Read() { reads++; return new[] { reads.ToString() }; }
        var first = MapActionCapture<string>.Read(location, null, Read);
        Assert.NotSame(first, MapActionCapture<string>.Read(location, null, Read));
        using (MapActionCapture<string>.Begin())
        {
            var scoped = MapActionCapture<string>.Read(location, null, Read);
            Assert.Same(scoped, MapActionCapture<string>.Read(location, null, Read));
        }
        Assert.NotSame(first, MapActionCapture<string>.Read(location, null, Read));
        Assert.Equal(4, reads);
    }

    [Fact]
    public void NestedExceptionRestoresParentAndRepeatedDisposalIsSafe()
    {
        var location = new object(); var map = new object();
        using var outer = MapActionCapture<string>.Begin();
        var first = MapActionCapture<string>.Read(location, map, () => new[] { "outer" });
        Assert.Throws<IOException>((Action)(() =>
        {
            using var inner = MapActionCapture<string>.Begin();
            Assert.NotSame(first, MapActionCapture<string>.Read(location, map, () => new[] { "inner" }));
            throw new IOException("collection failed");
        }));
        Assert.Same(first, MapActionCapture<string>.Read(location, map, () => throw new Exception("parent lost")));
        outer.Dispose(); outer.Dispose();
        Assert.NotSame(first, MapActionCapture<string>.Read(location, map, () => new[] { "fresh" }));
    }

    [Fact]
    public void OutOfOrderDisposalFailsWithoutChangingActiveScope()
    {
        var location = new object(); var map = new object();
        using var outer = MapActionCapture<string>.Begin();
        var first = MapActionCapture<string>.Read(location, map, () => new[] { "outer" });
        using var inner = MapActionCapture<string>.Begin();
        var second = MapActionCapture<string>.Read(location, map, () => new[] { "inner" });
        Assert.Throws<InvalidOperationException>(() => outer.Dispose());
        Assert.Same(second, MapActionCapture<string>.Read(location, map, () => throw new Exception("inner lost")));
        inner.Dispose(); inner.Dispose();
        Assert.Same(first, MapActionCapture<string>.Read(location, map, () => throw new Exception("outer lost")));
    }

    [Fact]
    public void FactoryFailureDoesNotPopulateOrPoisonReplacement()
    {
        var location = new object(); var map = new object(); var replacement = new object(); var reads = 0;
        string[] Fail() { reads++; throw new IOException("scan failed"); }
        string[] Read() { reads++; return new[] { "success" }; }
        using var capture = MapActionCapture<string>.Begin();
        Assert.Throws<IOException>(() => MapActionCapture<string>.Read(location, map, Fail));
        var first = MapActionCapture<string>.Read(location, map, Read);
        Assert.Same(first, MapActionCapture<string>.Read(location, map, Read));
        Assert.Throws<IOException>(() => MapActionCapture<string>.Read(location, replacement, Fail));
        var refreshed = MapActionCapture<string>.Read(location, replacement, Read);
        Assert.NotSame(first, refreshed);
        Assert.Same(refreshed, MapActionCapture<string>.Read(location, replacement, Read));
        Assert.Equal(4, reads);
    }

    [Fact]
    public void OriginalCollectAndReadMapActionsBindOnlyTheMapScanToCapture()
    {
        var adapter = RuntimeHarnessSources.RepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "ShopAccessReadAdapter.cs");
        var routes = RuntimeHarnessSources.RepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "ShopAccessReadAdapter.RouteGraph.cs");
        const string begin = "using var mapActionCapture = MapActionCapture<MapActionRow>.Begin();";
        Assert.Contains(begin, adapter);
        Assert.True(adapter.IndexOf("if (!Context.IsWorldReady)", StringComparison.Ordinal) < adapter.IndexOf(begin, StringComparison.Ordinal));
        Assert.True(adapter.IndexOf(begin, StringComparison.Ordinal) < adapter.IndexOf("var fields = new Dictionary", StringComparison.Ordinal));
        Assert.Contains("MapActionCapture<MapActionRow>.Read(location, location.map, () => ReadMapActionsUncached(location))", routes);
        Assert.Contains("private static MapActionRow[] ReadMapActions(GameLocation location)", routes);
        Assert.Contains("private static MapActionRow[] ReadMapActionsUncached(GameLocation location)", routes);
        Assert.Contains("for (var y = 0; y < height; y++)", routes);
        Assert.Contains("for (var x = 0; x < width; x++)", routes);
        Assert.Contains("AddMapAction(rows, x, y, \"Buildings.Action\", location.doesTileHaveProperty(x, y, \"Action\", \"Buildings\"));", routes);
        Assert.Contains("AddMapAction(rows, x, y, \"Back.TouchAction\", location.doesTileHaveProperty(x, y, \"TouchAction\", \"Back\"));", routes);
    }

    private sealed record Identity(string Name);
}
