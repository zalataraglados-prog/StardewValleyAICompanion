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
        Assert.Contains("AdvanceNativeObjectInteractionMovement(active, \"flute_block\"", source);
        Assert.Equal(1, Count(source, "active.Location.checkAction("));
        Assert.Contains("active.Computer.preservedParentSheetIndex.Value", source);
        Assert.Contains("active.Computer.shakeTimer", source);
        Assert.Contains("active.Computer.scale.Y", source);
        Assert.Contains("ComputeFluteBlockNextPitch(block.preservedParentSheetIndex.Value", source);
        Assert.DoesNotMatch(@"preservedParentSheetIndex\.Value\s*=(?!=)", source);
        Assert.DoesNotMatch(@"shakeTimer\s*=\s*200", source);
        Assert.DoesNotMatch(@"scale\.Y\s*=\s*1\.3", source);
        Assert.DoesNotContain("farmerAdjacentAction(", source);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("world.tune_flute_block"));
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
