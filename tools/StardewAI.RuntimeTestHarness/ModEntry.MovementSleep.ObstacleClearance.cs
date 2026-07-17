using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private bool ReplanTileMove(ActiveTileMove move, bool avoidSoftObstacles)
    {
        var currentTile = Game1.player.TilePoint;
        var remainingTiles = Math.Max(1, 512 - move.PathIndex);
        var path = TryBuildTilePath(Game1.currentLocation, currentTile, move.TargetTile, remainingTiles, out _, avoidSoftObstacles);
        if (path is null)
        {
            return false;
        }

        move.Path = path;
        move.PathIndex = 0;
        move.CurrentDirection = null;
        move.StuckTicks = 0;
        move.SoftObstacleTicks = 0;
        move.Pending.MovementExtraTicks += 30;
        return true;
    }

    private bool TryClearRemovableObstacle(GameLocation location, Point tile, ActiveTileMove move)
    {
        if (!CanClearRouteObstacles(location))
        {
            return false;
        }

        var key = new Vector2(tile.X, tile.Y);
        var before = ObstacleLabel(location, tile);
        var tool = SelectClearanceTool(location, tile);
        if (tool is null)
        {
            return false;
        }

        var staminaBefore = Game1.player.Stamina;
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, tile));
        ApplyClearanceTool(location, tile, tool);

        if (Game1.activeClickableMenu is DialogueBox)
        {
            Game1.exitActiveMenu();
            return false;
        }

        if (!IsTileWalkable(location, tile) && location.objects.ContainsKey(key))
        {
            return false;
        }

        move.Pending.MovementClearanceActions++;
        move.Pending.MovementExtraTicks += ClearanceTickCost(tool);
        move.Pending.ChangedFacts.Add(new SimulatedFactChange
        {
            Path = "movement.clearance[" + tile.X + "," + tile.Y + "]",
            Before = before,
            After = ObstacleLabel(location, tile)
        });
        move.Pending.ChangedFacts.Add(new SimulatedFactChange
        {
            Path = "player.energy",
            Before = staminaBefore.ToString("0.###"),
            After = Game1.player.Stamina.ToString("0.###")
        });
        return true;
    }

    private TrainingExecutionResult ExecuteClearObstacle(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", "current_location.obstacle=clear", ClearObstacleObservedEffect(null), "clear_obstacle_target_tile_required");
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = "current_location.obstacle[" + target.X + "," + target.Y + "]=clear";
        if (!CanClearRouteObstacles(location))
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_location_not_whitelisted");
        }

        if (ManhattanDistance(Game1.player.TilePoint, target) > 1)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_target_not_adjacent");
        }

        var tool = SelectClearanceTool(location, target);
        if (tool is null)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_no_matching_tool_or_obstacle");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var before = ObstacleLabel(location, target);
        var staminaBefore = Game1.player.Stamina;
        var swings = Math.Clamp(request.MaxCrops, 1, 64);
        var observedLabels = new List<string> { before };
        for (var swing = 0; swing < swings; swing++)
        {
            if (ObstacleLabel(location, target) == "clear")
            {
                break;
            }

            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
            ApplyClearanceTool(location, target, tool);
            if (Game1.activeClickableMenu is DialogueBox)
            {
                Game1.exitActiveMenu();
                break;
            }

            observedLabels.Add(ObstacleLabel(location, target));
        }

        var after = ObstacleLabel(location, target);
        var verified = after == "clear";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            EnergyBefore = staminaBefore,
            EnergyAfter = Game1.player.Stamina,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "clear_obstacle",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified
                ? new[] { "target_obstacle_cleared", "tool=" + tool.GetType().Name }
                : new[] { "target_obstacle_still_present", "tool=" + tool.GetType().Name },
            RequestedEffect = requested,
            ObservedEffect = "before=" + before + ";after=" + after + ";labels=" + string.Join(">", observedLabels),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "target_obstacle_still_present" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "current_location.obstacle[" + target.X + "," + target.Y + "]",
                    Before = before,
                    After = after
                },
                new SimulatedFactChange
                {
                    Path = "player.energy",
                    Before = staminaBefore.ToString("0.###"),
                    After = Game1.player.Stamina.ToString("0.###")
                }
            }
        };
    }

    private static void ApplyClearanceTool(GameLocation location, Point target, Tool tool)
    {
        var tile = new Vector2(target.X, target.Y);
        if (location.terrainFeatures.TryGetValue(tile, out var feature))
        {
            if (feature.performToolAction(tool, 0, tile))
            {
                location.terrainFeatures.Remove(tile);
            }

            return;
        }

        tool.DoFunction(location, target.X * Game1.tileSize, target.Y * Game1.tileSize, 0, Game1.player);
    }

    private static string ClearObstacleObservedEffect(Point? target)
    {
        return target.HasValue
            ? "location=" + Game1.currentLocation.NameOrUniqueName + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.Value.X + "," + target.Value.Y + ";obstacle=" + ObstacleLabel(Game1.currentLocation, target.Value)
            : "location=" + Game1.currentLocation.NameOrUniqueName + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static Tool? SelectClearanceTool(GameLocation location, Point tile)
    {
        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            if (obj is BreakableContainer)
            {
                return FindHeavyTool();
            }

            if (obj.IsBreakableStone())
            {
                return FindTool<Pickaxe>();
            }

            if (obj.IsWeeds())
            {
                return FindScythe() ?? FindHeavyTool();
            }

            if (obj.IsTwig())
            {
                return FindTool<Axe>();
            }

            return null;
        }

        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return feature switch
            {
                Grass => FindScythe() ?? FindHeavyTool(),
                Tree => FindTool<Axe>(),
                FruitTree => FindTool<Axe>(),
                _ => null
            };
        }

        var tileRect = TileRectangle(tile);
        foreach (var largeFeature in location.largeTerrainFeatures)
        {
            if (largeFeature.getBoundingBox().Intersects(tileRect))
            {
                return FindTool<Axe>();
            }
        }

        return null;
    }

    private static TTool? FindTool<TTool>() where TTool : Tool
    {
        return Game1.player.Items.OfType<TTool>().FirstOrDefault();
    }

    private static Tool? FindScythe()
    {
        return Game1.player.Items.OfType<MeleeWeapon>().FirstOrDefault(weapon => weapon.isScythe());
    }

    private static Tool? FindHeavyTool()
    {
        return Game1.player.Items.OfType<Tool>().FirstOrDefault(tool => tool.isHeavyHitter());
    }

    private static int ClearanceTickCost(Tool tool)
    {
        return tool switch
        {
            MeleeWeapon => 30,
            Axe => 60,
            Pickaxe => 60,
            _ => 60
        };
    }

    private static string ObstacleLabel(GameLocation location, Point tile)
    {
        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            return "object:" + obj.QualifiedItemId + ":" + obj.Name;
        }

        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return "terrain_feature:" + feature.GetType().Name;
        }

        var tileRect = TileRectangle(tile);
        if (location.largeTerrainFeatures.Any(feature => feature.getBoundingBox().Intersects(tileRect)))
        {
            return "large_terrain_feature";
        }

        if (location.resourceClumps.Any(clump => clump.getBoundingBox().Intersects(tileRect)))
        {
            return "resource_clump";
        }

        return "clear";
    }

    private static bool IsRemovableObstacle(GameLocation location, Point tile)
    {
        return CanClearRouteObstacles(location) && SelectClearanceTool(location, tile) is not null;
    }

    private static bool CanClearRouteObstacles(GameLocation location)
    {
        return location.IsFarm
            || location is MineShaft
            || location is VolcanoDungeon
            || string.Equals(location.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTileOccupiedByCharacter(GameLocation location, Point tile)
    {
        var tileRect = TileRectangle(tile);
        return location.characters.Any(character => character.GetBoundingBox().Intersects(tileRect));
    }

    private static XnaRectangle TileRectangle(Point tile)
    {
        return new XnaRectangle(tile.X * Game1.tileSize, tile.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
    }

    private static bool IsTileTraversableForPlan(GameLocation location, Point tile, bool avoidSoftObstacles, bool allowRemovableObstacles = true)
    {
        if (!IsTileOnMap(location, tile))
        {
            return false;
        }

        if (avoidSoftObstacles && IsTileOccupiedByCharacter(location, tile))
        {
            return false;
        }

        return IsTileWalkable(location, tile) || allowRemovableObstacles && IsRemovableObstacle(location, tile) || IsTileOccupiedByCharacter(location, tile);
    }

    private static bool IsTileHardBlocked(GameLocation location, Point tile)
    {
        return !IsTileWalkable(location, tile) && !IsRemovableObstacle(location, tile) && !IsTileOccupiedByCharacter(location, tile);
    }

    private static string MovementHardBlockReason(GameLocation location, Point tile)
    {
        if (!IsTileOnMap(location, tile))
        {
            return "movement_target_tile_out_of_map";
        }

        if (IsTileOccupiedByCharacter(location, tile))
        {
            return "movement_target_soft_obstacle";
        }

        if (IsRemovableObstacle(location, tile))
        {
            return "movement_target_requires_clearance";
        }

        return "movement_target_tile_hard_blocked";
    }

}
