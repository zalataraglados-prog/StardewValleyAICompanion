using Microsoft.Xna.Framework;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string SingingStoneNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)94->CheckForActionOnSingingStone->Game1.random.Next(2400)_floor_to_100->Game1.playSound_crystal_pitch->shakeTimer_100";

    private static object? ReadSingingStoneInteraction(GameLocation location, Vector2 tile, StardewObject item)
    {
        if (item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.Name, "Singing Stone", StringComparison.Ordinal) ||
            !string.Equals(item.Type, "Crafting", StringComparison.Ordinal) ||
            !string.Equals(item.ItemId, "94", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, "(BC)94", StringComparison.Ordinal))
        {
            return null;
        }

        var target = tile.ToPoint();
        var stands = ReadSafeObjectInteractionStands(location, target);
        return new
        {
            status = stands.Any(stand => stand.available) ? "ready" : "blocked_no_adjacent_stand",
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            target_runtime_type = item.GetType().FullName,
            sound_name = "crystal",
            pitch_rng_source = "Game1.random_shared_unread",
            exact_next_pitch_status = "unavailable_shared_rng_state_not_consumed",
            pitch_min_inclusive = 0,
            pitch_max_inclusive = 2300,
            pitch_step = 100,
            pitch_outcome_count = 24,
            pitch_distribution = "uniform_over_0_to_2300_step_100",
            expected_shake_timer_immediately_after_action = 100,
            expected_native_location_action_return = true,
            item_id_unchanged = true,
            qualified_item_id_unchanged = true,
            stand_tiles = stands,
            has_available_adjacent_stand = stands.Any(stand => stand.available),
            interaction_kind = "location_object",
            expected_action_type = "SingingStone",
            native_contract = SingingStoneNativeContract
        };
    }
}
