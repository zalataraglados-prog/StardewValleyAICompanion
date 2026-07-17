using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter : ReadAdapterBase
{
    private static object ReadCollisionGrid()
    {
        var location = Game1.currentLocation;
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length > 0 ? layers.Max(layer => layer.LayerWidth) : 0;
        var height = layers.Length > 0 ? layers.Max(layer => layer.LayerHeight) : 0;
        var notableTiles = new List<object>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                var touchAction = location.doesTileHaveProperty(x, y, "TouchAction", "Back");
                var friendshipDoor = ReadFriendshipDoorGate(location, touchAction);
                var warp = location.warps.FirstOrDefault(candidate => candidate.X == x && candidate.Y == y);
                var point = new Point(x, y);
                var hasDoor = location.doors.ContainsKey(point);
                var hasInteriorDoor = location.interiorDoors.ContainsKey(point);
                var collision = location.isCollidingPosition(
                    new Rectangle(x * 64 + 1, y * 64 + 1, 62, 62),
                    Game1.viewport,
                    isFarmer: true,
                    damagesFarmer: 0,
                    glider: false,
                    Game1.player,
                    pathfinding: true);

                var collisionBlocked = collision || friendshipDoor is { AllowedNow: false };
                if (!collisionBlocked && string.IsNullOrWhiteSpace(action) && string.IsNullOrWhiteSpace(touchAction) && warp is null && !hasDoor && !hasInteriorDoor)
                {
                    continue;
                }

                notableTiles.Add(new
                {
                    tile_x = x,
                    tile_y = y,
                    collision_blocked = collisionBlocked,
                    native_collision_blocked = collision,
                    action,
                    touch_action = touchAction,
                    warp_target = warp?.TargetName,
                    door = hasDoor,
                    interior_door = hasInteriorDoor,
                    friendship_door = friendshipDoor is not null,
                    friendship_door_allowed_now = friendshipDoor?.AllowedNow,
                    friendship_door_required_hearts = friendshipDoor?.RequiredHearts,
                    friendship_door_npc_names = friendshipDoor?.NpcNames,
                    friendship_door_green_rain_override = friendshipDoor?.GreenRainOverride,
                    friendship_door_gate_source = friendshipDoor?.Source
                });
            }
        }

        return new
        {
            location_id = location.NameOrUniqueName,
            width,
            height,
            compression = "blocked_or_action_or_warp_or_door_tiles_only",
            probe_rect_offset_x = 1,
            probe_rect_offset_y = 1,
            probe_rect_width = 62,
            probe_rect_height = 62,
            notable_tile_count = notableTiles.Count,
            notable_tiles = notableTiles
        };
    }

    private static FriendshipDoorGate? ReadFriendshipDoorGate(GameLocation location, string? touchAction)
    {
        var parts = (touchAction ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], "Door", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var npcNames = parts.Skip(1).ToArray();
        var greenRainOverride = Game1.year == 1 &&
            location.IsGreenRainingHere() &&
            npcNames.Any(name => string.Equals(name, "Sebastian", StringComparison.Ordinal));
        var friendshipAllowed = npcNames.Length == 0 ||
            npcNames.Any(name => Game1.player.getFriendshipHeartLevelForNPC(name) >= 2);

        return new FriendshipDoorGate(
            AllowedNow: friendshipAllowed || greenRainOverride,
            RequiredHearts: 2,
            NpcNames: npcNames,
            GreenRainOverride: greenRainOverride,
            Source: "GameLocation.performTouchAction Door branch; Farmer.getFriendshipHeartLevelForNPC; year-one Green Rain Sebastian override");
    }

}
