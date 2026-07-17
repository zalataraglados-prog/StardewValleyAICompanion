using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using StardewAI.Contracts.State;
using System.Reflection;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter : ReadAdapterBase
{
    private static object[] ActionTiles(xTile.Layers.Layer? layer, string actionToken)
    {
        if (layer is null)
        {
            return Array.Empty<object>();
        }

        var tiles = new List<object>();
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                var tile = layer.Tiles[x, y];
                var action = tile?.Properties.TryGetValue("Action", out var property) == true
                    ? property.ToString()
                    : tile?.TileIndexProperties.TryGetValue("Action", out property) == true ? property.ToString() : null;
                if (ActionTokenEquals(action, actionToken))
                {
                    tiles.Add(new { tile_x = x, tile_y = y, action, present = true, usable = new { status = "derived", reason = "exact_action_token_on_loaded_map" } });
                }
            }
        }

        return tiles.ToArray();
    }

    private static object[] IndexedTiles(xTile.Layers.Layer? layer, int tileIndex, string reason)
    {
        if (layer is null)
        {
            return Array.Empty<object>();
        }

        var tiles = new List<object>();
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                if (layer.Tiles[x, y]?.TileIndex == tileIndex)
                {
                    tiles.Add(new { tile_x = x, tile_y = y, tile_index = tileIndex, present = true, usable = new { status = "derived", reason } });
                }
            }
        }

        return tiles.ToArray();
    }

    private static object[] MineExitTiles(xTile.Layers.Layer? layer, MineShaft mine)
    {
        if (layer is null)
        {
            return Array.Empty<object>();
        }

        var destination = mine.mineLevel == 77377
            ? new { location_id = "Mine", tile_x = 67, tile_y = 10 }
            : mine.mineLevel > 120
                ? new { location_id = "SkullCave", tile_x = 3, tile_y = 4 }
                : new { location_id = "Mine", tile_x = 23, tile_y = 8 };
        var tiles = new List<object>();
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                if (layer.Tiles[x, y]?.TileIndex == 115)
                {
                    tiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        tile_index = 115,
                        present = true,
                        expected_destination = destination,
                        native_question_key = "ExitMine",
                        native_response_key = "ExitMine_Leave",
                        usable = new { status = "derived", reason = "native_mineshaft_exit_tile" }
                    });
                }
            }
        }

        return tiles.ToArray();
    }

    private static object[] ShaftTiles(xTile.Layers.Layer? layer, MineShaft mine, Farmer player)
    {
        if (layer is null || mine.getMineArea() != MineShaft.desertArea || mine.mineLevel <= MineShaft.bottomOfMineLevel)
        {
            return Array.Empty<object>();
        }

        var levels = ShaftFallLevels(mine.mineLevel, Game1.uniqueIDForThisGame, Game1.Date.TotalDays);
        var damage = levels * 3;
        var tiles = new List<object>();
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                if (layer.Tiles[x, y]?.TileIndex == 174)
                {
                    tiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        tile_index = 174,
                        present = true,
                        expected_level_delta = levels,
                        expected_mine_level_after = mine.mineLevel + levels,
                        expected_health_cost = damage,
                        expected_health_after = Math.Max(1, player.health - damage),
                        preview_source = "MineShaft.enterMineShaft deterministic local random",
                        usable = new { status = "derived", reason = "native_mineshaft_shaft_tile" }
                    });
                }
            }
        }

        return tiles.ToArray();
    }

    public static int ShaftFallLevels(int mineLevel, ulong uniqueGameId, int totalDays)
    {
        var random = Utility.CreateRandom(mineLevel, uniqueGameId, totalDays);
        var levels = random.Next(3, 9);
        if (random.NextDouble() < 0.1)
        {
            levels = levels * 2 - 1;
        }
        if (mineLevel < 220 && mineLevel + levels > 220)
        {
            levels = 220 - mineLevel;
        }
        return levels;
    }

    public static bool ActionTokenEquals(string? action, string expectedToken)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var token = action.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.Equals(token, expectedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static object Tile(Vector2 tile) => new { tile_x = (int)tile.X, tile_y = (int)tile.Y };

    public static int PickaxeDamagePerHit(int upgradeLevel, int additionalPower)
    {
        return Math.Max(1, upgradeLevel + 1) + Math.Max(0, additionalPower);
    }

    public static int RemainingHits(int remainingHealth, int damagePerHit)
    {
        return damagePerHit <= 0 || remainingHealth <= 0
            ? 0
            : (int)Math.Ceiling(remainingHealth / (double)damagePerHit);
    }

    public static double LadderChanceAfterBreak(int stonesBeforeBreak, int luckLevel, double dailyLuck, int enemyCount, bool dwarfStatueBuff)
    {
        var stonesAfterBreak = Math.Max(0, stonesBeforeBreak - 1);
        var chance = 0.02 + 1.0 / Math.Max(1, stonesAfterBreak) + luckLevel / 100.0 + dailyLuck / 5.0;
        if (enemyCount == 0)
        {
            chance += 0.04;
        }
        if (dwarfStatueBuff)
        {
            chance *= 1.25;
        }
        return chance;
    }

}
