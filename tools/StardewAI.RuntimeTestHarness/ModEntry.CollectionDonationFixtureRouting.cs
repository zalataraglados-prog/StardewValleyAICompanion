using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool MoveCollectionDonationFixtureFarmer(
        TrainingExecutionRequest request,
        GameLocation donationLocation,
        Point donationStand,
        out GameLocation fixtureLocation,
        out Point fixtureStand,
        out string blockReason)
    {
        if (string.IsNullOrWhiteSpace(request.LocationId))
        {
            fixtureLocation = donationLocation;
            fixtureStand = donationStand;
            blockReason = string.Empty;
            Game1.currentLocation = donationLocation;
            Game1.player.currentLocation = donationLocation;
            Game1.player.Position = donationStand.ToVector2() * Game1.tileSize;
            return true;
        }

        fixtureLocation = Game1.getLocationFromName(request.LocationId)!;
        fixtureStand = Point.Zero;
        if (fixtureLocation is null)
        {
            blockReason = "collection_donation_fixture_source_location_missing";
            return false;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            blockReason = "collection_donation_fixture_source_connector_tile_missing";
            return false;
        }

        return MoveFixtureFarmerToLocationAdjacent(
            fixtureLocation,
            new Point(request.TargetTileX.Value, request.TargetTileY.Value),
            out fixtureStand,
            out blockReason);
    }
}
