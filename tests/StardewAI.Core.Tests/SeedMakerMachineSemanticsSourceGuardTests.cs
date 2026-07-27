namespace StardewAI.Core.Tests;

public sealed class SeedMakerMachineSemanticsSourceGuardTests
{
    [Fact]
    public void SeedMakerUsesNarrowDeterministicNativePrediction()
    {
        var modelSource = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.SeedMakerPrediction.cs"));
        var dispatchSource = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.SpecialMachinePrediction.cs"));
        var smokeSource = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeMachineInputSmoke.ps1"));

        Assert.Contains(
            "seed_maker_day_save_rng.v1",
            modelSource);
        Assert.Contains(
            "machine.QualifiedItemId == SeedMakerQualifiedItemId",
            modelSource);
        Assert.Contains(
            "machine.GetType() == typeof(StardewValley.Object)",
            modelSource);
        Assert.Contains(": OutputSeedMaker", modelSource);
        Assert.Contains(
            "StardewValley.Object.OutputSeedMaker(",
            modelSource);
        Assert.Contains("probe: true", modelSource);
        Assert.Contains(
            "vetted_native_probe_uses_fresh_Utility_CreateDaySaveRandom",
            modelSource);
        Assert.Contains(
            "Game1.uniqueIDForThisGame / 2",
            modelSource);
        Assert.Contains(
            "seed_maker_ready_time_modifiers_not_modeled",
            modelSource);
        Assert.DoesNotContain("Game1.random", modelSource);
        Assert.DoesNotContain("OutputAnvil", modelSource);
        Assert.DoesNotContain("OutputGeodeCrusher", modelSource);

        Assert.Contains(
            "IsVettedSeedMakerOutputMethod(",
            dispatchSource);
        Assert.Contains(
            "TryReadSeedMakerPrediction(",
            dispatchSource);
        Assert.Contains(
            "return SeedMakerPredictionModelId;",
            dispatchSource);
        Assert.Contains(
            "$afterHeld -ne $predictedOutputItemId",
            smokeSource);
        Assert.Contains(
            "Vetted special-machine prediction was",
            smokeSource);
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
