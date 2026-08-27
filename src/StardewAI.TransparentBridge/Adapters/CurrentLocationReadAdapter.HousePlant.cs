using Microsoft.Xna.Framework;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string HousePlantNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)0..7->CheckForActionOnHousePlant;empty_hand;location_calls_object_twice_only_when_first_returns_false";

    private static object? ReadHousePlantRotation(GameLocation location, Vector2 tile, StardewObject item)
    {
        if (item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.Name, "House Plant", StringComparison.Ordinal) ||
            !string.Equals(item.Type, "Crafting", StringComparison.Ordinal) ||
            !IsCanonicalHousePlantQualifiedItemId(item.QualifiedItemId) ||
            item.ParentSheetIndex is < 0 or > 7)
        {
            return null;
        }

        var target = tile.ToPoint();
        var stands = ReadHousePlantAdjacentStands(location, target);
        var currentSpriteIndex = item.ParentSheetIndex;
        return new
        {
            status = stands.Any(stand => stand.available) ? "ready" : "blocked_no_adjacent_stand",
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            current_sprite_index = currentSpriteIndex,
            expected_sprite_index_after_native_location_action = currentSpriteIndex == 7 ? 1 : currentSpriteIndex + 1,
            expected_object_check_for_action_call_count = currentSpriteIndex == 7 ? 2 : 1,
            expected_native_location_action_return = true,
            item_id_unchanged = true,
            qualified_item_id_unchanged = true,
            target_runtime_type = item.GetType().FullName,
            stand_tiles = stands,
            has_available_adjacent_stand = stands.Any(stand => stand.available),
            interaction_kind = "location_object",
            expected_action_type = "HousePlant",
            native_contract = HousePlantNativeContract
        };
    }

    private static bool IsCanonicalHousePlantQualifiedItemId(string qualifiedItemId) =>
        qualifiedItemId is "(BC)0" or "(BC)1" or "(BC)2" or "(BC)3" or
            "(BC)4" or "(BC)5" or "(BC)6" or "(BC)7";

    private static HousePlantStandProjection[] ReadHousePlantAdjacentStands(GameLocation location, Point target) =>
        new[]
        {
            new Point(target.X, target.Y - 1),
            new Point(target.X, target.Y + 1),
            new Point(target.X - 1, target.Y),
            new Point(target.X + 1, target.Y)
        }.Select(stand =>
        {
            var onMap = location.isTileOnMap(stand.ToVector2());
            var collisionBlocked = !onMap || location.isCollidingPosition(
                new Rectangle(
                    stand.X * Game1.tileSize + 1,
                    stand.Y * Game1.tileSize + 1,
                    Game1.tileSize - 2,
                    Game1.tileSize - 2),
                Game1.viewport,
                Game1.player);
            var objectTrapBlocked = IsHousePlantObjectTrap(location, stand);
            return new HousePlantStandProjection(stand.X, stand.Y, onMap, collisionBlocked, objectTrapBlocked);
        }).ToArray();

    private static bool IsHousePlantObjectTrap(GameLocation location, Point stand) =>
        new[]
        {
            new Point(stand.X, stand.Y - 1),
            new Point(stand.X, stand.Y + 1),
            new Point(stand.X - 1, stand.Y),
            new Point(stand.X + 1, stand.Y)
        }.All(tile =>
            location.objects.TryGetValue(tile.ToVector2(), out var item) &&
            !item.isPassable());

    private sealed record HousePlantStandProjection(
        int tile_x,
        int tile_y,
        bool on_map,
        bool collision_blocked,
        bool object_trap_blocked)
    {
        public bool available => on_map && !collision_blocked && !object_trap_blocked;
    }
}
