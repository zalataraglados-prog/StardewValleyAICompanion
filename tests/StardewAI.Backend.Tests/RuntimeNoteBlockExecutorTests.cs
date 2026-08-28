using StardewAI.Contracts.Capabilities;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeNoteBlockExecutorTests
{
    [Fact]
    public void FluteAndDrumShareOneNativeTuningStateMachineWithoutDirectWrites()
    {
        var all = RuntimeHarnessSources.All;
        var shared = RuntimeHarnessSources.File("ModEntry.NoteBlockTuning.cs");

        Assert.Contains("StartFluteBlockTuning(pending);", all);
        Assert.Contains("StartDrumBlockTuning(pending);", all);
        Assert.Contains("AdvanceNativeObjectInteractionMovement(active, active.Profile.ReasonPrefix", shared);
        Assert.Equal(1, Count(shared, "active.Location.checkAction("));
        Assert.Contains("active.Block.preservedParentSheetIndex.Value", shared);
        Assert.Contains("active.Block.shakeTimer", shared);
        Assert.Contains("active.Block.scale.Y", shared);
        Assert.DoesNotMatch(@"preservedParentSheetIndex\.Value\s*=(?!=)", shared);
        Assert.DoesNotMatch(@"shakeTimer\s*=\s*200", shared);
        Assert.DoesNotMatch(@"scale\.Y\s*=\s*1\.3", shared);
        Assert.DoesNotContain("farmerAdjacentAction(", shared);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("world.tune_flute_block"));
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("world.tune_drum_block"));
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
