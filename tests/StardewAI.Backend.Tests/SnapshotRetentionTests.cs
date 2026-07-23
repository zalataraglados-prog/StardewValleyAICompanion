using StardewAI.Contracts.State;
using Xunit;

namespace StardewAI.Backend.Tests;

public sealed class SnapshotRetentionTests
{
    [Fact]
    public void SnapshotStoreEvictsOldestBeforeParsingNextSnapshot()
    {
        var store = new StateStore();

        store.StoreSnapshot(Snapshot("state-1", 1));
        store.StoreSnapshot(Snapshot("state-2", 2));
        store.PrepareForSnapshotIngest();

        Assert.DoesNotContain("state-1", store.Snapshots.Keys);
        Assert.Contains("state-2", store.Snapshots.Keys);

        store.StoreSnapshot(Snapshot("state-3", 3));

        Assert.Equal(2, store.Snapshots.Count);
        Assert.Equal("state-3", store.LatestSnapshot()!.StateHash);
    }

    [Fact]
    public void LiveLoopArtifactBudgetSurvivesProcessRestart()
    {
        var source = LiveTrainingLoopSources.All;

        Assert.Contains("NextArtifactIteration(options.SnapshotDir)", source, StringComparison.Ordinal);
        Assert.Contains("nextArtifactIteration + attemptOrdinal - 1", source, StringComparison.Ordinal);
        Assert.Contains("MaxPersistedIterations { get; set; } = 64", source, StringComparison.Ordinal);
        Assert.Contains("MinFreeSpaceMb { get; set; } = 8192", source, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(string stateHash, long gameTick)
    {
        return new SnapshotEnvelope
        {
            StateHash = stateHash,
            GameTick = gameTick
        };
    }
}
