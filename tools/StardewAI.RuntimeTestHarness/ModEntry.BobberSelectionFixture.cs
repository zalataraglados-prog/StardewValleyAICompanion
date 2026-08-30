using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupBobberSelectionFixture(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var fishShop = Game1.getLocationFromName("FishShop");
        if (!Context.IsWorldReady || fishShop is null || request.BobberFishCaughtSpeciesCount is < 0 or > 80)
            return BlockedWithPrimitive(request, "debug_setup_bobber_selection", "bobber_fixture_setup=true",
                BobberSelectionObservedEffect(), "bobber_selection_fixture_world_shop_or_count_invalid");
        Game1.exitActiveMenu();
        StopAllMovement();
        Game1.player.fishCaught.Clear();
        for (var i = 0; i < request.BobberFishCaughtSpeciesCount; i++)
            Game1.player.fishCaught.Add("fixture_fish_" + i, new[] { 1, 1 });
        Game1.player.bobberStyle.Value = request.BobberStyleId ?? 0;
        Game1.player.usingRandomizedBobber = request.BobberRandomBefore == true;
        var target = FindBobberFixtureAction(fishShop!);
        var stand = target.HasValue
            ? Neighbors(target.Value).FirstOrDefault(tile => IsTileOnMap(fishShop, tile) &&
                IsTileWalkable(fishShop, tile) && !IsTileOccupiedByCharacter(fishShop, tile))
            : Point.Zero;
        if (!target.HasValue || stand == Point.Zero)
            return BlockedWithPrimitive(request, "debug_setup_bobber_selection", "bobber_fixture_setup=true",
                BobberSelectionObservedEffect(), "bobber_selection_fixture_endpoint_or_stand_missing");
        Game1.warpFarmer(fishShop.NameOrUniqueName, stand.X, stand.Y, false);
        // Game1.warpFarmer settles the location on a later update tick; the smoke driver verifies
        // the exact FishShop location and stand from a fresh bridge snapshot before execution.
        var verified = Game1.player.fishCaught.Count() == request.BobberFishCaughtSpeciesCount &&
            Game1.player.bobberStyle.Value == (request.BobberStyleId ?? 0) &&
            Game1.player.usingRandomizedBobber == (request.BobberRandomBefore == true);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_bobber_selection",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_FishShop_bobber_fixture_ready" } : new[] { "bobber_selection_fixture_setup_mismatch" },
            RequestedEffect = "fish_caught_species_count=" + request.BobberFishCaughtSpeciesCount,
            ObservedEffect = BobberSelectionObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "bobber_selection_fixture_setup_mismatch" }
        };
    }

    private static Point? FindBobberFixtureAction(GameLocation fishShop)
    {
        var buildings = fishShop.map?.GetLayer("Buildings");
        if (buildings is null)
            return null;
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
            if (fishShop.doesTileHaveProperty(x, y, "Action", "Buildings") == "Bobbers")
                return new Point(x, y);
        return null;
    }
}
