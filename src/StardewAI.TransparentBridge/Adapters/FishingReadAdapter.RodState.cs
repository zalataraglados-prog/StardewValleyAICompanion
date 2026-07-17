using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FishingReadAdapter : ReadAdapterBase
{
    private sealed record SpawnOutputProjection(object Value, bool ResolutionComplete);

    private sealed record SpecialCatchSourcesProjection(object Value, bool Complete);

    private sealed record RodFishingContextsProjection(object[] Rows, bool Complete);

    private sealed record LocationOverrideProjection(object[] Handlers, bool Complete);

    private static object[] ReadRodInventory(Farmer player, FishingRod? currentRod)
    {
        return player.Items
            .Select((item, index) => item is FishingRod rod ? ReadRod(rod, index, ReferenceEquals(rod, currentRod)) : null)
            .Where(row => row is not null)
            .Cast<object>()
            .ToArray();
    }

    private static object ReadRod(FishingRod rod, int slotIndex, bool selected)
    {
        var bait = rod.GetBait();
        return new
        {
            slot_index = slotIndex,
            selected,
            item_id = rod.ItemId,
            qualified_item_id = rod.QualifiedItemId,
            display_name = rod.DisplayName,
            upgrade_level = rod.UpgradeLevel,
            attachment_slot_count = rod.AttachmentSlotsCount,
            can_use_bait = rod.CanUseBait(),
            can_use_tackle = rod.CanUseTackle(),
            has_magic_bait = rod.HasMagicBait(),
            has_curiosity_lure = rod.HasCuriosityLure(),
            bait = ReadAttachment(bait),
            tackle = rod.GetTackle().Where(item => item is not null).Select(ReadAttachment).ToArray(),
            in_use = selected && rod.inUse()
        };
    }

    private static object? ReadAttachment(StardewValley.Object? item)
    {
        return item is null
            ? null
            : new
            {
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                internal_name = item.Name,
                display_name = item.DisplayName,
                stack = item.Stack,
                quality = item.Quality,
                category = item.Category,
                preserved_parent_sheet_index = item.preservedParentSheetIndex.Value
            };
    }

    private static object ReadActiveCastState(FishingRod? rod)
    {
        return new
        {
            rod_selected = rod is not null,
            in_use = rod?.inUse() == true,
            is_fishing = rod?.isFishing == true,
            is_casting = rod?.isCasting == true,
            is_timing_cast = rod?.isTimingCast == true,
            is_nibbling = rod?.isNibbling == true,
            hit = rod?.hit == true,
            is_reeling = rod?.isReeling == true,
            pulling_out_of_water = rod?.pullingOutOfWater == true,
            fish_caught = rod?.fishCaught == true,
            showing_treasure = rod?.showingTreasure == true,
            cast_direction = rod is null ? (int?)null : rod.CastDirection,
            bobber_tile_x = rod is null ? (int?)null : (int)(rod.bobber.X / Game1.tileSize),
            bobber_tile_y = rod is null ? (int?)null : (int)(rod.bobber.Y / Game1.tileSize),
            clear_water_distance = rod?.clearWaterDistance,
            casting_power = rod?.castingPower,
            time_until_bite_ms = rod?.timeUntilFishingBite,
            bite_accumulator_ms = rod?.fishingBiteAccumulator,
            nibble_accumulator_ms = rod?.fishingNibbleAccumulator,
            nibble_window_remaining_ms = rod?.timeUntilFishingNibbleDone,
            caught_qualified_item_id = rod?.whichFish?.QualifiedItemId,
            fish_size = rod?.fishSize,
            fish_quality = rod?.fishQuality,
            number_caught = rod?.numberOfFishCaught,
            treasure_caught = rod?.treasureCaught == true,
            golden_treasure = rod?.goldenTreasure == true,
            boss_fish = rod?.bossFish == true,
            record_size = rod?.recordSize == true,
            last_catch_was_junk = rod?.lastCatchWasJunk == true,
            from_fish_pond = rod?.fromFishPond == true
        };
    }}
