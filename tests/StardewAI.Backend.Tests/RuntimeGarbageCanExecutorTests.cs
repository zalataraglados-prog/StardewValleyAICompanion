namespace StardewAI.Backend.Tests;

public sealed class RuntimeGarbageCanExecutorTests
{
    [Fact]
    public void RuntimeUsesOneNativeInteractionAndVerifiesAllDurableEffects()
    {
        var allSources = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.GarbageCans.cs");

        Assert.Contains("StartGarbageCanRummage(pending);", allSources);
        Assert.Contains("active.Location.checkAction(", source);
        Assert.Contains("CheckedGarbage.Contains(active.Projection.Id)", source);
        Assert.Contains("Game1.stats.Get(\"trashCansChecked\")", source);
        Assert.Contains("CaptureGarbageOutputs", source);
        Assert.Contains("npc_friendship_branch_verified", source);
        Assert.Contains("safe_empty_slot_restored", source);
        Assert.DoesNotContain("CheckedGarbage.Add(active.GarbageCanId)", source);
        Assert.DoesNotContain("Game1.stats.Set(\"trashCansChecked\"", source);
    }
}
