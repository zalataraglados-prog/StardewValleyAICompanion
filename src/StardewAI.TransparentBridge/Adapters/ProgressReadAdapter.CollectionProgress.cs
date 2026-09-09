using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static object? collectionCatalogSource;
    private static CollectionCatalogItem[] fishCollectionCatalog = Array.Empty<CollectionCatalogItem>();
    private static CollectionCatalogItem[] museumCollectionCatalog = Array.Empty<CollectionCatalogItem>();

    private static FishCollectionProgressRef? ReadFishCollectionProgress(Farmer? master)
    {
        var objectData = Game1.objectData;
        if (master is null || objectData is null)
        {
            return null;
        }

        EnsureCollectionCatalogs(objectData);
        var items = fishCollectionCatalog
            .Select(item =>
            {
                var caught = master.fishCaught.TryGetValue(item.QualifiedItemId, out var values);
                return new FishCollectionItemProgressRef
                {
                    ItemId = item.ItemId,
                    QualifiedItemId = item.QualifiedItemId,
                    DisplayName = item.DisplayName,
                    Caught = caught,
                    CaughtCount = caught && values is { Length: > 0 } ? values[0] : 0,
                    MaxSize = caught && values is { Length: > 1 } ? values[1] : 0
                };
            })
            .ToArray();
        var caughtCount = items.Count(item => item.Caught);
        var total = items.Length;

        return new FishCollectionProgressRef
        {
            EligibleSpeciesCount = total,
            CaughtEligibleSpeciesCount = caughtCount,
            MissingSpeciesCount = total - caughtCount,
            CompletionRatio = total > 0 ? (double)caughtCount / total : 0,
            Complete = total > 0 && caughtCount == total,
            Items = items,
            MissingItemIds = items
                .Where(item => !item.Caught)
                .Select(item => item.ItemId)
                .OrderBy(itemId => itemId, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static MuseumCollectionItemProgressRef[] ReadMuseumCollectionItems(LibraryMuseum museum)
    {
        var objectData = Game1.objectData;
        if (objectData is null)
        {
            return Array.Empty<MuseumCollectionItemProgressRef>();
        }

        EnsureCollectionCatalogs(objectData);
        var donatedItemIds = museum.museumPieces.Pairs
            .Select(pair => pair.Value)
            .ToHashSet(StringComparer.Ordinal);

        return museumCollectionCatalog
            .Select(item => new MuseumCollectionItemProgressRef
            {
                ItemId = item.ItemId,
                QualifiedItemId = item.QualifiedItemId,
                DisplayName = item.DisplayName,
                ObjectType = item.ObjectType,
                Donated = donatedItemIds.Contains(item.ItemId)
            })
            .ToArray();
    }

    private static void EnsureCollectionCatalogs(
        IDictionary<string, StardewValley.GameData.Objects.ObjectData> objectData)
    {
        if (ReferenceEquals(collectionCatalogSource, objectData))
        {
            return;
        }

        var all = objectData
            .Where(pair => pair.Value is not null)
            .Select(pair => new
            {
                Data = pair.Value,
                Item = new CollectionCatalogItem(
                    pair.Key,
                    ItemRegistry.QualifyItemId(pair.Key) ?? "(O)" + pair.Key,
                    pair.Value.DisplayName ?? pair.Value.Name ?? pair.Key,
                    pair.Value.Type ?? string.Empty)
            })
            .ToArray();
        fishCollectionCatalog = all
            .Where(value => value.Item.ObjectType == "Fish" &&
                value.Data.ExcludeFromFishingCollection == false)
            .Select(value => value.Item)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .ThenBy(item => item.QualifiedItemId, StringComparer.Ordinal)
            .ToArray();
        museumCollectionCatalog = all
            .Where(value => LibraryMuseum.IsItemSuitableForDonation(
                value.Item.QualifiedItemId,
                checkDonatedItems: false))
            .Select(value => value.Item)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .ThenBy(item => item.QualifiedItemId, StringComparer.Ordinal)
            .ToArray();
        collectionCatalogSource = objectData;
    }

    private sealed record CollectionCatalogItem(
        string ItemId,
        string QualifiedItemId,
        string DisplayName,
        string ObjectType);
}
