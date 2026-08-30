using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupPrairieKingFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        Game1.exitActiveMenu();
        if (Game1.currentMinigame is not null)
        {
            Game1.currentMinigame.forceQuit();
            Game1.currentMinigame = null;
        }
        Game1.dialogueUp = false;
        Game1.eventUp = false;

        var saloon = Game1.getLocationFromName("Saloon");
        var interaction = saloon is null ? null : FindPrairieKingActionTile(saloon);
        Point? stand = saloon is null || !interaction.HasValue
            ? null
            : (Point?)Neighbors(interaction.Value).FirstOrDefault(candidate =>
                IsTileOnMap(saloon, candidate) && IsTileWalkable(saloon, candidate) &&
                !IsTileOccupiedByCharacter(saloon, candidate));
        if (saloon is null || !interaction.HasValue || !stand.HasValue ||
            !AreAdjacent(stand.Value, interaction.Value))
        {
            return BlockedWithPrimitive(request, "debug_setup_prairie_king",
                "prairie_king_fixture=ready", "native_fixture_shape=missing_or_drifted",
                "prairie_king_fixture_native_data_or_topology_missing");
        }
        var readySaloon = saloon;
        var readyInteraction = interaction.Value;
        var readyStand = stand.Value;

        Game1.player.stats.Set("completedPrairieKing", 0);
        Game1.player.stats.Set("completedPrairieKingWithoutDying", 0);
        Game1.player.jotpkProgress.Value = null;
        Game1.player.mailReceived.Remove("Beat_PK");
        Game1.player.mailForTomorrow.Remove("Beat_PK");
        Game1.currentLocation = readySaloon;
        Game1.player.currentLocation = readySaloon;
        Game1.player.Position = readyStand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        Game1.player.faceDirection(DirectionTo(readyStand, readyInteraction));

        var verified = ReferenceEquals(Game1.currentLocation, readySaloon) &&
            ReferenceEquals(Game1.player.currentLocation, readySaloon) &&
            Game1.player.TilePoint == readyStand &&
            readySaloon.doesTileHaveProperty(readyInteraction.X, readyInteraction.Y,
                "Action", "Buildings") == "Arcade_Prairie" &&
            Game1.player.stats.Get("completedPrairieKing") == 0 &&
            Game1.player.stats.Get("completedPrairieKingWithoutDying") == 0 &&
            Game1.player.jotpkProgress.Value is null &&
            !Game1.player.hasOrWillReceiveMail("Beat_PK");
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_prairie_king",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "fixture_only_prairie_king_progress_reset",
                    "native_saloon_Arcade_Prairie_action_present",
                    "fixture_only_player_positioned_adjacent"
                }
                : new[] { "prairie_king_fixture_postcondition_mismatch" },
            RequestedEffect = "prairie_king_fixture=ready",
            ObservedEffect = "location=" + Game1.currentLocation.NameOrUniqueName +
                ";stand=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";action=" + readyInteraction.X + "," + readyInteraction.Y +
                ";completed=" + Game1.player.stats.Get("completedPrairieKing") +
                ";completed_without_dying=" + Game1.player.stats.Get("completedPrairieKingWithoutDying") +
                ";beat_pk_mail=" + Game1.player.hasOrWillReceiveMail("Beat_PK"),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "prairie_king_fixture_postcondition_mismatch" }
        };
    }

    private static Point? FindPrairieKingActionTile(GameLocation location)
    {
        var layer = location.Map?.GetLayer("Buildings");
        if (layer is null)
            return null;
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            if (location.doesTileHaveProperty(x, y, "Action", "Buildings") == "Arcade_Prairie")
                return new Point(x, y);
        }
        return null;
    }
}
