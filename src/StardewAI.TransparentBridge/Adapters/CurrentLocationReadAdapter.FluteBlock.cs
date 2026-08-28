using Microsoft.Xna.Framework;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string FluteBlockNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(O)464->CheckForActionOnFluteBlock->preservedParentSheetIndex_next_pitch->Game1.playSound_flute_pitch->shakeTimer_200->scaleY_1.3";

    private static object? ReadFluteBlockTuning(GameLocation location, Vector2 tile, StardewObject item)
    {
        if (item.GetType() != typeof(StardewObject) || item.bigCraftable.Value ||
            !string.Equals(item.Name, "Flute Block", StringComparison.Ordinal) ||
            !string.Equals(item.Type, "Crafting", StringComparison.Ordinal) ||
            !string.Equals(item.ItemId, "464", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, "(O)464", StringComparison.Ordinal))
        {
            return null;
        }

        var rawPitch = item.preservedParentSheetIndex.Value ?? string.Empty;
        _ = int.TryParse(rawPitch, out var parsedPitch);
        var nextPitch = parsedPitch switch
        {
            2300 => 2400,
            2400 => 0,
            _ => (parsedPitch + 100) % 2400
        };
        var stands = ReadSafeObjectInteractionStands(location, tile.ToPoint());
        return new
        {
            status = stands.Any(stand => stand.available) ? "ready" : "blocked_no_adjacent_stand",
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            target_runtime_type = item.GetType().FullName,
            current_pitch_raw = rawPitch,
            current_pitch_parsed = parsedPitch,
            next_pitch = nextPitch,
            pitch_min_inclusive = 0,
            pitch_max_inclusive = 2400,
            pitch_step = 100,
            pitch_state_count = 25,
            sound_cue = "flute",
            held_object_sound_override_disabled_by_safe_slot = true,
            adjacent_playback_entry = "Object.farmerAdjacentAction_separate_not_tuning",
            expected_shake_timer_immediately_after_action = 200,
            expected_scale_y_immediately_after_action = 1.3f,
            expected_native_location_action_return = true,
            stand_tiles = stands,
            has_available_adjacent_stand = stands.Any(stand => stand.available),
            interaction_kind = "location_object",
            expected_action_type = "FluteBlock",
            native_contract = FluteBlockNativeContract
        };
    }
}
