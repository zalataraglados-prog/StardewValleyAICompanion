using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object ReadFlooringDetails(Vector2 tile, Flooring flooring)
    {
        var data = flooring.GetData();
        var exactBase = flooring.GetType() == typeof(Flooring);
        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            type = flooring.GetType().FullName,
            flooring_state = new
            {
                status = exactBase && data is not null ? "available" : "custom_or_missing_data_blocked",
                runtime_type_supported = exactBase,
                floor_data_key = flooring.whichFloor.Value,
                floor_data_id = data?.Id,
                floor_data_item_id = data?.ItemId,
                connect_type = data?.ConnectType.ToString(),
                shadow_type = data?.ShadowType.ToString(),
                which_view = flooring.whichView.Value,
                derived_neighbor_mask = PlayerReadAdapter.ReadFlooringConnectionMask(Game1.currentLocation, tile, flooring.whichFloor.Value),
                is_passable = flooring.isPassable(),
                footstep_sound = flooring.getFootstepSound(),
                farm_speed_buff = data?.FarmSpeedBuff,
                native_runtime_type = flooring.GetType().FullName
            }
        };
    }
}
