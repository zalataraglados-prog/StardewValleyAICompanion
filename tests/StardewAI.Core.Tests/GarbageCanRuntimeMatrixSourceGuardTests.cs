namespace StardewAI.Core.Tests;

public sealed class GarbageCanRuntimeMatrixSourceGuardTests
{
    [Fact]
    public void FixtureAndSmokeCoverNativeBranchesExclusionsAndTypedRequest()
    {
        var root = FindRepositoryRoot();
        var fixture = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.GarbageCanFixture.cs"));
        var runtime = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.GarbageCans.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeForageTaskSourceSmoke.ps1"));

        foreach (var profile in new[]
                 {
                     "ordinary_output", "no_output", "direct_inventory_hat", "desert_multiple",
                     "already_checked", "negative_witness", "linus_witness"
                 })
        {
            Assert.Contains(profile, fixture, StringComparison.Ordinal);
            Assert.Contains(profile, smoke, StringComparison.Ordinal);
        }
        Assert.Contains("executor.rummage_garbage", smoke, StringComparison.Ordinal);
        Assert.Contains("expected_output_json", smoke, StringComparison.Ordinal);
        Assert.Contains("reacting_npc_json", smoke, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("CheckedGarbage", runtime, StringComparison.Ordinal);
        Assert.Contains("trashCansChecked", runtime, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root.");
    }
}
