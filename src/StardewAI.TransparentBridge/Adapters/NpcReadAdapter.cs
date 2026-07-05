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
                ["friendships"] = Unavailable("world_not_ready", "Game1.player.friendshipData", tick, "vanilla_1_6_npc"),
                ["schedules"] = Unavailable("world_not_ready", "Game1.currentLocation.characters[].Schedule", tick, "vanilla_1_6_npc")
            }, new[] { "npcs.positions", "npcs.friendships", "npcs.schedules" }, "unavailable");
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

        var friendships = Game1.player?.friendshipData.Pairs
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
            .Select(pair => new
            {
                npc_name = pair.Key,
                points = pair.Value.Points,
                heart_level = pair.Value.Points / NPC.friendshipPointsPerHeartLevel,
                gifts_this_week = pair.Value.GiftsThisWeek,
                gifts_today = pair.Value.GiftsToday,
                talked_to_today = pair.Value.TalkedToToday,
                status = pair.Value.Status.ToString(),
                is_dating = pair.Value.IsDating(),
                is_engaged = pair.Value.IsEngaged(),
                is_married = pair.Value.IsMarried(),
                is_divorced = pair.Value.IsDivorced(),
                is_roommate = pair.Value.IsRoommate(),
                proposal_rejected = pair.Value.ProposalRejected,
                roommate_marriage = pair.Value.RoommateMarriage,
                last_gift_date_total_days = pair.Value.LastGiftDate?.TotalDays,
                wedding_date_total_days = pair.Value.WeddingDate?.TotalDays,
                next_birthing_date_total_days = pair.Value.NextBirthingDate?.TotalDays,
                proposer = pair.Value.Proposer
            })
            .OrderBy(friendship => friendship.npc_name, StringComparer.Ordinal)
            .ToArray();

        var fields = new Dictionary<string, object>
        {
            ["positions"] = Field(npcs, "Game1.currentLocation.characters[].Name/displayName/TilePoint/FacingDirection/currentLocation; Utility.isOnScreen(TilePoint, 128, Game1.currentLocation)", tick, "vanilla_1_6_npc"),
            ["friendships"] = friendships is null
                ? (object)Unavailable("player_unavailable", "Game1.player.friendshipData", tick, "vanilla_1_6_npc")
                : Field(friendships, "Game1.player.friendshipData.Pairs[].Key/Value Points/GiftsThisWeek/GiftsToday/TalkedToToday/Status/ProposalRejected/RoommateMarriage/LastGiftDate/WeddingDate/NextBirthingDate/Proposer", tick, "vanilla_1_6_npc"),
            ["schedules"] = Field(ReadLoadedSchedules(location.characters), "Game1.currentLocation.characters[].Schedule/ScheduleKey/followSchedule/ignoreScheduleToday", tick, "vanilla_1_6_npc")
        };

        var unavailable = friendships is null
            ? new[] { "npcs.friendships" }
            : Array.Empty<string>();

        return Section("npcs", fields, unavailable, unavailable.Length == 0 ? "complete" : "partial");
    }

    private static object[] ReadLoadedSchedules(IEnumerable<NPC> npcs)
    {
        return npcs
            .Where(npc => npc is not null)
            .Select(npc => new
            {
                name = npc.Name,
                schedule_key = npc.ScheduleKey,
                follow_schedule = npc.followSchedule,
                ignore_schedule_today = npc.ignoreScheduleToday,
                schedule_loaded = npc.Schedule is not null,
                entries = npc.Schedule is null
                    ? Array.Empty<object>()
                    : npc.Schedule
                        .OrderBy(entry => entry.Key)
                        .Select(entry => new
                        {
                            time = entry.Key,
                            target_location_name = entry.Value.targetLocationName,
                            target_tile_x = entry.Value.targetTile.X,
                            target_tile_y = entry.Value.targetTile.Y,
                            facing_direction = entry.Value.facingDirection,
                            end_behavior = entry.Value.endOfRouteBehavior,
                            end_message = entry.Value.endOfRouteMessage,
                            route_count = entry.Value.route?.Count ?? 0
                        })
                        .Cast<object>()
                        .ToArray()
            })
            .OrderBy(schedule => schedule.name, StringComparer.Ordinal)
            .ToArray();
    }
}
