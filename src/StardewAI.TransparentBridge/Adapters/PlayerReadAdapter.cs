using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class PlayerReadAdapter : ReadAdapterBase
{
    public override string Domain => "player";
    public override int Priority => 20;

    public override StateAdapterResult Collect(long tick)
    {
        var player = Context.IsWorldReady ? Game1.player : null;
        var inventory = player?.Items
            .Select((item, index) => new
            {
                slot_index = index,
                item_id = item?.ItemId,
                qualified_item_id = item?.QualifiedItemId,
                display_name = item?.DisplayName,
                stack = item?.Stack,
                quality = item?.Quality,
                is_empty = item is null
            })
            .ToArray();

        var playerFields = new Dictionary<string, object>
        {
            ["location_id"] = Field(player?.currentLocation?.NameOrUniqueName, "Game1.player.currentLocation.NameOrUniqueName", tick),
            ["tile_x"] = Field(Context.IsWorldReady ? (int?)player?.TilePoint.X : null, "Game1.player.TilePoint.X", tick),
            ["tile_y"] = Field(Context.IsWorldReady ? (int?)player?.TilePoint.Y : null, "Game1.player.TilePoint.Y", tick),
            ["facing_direction"] = Field(Context.IsWorldReady ? (int?)player?.FacingDirection : null, "Game1.player.FacingDirection", tick),
            ["money"] = Field(player?.Money, "Game1.player.Money", tick),
            ["health"] = Field(player?.health, "Game1.player.health", tick),
            ["max_health"] = Field(player?.maxHealth, "Game1.player.maxHealth", tick),
            ["energy"] = Field(player?.Stamina, "Game1.player.Stamina", tick),
            ["max_energy"] = Field(player?.MaxStamina, "Game1.player.MaxStamina", tick),
            ["current_tool"] = Field(player?.CurrentTool?.QualifiedItemId ?? player?.CurrentTool?.DisplayName, "Game1.player.CurrentTool", tick),
            ["active_menu"] = Field(Game1.activeClickableMenu?.GetType().FullName ?? "none", "Game1.activeClickableMenu", tick)
        };

        return Section("player", playerFields.Concat(new Dictionary<string, object>
        {
            ["inventory"] = Field(inventory, "Game1.player.Items", tick)
        }).ToDictionary(item => item.Key, item => item.Value));
    }
}
