using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class SnapshotStateCacheTests
{
    [Fact]
    public void ReusesValueWhileSnapshotStateHashIsUnchanged()
    {
        var cache = new SnapshotStateCache<CachedValue>();
        var snapshot = new SnapshotEnvelope { StateHash = "state-a" };

        var first = cache.GetValue(snapshot, _ => new CachedValue("first"));
        var second = cache.GetValue(snapshot, _ => new CachedValue("second"));

        Assert.Same(first, second);
        Assert.Equal("first", second.Value);
        Assert.Equal(1, cache.BuildCount);
    }

    [Fact]
    public void RebuildsValueWhenSameSnapshotReferenceChangesStateHash()
    {
        var cache = new SnapshotStateCache<CachedValue>();
        var snapshot = new SnapshotEnvelope { StateHash = "state-a" };
        var first = cache.GetValue(snapshot, _ => new CachedValue("first"));

        snapshot.StateHash = "state-b";
        var second = cache.GetValue(snapshot, _ => new CachedValue("second"));

        Assert.NotSame(first, second);
        Assert.Equal("second", second.Value);
        Assert.Equal(2, cache.BuildCount);
    }

    [Fact]
    public void EmptyStateHashNeverAuthorizesReuse()
    {
        var cache = new SnapshotStateCache<CachedValue>();
        var snapshot = new SnapshotEnvelope();

        var first = cache.GetValue(snapshot, _ => new CachedValue("first"));
        var second = cache.GetValue(snapshot, _ => new CachedValue("second"));

        Assert.NotSame(first, second);
        Assert.Equal(2, cache.BuildCount);
    }

    [Fact]
    public void StateHashChangeDuringBuildFailsClosed()
    {
        var cache = new SnapshotStateCache<CachedValue>();
        var snapshot = new SnapshotEnvelope { StateHash = "state-a" };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            cache.GetValue(snapshot, value =>
            {
                value.StateHash = "state-b";
                return new CachedValue("unstable");
            }));

        Assert.Contains("StateHash changed", exception.Message);
        Assert.Equal(0, cache.BuildCount);
    }

    private sealed record CachedValue(string Value);
}
