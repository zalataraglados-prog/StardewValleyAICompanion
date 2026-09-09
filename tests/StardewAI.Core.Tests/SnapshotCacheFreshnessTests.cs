using System.Reflection;
using StardewAI.Contracts.State;
using StardewAI.TransparentBridge;

namespace StardewAI.Core.Tests;

public sealed class SnapshotCacheFreshnessTests
{
    [Fact]
    public void GameTickRollbackInvalidatesPreLoadSnapshot()
    {
        var snapshot = new SnapshotEnvelope { GameTick = 500 };

        Assert.False(IsFresh(snapshot, 10));
        Assert.True(IsFresh(snapshot, 500));
        Assert.True(IsFresh(snapshot, 530));
        Assert.False(IsFresh(snapshot, 531));
    }

    private static bool IsFresh(SnapshotEnvelope snapshot, long currentGameTick)
    {
        var method = typeof(ModEntry).GetMethod(
            "IsSnapshotFresh",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Snapshot freshness policy method not found.");
        return (bool)(method.Invoke(null, new object[] { snapshot, currentGameTick }) ?? false);
    }
}
