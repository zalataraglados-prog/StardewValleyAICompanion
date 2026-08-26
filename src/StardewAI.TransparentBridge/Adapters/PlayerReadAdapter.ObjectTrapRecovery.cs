using Microsoft.Xna.Framework;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadObjectTrapRecovery(Farmer? player)
    {
        if (player?.currentLocation is not { } location)
        {
            return new
            {
                is_applicable = false,
                trapped_by_four_non_passable_objects = false,
                recovery_mode = "unavailable",
                adjacent_objects = Array.Empty<object>()
            };
        }

        var center = player.TilePoint;
        var offsets = new[]
        {
            new Point(0, -1),
            new Point(1, 0),
            new Point(0, 1),
            new Point(-1, 0)
        };
        var rows = offsets.Select(offset =>
        {
            var tile = new Vector2(center.X + offset.X, center.Y + offset.Y);
            location.objects.TryGetValue(tile, out var item);
            var checkDeclaringType = item?.GetType().GetMethod(
                nameof(StardewObject.checkForAction),
                new[] { typeof(Farmer), typeof(bool) })?.DeclaringType?.FullName ?? string.Empty;
            return new
            {
                tile_x = (int)tile.X,
                tile_y = (int)tile.Y,
                direction_from_player = offset switch
                {
                    { X: 0, Y: -1 } => 0,
                    { X: 1, Y: 0 } => 1,
                    { X: 0, Y: 1 } => 2,
                    _ => 3
                },
                object_present = item is not null,
                object_passable = item?.isPassable(),
                qualified_item_id = item?.QualifiedItemId ?? string.Empty,
                runtime_type = item?.GetType().FullName ?? string.Empty,
                object_type = item?.Type ?? string.Empty,
                check_for_action_declaring_type = checkDeclaringType,
                native_null_tool_branch_reachable = item is not null &&
                    string.Equals(checkDeclaringType, typeof(StardewObject).FullName, StringComparison.Ordinal),
                native_null_tool_branch_is_asset_destructive = true
            };
        }).ToArray();
        var trapped = rows.All(row => row.object_present && row.object_passable == false);

        return new
        {
            is_applicable = true,
            location_id = location.NameOrUniqueName,
            player_tile_x = center.X,
            player_tile_y = center.Y,
            active_menu_clear = Game1.activeClickableMenu is null,
            active_object_clear = player.ActiveObject is null,
            player_not_riding_horse = !player.isRidingHorse(),
            trapped_by_four_non_passable_objects = trapped,
            recovery_mode = trapped
                ? "prefer_existing_recoverable_machine_removal"
                : "not_trapped",
            destructive_native_fallback_enabled = false,
            destructive_native_fallback_reason =
                "Object.checkForAction calls performToolAction(null), which can remove the target without returning the asset; policy blocks automatic dispatch",
            adjacent_objects = rows,
            native_contract =
                "Object.checkForAction(four_cardinal_non_passable_objects)->target.performToolAction(null); safer recovery compiles to executor.remove_machine"
        };
    }
}
