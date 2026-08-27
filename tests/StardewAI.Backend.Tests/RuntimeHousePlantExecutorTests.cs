using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeHousePlantExecutorTests
{
    [Fact]
    public void ProductionRuntimeUsesOneNativeLocationActionAndNeverWritesTheVisualFrameDirectly()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.HousePlant.cs");
        var fixture = RuntimeHarnessSources.File("ModEntry.HousePlantFixture.cs");

        Assert.Contains("StartHousePlantRotation(pending);", all);
        Assert.Contains("TryBuildTilePath(", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("Game1.player.CurrentItem is not null", source);
        Assert.Contains("Game1.player.CurrentToolIndex = active.SafeSlotIndex", source);
        Assert.Contains("Game1.player.CurrentToolIndex = active.RestoreSlotIndex", source);
        Assert.Contains("IsHousePlantObjectTrap(active.Location, active.Stand)", source);
        Assert.Contains("house_plant_destructive_object_trap_preamble_blocked", source);
        Assert.DoesNotMatch(@"ParentSheetIndex\s*=(?!=)", source);
        Assert.DoesNotContain("checkForAction(", source, StringComparison.Ordinal);
        Assert.Contains("plant.ParentSheetIndex = requestedSpriteIndex", fixture, StringComparison.Ordinal);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("world.rotate_house_plant"));
    }
}
