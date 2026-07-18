using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

internal static class MachineLocationTopology
{
    public static MachineLocationRef[] ReadPersistentLocations(Farm farm, Farmer? player)
    {
        var locations = new List<GameLocation>();
        Utility.ForEachLocation(location =>
        {
            if (location is not null)
            {
                locations.Add(location);
            }
            return true;
        }, includeInteriors: true, includeGenerated: false);

        locations.Add(farm);
        var home = player is null ? null : Utility.getHomeOfFarmer(player);
        if (home is not null)
        {
            locations.Add(home);
            var cellarName = home.GetCellarName();
            if (!string.IsNullOrWhiteSpace(cellarName) && Game1.getLocationFromName(cellarName) is { } cellar)
            {
                locations.Add(cellar);
            }
        }

        return locations
            .Where(location => !string.IsNullOrWhiteSpace(location.NameOrUniqueName))
            .GroupBy(location => location.NameOrUniqueName, StringComparer.OrdinalIgnoreCase)
            .Select(group => Classify(group.First(), farm, home))
            .OrderBy(location => location.Location.NameOrUniqueName, StringComparer.Ordinal)
            .ToArray();
    }

    private static MachineLocationRef Classify(GameLocation location, Farm farm, FarmHouse? home)
    {
        var root = location.GetRootLocation();
        var isFarmRoot = SameLocation(location, farm) || SameLocation(root, farm) || location.IsFarm || root.IsFarm;
        var isHomeRoot = home is not null && (SameLocation(location, home) || SameLocation(root, home));
        var isPlayerControlled = isFarmRoot || isHomeRoot || location.IsGreenhouse || root.IsGreenhouse;
        var kind = SameLocation(location, farm)
            ? "farm_outdoor"
            : location.IsGreenhouse
                ? "greenhouse"
                : home is not null && SameLocation(location, home)
                    ? "farmhouse"
                    : location is Cellar
                        ? "cellar"
                        : location.ParentBuilding is not null && isFarmRoot
                            ? "farm_building_interior"
                            : isFarmRoot
                                ? "player_farm_root"
                                : isHomeRoot
                                    ? "player_home_interior"
                                    : "persistent_world";

        return new MachineLocationRef(
            location,
            kind,
            isPlayerControlled,
            root.NameOrUniqueName,
            location.ParentBuilding?.GetType().FullName ?? string.Empty);
    }

    private static bool SameLocation(GameLocation left, GameLocation right) =>
        ReferenceEquals(left, right) || string.Equals(
            left.NameOrUniqueName,
            right.NameOrUniqueName,
            StringComparison.OrdinalIgnoreCase);
}

internal sealed record MachineLocationRef(
    GameLocation Location,
    string Kind,
    bool IsPlayerControlled,
    string RootLocationId,
    string ParentBuildingRuntimeType);
