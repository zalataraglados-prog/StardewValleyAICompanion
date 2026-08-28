using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeFluteBlockExecutorTests
{
    [Fact]
    public void RuntimeUsesSharedMovementAndOneNativeActionWithoutDirectPitchOrAnimationWrites()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.FluteBlock.cs");

        Assert.Contains("StartFluteBlockTuning(pending);", all);
        Assert.Contains("ComputeFluteBlockNextPitch(block.preservedParentSheetIndex.Value", source);
        Assert.DoesNotMatch(@"preservedParentSheetIndex\.Value\s*=(?!=)", source);
        Assert.DoesNotMatch(@"shakeTimer\s*=\s*200", source);
        Assert.DoesNotMatch(@"scale\.Y\s*=\s*1\.3", source);
        Assert.DoesNotContain("farmerAdjacentAction(", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("world.tune_flute_block"));
    }
}
