using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeDwarfKingStatueExecutorTests
{
    [Fact]
    public void RuntimeUsesSharedMovementAndNativeObjectMenuWithoutDirectProductionBuffMutation()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.DwarfKingStatue.cs");

        Assert.Contains("StartDwarfKingStatuePowerChoice(pending);", all);
        Assert.Contains("TryBuildTilePath(", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("ChooseFromIconsMenu", source);
        Assert.Contains("menu.receiveLeftClick(", source);
        Assert.Contains("Game1.player.hasBuff(active.ExpectedBuffId)", source);
        Assert.DoesNotContain("Game1.player.applyBuff(", source);
        Assert.DoesNotContain("AppliedBuffs.Add", source);
        Assert.DoesNotContain("AppliedBuffs[", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("mining.choose_dwarf_statue_power"));
    }
}
