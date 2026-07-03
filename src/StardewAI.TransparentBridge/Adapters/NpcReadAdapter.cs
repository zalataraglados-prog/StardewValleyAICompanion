using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class NpcReadAdapter : ReadAdapterBase
{
    public override string Domain => "npcs";
    public override int Priority => 40;

    public override StateAdapterResult Collect(long tick)
    {
        var location = Context.IsWorldReady ? Game1.currentLocation : null;
        var npcs = location?.characters
            .Select(npc => new
            {
                name = npc.Name,
                display_name = npc.displayName,
                location = location.NameOrUniqueName,
                tile = new { x = npc.Tile.X, y = npc.Tile.Y },
                facing_direction = npc.FacingDirection
            })
            .ToArray();

        return Section("npcs", new Dictionary<string, object>
        {
            ["current_location_npc_count"] = Field(npcs?.Length, "Game1.currentLocation.characters", tick),
            ["current_location_npcs"] = Field(npcs, "Game1.currentLocation.characters", tick),
            ["active_event_id"] = Field(Game1.CurrentEvent?.id, "Game1.CurrentEvent.id", tick),
            ["schedules"] = Unavailable("npc_schedule_adapter_not_implemented", "NPC.Schedule", tick)
        }, new[]
        {
            "npcs.global_positions",
            "npcs.schedules",
            "npcs.friendship_details"
        });
    }
}
