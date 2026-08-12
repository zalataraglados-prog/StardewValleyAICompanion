namespace StardewAI.Backend.Tests;

public sealed class RuntimeBuildingConstructionExecutorTests
{
    [Fact]
    public void RuntimeDispatchUsesNativeCarpenterMenuAndDoesNotDirectlyBuildStructure()
    {
        var source = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "ModEntry.BuildingConstruction.cs"));
        Assert.Contains("answerDialogue(response)", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("menu.SetNewActiveBlueprint", source, StringComparison.Ordinal);
        Assert.Contains("ExpectedBuildingServiceAction(request)", source, StringComparison.Ordinal);
        Assert.Contains("request.PlacementLocationId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("buildStructure(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("questComplete(", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
