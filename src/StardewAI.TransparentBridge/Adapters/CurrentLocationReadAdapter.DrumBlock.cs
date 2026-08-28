using Microsoft.Xna.Framework;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string DrumBlockNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(O)463->CheckForActionOnDrumBlock->preservedParentSheetIndex_next_tone->Game1.playSound_drumkitN->shakeTimer_200->scaleY_1.3";

    private static object? ReadDrumBlockTuning(GameLocation location, Vector2 tile, StardewObject item)
    {
        if (item.GetType() != typeof(StardewObject) || item.bigCraftable.Value ||
            !string.Equals(item.Name, "Drum Block", StringComparison.Ordinal) ||
            !string.Equals(item.Type, "Crafting", StringComparison.Ordinal) ||
            !string.Equals(item.ItemId, "463", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, "(O)463", StringComparison.Ordinal))
        {
            return null;
        }

        var rawTone = item.preservedParentSheetIndex.Value ?? string.Empty;
        _ = int.TryParse(rawTone, out var parsedTone);
        var nextTone = (parsedTone + 1) % 7;
        var stands = ReadSafeObjectInteractionStands(location, tile.ToPoint());
        return new
        {
            status = stands.Any(stand => stand.available) ? "ready" : "blocked_no_adjacent_stand",
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            target_runtime_type = item.GetType().FullName,
            current_tone_raw = rawTone,
            current_tone_parsed = parsedTone,
            next_tone = nextTone,
            tone_min_inclusive = 0,
            tone_max_inclusive = 6,
            tone_step = 1,
            tone_state_count = 7,
            sound_cue = "drumkit" + nextTone,
            adjacent_playback_entry = "Object.farmerAdjacentAction_separate_not_tuning",
            expected_shake_timer_immediately_after_action = 200,
            expected_scale_y_immediately_after_action = 1.3f,
            expected_native_location_action_return = true,
            stand_tiles = stands,
            has_available_adjacent_stand = stands.Any(stand => stand.available),
            interaction_kind = "location_object",
            expected_action_type = "DrumBlock",
            native_contract = DrumBlockNativeContract
        };
    }
}
