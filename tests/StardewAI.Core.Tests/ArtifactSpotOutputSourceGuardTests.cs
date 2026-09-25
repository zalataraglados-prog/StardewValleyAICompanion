namespace StardewAI.Core.Tests;

public sealed class ArtifactSpotOutputSourceGuardTests
{
    [Fact]
    public void ArtifactSourcesFollowNativeLocationAndObjectDataRows()
    {
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "CurrentLocationReadAdapter.ArtifactSpots.cs");
        var candidate = ReadRepositoryFile(
            "src",
            "StardewAI.Core",
            "OptionRegistry",
            "CandidateOptionAvailabilityEvaluator.ClearQuest.cs");

        Assert.Contains(
            "location:Default:",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResolveCurrentLocationDataId",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_location_artifact_spot",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_object_artifact_spot_chance",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "ArtifactSpotChances.TryGetValue",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "RANDOM_ARTIFACT_FOR_DIG_SPOT",
            bridge,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random", bridge, StringComparison.Ordinal);
        Assert.Contains(
            "clear_authoritative_route_sources",
            candidate,
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
            "Repository file not found: " +
            Path.Combine(segments));
    }
}
