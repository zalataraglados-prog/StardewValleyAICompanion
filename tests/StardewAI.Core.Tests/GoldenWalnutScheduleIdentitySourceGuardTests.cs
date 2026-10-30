namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutScheduleIdentitySourceGuardTests
{
    [Fact]
    public void ScheduleBuilderBindsEveryEpisodeToReadableInitialPlayer()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.Core",
            "Infrastructure",
            "GoldenWalnutFreshSaveEpisodeScheduleBuilder.cs"));

        Assert.Contains(
            "initialSnapshot.PlayerId.Status == FieldStatus.Available",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "!string.IsNullOrWhiteSpace(initialSnapshot.PlayerId.Value)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "var playerId = initialSnapshot.PlayerId.Value",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "before.PlayerId.Status == FieldStatus.Available",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "after.PlayerId.Status == FieldStatus.Available",
            source,
            StringComparison.Ordinal);
        Assert.Contains("before.PlayerId.Value == playerId", source, StringComparison.Ordinal);
        Assert.Contains("after.PlayerId.Value == playerId", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate repository file: " + Path.Combine(parts));
    }
}
