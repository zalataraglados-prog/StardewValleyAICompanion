using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string SlimeBallNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)56->CheckForActionOnSlimeBall->remove_object->seeded_(O)766_debris_10_20->seeded_geometric_(O)557_debris";

    private static object? ReadSlimeBallCollection(GameLocation location, Vector2 tile, StardewObject item)
    {
        if (location.GetType() != typeof(SlimeHutch) ||
            item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            item.Fragility != 2 ||
            !string.Equals(item.Name, "Slime Ball", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, "(BC)56", StringComparison.Ordinal))
        {
            return null;
        }

        var target = tile.ToPoint();
        var stands = ReadSafeObjectInteractionStands(location, target);
        var random = Utility.CreateRandom(
            Game1.stats.DaysPlayed,
            Game1.uniqueIDForThisGame,
            tile.X * 77d,
            tile.Y * 777d,
            2d);
        var slimeQuantity = random.Next(10, 21);
        var petrifiedSlimeQuantity = 0;
        while (random.NextDouble() < 0.33)
        {
            petrifiedSlimeQuantity++;
        }

        return new
        {
            status = stands.Any(stand => stand.available) ? "ready" : "blocked_no_adjacent_stand",
            source_kind = "natural_slime_hutch_day_update_output",
            source_location_runtime_type = location.GetType().FullName,
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            required_fragility = 2,
            target_runtime_type = item.GetType().FullName,
            day_seed_days_played = Game1.stats.DaysPlayed,
            day_seed_unique_game_id = Game1.uniqueIDForThisGame,
            day_seed_tile_x_multiplier = 77,
            day_seed_tile_y_multiplier = 777,
            day_seed_salt = 2,
            expected_slime_qualified_item_id = "(O)766",
            expected_slime_quantity = slimeQuantity,
            expected_petrified_slime_qualified_item_id = "(O)557",
            expected_petrified_slime_quantity = petrifiedSlimeQuantity,
            petrified_slime_distribution = "while_seeded_random_next_double_lt_0.33_geometric_support_0_to_unbounded",
            object_removed_before_debris_creation = true,
            output_delivery = "native_debris_then_shared_executor.pickup_debris",
            interaction_kind = "location_object",
            expected_action_type = "SlimeBall",
            expected_native_location_action_return = true,
            stand_tiles = stands,
            has_available_adjacent_stand = stands.Any(stand => stand.available),
            generation_contract = "SlimeHutch.DayUpdate:one_ball_per_consumed_water_spot_and_five_slimes;max_four;50_random_tile_attempts;y_lt_12;fragility=2",
            native_contract = SlimeBallNativeContract
        };
    }
}
