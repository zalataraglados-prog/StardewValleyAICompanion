namespace StardewAI.Core.Tests;

public sealed class MachineOutputRouteSourceGuardTests
{
    [Fact]
    public void OrdinaryMachineSourcesPreferPersistedRuleAndBoundLegacyRows()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.MachineOutputSources.cs");

        Assert.Contains("machine.lastOutputRuleId.Value", bridge,
            StringComparison.Ordinal);
        Assert.Contains("selectedRules.Length != 1", bridge,
            StringComparison.Ordinal);
        Assert.Contains("route_kind = \"machine_output\"", bridge,
            StringComparison.Ordinal);
        Assert.Contains("ReadLegacyMachineOutputRowSources", bridge,
            StringComparison.Ordinal);
        Assert.Contains("native_machine_flavored_output", bridge,
            StringComparison.Ordinal);
        Assert.Contains("native_machine_item_query_output", bridge,
            StringComparison.Ordinal);
        Assert.Contains("FlavoredMachineOutputItemIds", bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random", bridge,
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
