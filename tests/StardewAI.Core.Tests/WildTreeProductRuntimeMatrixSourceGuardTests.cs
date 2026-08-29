namespace StardewAI.Core.Tests;

public sealed class WildTreeProductRuntimeMatrixSourceGuardTests
{
    [Fact]
    public void FixtureAndSmokeCoverNativeBranchesAndExclusions()
    {
        var root = FindRepositoryRoot();
        var fixture = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.ForageSourceFixture.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeForageTaskSourceSmoke.ps1"));
        foreach (var profile in new[] { "ordinary_seed", "fall_hazelnut", "island_palm", "no_seed", "active_shake", "tapped" })
        {
            Assert.Contains(profile, fixture, StringComparison.Ordinal);
            Assert.Contains(profile, smoke, StringComparison.Ordinal);
        }
        Assert.Contains("executor.harvest_tree_product", smoke, StringComparison.Ordinal);
        Assert.Contains("tree_product_output_domain_json", smoke, StringComparison.Ordinal);
        Assert.Contains("safe_slot_index", smoke, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root.");
    }
}
