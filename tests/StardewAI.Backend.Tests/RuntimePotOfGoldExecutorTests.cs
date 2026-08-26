using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimePotOfGoldExecutorTests
{
    [Fact]
    public void RuntimeUsesNativeObjectActionAndSharedMovementWithoutDirectRewardMutation()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.PotOfGold.cs");

        Assert.Contains("StartPotOfGoldClaim(pending);", all);
        Assert.Contains("TryBuildTilePath(", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("CountPotOfGoldReward", source);
        Assert.Contains("remaining_debris_deferred_to_shared_pickup_executor", source);
        Assert.DoesNotContain("removeObject(", source);
        Assert.DoesNotContain("location.debris.Add", source);
        Assert.DoesNotContain("Game1.player.addItem", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("rewards.claim_pot_of_gold"));
    }
}
