using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Tests;

public sealed class CollectionDenominatorTransparencyTests
{
    [Fact]
    public void FishProgressSerializesCompleteNativeDenominatorEvidence()
    {
        var progress = new FishCollectionProgressRef
        {
            EligibleSpeciesCount = 72,
            CaughtEligibleSpeciesCount = 1,
            MissingSpeciesCount = 71,
            CompletionRatio = 1d / 72d,
            Complete = false,
            Items = new[]
            {
                new FishCollectionItemProgressRef
                {
                    ItemId = "128",
                    QualifiedItemId = "(O)128",
                    DisplayName = "Pufferfish",
                    Caught = true,
                    CaughtCount = 1,
                    MaxSize = 36
                }
            },
            MissingItemIds = new[] { "129" }
        };

        var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"eligible_species_count\":72", json, StringComparison.Ordinal);
        Assert.Contains("\"caught\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"missing_item_ids\":[\"129\"]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MuseumProgressSerializesEveryDonatableItemAndMissingItem()
    {
        var progress = new MuseumProgressRef
        {
            DonatedCount = 1,
            TotalDonatableItems = 95,
            MissingItemCount = 94,
            DonatableItems = new[]
            {
                new MuseumCollectionItemProgressRef
                {
                    ItemId = "96",
                    QualifiedItemId = "(O)96",
                    DisplayName = "Dwarf Scroll I",
                    ObjectType = "Arch",
                    Donated = false
                }
            },
            MissingItemIds = new[] { "96" }
        };

        var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"total_donatable_items\":95", json, StringComparison.Ordinal);
        Assert.Contains("\"donatable_items\"", json, StringComparison.Ordinal);
        Assert.Contains("\"missing_item_ids\":[\"96\"]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterUsesNativeCollectionRulesWithoutProgressWrites()
    {
        var main = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.cs"));
        var collection = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.CollectionProgress.cs"));

        Assert.Contains("fish_collection_progress", main, StringComparison.Ordinal);
        Assert.Contains("ExcludeFromFishingCollection == false", collection, StringComparison.Ordinal);
        Assert.Contains("master.fishCaught.TryGetValue(item.QualifiedItemId", collection, StringComparison.Ordinal);
        Assert.Contains("LibraryMuseum.IsItemSuitableForDonation", collection, StringComparison.Ordinal);
        Assert.Contains("checkDonatedItems: false", collection, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(collectionCatalogSource, objectData)", collection, StringComparison.Ordinal);
        Assert.DoesNotContain("fishCaught.Add", collection, StringComparison.Ordinal);
        Assert.DoesNotContain("museumPieces.Add", collection, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }
}
