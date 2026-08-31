using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Monsters;
using SObject = StardewValley.Object;

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
                ["positions"] = Unavailable("world_not_ready", "Utility.ForEachLocation(includeInteriors:true, includeGenerated:true): Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels[].characters", tick, "vanilla_1_6_npc"),
                ["friendships"] = Unavailable("world_not_ready", "Game1.player.friendshipData", tick, "vanilla_1_6_npc"),
                ["schedules"] = Unavailable("world_not_ready", "Utility.ForEachLocation(includeInteriors:true, includeGenerated:true): Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels[].characters[].Schedule", tick, "vanilla_1_6_npc"),
                ["social_interaction"] = Unavailable("world_not_ready", "Utility.ForEachLocation(includeInteriors:true, includeGenerated:true): Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels[].characters social fields", tick, "vanilla_1_6_npc"),
                ["gift_tastes"] = Unavailable("world_not_ready", "Utility.ForEachLocation(includeInteriors:true, includeGenerated:true): Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels[].characters[].getGiftTasteForThisItem; transparent gift taste derivation unavailable", tick, "vanilla_1_6_npc")
            }, new[] { "npcs.positions", "npcs.friendships", "npcs.schedules", "npcs.social_interaction", "npcs.gift_tastes" }, "unavailable");
        }

        var allLoadedNpcs = CollectAllLoadedNpcs();
        var npcs = allLoadedNpcs
            .Select(npc =>
            {
                var tile = npc.TilePoint;
                var npcLocation = npc.currentLocation;
                var isCurrentLocation = ReferenceEquals(npcLocation, Game1.currentLocation);
                var visibleOnScreen = isCurrentLocation && Utility.isOnScreen(tile, 128, npcLocation);

                var locationId = npc.currentLocation?.NameOrUniqueName ?? "";
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
                    is_monster = npc.IsMonster,
                    monster = npc is Monster monster ? ReadMonster(monster) : null
                };
            })
            .OrderBy(npc => npc.name, StringComparer.Ordinal)
            .ThenBy(npc => npc.location_id, StringComparer.Ordinal)
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
            ["positions"] = Field(npcs, "Utility.ForEachLocation(includeInteriors:true, includeGenerated:true): Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels[].characters[].Name/displayName/TilePoint/FacingDirection/currentLocation; Utility.isOnScreen current-location only; Monster Health/MaxHealth/DamageToFarmer/resilience", tick, "vanilla_1_6_npc"),
            ["friendships"] = friendships is null
                ? (object)Unavailable("player_unavailable", "Game1.player.friendshipData", tick, "vanilla_1_6_npc")
                : Field(friendships, "Game1.player.friendshipData.Pairs[].Key/Value Points/GiftsThisWeek/GiftsToday/TalkedToToday/Status/ProposalRejected/RoommateMarriage/LastGiftDate/WeddingDate/NextBirthingDate/Proposer", tick, "vanilla_1_6_npc"),
            ["schedules"] = Field(ReadLoadedSchedules(allLoadedNpcs), "Utility.ForEachLocation(includeInteriors:true, includeGenerated:true): Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels[].characters[].Schedule/ScheduleKey/followSchedule/ignoreScheduleToday", tick, "vanilla_1_6_npc"),
            ["social_interaction"] = Field(ReadSocialInteractions(allLoadedNpcs), "Utility.ForEachLocation(includeInteriors:true, includeGenerated:true): Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels[].characters raw fields plus NPC.CanSocialize/CanReceiveGifts when runtime type uses vanilla non-overridden query paths", tick, "vanilla_1_6_npc"),
            ["gift_tastes"] = Field(ReadGiftTastes(allLoadedNpcs, Game1.player), "Utility.ForEachLocation(includeInteriors:true, includeGenerated:true): Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels[].characters[].getGiftTasteForThisItem; NPC.getGiftTasteForThisItem(current owned Object items) for supported vanilla NPC query paths; expected delta only when Farmer.changeFriendship deterministic modifiers and cap are transparent", tick, "vanilla_1_6_npc")
        };

        var unavailable = friendships is null
            ? new[] { "npcs.friendships" }
            : Array.Empty<string>();

        return Section("npcs", fields, unavailable, unavailable.Length == 0 ? "complete" : "partial");
    }

    private static object ReadMonster(Monster monster)
    {
        return new
        {
            type = monster.GetType().FullName,
            health = monster.Health,
            max_health = monster.MaxHealth,
            damage_to_farmer = monster.DamageToFarmer,
            resilience = monster.resilience.Value,
            is_glider = monster.isGlider.Value,
            ignore_damage_los = monster.ignoreDamageLOS.Value,
            focused_on_farmers = monster.focusedOnFarmers,
            invisible = monster.IsInvisible,
            invincible = monster.isInvincible()
        };
    }

    internal static NPC[] CollectAllLoadedNpcs()
    {
        var seen = new HashSet<NPC>(ReferenceEqualityComparer.Instance);
        var firstSeen = new List<NPC>();
        Utility.ForEachLocation(delegate(GameLocation location)
        {
            if (location?.characters is not null)
            {
                foreach (var npc in location.characters)
                {
                    if (npc is not null && seen.Add(npc))
                    {
                        firstSeen.Add(npc);
                    }
                }
            }
            return true;
        }, includeInteriors: true, includeGenerated: true);
        return firstSeen
            .OrderBy(npc => npc.Name, StringComparer.Ordinal)
            .ThenBy(npc => npc.currentLocation?.NameOrUniqueName ?? "", StringComparer.Ordinal)
            .ThenBy(npc => npc.TilePoint.X)
            .ThenBy(npc => npc.TilePoint.Y)
            .ToArray();
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

    private static object[] ReadSocialInteractions(IEnumerable<NPC> npcs)
    {
        return npcs
            .Where(npc => npc is not null)
            .Select(npc =>
            {
                var tile = npc.TilePoint;
                var bounds = npc.GetBoundingBox();
                var socialQuerySupported = SupportsVanillaSocialQueries(npc);
                var masterDataPresent = NPC.TryGetData(npc.Name, out var data);
                var giftTastePresent = Game1.NPCGiftTastes?.ContainsKey(npc.Name) == true;
                var canSocialize = socialQuerySupported && npc.CanSocialize;
                var canReceiveGifts = socialQuerySupported && npc.CanReceiveGifts();
                var npcCurrentLocation = npc.currentLocation;
                var instanceLoaded = npcCurrentLocation is not null &&
                    npcCurrentLocation.characters.Any(c => ReferenceEquals(c, npc));
                return new
                {
                    name = npc.Name,
                    display_name = npc.displayName,
                    runtime_type = npc.GetType().FullName,
                    vanilla_social_query_supported = socialQuerySupported,
                    master_data_present = masterDataPresent,
                    gift_taste_master_data_present = giftTastePresent,
                    current_instance_loaded = instanceLoaded,
                    location_id = npc.currentLocation?.NameOrUniqueName ?? "",
                    tile_x = tile.X,
                    tile_y = tile.Y,
                    bounding_box_x = bounds.X,
                    bounding_box_y = bounds.Y,
                    bounding_box_width = bounds.Width,
                    bounding_box_height = bounds.Height,
                    facing_direction = npc.FacingDirection,
                    is_villager = npc.IsVillager,
                    is_child = npc is Child,
                    is_datably_flagged = npc.datable.Value,
                    is_married_or_engaged = npc.isMarriedOrEngaged(),
                    spouse_name = npc.getSpouse()?.Name ?? string.Empty,
                    simple_non_villager_npc = npc.SimpleNonVillagerNPC,
                    is_invisible = npc.IsInvisible,
                    is_sleeping = npc.isSleeping.Value,
                    has_controller = npc.controller is not null,
                    is_busy = npc.isMoving() || npc.IsEmoting || npc.shouldPlayRobinHammerAnimation.Value || npc.shouldPlaySpousePatioAnimation.Value,
                    follow_schedule = npc.followSchedule,
                    ignore_schedule_today = npc.ignoreScheduleToday,
                    schedule_loaded = npc.Schedule is not null,
                    schedule_key = npc.ScheduleKey,
                    can_socialize = canSocialize,
                    can_socialize_complete = socialQuerySupported,
                    can_socialize_status = socialQuerySupported ? "complete_live_vanilla_query" : "unavailable_runtime_type_or_override_not_proven_pure",
                    can_receive_gifts = canReceiveGifts,
                    can_receive_gifts_complete = socialQuerySupported,
                    can_receive_gifts_status = socialQuerySupported ? "complete_live_vanilla_query" : "unavailable_runtime_type_or_override_not_proven_pure",
                    character_can_receive_gifts_data = data?.CanReceiveGifts,
                    birthday_season = npc.Birthday_Season,
                    birthday_day = npc.Birthday_Day,
                    is_birthday = npc.isBirthday(),
                    current_route_window_complete = true,
                    current_route_window_status = "current_position_only_from_loaded_instance; future_schedule_projection_not_emitted"
                };
            })
            .OrderBy(npc => npc.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static object[] ReadGiftTastes(IEnumerable<NPC> npcs, Farmer? player)
    {
        if (player is null)
        {
            return Array.Empty<object>();
        }

        return npcs
            .Where(npc => npc is not null)
            .SelectMany(npc => player.Items.Select((item, index) => ReadGiftTaste(npc, item, index, player)))
            .Where(row => row is not null)
            .Cast<object>()
            .OrderBy(row => row.GetType().GetProperty("npc_name")?.GetValue(row)?.ToString(), StringComparer.Ordinal)
            .ThenBy(row => (int)(row.GetType().GetProperty("slot_index")?.GetValue(row) ?? 0))
            .ToArray();
    }

    private static object? ReadGiftTaste(NPC npc, Item? item, int slotIndex, Farmer player)
    {
        if (item is not SObject obj)
        {
            return null;
        }

        var supported = SupportsVanillaSocialQueries(npc);
        if (!supported || !npc.CanReceiveGifts())
        {
            return new
            {
                npc_name = npc.Name,
                slot_index = slotIndex,
                qualified_item_id = item.QualifiedItemId,
                quality = item.Quality,
                complete = false,
                status = supported ? "npc_cannot_receive_gifts" : "runtime_type_or_override_not_proven_pure"
            };
        }

        var taste = npc.getGiftTasteForThisItem(item);
        var delta = ExpectedGiftDelta(npc, obj, player, taste);
        return new
        {
            npc_name = npc.Name,
            slot_index = slotIndex,
            qualified_item_id = item.QualifiedItemId,
            quality = item.Quality,
            taste_code = taste,
            taste = TasteLabel(taste),
            expected_friendship_delta = delta?.ToString(),
            expected_friendship_delta_complete = delta.HasValue,
            complete = delta.HasValue,
            status = delta.HasValue ? "complete_live_vanilla_taste_and_delta" : "taste_complete_delta_incomplete_due_to_modifier_or_cap"
        };
    }

    private static int? ExpectedGiftDelta(NPC npc, SObject obj, Farmer player, int taste)
    {
        var friendshipPoints = player.friendshipData.TryGetValue(npc.Name, out var friendship) ? friendship.Points : 0;

        var friendshipMultiplier = npc.isBirthday() ? 8f : 1f;
        if (npc.getSpouse()?.Equals(player) == true)
        {
            friendshipMultiplier /= 2f;
        }

        var qualityMultiplier = obj.Quality switch
        {
            1 => 1.1f,
            2 => 1.25f,
            4 => 1.5f,
            _ => 1f
        };

        var raw = taste switch
        {
            7 => Math.Min(750, (int)(250f * friendshipMultiplier)),
            0 => (int)(80f * friendshipMultiplier * qualityMultiplier),
            6 => (int)(-40f * friendshipMultiplier),
            2 => (int)(45f * friendshipMultiplier * qualityMultiplier),
            4 => (int)(-20f * friendshipMultiplier),
            _ => (int)(20f * friendshipMultiplier)
        };

        if (raw > 0 && npc.isDivorcedFrom(player))
        {
            raw = 0;
        }
        if (raw > 0 && player.stats.Get("Book_Friendship") != 0)
        {
            raw = (int)(raw * 1.1f);
        }
        if (raw > 0 && npc.SpeaksDwarvish() && !player.canUnderstandDwarves)
        {
            raw = 0;
        }
        if (raw > 0 && npc.Equals(player.getSpouse()))
        {
            raw = (int)(raw * 0.66f);
        }

        var maxPoints = (Utility.GetMaximumHeartsForCharacter(npc) + 1) * NPC.friendshipPointsPerHeartLevel - 1;
        return Math.Max(0, Math.Min(friendshipPoints + raw, maxPoints)) - friendshipPoints;
    }

    private static string TasteLabel(int taste)
    {
        return taste switch
        {
            7 => "stardrop_tea",
            0 => "love",
            2 => "like",
            4 => "dislike",
            6 => "hate",
            _ => "neutral"
        };
    }

    internal static bool SupportsVanillaSocialQueries(NPC npc)
    {
        return npc.GetType().Assembly == typeof(NPC).Assembly &&
            npc.GetType().GetProperty(nameof(NPC.CanSocialize))?.GetMethod?.DeclaringType == typeof(NPC) &&
            npc.GetType().GetMethod(nameof(NPC.CheckTasteContextTags))?.DeclaringType == typeof(NPC) &&
            npc.GetType().GetMethod(nameof(NPC.receiveGift))?.DeclaringType == typeof(NPC) &&
            npc.GetType().GetMethod(nameof(NPC.tryToReceiveActiveObject))?.DeclaringType == typeof(NPC);
    }
}
