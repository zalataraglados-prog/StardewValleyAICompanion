using Microsoft.Xna.Framework;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string MiniObeliskNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)238->CheckForActionOnMiniObelisk;native_first_two_nonzero_pair;farther_from_interaction_stand;landing_order_down_left_right_up;IsTileBlockedBy_All_ignorePassables_All;fade_delay_50ms";

    private static object? ReadMiniObeliskUse(
        GameLocation location,
        Vector2 tile,
        StardewObject item)
    {
        if (!IsExactBaseMiniObelisk(item))
            return null;

        var pair = ReadNativeMiniObeliskPair(location);
        var pairMemberIndex = pair is null
            ? -1
            : ReferenceEquals(item, pair.First.Item) && tile == pair.First.Tile
                ? 0
                : ReferenceEquals(item, pair.Second.Item) && tile == pair.Second.Tile
                    ? 1
                    : -1;
        var pairIsExactBase = pair is not null &&
            IsExactBaseMiniObelisk(pair.First.Item) &&
            IsExactBaseMiniObelisk(pair.Second.Item);
        var stands = pair is null || pairMemberIndex < 0 || !pairIsExactBase
            ? Array.Empty<MiniObeliskStandProjection>()
            : ReadMiniObeliskStands(location, tile.ToPoint(), pair);

        var status = pair is null
            ? "blocked_native_pair_missing"
            : !pairIsExactBase
                ? "blocked_non_base_native_pair_member"
                : pairMemberIndex < 0
                    ? "blocked_not_in_native_first_pair"
                    : stands.Any(stand => stand.available)
                        ? "ready"
                        : "blocked_no_safe_source_stand_or_native_landing";
        return new
        {
            status,
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            target_runtime_type = item.GetType().FullName,
            native_pair_member_index = pairMemberIndex,
            native_pair_first_tile_x = pair is null ? (int?)null : (int)pair.First.Tile.X,
            native_pair_first_tile_y = pair is null ? (int?)null : (int)pair.First.Tile.Y,
            native_pair_second_tile_x = pair is null ? (int?)null : (int)pair.Second.Tile.X,
            native_pair_second_tile_y = pair is null ? (int?)null : (int)pair.Second.Tile.Y,
            native_pair_exact_base = pairIsExactBase,
            native_pair_scan_rule = "location.objects.Pairs;first_two_(BC)238_bigCraftable_with_Vector2.Zero_sentinel",
            native_destination_rule = "farther_Euclidean_distance_from_player_tile;tie_selects_second_pair_member",
            native_landing_order = new[] { "down", "left", "right", "up" },
            native_landing_collision_contract = "GameLocation.IsTileBlockedBy(tile,CollisionMask.All,CollisionMask.All)",
            expected_native_location_action_return = true,
            expected_delay_milliseconds = 50,
            cosmetic_rng_status = "unavailable_shared_rng_not_consumed",
            stand_tiles = stands,
            has_available_source_stand = stands.Any(stand => stand.available),
            interaction_kind = "location_object",
            expected_action_type = "MiniObelisk",
            native_contract = MiniObeliskNativeContract
        };
    }

    private static MiniObeliskStandProjection[] ReadMiniObeliskStands(
        GameLocation location,
        Point source,
        NativeMiniObeliskPair pair)
    {
        var safeStands = ReadSafeObjectInteractionStands(location, source);
        return safeStands.Select(stand =>
        {
            var standTile = new Vector2(stand.tile_x, stand.tile_y);
            var destination = Vector2.Distance(standTile, pair.First.Tile) >
                Vector2.Distance(standTile, pair.Second.Tile)
                    ? pair.First.Tile
                    : pair.Second.Tile;
            var destinationIsOtherEndpoint = destination != source.ToVector2();
            var landing = ReadFirstNativeMiniObeliskLanding(location, destination);
            return new MiniObeliskStandProjection(
                stand.tile_x,
                stand.tile_y,
                stand.on_map,
                stand.collision_blocked,
                stand.object_trap_blocked,
                destinationIsOtherEndpoint,
                (int)destination.X,
                (int)destination.Y,
                landing?.X,
                landing?.Y,
                landing is not null);
        }).ToArray();
    }

    private static Point? ReadFirstNativeMiniObeliskLanding(GameLocation location, Vector2 destination)
    {
        var candidates = new[]
        {
            new Vector2(destination.X, destination.Y + 1f),
            new Vector2(destination.X - 1f, destination.Y),
            new Vector2(destination.X + 1f, destination.Y),
            new Vector2(destination.X, destination.Y - 1f)
        };
        foreach (var candidate in candidates)
        {
            if (!location.IsTileBlockedBy(candidate, CollisionMask.All, CollisionMask.All))
                return candidate.ToPoint();
        }
        return null;
    }

    private static NativeMiniObeliskPair? ReadNativeMiniObeliskPair(GameLocation location)
    {
        NativeMiniObeliskMember? first = null;
        NativeMiniObeliskMember? second = null;
        var firstTile = Vector2.Zero;
        var secondTile = Vector2.Zero;
        foreach (var row in location.objects.Pairs)
        {
            if (!row.Value.bigCraftable.Value ||
                !string.Equals(row.Value.QualifiedItemId, "(BC)238", StringComparison.Ordinal))
            {
                continue;
            }
            if (firstTile == Vector2.Zero)
            {
                firstTile = row.Key;
                first = new NativeMiniObeliskMember(row.Key, row.Value);
            }
            else if (secondTile == Vector2.Zero)
            {
                secondTile = row.Key;
                second = new NativeMiniObeliskMember(row.Key, row.Value);
                break;
            }
        }
        return secondTile == Vector2.Zero || first is null || second is null
            ? null
            : new NativeMiniObeliskPair(first, second);
    }

    private static bool IsExactBaseMiniObelisk(StardewObject item) =>
        item.GetType() == typeof(StardewObject) &&
        item.bigCraftable.Value &&
        string.Equals(item.QualifiedItemId, "(BC)238", StringComparison.Ordinal);

    private sealed record NativeMiniObeliskMember(Vector2 Tile, StardewObject Item);

    private sealed record NativeMiniObeliskPair(
        NativeMiniObeliskMember First,
        NativeMiniObeliskMember Second);

    private sealed record MiniObeliskStandProjection(
        int tile_x,
        int tile_y,
        bool on_map,
        bool collision_blocked,
        bool object_trap_blocked,
        bool native_destination_is_other_endpoint,
        int native_destination_tile_x,
        int native_destination_tile_y,
        int? native_landing_tile_x,
        int? native_landing_tile_y,
        bool native_landing_available)
    {
        public bool available =>
            on_map &&
            !collision_blocked &&
            !object_trap_blocked &&
            native_destination_is_other_endpoint &&
            native_landing_available;
    }
}
