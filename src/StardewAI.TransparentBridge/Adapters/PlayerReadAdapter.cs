using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

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
            ["total_money_earned"] = Field(Context.IsWorldReady ? (uint?)player?.totalMoneyEarned : null, "Game1.player.totalMoneyEarned", tick),
            ["health"] = Field(player?.health, "Game1.player.health", tick),
            ["max_health"] = Field(player?.maxHealth, "Game1.player.maxHealth", tick),
            ["energy"] = Field(player?.Stamina, "Game1.player.Stamina", tick),
            ["max_energy"] = Field(player?.MaxStamina, "Game1.player.MaxStamina", tick),
            ["level"] = Field(Context.IsWorldReady ? (int?)player?.Level : null, "Game1.player.Level", tick),
            ["has_skull_key"] = Field(Context.IsWorldReady ? (bool?)player?.hasSkullKey : null, "Game1.player.hasSkullKey", tick),
            ["has_rusty_key"] = Field(Context.IsWorldReady ? (bool?)player?.hasRustyKey : null, "Game1.player.hasRustyKey", tick),
            ["married_or_roommate"] = Field(Context.IsWorldReady ? (bool?)player?.isMarriedOrRoommates() : null, "Game1.player.isMarriedOrRoommates()", tick),
            ["farmhouse_upgrade_level"] = Field(ReadFarmhouseUpgradeLevel(player), "Utility.getHomeOfFarmer(Game1.player).upgradeLevel", tick),
            ["current_tool"] = Field(player?.CurrentTool?.QualifiedItemId ?? player?.CurrentTool?.DisplayName, "Game1.player.CurrentTool", tick),
            ["current_item_qualified_id"] = Field(player?.CurrentItem?.QualifiedItemId, "Game1.player.CurrentItem.QualifiedItemId", tick),
            ["active_object_qualified_id"] = Field(player?.ActiveObject?.QualifiedItemId, "Game1.player.ActiveObject.QualifiedItemId", tick),
            ["active_menu"] = Field(Game1.activeClickableMenu?.GetType().FullName ?? "none", "Game1.activeClickableMenu", tick)
        };

        return Section("player", playerFields.Concat(new Dictionary<string, object>
        {
            ["inventory"] = Field(inventory, "Game1.player.Items", tick)
        }).ToDictionary(item => item.Key, item => item.Value));
    }

    private static int? ReadFarmhouseUpgradeLevel(Farmer? player)
    {
        if (player is null)
        {
            return null;
        }

        return Utility.getHomeOfFarmer(player) is FarmHouse farmhouse
            ? farmhouse.upgradeLevel
            : null;
    }
}
