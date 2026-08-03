using Microsoft.Xna.Framework;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static SpawnedObjectHarvestProjection ReadSpawnedObjectHarvest(
        GameLocation location,
        Vector2 tile,
        StardewObject item,
        Farmer player)
    {
        if (!item.IsSpawnedObject)
        {
            return SpawnedObjectHarvestProjection.NotApplicable();
        }
        if (item.GetType() != typeof(StardewObject))
        {
            return SpawnedObjectHarvestProjection.Blocked("blocked_custom_spawned_object_runtime_type");
        }
        if (ItemRegistry.GetDataOrErrorItem(item.QualifiedItemId).IsErrorItem)
        {
            return SpawnedObjectHarvestProjection.Blocked("blocked_error_item_identity");
        }
        if (string.IsNullOrWhiteSpace(item.Type))
        {
            return SpawnedObjectHarvestProjection.Blocked("blocked_spawned_object_type_missing");
        }
        if (item.Stack != 1)
        {
            return SpawnedObjectHarvestProjection.Blocked("blocked_spawned_object_stack_semantics_unsupported");
        }
        if (string.Equals(location.NameOrUniqueName, "LewisBasement", StringComparison.Ordinal) &&
            string.Equals(item.ItemId, "789", StringComparison.Ordinal))
        {
            return SpawnedObjectHarvestProjection.Blocked("blocked_unprojected_lewis_basement_789_side_effect");
        }
        if (item.questItem.Value &&
            !string.IsNullOrWhiteSpace(item.questId.Value) &&
            item.questId.Value != "0" &&
            !player.hasQuest(item.questId.Value))
        {
            return SpawnedObjectHarvestProjection.Blocked("blocked_required_quest_not_active");
        }

        var forage = item.isForage();
        var random = Utility.CreateDaySaveRandom(tile.X, tile.Y * 777f);
        var quality = forage
            ? location.GetHarvestSpawnedObjectQuality(player, true, tile, random)
            : item.Quality;
        var primary = (StardewObject)item.getOne();
        primary.Quality = quality;
        primary.Stack = 1;
        if (!player.couldInventoryAcceptThisItem(primary))
        {
            return SpawnedObjectHarvestProjection.Blocked("blocked_inventory_cannot_accept_primary_item", quality);
        }

        var farmBuildingInterior = location.isFarmBuildingInterior();
        var gathererRoll = player.professions.Contains(13) && random.NextDouble() < 0.2;
        var twoItems = (StardewObject)primary.getOne();
        twoItems.Stack = 2;
        var gathererDuplicate = gathererRoll &&
            !item.questItem.Value &&
            !farmBuildingInterior &&
            player.couldInventoryAcceptThisItem(twoItems);

        var foragingExperience = 0;
        var farmingExperience = 0;
        var basis = "native_spawned_non_forage_pickup";
        if (farmBuildingInterior)
        {
            farmingExperience = 5;
            basis = "native_farm_building_interior_spawned_object_pickup";
        }
        else if (forage && item.SpecialVariable == 724519)
        {
            foragingExperience = 2;
            farmingExperience = 3;
            basis = "native_special_724519_forage_pickup";
        }
        else if (forage)
        {
            foragingExperience = 7;
            basis = "native_spawned_forage_pickup";
        }
        if (gathererDuplicate)
        {
            foragingExperience += 7;
            basis += "+native_gatherer_duplicate";
        }

        return new SpawnedObjectHarvestProjection
        {
            Status = "ready",
            Quality = quality,
            PrimaryQuantity = 1,
            GathererDuplicate = gathererDuplicate,
            TotalQuantity = gathererDuplicate ? 2 : 1,
            ForagingExperience = foragingExperience,
            FarmingExperience = farmingExperience,
            ExperienceStatus = "exact",
            ExperienceBasis = basis
        };
    }
}

internal sealed class SpawnedObjectHarvestProjection
{
    public string Status { get; init; } = string.Empty;

    public int? Quality { get; init; }

    public int? PrimaryQuantity { get; init; }

    public bool? GathererDuplicate { get; init; }

    public int? TotalQuantity { get; init; }

    public int? ForagingExperience { get; init; }

    public int? FarmingExperience { get; init; }

    public string ExperienceStatus { get; init; } = string.Empty;

    public string ExperienceBasis { get; init; } = string.Empty;

    public static SpawnedObjectHarvestProjection Blocked(string status, int? quality = null)
    {
        return new SpawnedObjectHarvestProjection
        {
            Status = status,
            Quality = quality,
            ExperienceStatus = "unavailable"
        };
    }

    public static SpawnedObjectHarvestProjection NotApplicable()
    {
        return new SpawnedObjectHarvestProjection
        {
            Status = "not_applicable",
            ExperienceStatus = "not_applicable"
        };
    }
}
