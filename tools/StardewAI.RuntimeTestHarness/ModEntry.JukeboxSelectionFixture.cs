using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupJukeboxSelectionFixture(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var saloon = Game1.getLocationFromName("Saloon");
        if (!Context.IsWorldReady || saloon is null)
            return BlockedWithPrimitive(request, "debug_setup_jukebox_selection", "jukebox_fixture_setup=true",
                JukeboxSelectionObservedEffect(), "jukebox_selection_fixture_world_or_saloon_invalid");
        Game1.exitActiveMenu();
        StopAllMovement();
        var target = FindJukeboxFixtureAction(saloon);
        var stand = target.HasValue
            ? Neighbors(target.Value).FirstOrDefault(tile => IsTileOnMap(saloon, tile) &&
                IsTileWalkable(saloon, tile) && !IsTileOccupiedByCharacter(saloon, tile))
            : Point.Zero;
        if (!target.HasValue || stand == Point.Zero)
            return BlockedWithPrimitive(request, "debug_setup_jukebox_selection", "jukebox_fixture_setup=true",
                JukeboxSelectionObservedEffect(), "jukebox_selection_fixture_endpoint_or_stand_missing");
        Game1.warpFarmer(saloon.NameOrUniqueName, stand.X, stand.Y, false);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_jukebox_selection",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "isolated_Saloon_jukebox_fixture_warp_requested" },
            RequestedEffect = "jukebox_fixture_setup=true",
            ObservedEffect = JukeboxSelectionObservedEffect()
        };
    }

    private static Point? FindJukeboxFixtureAction(GameLocation saloon)
    {
        var buildings = saloon.map?.GetLayer("Buildings");
        if (buildings is null)
            return null;
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
            if (saloon.doesTileHaveProperty(x, y, "Action", "Buildings") == "Jukebox")
                return new Point(x, y);
        return null;
    }
}
