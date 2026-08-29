using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string WarpTotemNativeContract =
        "Object.performUseAction((O)261|688|689|690|886)->2000ms_totem_animation->Object.totemWarp->1000ms_fadeAfterDelay->Object.totemWarpForReal->Farm_WarpTotemEntry_or_variant_destination->Game1.warpFarmer->active_or_passive_festival_routing";

    private static readonly IReadOnlyDictionary<string, WarpTotemVariant> WarpTotemVariants =
        new Dictionary<string, WarpTotemVariant>(StringComparer.Ordinal)
        {
            ["688"] = new("Farm", 48, 7, "LimeGreen"),
            ["689"] = new("Mountain", 31, 20, "OrangeRed"),
            ["690"] = new("Beach", 20, 4, "LightBlue"),
            ["261"] = new("Desert", 35, 43, "255,200,0,255"),
            ["886"] = new("IslandSouth", 11, 11, "LightBlue")
        };

    private static object ReadWarpTotemContext(Farmer? player)
    {
        if (player?.currentLocation is not { } location)
            return new { projection_status = "unavailable_world_player_or_location", rows = Array.Empty<object>() };

        var inventoryEntries = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                WarpTotemVariants.ContainsKey(entry.item.ItemId))
            .ToArray();
        var visibleItem = inventoryEntries.Any(entry =>
            entry.item is StardewValley.Object obj && !obj.isTemporarilyInvisible && obj.Stack > 0);
        var commonBaseGate = player.canMove && visibleItem && !Game1.eventUp && !Game1.isFestival() &&
            !Game1.fadeToBlack && !player.swimming.Value && !player.bathingClothes.Value &&
            !player.onBridge.Value && Game1.activeClickableMenu is null;
        var rows = inventoryEntries.Select(entry =>
        {
            var item = (StardewValley.Object)entry.item!;
            var route = ResolveWarpTotemRoute(player, item.ItemId);
            var dataAvailable = Game1.objectData.TryGetValue(item.ItemId, out var objectData);
            var nameContainsTotem = dataAvailable && objectData!.Name.Contains("Totem", StringComparison.Ordinal);
            var rowGate = !commonBaseGate || item.isTemporarilyInvisible || item.Stack <= 0
                ? "blocked_base_object_use_gate"
                : !nameContainsTotem
                    ? "blocked_object_data_name_not_totem"
                    : !route.route_complete
                        ? "blocked_destination_route_unavailable"
                        : route.festival_prestart_warp_cancelled
                            ? "blocked_festival_not_started_consumption_without_warp"
                            : route.festival_ready_check_required
                                ? "blocked_multiplayer_festival_ready_check_required"
                                : route.already_at_exact_destination
                                    ? "blocked_already_at_exact_destination"
                                    : "ready";
            return new
            {
                inventory_slot_index = entry.slot,
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                display_name = item.DisplayName,
                inventory_runtime_type = item.GetType().FullName,
                stack_before = item.Stack,
                stack_after = Math.Max(0, item.Stack - 1),
                temporarily_invisible = item.isTemporarilyInvisible,
                object_data_available = dataAvailable,
                object_data_name = dataAvailable ? objectData!.Name : string.Empty,
                object_data_type = dataAvailable ? objectData!.Type : string.Empty,
                object_data_price = dataAvailable ? objectData!.Price : -1,
                object_data_context_tags = dataAvailable ? objectData!.ContextTags ?? new List<string>() : new List<string>(),
                object_name_contains_totem = nameContainsTotem,
                native_use_gate_status = rowGate,
                glow_color_rgba = WarpTotemVariants[item.ItemId].GlowColor,
                destination_route = route
            };
        }).ToArray();
        var gateStatus = rows.Length == 0 ? "blocked_no_inventory_warp_totem" :
            !commonBaseGate ? "blocked_base_object_use_gate" :
            rows.Any(row => row.native_use_gate_status == "ready") ? "ready" :
            rows.Length == 1 ? rows[0].native_use_gate_status : "blocked_no_ready_warp_totem_variant";
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "warp_totem.v1",
            location = location.NameOrUniqueName,
            tile_x = player.TilePoint.X,
            tile_y = player.TilePoint.Y,
            time_of_day = Game1.timeOfDay,
            season = Game1.season.ToString(),
            day = Game1.dayOfMonth,
            Game1.IsMultiplayer,
            Game1.weatherIcon,
            Game1.whereIsTodaysFest,
            active_passive_festivals = Game1.netWorldState.Value.ActivePassiveFestivals.ToArray(),
            commonBaseGate,
            rows
        }));

        return new
        {
            schema_version = "warp_totem.v1",
            projection_status = "complete_current_native_warp_totem_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            native_use_gate_status = gateStatus,
            native_base_use_gate = new
            {
                can_move = player.canMove,
                visible_warp_totem_available = visibleItem,
                event_up = Game1.eventUp,
                festival_event_active = Game1.isFestival(),
                fade_to_black = Game1.fadeToBlack,
                swimming = player.swimming.Value,
                bathing_clothes = player.bathingClothes.Value,
                on_bridge = player.onBridge.Value,
                active_menu_clear = Game1.activeClickableMenu is null,
                passed = commonBaseGate
            },
            native_animation_contract = new
            {
                facing_direction = 2,
                animation_duration_ms = 2000,
                totem_callback_delay_ms = 1000,
                initial_item_sprite_count = 3,
                sprinkle_sprite_count = 65,
                sprinkle_duration_ms = 1300,
                sprinkle_interval_ms = 20,
                poof_sprite_count = 12,
                trail_sprite_count = 17,
                trail_delay_step_ms = 25,
                initial_sound = "warrior",
                warp_sound = "wand",
                initial_invincibility_timer = -4000,
                warp_invincibility_timer = -2000,
                warp_freeze_pause_ms = 1000,
                visual_randomness = "Game1.random_visual_only_no_destination_effect"
            },
            native_contract = WarpTotemNativeContract,
            rows
        };
    }

    private static WarpTotemRoute ResolveWarpTotemRoute(Farmer player, string itemId)
    {
        if (!WarpTotemVariants.TryGetValue(itemId, out var variant))
            return WarpTotemRoute.Unavailable("unknown_warp_totem_item_id");

        var baseDestination = variant.DestinationLocation;
        var requestedX = variant.TileX;
        var requestedY = variant.TileY;
        var farmSource = "fixed_variant";
        if (itemId == "688")
        {
            if (Game1.getFarm().TryGetMapPropertyAs("WarpTotemEntry", out Point parsed, required: false))
            {
                requestedX = parsed.X;
                requestedY = parsed.Y;
                farmSource = "map_property_WarpTotemEntry";
            }
            else
            {
                (requestedX, requestedY, farmSource) = Game1.whichFarm switch
                {
                    6 => (82, 29, "fallback_beach_farm"),
                    5 => (48, 39, "fallback_four_corners_farm"),
                    _ => (48, 7, "fallback_default")
                };
            }
        }

        var effectiveDestination = baseDestination;
        var passiveRows = new List<WarpTotemPassiveRoute>();
        foreach (var festivalId in Game1.netWorldState.Value.ActivePassiveFestivals)
        {
            if (!Utility.TryGetPassiveFestivalData(festivalId, out var data) || data is null ||
                Game1.dayOfMonth < data.StartDay || Game1.dayOfMonth > data.EndDay || data.Season != Game1.season ||
                data.MapReplacements is null || !data.MapReplacements.TryGetValue(effectiveDestination, out var replacement))
                continue;
            passiveRows.Add(new WarpTotemPassiveRoute(festivalId, effectiveDestination, replacement));
            effectiveDestination = replacement;
        }
        var passiveJson = JsonSerializer.Serialize(passiveRows);
        var routeMode = passiveRows.Count > 0 ? "passive_festival_replacement" : "ordinary";
        var activeFestival = ReadWarpTotemActiveFestival(baseDestination);
        if (!activeFestival.RouteComplete)
            return WarpTotemRoute.Unavailable(activeFestival.Status);

        var festivalPrestartCancelled = activeFestival.TargetsDestination && Game1.timeOfDay < activeFestival.StartTime;
        var activeFestivalWindow = activeFestival.TargetsDestination &&
            Game1.timeOfDay >= activeFestival.StartTime && Game1.timeOfDay <= activeFestival.EndTime;
        var readyCheckRequired = activeFestivalWindow && Game1.IsMultiplayer;
        var effectiveX = requestedX;
        var effectiveY = requestedY;
        if (activeFestivalWindow && !readyCheckRequired)
        {
            routeMode = "active_festival_entry";
            effectiveDestination = baseDestination;
            effectiveX = activeFestival.EntryTileX;
            effectiveY = activeFestival.EntryTileY;
        }
        else
        {
            var target = Game1.getLocationFromName(effectiveDestination);
            if (target?.Map?.Layers.Count > 0 && effectiveX >= target.Map.Layers[0].LayerWidth - 1)
                effectiveX--;
        }
        var targetLocationAvailable = activeFestivalWindow || Game1.getLocationFromName(effectiveDestination) is not null;
        var alreadyAtDestination = !activeFestivalWindow && targetLocationAvailable &&
            string.Equals(player.currentLocation?.NameOrUniqueName, effectiveDestination, StringComparison.Ordinal) &&
            player.TilePoint.X == effectiveX && player.TilePoint.Y == effectiveY;

        return new WarpTotemRoute
        {
            route_complete = targetLocationAvailable,
            route_status = targetLocationAvailable ? "complete" : "destination_location_unavailable",
            base_destination_location_id = baseDestination,
            requested_destination_tile_x = requestedX,
            requested_destination_tile_y = requestedY,
            effective_destination_location_id = effectiveDestination,
            effective_destination_tile_x = effectiveX,
            effective_destination_tile_y = effectiveY,
            destination_route_mode = routeMode,
            farm_destination_source = farmSource,
            passive_festival_route_json = passiveJson,
            active_festival_id = activeFestival.FestivalId,
            active_festival_location_id = activeFestival.LocationId,
            active_festival_start_time = activeFestival.StartTime,
            active_festival_end_time = activeFestival.EndTime,
            active_festival_entry_tile_x = activeFestival.EntryTileX,
            active_festival_entry_tile_y = activeFestival.EntryTileY,
            active_festival_entry_facing = activeFestival.EntryFacing,
            festival_prestart_warp_cancelled = festivalPrestartCancelled,
            festival_ready_check_required = readyCheckRequired,
            already_at_exact_destination = alreadyAtDestination
        };
    }

    private static WarpTotemActiveFestival ReadWarpTotemActiveFestival(string destinationLocation)
    {
        if (!Utility.isFestivalDay())
            return WarpTotemActiveFestival.None;
        try
        {
            var festivalId = Game1.season.ToString().ToLowerInvariant() + Game1.dayOfMonth;
            var data = Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + festivalId);
            if (!data.TryGetValue("conditions", out var rawConditions))
                return WarpTotemActiveFestival.Unavailable("festival_conditions_missing");
            var conditionParts = rawConditions.Split('/');
            if (conditionParts.Length < 2)
                return WarpTotemActiveFestival.Unavailable("festival_conditions_invalid");
            var times = ArgUtility.SplitBySpace(conditionParts[1]);
            if (times.Length < 2 || !int.TryParse(times[0], out var start) || !int.TryParse(times[1], out var end))
                return WarpTotemActiveFestival.Unavailable("festival_time_window_invalid");
            var locationId = conditionParts[0];
            var entryX = -1;
            var entryY = -1;
            var entryFacing = -1;
            if (data.TryGetValue("set-up", out var setup))
            {
                foreach (var command in setup.Split('/').Select(value => ArgUtility.SplitBySpace(value)))
                {
                    if (command.Length >= 4 && string.Equals(command[0], "farmer", StringComparison.Ordinal) &&
                        int.TryParse(command[1], out entryX) && int.TryParse(command[2], out entryY) &&
                        int.TryParse(command[3], out entryFacing))
                        break;
                }
            }
            var targets = string.Equals(locationId, destinationLocation, StringComparison.Ordinal);
            if (targets && (entryX < 0 || entryY < 0 || entryFacing < 0))
                return WarpTotemActiveFestival.Unavailable("festival_farmer_entry_missing");
            return new WarpTotemActiveFestival(true, "complete", festivalId, locationId, start, end,
                entryX, entryY, entryFacing, targets);
        }
        catch
        {
            return WarpTotemActiveFestival.Unavailable("festival_data_load_failed");
        }
    }

    private sealed record WarpTotemVariant(string DestinationLocation, int TileX, int TileY, string GlowColor);
    private sealed record WarpTotemPassiveRoute(
        string festival_id,
        string source_location_id,
        string replacement_location_id);
    private sealed record WarpTotemActiveFestival(
        bool RouteComplete,
        string Status,
        string FestivalId,
        string LocationId,
        int StartTime,
        int EndTime,
        int EntryTileX,
        int EntryTileY,
        int EntryFacing,
        bool TargetsDestination)
    {
        public static WarpTotemActiveFestival None { get; } =
            new(true, "not_festival_day", string.Empty, string.Empty, -1, -1, -1, -1, -1, false);

        public static WarpTotemActiveFestival Unavailable(string status) =>
            new(false, status, string.Empty, string.Empty, -1, -1, -1, -1, -1, false);
    }

    private sealed class WarpTotemRoute
    {
        public bool route_complete { get; init; }
        public string route_status { get; init; } = string.Empty;
        public string base_destination_location_id { get; init; } = string.Empty;
        public int requested_destination_tile_x { get; init; }
        public int requested_destination_tile_y { get; init; }
        public string effective_destination_location_id { get; init; } = string.Empty;
        public int effective_destination_tile_x { get; init; }
        public int effective_destination_tile_y { get; init; }
        public string destination_route_mode { get; init; } = string.Empty;
        public string farm_destination_source { get; init; } = string.Empty;
        public string passive_festival_route_json { get; init; } = "[]";
        public string active_festival_id { get; init; } = string.Empty;
        public string active_festival_location_id { get; init; } = string.Empty;
        public int active_festival_start_time { get; init; } = -1;
        public int active_festival_end_time { get; init; } = -1;
        public int active_festival_entry_tile_x { get; init; } = -1;
        public int active_festival_entry_tile_y { get; init; } = -1;
        public int active_festival_entry_facing { get; init; } = -1;
        public bool festival_prestart_warp_cancelled { get; init; }
        public bool festival_ready_check_required { get; init; }
        public bool already_at_exact_destination { get; init; }

        public static WarpTotemRoute Unavailable(string status) => new()
        {
            route_status = status
        };
    }
}
