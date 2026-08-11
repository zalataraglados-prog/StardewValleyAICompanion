using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.FarmAnimals;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static object[] ReadAnimalPurchaseCatalog()
    {
        var targetLocations = Game1.locations
            .Where(location =>
                location.buildings.Any(building => building.GetIndoors() is AnimalHouse) &&
                (!Game1.IsClient || location.CanBeRemotedlyViewed()))
            .ToList();

        if (targetLocations.Count == 0)
        {
            targetLocations.Add(Game1.getFarm());
        }

        return targetLocations
            .Select((location, index) => ReadAnimalPurchaseLocation(location, index))
            .Cast<object>()
            .ToArray();
    }

    private static object ReadAnimalPurchaseLocation(GameLocation location, int nativeChoiceIndex)
    {
        var homes = location.buildings
            .Where(building => building.GetIndoors() is AnimalHouse)
            .OrderBy(building => building.tileY.Value)
            .ThenBy(building => building.tileX.Value)
            .ThenBy(building => building.buildingType.Value, StringComparer.Ordinal)
            .ToArray();

        var stock = Utility.getPurchaseAnimalStock(location)
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item =>
            {
                var baseTypeId = item.Name;
                Game1.farmAnimalData.TryGetValue(baseTypeId, out var baseData);
                var possibleTypeIds = ReadPossiblePurchasedAnimalTypes(baseTypeId, baseData);
                var compatibleHomes = homes
                    .Select(building => ReadAnimalPurchaseHome(location, building, possibleTypeIds))
                    .Where(home => home.compatible_with_all_possible_types)
                    .ToArray();
                var generatedName = GenerateUniqueAnimalName(baseTypeId);
                var price = item.salePrice();
                var requiredBuildingMet = item.Type is null;
                var hasCapacity = compatibleHomes.Any(home => home.available_slots > 0 && !home.is_under_construction);
                var canAfford = Game1.player.Money >= price;
                var identity = ComputeAnimalPurchaseIdentity(
                    location.NameOrUniqueName,
                    baseTypeId,
                    possibleTypeIds,
                    price,
                    generatedName,
                    compatibleHomes);

                return new
                {
                    animal_type_id = baseTypeId,
                    display_name = FarmAnimal.GetDisplayName(baseTypeId, forShop: true),
                    possible_actual_type_ids = possibleTypeIds,
                    price,
                    unlock_condition = baseData?.UnlockCondition,
                    required_building = baseData?.RequiredBuilding,
                    required_house_type = baseData?.House,
                    required_building_met = requiredBuildingMet,
                    blocked_description = item.Type,
                    player_money = Game1.player.Money,
                    can_afford = canAfford,
                    generated_unique_name = generatedName,
                    compatible_homes = compatibleHomes,
                    compatible_home_count = compatibleHomes.Length,
                    compatible_home_with_capacity_count = compatibleHomes.Count(home => home.available_slots > 0 && !home.is_under_construction),
                    purchase_ready = requiredBuildingMet && canAfford && hasCapacity,
                    candidate_identity_sha256 = identity
                };
            })
            .Cast<object>()
            .ToArray();

        return new
        {
            target_location_id = location.NameOrUniqueName,
            target_location_display_name = location.DisplayName,
            native_location_choice_index = nativeChoiceIndex,
            native_auto_selects_location = Game1.locations.Count(candidate =>
                candidate.buildings.Any(building => building.GetIndoors() is AnimalHouse) &&
                (!Game1.IsClient || candidate.CanBeRemotedlyViewed())) <= 1,
            animal_house_count = homes.Length,
            stock
        };
    }

    private static string[] ReadPossiblePurchasedAnimalTypes(string baseTypeId, FarmAnimalData? data)
    {
        if (data?.AlternatePurchaseTypes is not null)
        {
            foreach (var alternate in data.AlternatePurchaseTypes)
            {
                if (GameStateQuery.CheckConditions(alternate.Condition) && alternate.AnimalIds is { Count: > 0 })
                {
                    return alternate.AnimalIds
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToArray();
                }
            }
        }

        return new[] { baseTypeId };
    }

    private static AnimalPurchaseHomeRow ReadAnimalPurchaseHome(
        GameLocation location,
        Building building,
        IReadOnlyCollection<string> possibleTypeIds)
    {
        var house = (AnimalHouse)building.GetIndoors()!;
        var validOccupantTypes = building.GetData()?.ValidOccupantTypes?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        var requiredHouseTypes = possibleTypeIds
            .Select(typeId => Game1.farmAnimalData.TryGetValue(typeId, out var data) ? data.House : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var compatible = !building.isUnderConstruction() && possibleTypeIds.All(typeId =>
            new FarmAnimal(typeId, -1, Game1.player.UniqueMultiplayerID).CanLiveIn(building));
        var occupants = house.animalsThatLiveHere.Count;
        var capacity = house.animalLimit.Value;

        return new AnimalPurchaseHomeRow(
            location.NameOrUniqueName,
            building.buildingType.Value,
            building.tileX.Value,
            building.tileY.Value,
            house.NameOrUniqueName,
            building.isUnderConstruction(),
            validOccupantTypes,
            requiredHouseTypes,
            compatible,
            occupants,
            capacity,
            Math.Max(0, capacity - occupants));
    }

    private static string GenerateUniqueAnimalName(string baseTypeId)
    {
        var prefix = "AI " + FarmAnimal.GetDisplayName(baseTypeId, forShop: false).Trim();
        for (var ordinal = 1; ordinal < 10000; ordinal++)
        {
            var candidate = prefix + " " + ordinal;
            if (!Utility.areThereAnyOtherAnimalsWithThisName(candidate))
            {
                return candidate;
            }
        }

        return "AI Animal " + Game1.player.UniqueMultiplayerID;
    }

    private static string ComputeAnimalPurchaseIdentity(
        string locationId,
        string baseTypeId,
        IEnumerable<string> possibleTypeIds,
        int price,
        string generatedName,
        IEnumerable<AnimalPurchaseHomeRow> homes)
    {
        var source = new StringBuilder()
            .Append(locationId).Append('|')
            .Append(baseTypeId).Append('|')
            .Append(string.Join(",", possibleTypeIds)).Append('|')
            .Append(price).Append('|')
            .Append(Game1.player.Money).Append('|')
            .Append(generatedName);
        foreach (var home in homes)
        {
            source.Append('|')
                .Append(home.location_id).Append(':')
                .Append(home.building_type).Append(':')
                .Append(home.building_tile_x).Append(',').Append(home.building_tile_y).Append(':')
                .Append(home.occupant_count).Append('/').Append(home.capacity).Append(':')
                .Append(string.Join(",", home.valid_occupant_types));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString()))).ToLowerInvariant();
    }

    private sealed record AnimalPurchaseHomeRow(
        string location_id,
        string building_type,
        int building_tile_x,
        int building_tile_y,
        string indoor_location_id,
        bool is_under_construction,
        string[] valid_occupant_types,
        string[] required_house_types,
        bool compatible_with_all_possible_types,
        int occupant_count,
        int capacity,
        int available_slots);
}
