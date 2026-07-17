using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

internal static class ShopIdResolver
{
    public static string? ResolveLegacyBuy(GameLocation location, string? legacyShopId)
    {
        if (string.Equals(legacyShopId, "Fish", StringComparison.OrdinalIgnoreCase))
        {
            return "FishShop";
        }

        if (location is SeedShop && string.Equals(legacyShopId, "General", StringComparison.OrdinalIgnoreCase))
        {
            return "SeedShop";
        }

        if (string.Equals(location.NameOrUniqueName, "SandyHouse", StringComparison.OrdinalIgnoreCase))
        {
            return "Sandy";
        }

        return legacyShopId;
    }
}
