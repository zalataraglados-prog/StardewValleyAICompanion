using System.Security.Cryptography;
using System.Text;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Network;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class NpcReadAdapter
{
    private static object ReadGrandpaScheduleCatalog()
    {
        var rows = new List<object>();
        var fingerprint = new StringBuilder();
        var mailOrWorldStateConditionIds = new HashSet<string>(StringComparer.Ordinal);
        Utility.ForEachVillager(npc =>
        {
            var masterDataPresent = NPC.TryGetData(npc.Name, out var characterData);
            var friendshipRowExists = Game1.player.friendshipData.TryGetValue(
                npc.Name,
                out var friendship);
            var rawData = npc.getMasterScheduleRawData();
            var scheduleAssetName = npc.Name == "Leo" && npc.DefaultMap != "IslandHut"
                ? "Characters/schedules/LeoMainland"
                : "Characters/schedules/" + npc.Name;
            fingerprint
                .Append(npc.Name).Append('\u001f')
                .Append(scheduleAssetName).Append('\u001f')
                .Append(rawData is null ? "<missing>" : "<present>").Append('\u001e');
            var entries = rawData is null
                ? Array.Empty<object>()
                : rawData
                    .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry =>
                    {
                        var raw = entry.Value ?? string.Empty;
                        var rawSha256 = ScheduleSha256(raw);
                        fingerprint
                            .Append(npc.Name).Append('\u001f')
                            .Append(entry.Key).Append('\u001f')
                            .Append(rawSha256).Append('\u001e');
                        return (object)new
                        {
                            schedule_key = entry.Key,
                            raw_schedule = raw,
                            raw_schedule_sha256 = rawSha256
                        };
                    })
                    .ToArray();
            if (rawData is not null)
            {
                foreach (var rawSchedule in rawData.Values)
                {
                    foreach (var command in NPC.SplitScheduleCommands(rawSchedule ?? string.Empty))
                    {
                        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 2 && string.Equals(tokens[0], "MAIL", StringComparison.Ordinal))
                            mailOrWorldStateConditionIds.Add(tokens[1]);
                    }
                }
            }
            var allPlayerFriendshipPoints = Utility.GetAllPlayerFriendshipLevel(npc);
            var maximumFarmerFriendshipHearts = Game1.getAllFarmers()
                .Select(farmer => farmer.getFriendshipHeartLevelForNPC(npc.Name))
                .DefaultIfEmpty(0)
                .Max();
            rows.Add(new
            {
                npc_name = npc.Name,
                runtime_type = npc.GetType().FullName,
                is_villager = npc.IsVillager,
                event_actor = npc.EventActor,
                schedule_asset_name = scheduleAssetName,
                default_map = npc.DefaultMap,
                default_tile_x = (int)(npc.DefaultPosition.X / 64f),
                default_tile_y = (int)(npc.DefaultPosition.Y / 64f),
                currently_married = npc.isMarried(),
                is_child = npc is Child,
                is_player_spouse = string.Equals(
                    Game1.player.spouse,
                    npc.Name,
                    StringComparison.Ordinal),
                simple_non_villager_npc = npc.SimpleNonVillagerNPC,
                gift_taste_master_data_present =
                    Game1.NPCGiftTastes?.ContainsKey(npc.Name) == true,
                birthday_season = npc.Birthday_Season,
                birthday_day = npc.Birthday_Day,
                is_birthday_on_capture_date = npc.isBirthday(),
                friendship_row_exists = friendshipRowExists,
                talked_to_today = friendshipRowExists &&
                    friendship!.TalkedToToday,
                gifts_today = friendshipRowExists
                    ? friendship!.GiftsToday
                    : (int?)null,
                gifts_this_week = friendshipRowExists
                    ? friendship!.GiftsThisWeek
                    : (int?)null,
                friendship_is_divorced = friendshipRowExists &&
                    friendship!.IsDivorced(),
                island_schedule_name = npc.islandScheduleName.Value ?? string.Empty,
                all_player_friendship_points = allPlayerFriendshipPoints,
                friendship_heart_selector = Math.Max(0, allPlayerFriendshipPoints) / 250,
                maximum_farmer_friendship_hearts = maximumFarmerFriendshipHearts,
                current_location_name = npc.currentLocation?.NameOrUniqueName ?? string.Empty,
                current_location_weather_available = npc.currentLocation is not null,
                current_location_is_raining = npc.currentLocation?.IsRainingHere() ?? false,
                vanilla_social_query_supported = SupportsVanillaSocialQueries(npc),
                character_master_data_present = masterDataPresent,
                can_socialize_condition = characterData?.CanSocialize,
                can_socialize_now = npc.CanSocialize,
                can_receive_gifts_data = characterData?.CanReceiveGifts,
                can_receive_gifts_now = npc.CanReceiveGifts(),
                master_schedule_present = rawData is not null,
                master_schedule_entry_count = entries.Length,
                master_schedule_entries = entries
            });
            return true;
        }, includeEventActors: false);

        return new
        {
            population_owner = "Utility.ForEachVillager(includeEventActors:false)",
            selection_owner = "NPC.TryLoadSchedule",
            parser_owner = "NPC.parseMasterSchedule",
            selection_precedence = new[]
            {
                "year1_green_rain",
                "explicit_island_schedule",
                "active_passive_festival_marriage_variant_then_base_variant",
                "marriage_date_or_job_or_nonrain_weekday",
                "season_day",
                "day_friendship_heart_variant_descending",
                "day",
                "pam_bus_unlock",
                "rain2_random_then_rain",
                "season_weekday_friendship_variant_descending",
                "season_weekday",
                "weekday_friendship_variant_descending",
                "weekday",
                "season",
                "spring_weekday",
                "spring"
            },
            required_future_scenario_inputs = new[]
            {
                "year_season_day_and_weekday",
                "green_rain",
                "active_passive_festivals_and_day_indexes",
                "marriage_and_island_assignment_state",
                "all_player_friendship_points",
                "mail_and_world_state_ids",
                "location_accessibility",
                "location_specific_rain",
                "rain2_random_branch_when_present"
            },
            exact_current_loaded_schedule_field = "npcs.schedules",
            current_selection_context = new
            {
                capture_total_days = Game1.Date.TotalDays,
                year = Game1.year,
                season = Game1.currentSeason,
                day_of_month = Game1.dayOfMonth,
                weekday = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth),
                game_time = Game1.timeOfDay,
                real_milliseconds_per_game_ten_minutes = Game1.realMilliSecondsPerGameTenMinutes,
                day_start_capture = Game1.timeOfDay == 600,
                is_green_rain = Game1.isGreenRain,
                valley_is_raining = Game1.isRaining,
                active_passive_festivals = Game1.netWorldState.Value.ActivePassiveFestivals
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .Select(id => new
                    {
                        festival_id = id,
                        day_index = Utility.GetDayOfPassiveFestival(id)
                    })
                    .ToArray(),
                current_player_mail_received = Game1.player.mailReceived
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                master_player_mail_received = Game1.MasterPlayer.mailReceived
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                mail_or_world_state_conditions = mailOrWorldStateConditionIds
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .Select(id => new
                    {
                        condition_id = id,
                        condition_met = Game1.MasterPlayer.mailReceived.Contains(id) ||
                            NetWorldState.checkAnywhereForWorldStateID(id)
                    })
                    .ToArray(),
                location_accessibility = new[]
                {
                    new { location_name = "CommunityCenter", accessible = Game1.isLocationAccessible("CommunityCenter") },
                    new { location_name = "JojaMart", accessible = Game1.isLocationAccessible("JojaMart") },
                    new { location_name = "Railroad", accessible = Game1.isLocationAccessible("Railroad") }
                },
                rain2_random_roll_observed = false,
                status = "complete_live_current_inputs_except_unobserved_rain2_roll"
            },
            catalog_sha256 = ScheduleSha256(fingerprint.ToString()),
            villagers = rows.ToArray(),
            projection_status = "complete_live_master_schedule_catalog_conditional_future_resolution_pending"
        };
    }

    private static string ScheduleSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
