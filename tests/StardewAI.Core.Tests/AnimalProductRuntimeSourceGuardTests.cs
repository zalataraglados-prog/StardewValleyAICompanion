namespace StardewAI.Core.Tests;

public sealed class AnimalProductRuntimeSourceGuardTests
{
    private static readonly string RuntimeSource = RuntimeHarnessSources.All;
    private static readonly string SmokeSource = File.ReadAllText(
        FindRepositoryFile("scripts", "Invoke-RuntimeAnimalProductSmoke.ps1"));

    [Fact]
    public void ProductionExecutorUsesNativeToolLifecycleWithoutDirectOutcomeMutation()
    {
        var source = Slice(
            RuntimeSource,
            "private void StartAnimalProductHarvest",
            "private static bool TryParseAnimalStatIncrements");

        Assert.Contains("Game1.player.BeginUsingTool()", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.EndUsingTool()", source, StringComparison.Ordinal);
        Assert.Contains("animal.CanGetProduceWithTool(tool)", source, StringComparison.Ordinal);
        Assert.Contains("tool.GetType() != typeof(MilkPail)", source, StringComparison.Ordinal);
        Assert.Contains("tool.GetType() != typeof(Shears)", source, StringComparison.Ordinal);
        Assert.Contains("ProjectAnimalProductStatAmountAfterInventoryInsert(output)", source, StringComparison.Ordinal);
        Assert.Contains("stat.Amount != nativeStatIncrementAmount", source, StringComparison.Ordinal);
        Assert.DoesNotContain("currentProduce.Value = null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("addItemToInventoryBool", source, StringComparison.Ordinal);
        Assert.DoesNotContain("gainExperience", source, StringComparison.Ordinal);
        Assert.DoesNotContain("friendshipTowardFarmer.Value =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputRecordsExactUnitStateAndEveryProjectedNativeStat()
    {
        var source = Slice(
            RuntimeSource,
            "private void CompleteAnimalProduct",
            "private void CompleteAnimalProductBlocked");

        Assert.Contains("output_unit_state_sha256=", source, StringComparison.Ordinal);
        Assert.Contains("output_quality=", source, StringComparison.Ordinal);
        Assert.Contains("stats.", source, StringComparison.Ordinal);
        Assert.Contains("changedFacts.AddRange(active.StatIncrements", source, StringComparison.Ordinal);
        Assert.Contains("friendship_delta=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentProjectionModelsNativePostMergeStackStatQuirk()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "FarmReadAdapter.Entities.cs"));

        Assert.Contains("ProjectAnimalProduceStatAmountAfterInventoryInsert", source, StringComparison.Ordinal);
        Assert.Contains("output.canStackWith(existing)", source, StringComparison.Ordinal);
        Assert.Contains("existing.getRemainingStackSpace()", source, StringComparison.Ordinal);
        Assert.Contains("harvest_native_stat_increment_amount", source, StringComparison.Ordinal);
        Assert.Contains("friendshipAfterHarvest - animal.friendshipTowardFarmer.Value", source, StringComparison.Ordinal);
        Assert.DoesNotContain("amount = output.Stack", source, StringComparison.Ordinal);
        Assert.DoesNotContain("harvest_friendship_delta = 5", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeCoversBothToolsWithAndWithoutAnimalCracker()
    {
        Assert.Contains("Invoke-AnimalCase \"Milk Pail\" \"(O)184\" 2 1", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Invoke-AnimalCase \"Milk Pail\" \"(O)184\" 2 2", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Invoke-AnimalCase \"Shears\" \"(O)440\" 1 1", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Invoke-AnimalCase \"Shears\" \"(O)440\" 1 2", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("expected_case_count = 4", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("EVD-222", SmokeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeRefusesExistingRuntimeOrListeners()
    {
        Assert.Contains("Get-NetTCPConnection", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Get-Process -Name \"StardewModdingAPI\"", SmokeSource, StringComparison.Ordinal);
        Assert.Contains("Refusing to attach", SmokeSource, StringComparison.Ordinal);
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}
