namespace StardewAI.Core.Tests;

public sealed class DeconstructorMachineSemanticsSourceGuardTests
{
    [Fact]
    public void DeconstructorUsesNarrowVettedNativeProbe()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.DeconstructorPrediction.cs"));

        Assert.Contains("deconstructor_recipe_recovery.v1", source);
        Assert.Contains(
            "machine.QualifiedItemId == DeconstructorQualifiedItemId",
            source);
        Assert.Contains(
            "machine.GetType() == typeof(StardewValley.Object)",
            source);
        Assert.Contains(": OutputDeconstructor", source);
        Assert.Contains(
            "StardewValley.Object.OutputDeconstructor(",
            source);
        Assert.Contains("probe: true", source);
        Assert.Contains(
            "live_CraftingRecipe.craftingRecipes",
            source);
        Assert.DoesNotContain("OutputAnvil", source);
        Assert.DoesNotContain("OutputGeodeCrusher", source);
        Assert.DoesNotContain("OutputSeedMaker", source);
        Assert.DoesNotContain("OutputMushroomLog", source);
    }

    private static string FindRepositoryFile(
        params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                new[] { current.FullName }
                    .Concat(segments)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Repository file not found: " +
            Path.Combine(segments));
    }
}
