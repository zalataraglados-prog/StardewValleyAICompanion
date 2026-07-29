namespace StardewAI.Core.Tests;

public sealed class RuntimeHarnessAutoLoadSourceGuardTests
{
    [Fact]
    public void ExplicitTestSlotDefaultsToAutoLoadUnlessCallerOverridesIt()
    {
        var source = RuntimeHarnessSources.All;
        var explicitOverride = source.IndexOf(
            "if (bool.TryParse(autoLoad, out var autoLoadEnabled))",
            StringComparison.Ordinal);
        var slotFallback = source.IndexOf(
            "else if (!string.IsNullOrWhiteSpace(slotName))",
            StringComparison.Ordinal);
        var enableAutoLoad = source.IndexOf(
            "config.AutoLoad = true;",
            slotFallback,
            StringComparison.Ordinal);

        Assert.True(explicitOverride >= 0);
        Assert.True(slotFallback > explicitOverride);
        Assert.True(enableAutoLoad > slotFallback);
    }
}
