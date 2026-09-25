namespace StardewAI.Core.Tests;

public sealed class MiningMonsterRouteSourceGuardTests
{
    [Fact]
    public void MonsterSourcesRequireExactNativeProbabilityRowsAndSelectedRuntimeIdentity()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MiningReadAdapter.MonsterRouteSources.cs");
        var core = ReadRepositoryFile(
            "src",
            "StardewAI.Core",
            "OptionRegistry",
            "MiningAuthoritativeRouteSourceBinding.cs");

        Assert.Contains("drops.DropProbabilityRules", bridge,
            StringComparison.Ordinal);
        Assert.Contains("GameLocation.monsterDrop/Data/Monsters", bridge,
            StringComparison.Ordinal);
        Assert.Contains("native_monster_drop_table", bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("selected_drop_qualified_item_ids", bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random", bridge,
            StringComparison.Ordinal);

        Assert.Contains("floorStep.TargetRuntimeIdentity", core,
            StringComparison.Ordinal);
        Assert.Contains("floorStep.TargetName", core,
            StringComparison.Ordinal);
        Assert.Contains("floorStep.CombatTerminalState", core,
            StringComparison.Ordinal);
        Assert.Contains("matches.Length != 1", core,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }
                    .Concat(segments)
                    .ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        throw new FileNotFoundException(
            "Repository file not found: " + Path.Combine(segments));
    }
}
