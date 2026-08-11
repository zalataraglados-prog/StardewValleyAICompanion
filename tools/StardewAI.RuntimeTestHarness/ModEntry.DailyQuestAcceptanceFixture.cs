using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupDailyQuestAcceptanceFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var town = Game1.getLocationFromName("Town");
        var endpoint = FindDailyQuestFixtureEndpoint(town);
        if (town is null || endpoint is null)
        {
            reasons.Add("daily_quest_fixture_board_endpoint_missing");
        }
        if (reasons.Count > 0 || town is null || endpoint is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_daily_quest_acceptance",
                "daily_quest_offer=ready",
                "daily_quest_offer=unavailable",
                reasons.ToArray());
        }

        var player = Game1.player;
        var quest = new ItemDeliveryQuest(
            "Robin",
            "(O)388",
            "StardewAI daily quest fixture",
            "Bring one wood to Robin.",
            "Strings\\StringsFromCSFiles:ItemDeliveryQuest.cs.13336",
            "Strings\\StringsFromCSFiles:ItemDeliveryQuest.cs.13336");
        quest.parts.Add(new DescriptionElement(
            "Strings\\StringsFromCSFiles:ItemDeliveryQuest.cs.13336"));
        Helper.Reflection
            .GetField<bool>(quest, "_loadedDescription")
            .SetValue(true);
        quest.reloadObjective();
        quest.id.Value = "stardewai.runtime.daily";
        quest.dailyQuest.Value = true;
        quest.accepted.Value = false;
        quest.canBeCancelled.Value = false;
        quest.daysLeft.Value = 0;
        quest.dayQuestAccepted.Value = -1;
        foreach (var existing in player.questLog.Where(row =>
            ReferenceEquals(row, Game1.questOfTheDay) ||
            string.Equals(row.id.Value, quest.id.Value, StringComparison.Ordinal)).ToArray())
        {
            player.questLog.Remove(existing);
        }
        player.acceptedDailyQuest.Value = false;
        Game1.netWorldState.Value.SetQuestOfTheDay(quest);
        var liveQuest = Game1.questOfTheDay;
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.currentLocation = town;
        player.currentLocation = town;
        town.currentEvent = null;
        player.Position = endpoint.Value.Stand.ToVector2() * Game1.tileSize;
        player.faceDirection(DirectionTo(endpoint.Value.Stand, endpoint.Value.Action));
        player.UsingTool = false;
        player.canMove = true;

        var verified = liveQuest is ItemDeliveryQuest &&
            Game1.CanAcceptDailyQuest() &&
            player.TilePoint == endpoint.Value.Stand &&
            !player.questLog.Contains(liveQuest);
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
            PrimitiveKind = "debug_setup_daily_quest_acceptance",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_daily_quest_offer_installed", "live_Billboard_3_endpoint_selected" }
                : new[] { "daily_quest_fixture_receipt_mismatch" },
            RequestedEffect = "daily_quest_offer=ready",
            ObservedEffect = "daily_quest_offer=" + (Game1.CanAcceptDailyQuest() ? "ready" : "blocked") +
                ";same_reference=" + ReferenceEquals(Game1.questOfTheDay, quest).ToString().ToLowerInvariant() +
                ";description_length=" + (Game1.questOfTheDay?.questDescription?.Length ?? -1) +
                ";accepted_daily_quest=" + player.acceptedDailyQuest.Value.ToString().ToLowerInvariant() +
                ";player_tile=" + player.TilePoint.X + "," + player.TilePoint.Y +
                ";action_tile=" + endpoint.Value.Action.X + "," + endpoint.Value.Action.Y +
                ";stand_tile=" + endpoint.Value.Stand.X + "," + endpoint.Value.Stand.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "daily_quest_fixture_receipt_mismatch" }
        };
    }

    private static (Point Action, Point Stand)? FindDailyQuestFixtureEndpoint(GameLocation? town)
    {
        var buildings = town?.Map?.GetLayer("Buildings");
        if (town is null || buildings is null)
        {
            return null;
        }
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                var raw = town.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (!string.Equals(raw, "Billboard 3", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (var stand in new[]
                {
                    new Point(x, y + 1), new Point(x - 1, y),
                    new Point(x + 1, y), new Point(x, y - 1)
                })
                {
                    if (town.isTilePassable(new xTile.Dimensions.Location(stand.X, stand.Y), Game1.viewport))
                    {
                        return (new Point(x, y), stand);
                    }
                }
            }
        }
        return null;
    }
}
