using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupJunimoKartQuest(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        const string questKey = "QiChallenge3";
        var saloon = Game1.getLocationFromName("Saloon");
        var actionTile = saloon is null ? null : FindJunimoKartActionTile(saloon);
        var standTile = saloon is null || !actionTile.HasValue
            ? null
            : FindQuestDropBoxStandTile(saloon, actionTile.Value);
        var order = SpecialOrder.GetSpecialOrder(questKey, 0);
        var objective = order?.objectives.OfType<JKScoreObjective>().SingleOrDefault();
        if (saloon is null || !actionTile.HasValue || !standTile.HasValue ||
            order is null || objective is null || objective.GetMaxCount() != 50000)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_junimo_kart_quest",
                "junimo_kart_fixture=ready",
                "native_fixture_shape=missing_or_drifted",
                "junimo_kart_fixture_native_data_or_topology_missing");
        }

        if (Game1.currentMinigame is not null)
        {
            Game1.currentMinigame.forceQuit();
            Game1.currentMinigame = null;
        }
        foreach (var existing in Game1.player.team.specialOrders
            .Where(candidate => string.Equals(candidate.questKey.Value, questKey, StringComparison.Ordinal))
            .ToArray())
        {
            Game1.player.team.specialOrders.Remove(existing);
        }
        Game1.player.team.completedSpecialOrders.Remove(questKey);
        objective.SetCount(0);
        Game1.player.team.specialOrders.Add(order);
        order.Update();

        Game1.player.hasSkullKey = true;
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;
        Game1.currentLocation = saloon;
        Game1.player.currentLocation = saloon;
        Game1.player.Position = standTile.Value.ToVector2() * Game1.tileSize;
        Game1.player.faceDirection(DirectionTo(standTile.Value, actionTile.Value));

        var verified = Game1.player.hasSkullKey &&
            ReferenceEquals(Game1.currentLocation, saloon) &&
            Game1.player.TilePoint == standTile.Value &&
            Game1.player.team.specialOrders.Contains(order) &&
            objective.GetCount() == 0 &&
            objective.GetMaxCount() == 50000;
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
            PrimitiveKind = "debug_setup_junimo_kart_quest",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_QiChallenge3_installed", "skull_key_enabled", "saloon_arcade_stand_ready" }
                : new[] { "junimo_kart_fixture_postcondition_mismatch" },
            RequestedEffect = "quest_key=QiChallenge3;objective=JKScoreObjective;target=50000;location=Saloon",
            ObservedEffect = "quest_present=" + Game1.player.team.specialOrders.Contains(order) +
                ";progress=" + objective.GetCount() + "/" + objective.GetMaxCount() +
                ";location=" + Game1.currentLocation.NameOrUniqueName +
                ";stand=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";action=" + actionTile.Value.X + "," + actionTile.Value.Y,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "junimo_kart_fixture_postcondition_mismatch" }
        };
    }

    private static Point? FindJunimoKartActionTile(GameLocation location)
    {
        var layer = location.Map?.GetLayer("Buildings");
        if (layer is null)
        {
            return null;
        }
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.Equals(action, "Arcade_Minecart", StringComparison.Ordinal))
                {
                    return new Point(x, y);
                }
            }
        }
        return null;
    }
}
