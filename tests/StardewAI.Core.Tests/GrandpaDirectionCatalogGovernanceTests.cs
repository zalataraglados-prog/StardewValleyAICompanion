using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class GrandpaDirectionCatalogGovernanceTests
{
    [Fact]
    public void EveryDirectionHasOneGovernedProgressionFamily()
    {
        var entries = GrandpaDirectionCatalog.Entries;

        Assert.Equal(11, entries.Length);
        Assert.All(entries, entry =>
            Assert.False(string.IsNullOrWhiteSpace(
                entry.ProgressionFamilyId)));

        var families = entries.GroupBy(
                entry => entry.ProgressionFamilyId,
                StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(10, families.Length);

        var shared = Assert.Single(families.Where(group =>
            group.Count() > 1));
        Assert.Equal("progression.museum.donations", shared.Key);
        Assert.Equal(
            new[] { "complete_museum_collection", "obtain_rusty_key" },
            shared.Select(entry => entry.DirectionId)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }
}
