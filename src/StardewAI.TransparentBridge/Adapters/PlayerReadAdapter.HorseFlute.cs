using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string HorseFluteNativeContract =
        "Object.performUseAction((O)911)->Utility.GetHorseWarpRestrictionsForFarmer(start+delayed)->FarmerTeam.requestHorseWarpEvent->OnRequestHorseWarp->Horse.mutex->Game1.warpCharacter";

    private static object ReadHorseFluteContext(Farmer? player)
    {
        if (player is null || player.currentLocation is not { } location)
            return new { projection_status = "unavailable_world_player_or_location", rows = Array.Empty<object>() };

        var rows = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                string.Equals(entry.item.QualifiedItemId, "(O)911", StringComparison.Ordinal))
            .Select(entry => new
            {
                inventory_slot_index = entry.slot,
                item_id = entry.item!.ItemId,
                qualified_item_id = entry.item.QualifiedItemId,
                display_name = entry.item.DisplayName,
                inventory_runtime_type = entry.item.GetType().FullName,
                stack_before = entry.item.Stack,
                stack_after = entry.item.Stack,
                temporarily_invisible = ((StardewValley.Object)entry.item).isTemporarilyInvisible,
                reusable_item = true
            })
            .ToArray();

        var restrictions = Utility.GetHorseWarpRestrictionsForFarmer(player);
        var horse = Utility.findHorseForPlayer(player.UniqueMultiplayerID);
        var horseNearby = horse is not null && ReferenceEquals(horse.currentLocation, location) &&
            Math.Abs(player.TilePoint.X - horse.TilePoint.X) <= 1 &&
            Math.Abs(player.TilePoint.Y - horse.TilePoint.Y) <= 1;
        var teamEventBinding = FindHorseFluteTeamEventStable(player);
        var stableMatchesOwnedHorse = horse is not null && ReferenceEquals(teamEventBinding.Horse, horse) &&
            teamEventBinding.Horse.HorseId == horse.HorseId;
        var visibleFluteAvailable = rows.Any(row => !row.temporarily_invisible);
        var baseGate = player.canMove && visibleFluteAvailable && !Game1.eventUp &&
            !Game1.isFestival() && !Game1.fadeToBlack && !player.swimming.Value &&
            !player.bathingClothes.Value && !player.onBridge.Value;
        var restrictionNames = HorseWarpRestrictionNames(restrictions);
        var ready = rows.Length > 0 && baseGate && restrictions == Utility.HorseWarpRestrictions.None &&
            horse is not null && (horseNearby || stableMatchesOwnedHorse);
        var expectedResult = !ready ? "blocked" : horseNearby ? "already_adjacent_no_warp" : "summon_after_1500ms";
        var delay = ready && !horseNearby ? 1500 : 0;
        var fingerprint = Sha256(string.Join("|", new[]
        {
            "horse_flute.v1", location.NameOrUniqueName, player.TilePoint.X.ToString(), player.TilePoint.Y.ToString(),
            player.FacingDirection.ToString(),
            baseGate.ToString(), ((int)restrictions).ToString(), string.Join(",", restrictionNames),
            horse?.HorseId.ToString() ?? string.Empty, horse?.ownerId.Value.ToString() ?? string.Empty,
            horse?.currentLocation?.NameOrUniqueName ?? string.Empty, horse?.TilePoint.X.ToString() ?? string.Empty,
            horse?.TilePoint.Y.ToString() ?? string.Empty, horseNearby.ToString(),
            teamEventBinding.Horse?.HorseId.ToString() ?? string.Empty,
            teamEventBinding.Stable?.GetParentLocation()?.NameOrUniqueName ?? string.Empty,
            teamEventBinding.Stable?.tileX.Value.ToString() ?? string.Empty,
            teamEventBinding.Stable?.tileY.Value.ToString() ?? string.Empty, stableMatchesOwnedHorse.ToString(),
            string.Join(";", rows.Select(row => row.inventory_slot_index + ":" + row.qualified_item_id + ":" +
                row.stack_before + ":" + row.temporarily_invisible))
        }));

        return new
        {
            schema_version = "horse_flute.v1",
            projection_status = "complete_current_native_horse_flute_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            native_use_gate_status = ready ? "ready" : rows.Length == 0 ? "blocked_no_inventory_horse_flute" :
                !baseGate ? "blocked_base_object_use_gate" : restrictions != Utility.HorseWarpRestrictions.None ?
                "blocked_horse_warp_restrictions" : horse is null ? "blocked_owned_horse_instance_unavailable" :
                !horseNearby && !stableMatchesOwnedHorse ? "blocked_team_event_stable_binding" : "blocked_unknown",
            native_base_use_gate = new
            {
                can_move = player.canMove,
                selected_object_visibility_required = true,
                visible_horse_flute_available = visibleFluteAvailable,
                event_up = Game1.eventUp,
                festival = Game1.isFestival(),
                fade_to_black = Game1.fadeToBlack,
                swimming = player.swimming.Value,
                bathing_clothes = player.bathingClothes.Value,
                on_bridge = player.onBridge.Value,
                passed = baseGate
            },
            horse_warp_restrictions = (int)restrictions,
            horse_warp_restriction_names = restrictionNames,
            restriction_error_precedence = new[] { "no_owned_horse", "indoors", "no_room", "in_use" },
            summon_rectangle = new
            {
                pixel_x = player.TilePoint.X * 64,
                pixel_y = player.TilePoint.Y * 64,
                width = 128,
                height = 64,
                colliding = restrictions.HasFlag(Utility.HorseWarpRestrictions.NoRoom)
            },
            owned_horse = ReadOwnedHorse(horse, player, location, horseNearby),
            team_event_stable_binding = new
            {
                available = teamEventBinding.Stable is not null && teamEventBinding.Horse is not null,
                stable_horse_id = teamEventBinding.Horse?.HorseId.ToString() ?? string.Empty,
                stable_location_id = teamEventBinding.Stable?.GetParentLocation()?.NameOrUniqueName ?? string.Empty,
                stable_tile_x = teamEventBinding.Stable?.tileX.Value ?? -1,
                stable_tile_y = teamEventBinding.Stable?.tileY.Value ?? -1,
                matches_owned_horse = stableMatchesOwnedHorse
            },
            expected_result = expectedResult,
            use_delay_ms = delay,
            delayed_restriction_recheck = ready && !horseNearby,
            facing_direction = ready && !horseNearby ? 2 : player.FacingDirection,
            freeze_pause_ms = delay,
            music_duck_ms = ready && !horseNearby ? 2000 : 0,
            native_contract = HorseFluteNativeContract,
            rows
        };
    }

    private static (Stable? Stable, Horse? Horse) FindHorseFluteTeamEventStable(Farmer player)
    {
        Stable? matchStable = null;
        Horse? matchHorse = null;
        Utility.ForEachBuilding((Stable stable) =>
        {
            var stableHorse = stable.getStableHorse();
            if (stableHorse is null || stableHorse.getOwner() != player)
                return true;
            matchStable = stable;
            matchHorse = stableHorse;
            return false;
        });
        return (matchStable, matchHorse);
    }

    private static object? ReadOwnedHorse(Horse? horse, Farmer player, GameLocation currentLocation, bool nearby)
    {
        if (horse is null)
            return null;
        return new
        {
            horse_id = horse.HorseId.ToString(),
            owner_player_id = horse.ownerId.Value.ToString(),
            owner_matches_player = horse.getOwner() == player,
            location_id = horse.currentLocation?.NameOrUniqueName ?? string.Empty,
            tile_x = horse.TilePoint.X,
            tile_y = horse.TilePoint.Y,
            is_in_current_location = ReferenceEquals(horse.currentLocation, currentLocation),
            is_nearby = nearby,
            rider_player_id = horse.rider?.UniqueMultiplayerID.ToString() ?? string.Empty,
            mutex_locked = horse.mutex.IsLocked()
        };
    }

    private static string[] HorseWarpRestrictionNames(Utility.HorseWarpRestrictions restrictions)
    {
        var names = new List<string>();
        if (restrictions.HasFlag(Utility.HorseWarpRestrictions.NoOwnedHorse)) names.Add("no_owned_horse");
        if (restrictions.HasFlag(Utility.HorseWarpRestrictions.Indoors)) names.Add("indoors");
        if (restrictions.HasFlag(Utility.HorseWarpRestrictions.NoRoom)) names.Add("no_room");
        if (restrictions.HasFlag(Utility.HorseWarpRestrictions.InUse)) names.Add("in_use");
        return names.ToArray();
    }
}
