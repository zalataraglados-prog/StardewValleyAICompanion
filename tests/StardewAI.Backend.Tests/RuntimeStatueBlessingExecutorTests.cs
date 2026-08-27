using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeStatueBlessingExecutorTests
{
    [Fact]
    public void RuntimeUsesSharedMovementAndNativeObjectActionWithoutDirectProductionBuffMutation()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.StatueBlessing.cs");

        Assert.Contains("StartStatueBlessingClaim(pending);", all);
        Assert.Contains("TryBuildTilePath(", source);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("Game1.player.hasBeenBlessedByStatueToday", source);
        Assert.Contains("StatueBlessingActiveBuffIds()", source);
        Assert.DoesNotContain("Game1.player.applyBuff(", source);
        Assert.DoesNotContain("AppliedBuffs.Add", source);
        Assert.DoesNotContain("AppliedBuffs[", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("rewards.claim_statue_blessing"));
    }
}
