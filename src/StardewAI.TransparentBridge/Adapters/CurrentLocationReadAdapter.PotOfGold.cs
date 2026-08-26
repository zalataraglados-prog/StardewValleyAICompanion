using Microsoft.Xna.Framework;
using System.Text.Json;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string PotOfGoldQualifiedItemId = "(O)PotOfGold";
    internal const string PotOfGoldCoinQualifiedItemId = "(O)GoldCoin";
    internal const string PotOfGoldHatQualifiedItemId = "(H)LeprechuanHat";
    internal const string PotOfGoldNativeContract = "Forest.DayUpdate_spring_17_tile_52_98->Object.checkForAction_PotOfGold->removeObject_and_createMultipleItemDebris";

    private static object ReadPotOfGoldReward(GameLocation location)
    {
        var target = new Point(52, 98);
        var targetVector = target.ToVector2();
        var isForest = string.Equals(location.NameOrUniqueName, "Forest", StringComparison.Ordinal);
        var isNativeDate = Game1.IsSpring && Game1.dayOfMonth == 17;
        var exactObjectPresent = location.objects.TryGetValue(targetVector, out var item) &&
            item.GetType() == typeof(StardewObject) &&
            string.Equals(item.QualifiedItemId, PotOfGoldQualifiedItemId, StringComparison.Ordinal) &&
            item.Stack == 1;
        var expectedCoinQuantity = Math.Min(100, 7 + Game1.year);
        var standTiles = new[]
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
            return new
            {
                tile_x = stand.X,
                tile_y = stand.Y,
                on_map = onMap,
                collision_blocked = collisionBlocked,
                available = onMap && !collisionBlocked
            };
        }).ToArray();
        var outputItems = new object[]
        {
            new
            {
                qualified_item_id = PotOfGoldCoinQualifiedItemId,
                quantity = expectedCoinQuantity,
                quality = 0,
                delivery = "individual_item_debris"
            },
            new
            {
                qualified_item_id = PotOfGoldHatQualifiedItemId,
                quantity = 1,
                quality = 0,
                delivery = "item_debris"
            }
        };
        var status = !isForest
            ? "not_current_forest"
            : !isNativeDate
                ? exactObjectPresent
                    ? "blocked_vanilla_date_drift"
                    : "not_spring_17"
                : !exactObjectPresent
                    ? "absent_or_already_claimed"
                    : standTiles.Any(stand => stand.available)
                        ? "ready"
                        : "blocked_no_adjacent_stand";

        return new
        {
            status,
            location_id = location.NameOrUniqueName,
            current_season = Game1.currentSeason,
            current_day = Game1.dayOfMonth,
            current_year = Game1.year,
            target_tile_x = target.X,
            target_tile_y = target.Y,
            exact_object_present = exactObjectPresent,
            qualified_item_id = PotOfGoldQualifiedItemId,
            target_runtime_type = typeof(StardewObject).FullName,
            object_type = exactObjectPresent ? item!.Type : string.Empty,
            object_stack = exactObjectPresent ? item!.Stack : 0,
            stand_tiles = standTiles,
            reward_branch = "spring_17_forest_pot_of_gold",
            expected_coin_qualified_item_id = PotOfGoldCoinQualifiedItemId,
            expected_coin_quantity = expectedCoinQuantity,
            expected_hat_qualified_item_id = PotOfGoldHatQualifiedItemId,
            expected_hat_quantity = 1,
            expected_reward_debris_count_delta = expectedCoinQuantity + 1,
            expected_output_items = outputItems,
            expected_output_items_json = JsonSerializer.Serialize(outputItems),
            inventory_capacity_blocks_open = false,
            native_removal_day = 18,
            expires_if_unclaimed = true,
            pickup_handoff = "fresh_snapshot_then_executor.pickup_debris_for_each_remaining_reward_debris",
            interaction_kind = "location_object",
            expected_action_type = "PotOfGold",
            native_contract = PotOfGoldNativeContract
        };
    }
}
