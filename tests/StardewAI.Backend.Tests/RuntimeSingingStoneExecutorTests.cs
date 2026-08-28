using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeSingingStoneExecutorTests
{
    [Fact]
    public void ProductionRuntimeUsesOneNativeLocationActionAndNeverSynthesizesSoundShakeOrRng()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.SingingStone.cs");
        var movement = RuntimeHarnessSources.File("ModEntry.NativeObjectInteractionMovement.cs");
        var fixture = RuntimeHarnessSources.File("ModEntry.SingingStoneFixture.cs");

        Assert.Contains("StartSingingStone(pending);", all);
        Assert.Contains("AdvanceNativeObjectInteractionMovement(active, \"singing_stone\"", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Equal(1, Count(source, "active.Location.checkAction("));
        Assert.Contains("Game1.player.CurrentToolIndex = active.SafeSlotIndex", source);
        Assert.Contains("Game1.player.CurrentToolIndex = active.RestoreSlotIndex", source);
        Assert.Contains("IsDestructiveObjectTrap(active.Location, active.Stand)", source);
        Assert.Contains("Game1.random_shared_unread", source);
        Assert.DoesNotContain("Game1.playSound(", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"shakeTimer\s*=\s*100", source);
        Assert.Equal(1, Count(source, "Game1.random.Next"));
        Assert.Contains("TryBuildTilePath(", source);
        Assert.Contains("AdvanceNativeObjectInteractionMovement", movement);
        Assert.Contains("new StardewObject(target.Value.ToVector2(), \"94\")", fixture);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("world.play_singing_stone"));
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
