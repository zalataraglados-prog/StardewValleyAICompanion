using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeFeedHopperExecutorTests
{
    [Fact]
    public void RuntimeUsesSharedMovementAndOneNativeLocationActionWithConservationReceipt()
    {
        var source = RuntimeHarnessSources.File("ModEntry.FeedHopper.cs");
        var movement = RuntimeHarnessSources.File("ModEntry.NativeObjectInteractionMovement.cs");
        var fixture = RuntimeHarnessSources.File("ModEntry.FeedHopperFixture.cs");

        Assert.Contains("AdvanceNativeObjectInteractionMovement(active, \"feed_hopper\"", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("siloAfter == active.ExpectedSiloHayAfter", source);
        Assert.Contains("inventoryAfter == active.InventoryHayBefore + active.ExpectedWithdrawal", source);
        Assert.DoesNotContain("piecesOfHay.Value -=", source);
        Assert.Contains("INativeObjectInteractionMovement", movement);
        Assert.Contains("debug_setup_feed_hopper", fixture);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("animals.withdraw_feed_hopper_hay"));
    }
}
