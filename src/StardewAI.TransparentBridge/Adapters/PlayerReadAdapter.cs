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
            .Where(item => item is not null)
            .Select((item, index) => new
            {
                slot = index,
                id = item!.QualifiedItemId,
                name = item.DisplayName,
                stack = item.Stack,
                category = item.Category
            })
            .ToArray();

        return Section("player", new Dictionary<string, object>
        {
            ["player_id"] = Field(player?.UniqueMultiplayerID.ToString(), "Game1.player.UniqueMultiplayerID", tick),
            ["farm_name"] = Field(player?.farmName.Value, "Game1.player.farmName", tick),
            ["name"] = Field(player?.Name, "Game1.player.Name", tick),
            ["money"] = Field(player?.Money, "Game1.player.Money", tick),
            ["stamina"] = Field(player?.Stamina, "Game1.player.Stamina", tick),
            ["max_stamina"] = Field(player?.MaxStamina, "Game1.player.MaxStamina", tick),
            ["health"] = Field(player?.health, "Game1.player.health", tick),
            ["max_health"] = Field(player?.maxHealth, "Game1.player.maxHealth", tick),
            ["current_tool"] = Field(player?.CurrentTool?.DisplayName, "Game1.player.CurrentTool", tick),
            ["inventory_count"] = Field(inventory?.Length, "Game1.player.Items", tick),
            ["inventory"] = Field(inventory, "Game1.player.Items", tick)
        });
    }
}
