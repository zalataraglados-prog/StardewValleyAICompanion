using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ProgressQuestReadAdapter
{
    private SpecialOrderBoardRef[]? ReadSpecialOrderBoards(FarmerTeam? team)
    {
        if (team is null)
        {
            return null;
        }

        var menu = Game1.activeClickableMenu as SpecialOrdersBoard;
        return FindSpecialOrderBoardTiles()
            .Select(tile => MapSpecialOrderBoard(team, menu, tile))
            .OrderBy(board => board.BoardType, StringComparer.Ordinal)
            .ThenBy(board => board.LocationId, StringComparer.Ordinal)
            .ToArray();
    }

    private SpecialOrderBoardRef MapSpecialOrderBoard(
        FarmerTeam team,
        SpecialOrdersBoard? menu,
        SpecialOrderBoardTile tile)
    {
        var menuOpen = menu is not null &&
            string.Equals(menu.boardType ?? string.Empty, tile.BoardType, StringComparison.Ordinal);
        var sourceOffers = menuOpen
            ? new[] { menu!.leftOrder, menu.rightOrder }
            : team.availableSpecialOrders
                .Where(order => string.Equals(order.orderType.Value ?? string.Empty, tile.BoardType, StringComparison.Ordinal))
                .Take(2)
                .ToArray();
        var offers = sourceOffers
            .Select((order, index) => new { order, index })
            .Where(row => row.order is not null)
            .Select(row =>
            {
                var mapped = mapper.MapSpecialOrder(row.order!);
                return new SpecialOrderOfferRef
                {
                    SelectionIndex = row.index,
                    SelectionSide = row.index == 0 ? "left" : "right",
                    OfferFingerprint = SpecialOrderOfferIdentity.Compute(mapped),
                    Order = mapped
                };
            })
            .ToArray();
        var unlocked = tile.BoardType != string.Empty || StardewValley.SpecialOrders.SpecialOrder.IsSpecialOrdersBoardUnlocked();
        var accepted = team.acceptedSpecialOrderTypes.Contains(tile.BoardType);
        var dialogueReady = tile.BoardType == "DesertFestivalMarlon" &&
            Game1.dialogueUp &&
            Game1.activeClickableMenu is DialogueBox dialogue &&
            string.Equals(dialogue.characterDialogue?.speaker?.Name, "Marlon", StringComparison.Ordinal) &&
            !dialogue.isQuestion &&
            Game1.afterDialogues is not null;
        var reasons = new List<string>();
        if (!unlocked) reasons.Add("special_order_board_locked");
        if (accepted) reasons.Add("special_order_type_already_accepted_this_cycle");
        if (!tile.StandTile.HasValue) reasons.Add("special_order_board_stand_tile_unavailable");
        if (offers.Length == 0 && !menuOpen) reasons.Add("special_order_offers_not_materialized");
        if (Game1.activeClickableMenu is not null && !menuOpen && !dialogueReady)
        {
            reasons.Add("special_order_unrelated_menu_open");
        }

        return new SpecialOrderBoardRef
        {
            BoardType = tile.BoardType,
            LocationId = tile.Location.NameOrUniqueName,
            ActionToken = tile.ActionToken,
            ActionRaw = tile.ActionRaw,
            ActionTileX = tile.ActionTile.X,
            ActionTileY = tile.ActionTile.Y,
            StandTileX = tile.StandTile?.X,
            StandTileY = tile.StandTile?.Y,
            Unlocked = unlocked,
            AcceptedThisCycle = accepted,
            MenuOpen = menuOpen,
            DialogueReadyForBoard = dialogueReady,
            Offers = offers,
            Status = reasons.Count == 0 ? "ready" : "blocked",
            BlockedDiagnostics = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static SpecialOrderBoardTile[] FindSpecialOrderBoardTiles()
    {
        var locations = new List<GameLocation>();
        Utility.ForEachLocation(location =>
        {
            if (location is not null) locations.Add(location);
            return true;
        }, includeInteriors: true, includeGenerated: true);

        var rows = new List<SpecialOrderBoardTile>();
        foreach (var location in locations
                     .GroupBy(location => location.NameOrUniqueName, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            var layer = location.map?.GetLayer("Buildings");
            if (layer is null) continue;
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                for (var x = 0; x < layer.LayerWidth; x++)
                {
                    var raw = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                    var token = raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                    var boardType = token switch
                    {
                        "SpecialOrders" => string.Empty,
                        "QiChallengeBoard" => "Qi",
                        "DesertMarlon" => "DesertFestivalMarlon",
                        _ => null
                    };
                    if (boardType is null) continue;
                    rows.Add(new SpecialOrderBoardTile(
                        location,
                        token,
                        boardType,
                        raw!,
                        new Point(x, y),
                        FindSpecialOrderStandTile(location, x, y)));
                }
            }
        }

        return rows
            .GroupBy(row => row.Location.NameOrUniqueName + "\n" + row.BoardType, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(row => row.StandTile.HasValue)
                .ThenBy(row => row.ActionTile.Y)
                .ThenBy(row => row.ActionTile.X)
                .First())
            .ToArray();
    }

    private static Point? FindSpecialOrderStandTile(GameLocation location, int actionX, int actionY)
    {
        var collisionMask = CollisionMask.All & ~CollisionMask.Farmers;
        foreach (var tile in new[]
                 {
                     new Point(actionX, actionY + 1),
                     new Point(actionX - 1, actionY),
                     new Point(actionX + 1, actionY),
                     new Point(actionX, actionY - 1)
                 })
        {
            if (location.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport) &&
                !location.IsTileBlockedBy(new Vector2(tile.X, tile.Y), collisionMask, CollisionMask.None, useFarmerTile: true))
            {
                return tile;
            }
        }
        return null;
    }

    private sealed record SpecialOrderBoardTile(
        GameLocation Location,
        string ActionToken,
        string BoardType,
        string ActionRaw,
        Point ActionTile,
        Point? StandTile);
}
