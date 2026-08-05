namespace StardewAI.Core.Tests;

public sealed class RuntimePurchaseMainlineSmokeSourceGuardTests
{
    [Fact]
    public void SmokeUsesProductionPurchaseChainAndExactCompletionGate()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimePurchaseMainlineSmoke.ps1"));

        Assert.Contains(
            "--daily-plan-candidate-options economy.buy_supplies",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "--stop-after-objective-complete",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$report.objective_completed -eq $true",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$queueOptionIds -contains \"executor.traverse_connector\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$queueOptionIds -contains \"executor.interact\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$queueOptionIds -contains \"executor.buy_shop_item\"",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}
