using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class NpcReadAdapter : ReadAdapterBase
{
    public override string Domain => "npcs";
    public override int Priority => 40;

    public override StateAdapterResult Collect(long tick)
    {
        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            return Section("npcs", new Dictionary<string, object>
            {
                ["positions"] = Unavailable("world_not_ready", "Game1.currentLocation.characters", tick, "vanilla_1_6_npc"),
                ["schedules"] = Unavailable("npc_schedules_unavailable_without_complete_read_only_decompile_proof", "StardewValley.NPC.Schedule", tick, "vanilla_1_6_npc")
            }, new[] { "npcs.positions", "npcs.schedules" }, "unavailable");
        }

        var location = Game1.currentLocation;
        var locationId = location.NameOrUniqueName;
        var npcs = location.characters
            .Where(npc => npc is not null && ReferenceEquals(npc.currentLocation, location))
            .Select(npc =>
            {
                var tile = npc.TilePoint;
                var visibleOnScreen = Utility.isOnScreen(tile, 128, location);

                return new
                {
                    name = npc.Name,
                    display_name = npc.displayName,
                    location_id = locationId,
                    tile_x = tile.X,
                    tile_y = tile.Y,
                    facing_direction = npc.FacingDirection,
                    visible_on_screen = visibleOnScreen,
                    is_villager = npc.IsVillager,
                    is_monster = npc.IsMonster
                };
            })
            .OrderBy(npc => npc.name, StringComparer.Ordinal)
            .ThenBy(npc => npc.tile_x)
            .ThenBy(npc => npc.tile_y)
            .ToArray();

        return Section("npcs", new Dictionary<string, object>
        {
            ["positions"] = Field(npcs, "Game1.currentLocation.characters[].Name/displayName/TilePoint/FacingDirection/currentLocation; Utility.isOnScreen(TilePoint, 128, Game1.currentLocation)", tick, "vanilla_1_6_npc"),
            ["schedules"] = Unavailable("npc_schedules_unavailable_without_complete_read_only_decompile_proof", "StardewValley.NPC.Schedule", tick, "vanilla_1_6_npc")
        }, new[] { "npcs.schedules" }, "partial");
    }
}
