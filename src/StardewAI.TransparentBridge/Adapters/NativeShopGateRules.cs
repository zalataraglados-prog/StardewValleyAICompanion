using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

internal static class NativeShopGateRules
{
    private const int FishShopGoneFishingDoorTileIndex = 496;

    public static bool IsFishShopGoneFishingClosed(
        GameLocation fromLocation,
        int tileX,
        int tileY,
        string? targetLocation) =>
        Game1.Date.TotalDays < 1 &&
        fromLocation is Beach &&
        string.Equals(
            targetLocation,
            "FishShop",
            StringComparison.OrdinalIgnoreCase) &&
        fromLocation.getTileIndexAt(
            tileX,
            tileY,
            "Buildings",
            "untitled tile sheet") == FishShopGoneFishingDoorTileIndex;
}
