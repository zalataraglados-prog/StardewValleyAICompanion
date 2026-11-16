using StardewAI.Contracts.State;
using StardewAI.TransparentBridge.State;

namespace StardewAI.Backend.Tests;

public sealed class MaterialInventoryGraphCaptureTests
{
    [Fact]
    public void AdaptersShareOneReadButAnotherCaptureAtSameTickSeesMutation()
    {
        var farm = new object();
        var reads = 0;
        MaterialInventoryGraph Read() { reads++; return new() { PlayerId = reads }; }
        MaterialInventoryGraph first;
        using (MaterialInventoryGraphCapture.Begin())
        {
            first = MaterialInventoryGraphCapture.Read(farm, 7, 10461, Read);
            Assert.Same(first, MaterialInventoryGraphCapture.Read(farm, 7, 10461, Read));
            Assert.Equal(1, reads);
        }
        using (MaterialInventoryGraphCapture.Begin())
        {
            var after = MaterialInventoryGraphCapture.Read(farm, 7, 10461, Read);
            Assert.NotSame(first, after);
            Assert.Same(after, MaterialInventoryGraphCapture.Read(farm, 7, 10461, Read));
            Assert.Equal(2, reads);
        }
    }

    [Theory]
    [InlineData("farm")]
    [InlineData("player")]
    [InlineData("tick")]
    public void ChangedIdentityCannotReuseWithinCapture(string change)
    {
        using var capture = MaterialInventoryGraphCapture.Begin();
        var farm = new object();
        var first = MaterialInventoryGraphCapture.Read(farm, 7, 1, () => new());
        var after = MaterialInventoryGraphCapture.Read(change == "farm" ? new object() : farm,
            change == "player" ? 8 : 7, change == "tick" ? 2 : 1, () => new());
        Assert.NotSame(first, after);
    }

    [Fact]
    public void UnscopedReadersNeverReuseAnEarlierCapture()
    {
        var farm = new object();
        var first = MaterialInventoryGraphCapture.Read(farm, 7, 1, () => new());
        Assert.NotSame(first, MaterialInventoryGraphCapture.Read(farm, 7, 1, () => new()));
    }

    [Fact]
    public void NestedFailureRestoresOuterCaptureAndDisposalIsIdempotent()
    {
        var farm = new object();
        using var outer = MaterialInventoryGraphCapture.Begin();
        var first = MaterialInventoryGraphCapture.Read(farm, 7, 1, () => new());
        Assert.Throws<IOException>((Action)(() =>
        {
            using var nested = MaterialInventoryGraphCapture.Begin();
            Assert.NotSame(first, MaterialInventoryGraphCapture.Read(farm, 7, 1, () => new()));
            throw new IOException("capture failed");
        }));
        Assert.Same(first, MaterialInventoryGraphCapture.Read(farm, 7, 1, () => new()));
        outer.Dispose();
        outer.Dispose();
        Assert.NotSame(first, MaterialInventoryGraphCapture.Read(farm, 7, 1, () => new()));
    }

    [Fact]
    public void OriginalCollectorAndAdapterUseOneCaptureBoundedCache()
    {
        var collector = RuntimeHarnessSources.RepositoryFile("src", "StardewAI.TransparentBridge", "State", "TransparentStateCollector.cs");
        var adapter = RuntimeHarnessSources.RepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "FarmReadAdapter.MaterialInventoryGraph.cs");
        Assert.Contains("using var inventoryCapture = MaterialInventoryGraphCapture.Begin();", collector);
        Assert.True(collector.IndexOf("MaterialInventoryGraphCapture.Begin()", StringComparison.Ordinal) <
            collector.IndexOf("foreach (var adapter in adapters)", StringComparison.Ordinal));
        Assert.Contains("MaterialInventoryGraphCapture.Read(farm, player.UniqueMultiplayerID, tick,", adapter);
        Assert.DoesNotContain("cachedMaterialInventoryGraphTick", adapter);
    }
}
