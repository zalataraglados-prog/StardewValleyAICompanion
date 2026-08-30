using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeCalicoStatueExecutorTests
{
    [Fact]
    public void RuntimeUsesSharedMovementAndNativeMineActionWithoutDirectProductionMutation()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.CalicoStatue.cs");

        Assert.Contains("StartCalicoStatue(pending);", all);
        Assert.Contains("TryBuildTilePath(", source);
        Assert.Contains("AdvanceNativeObjectInteractionMovement", source);
        Assert.Contains("active.Mine.checkAction(", source);
        Assert.Contains("CalicoStatueEffectModel.SelectEffect", source);
        Assert.Contains("CalicoStatueReceiptMatches", source);
        Assert.DoesNotContain("calicoStatueEffects.Clear", source);
        Assert.DoesNotContain("calicoStatueEffects.Add", source);
        Assert.DoesNotContain("Game1.player.applyBuff", source);
        Assert.DoesNotContain("createMultipleItemDebris", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.activate_calico_statue"));
    }
}
