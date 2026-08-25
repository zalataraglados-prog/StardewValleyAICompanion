using StardewValley;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object[] ReadFurniture(GameLocation location) => location.furniture
        .Select((item, index) => ReadFurnitureRow(item, index))
        .OrderBy(row => row.tile_y)
        .ThenBy(row => row.tile_x)
        .ThenBy(row => row.index)
        .Cast<object>()
        .ToArray();

    private static FurnitureStateRow ReadFurnitureRow(Furniture item, int index)
    {
        var box = item.GetBoundingBox();
        var source = item.sourceRect.Value;
        var held = item.heldObject.Value;
        var storage = item as StorageFurniture;
        return new FurnitureStateRow
        {
            index = index,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            display_name = item.DisplayName,
            runtime_type = item.GetType().FullName ?? string.Empty,
            tile_x = (int)item.TileLocation.X,
            tile_y = (int)item.TileLocation.Y,
            furniture_type = item.furniture_type.Value,
            rotations = item.rotations.Value,
            current_rotation = item.currentRotation.Value,
            placement_restriction = item.placementRestriction,
            is_ground_furniture = item.isGroundFurniture(),
            is_passable = item.isPassable(),
            is_on = item.IsOn,
            flipped = item.Flipped,
            tiles_wide = item.getTilesWide(),
            tiles_high = item.getTilesHigh(),
            bounding_box_x = box.X / Game1.tileSize,
            bounding_box_y = box.Y / Game1.tileSize,
            bounding_box_width_pixels = box.Width,
            bounding_box_height_pixels = box.Height,
            source_rect_x = source.X,
            source_rect_y = source.Y,
            source_rect_width = source.Width,
            source_rect_height = source.Height,
            held_object_runtime_type = held?.GetType().FullName,
            held_object_qualified_item_id = held?.QualifiedItemId,
            held_object_stack = held?.Stack,
            storage_capacity = storage is null ? null : 36,
            storage_mutex_locked = storage?.mutex.IsLocked(),
            storage_items = storage?.heldItems.Select((entry, slot) => new
            {
                slot_index = slot,
                runtime_type = entry?.GetType().FullName,
                qualified_item_id = entry?.QualifiedItemId,
                stack = entry?.Stack,
                quality = entry?.Quality
            }).ToArray() ?? Array.Empty<object>()
        };
    }

    private sealed class FurnitureStateRow
    {
        public int index { get; init; }
        public string item_id { get; init; } = string.Empty;
        public string qualified_item_id { get; init; } = string.Empty;
        public string display_name { get; init; } = string.Empty;
        public string runtime_type { get; init; } = string.Empty;
        public int tile_x { get; init; }
        public int tile_y { get; init; }
        public int furniture_type { get; init; }
        public int rotations { get; init; }
        public int current_rotation { get; init; }
        public int placement_restriction { get; init; }
        public bool is_ground_furniture { get; init; }
        public bool is_passable { get; init; }
        public bool is_on { get; init; }
        public bool flipped { get; init; }
        public int tiles_wide { get; init; }
        public int tiles_high { get; init; }
        public int bounding_box_x { get; init; }
        public int bounding_box_y { get; init; }
        public int bounding_box_width_pixels { get; init; }
        public int bounding_box_height_pixels { get; init; }
        public int source_rect_x { get; init; }
        public int source_rect_y { get; init; }
        public int source_rect_width { get; init; }
        public int source_rect_height { get; init; }
        public string? held_object_runtime_type { get; init; }
        public string? held_object_qualified_item_id { get; init; }
        public int? held_object_stack { get; init; }
        public int? storage_capacity { get; init; }
        public bool? storage_mutex_locked { get; init; }
        public object[] storage_items { get; init; } = Array.Empty<object>();
    }
}
