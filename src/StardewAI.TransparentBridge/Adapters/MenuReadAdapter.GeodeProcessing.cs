using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MenuReadAdapter
{
    private static object ReadGeodeMenuState(GeodeMenu menu) => new
    {
        kind = "geode_processing",
        held_item = ReadGeodeMenuItem(menu.heldItem),
        geode_spot_item = ReadGeodeMenuItem(menu.geodeSpot.item),
        treasure_item = ReadGeodeMenuItem(menu.geodeTreasure),
        treasure_override_item = ReadGeodeMenuItem(menu.geodeTreasureOverride),
        geode_animation_timer_ms = menu.geodeAnimationTimer,
        waiting_for_server_response = menu.waitingForServerResponse,
        ready_to_close = menu.readyToClose(),
        geode_spot_bounds = new
        {
            x = menu.geodeSpot.bounds.X,
            y = menu.geodeSpot.bounds.Y,
            width = menu.geodeSpot.bounds.Width,
            height = menu.geodeSpot.bounds.Height
        }
    };

    private static object? ReadGeodeMenuItem(StardewValley.Item? item) => item is null ? null : new
    {
        qualified_item_id = item.QualifiedItemId,
        item_id = item.ItemId,
        display_name = item.DisplayName,
        stack = item.Stack,
        quality = item.Quality
    };
}
