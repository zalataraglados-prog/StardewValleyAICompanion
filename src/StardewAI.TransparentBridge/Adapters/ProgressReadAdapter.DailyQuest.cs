using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ProgressQuestReadAdapter
{
    private DailyQuestOfferRef? ReadDailyQuestOffer(Farmer? player)
    {
        if (player is null)
        {
            return null;
        }

        var offer = Game1.questOfTheDay;
        var mapped = offer is null ? null : mapper.MapQuest(offer);
        var town = Game1.getLocationFromName("Town");
        var board = FindDailyQuestBoard(town);
        var stand = board is null ? null : FindDailyQuestStandTile(town, board.X, board.Y);
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var reasons = new List<string>();
        if (offer is null)
        {
            reasons.Add("daily_quest_offer_missing");
        }
        else if (string.IsNullOrWhiteSpace(offer.questDescription))
        {
            reasons.Add("daily_quest_description_missing");
        }
        if (player.acceptedDailyQuest.Value)
        {
            reasons.Add("daily_quest_already_accepted_today");
        }
        if (board is null)
        {
            reasons.Add("daily_quest_board_action_missing");
        }
        if (stand is null)
        {
            reasons.Add("daily_quest_board_stand_tile_unavailable");
        }
        if (!menuClear)
        {
            reasons.Add("daily_quest_menu_or_dialogue_not_clear");
        }

        var canAccept = Game1.CanAcceptDailyQuest();
        return new DailyQuestOfferRef
        {
            Available = mapped is not null && board is not null && stand is not null,
            CanAccept = canAccept,
            AcceptedDailyQuest = player.acceptedDailyQuest.Value,
            OfferFingerprint = mapped is null ? string.Empty : QuestOfferIdentity.Compute(mapped),
            Quest = mapped,
            BoardLocationId = town?.NameOrUniqueName ?? "Town",
            BoardActionTileX = board?.X,
            BoardActionTileY = board?.Y,
            BoardActionRaw = board?.Action ?? string.Empty,
            StandTileX = stand?.X,
            StandTileY = stand?.Y,
            MenuClear = menuClear,
            Status = canAccept && board is not null && stand is not null && menuClear
                ? "ready"
                : "blocked",
            BlockedDiagnostics = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static DailyQuestBoardTile? FindDailyQuestBoard(GameLocation? town)
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
                var action = town.doesTileHaveProperty(x, y, "Action", "Buildings");
                var parts = action?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                if (parts.Length >= 2 &&
                    string.Equals(parts[0], "Billboard", StringComparison.Ordinal) &&
                    string.Equals(parts[1], "3", StringComparison.Ordinal))
                {
                    return new DailyQuestBoardTile(x, y, action!);
                }
            }
        }

        return null;
    }

    private static Point? FindDailyQuestStandTile(GameLocation? town, int boardX, int boardY)
    {
        if (town is null)
        {
            return null;
        }

        var candidates = new[]
        {
            new Point(boardX, boardY + 1),
            new Point(boardX - 1, boardY),
            new Point(boardX + 1, boardY),
            new Point(boardX, boardY - 1)
        };
        var collisionMask = CollisionMask.All & ~CollisionMask.Farmers;
        foreach (var tile in candidates)
        {
            if (town.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport) &&
                !town.IsTileBlockedBy(
                    new Vector2(tile.X, tile.Y),
                    collisionMask,
                    CollisionMask.None,
                    useFarmerTile: true))
            {
                return tile;
            }
        }
        return null;
    }

    private sealed record DailyQuestBoardTile(int X, int Y, string Action);
}
