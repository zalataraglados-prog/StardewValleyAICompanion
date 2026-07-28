using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object[] ReadCurrentLocationDebris(GameLocation location)
    {
        return location.debris
            .Select((debris, index) => new
            {
                debris_index = index,
                debris_type = debris.debrisType.Value.ToString(),
                chunk_type = debris.chunkType.Value,
                item_id = debris.itemId.Value,
                qualified_item_id = debris.item?.QualifiedItemId ?? debris.itemId.Value,
                item_quality = debris.itemQuality,
                item = SummarizeItem(debris.item),
                chunk_count = debris.Chunks.Count,
                chunk_final_y_level = debris.chunkFinalYLevel,
                chunk_final_y_target = debris.chunkFinalYTarget,
                time_since_done_bouncing = debris.timeSinceDoneBouncing,
                is_sinking = debris.isSinking.Value,
                is_essential_item = debris.isEssentialItem(),
                pickup_executor_status = "covered_by_current_location_debris_pickup",
                chunks = debris.Chunks
                    .Select((chunk, chunkIndex) => new
                    {
                        chunk_index = chunkIndex,
                        pixel_x = (int)MathF.Round(chunk.position.X),
                        pixel_y = (int)MathF.Round(chunk.position.Y),
                        tile_x = (int)((chunk.position.X + 32f) / Game1.tileSize),
                        tile_y = (int)((chunk.position.Y + 32f) / Game1.tileSize),
                        x_velocity = chunk.xVelocity.Value,
                        y_velocity = chunk.yVelocity.Value,
                        bounces = chunk.bounces,
                        has_passed_resting_line_once = chunk.hasPassedRestingLineOnce.Value,
                        alpha = chunk.alpha
                    })
                    .ToArray()
            })
            .ToArray();
    }
}
