using StardewValley;
using StardewValley.Objects.Trinkets;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadTrinketLoadout(
        Farmer? player)
    {
        if (player is null)
        {
            return new
            {
                schema_version =
                    "trinket_loadout_context.v1",
                status = "unavailable_world_not_ready",
                unlock_stat_value = 0L,
                native_maximum_slot_count =
                    Math.Max(
                        0,
                        Farmer.MaximumTrinkets),
                serialized_slot_count = 0,
                unlocked_slot_count = 0,
                occupied_slot_count = 0,
                empty_unlocked_slot_count = 0,
                slots = Array.Empty<object>()
            };
        }

        var unlockStatValue =
            player.stats.Get("trinketSlots");
        var nativeMaximumSlotCount = Math.Max(
            0,
            Farmer.MaximumTrinkets);
        var unlockedSlotCount = unlockStatValue != 0
            ? nativeMaximumSlotCount
            : 0;
        var serializedSlotCount = Math.Max(
            nativeMaximumSlotCount,
            player.trinketItems.Count);
        var slots = Enumerable
            .Range(0, serializedSlotCount)
            .Select(index =>
                ReadTrinketLoadoutSlot(
                    index < player.trinketItems.Count
                        ? player.trinketItems[index]
                        : null,
                    index,
                    index < unlockedSlotCount))
            .ToArray();
        var occupied = slots.Count(slot =>
            slot.occupied);
        return new
        {
            schema_version =
                "trinket_loadout_context.v1",
            status = "available_exact_live_slots",
            unlock_stat_value = unlockStatValue,
            native_maximum_slot_count =
                nativeMaximumSlotCount,
            serialized_slot_count =
                serializedSlotCount,
            unlocked_slot_count = unlockedSlotCount,
            occupied_slot_count = occupied,
            empty_unlocked_slot_count = Math.Max(
                0,
                unlockedSlotCount - occupied),
            slots
        };
    }

    private static TrinketLoadoutSlot
        ReadTrinketLoadoutSlot(
            Trinket? trinket,
            int index,
            bool unlocked)
    {
        return new TrinketLoadoutSlot(
            index,
            unlocked,
            trinket is not null,
            trinket?.ItemId ?? string.Empty,
            trinket?.QualifiedItemId ?? string.Empty,
            trinket?.GetType().FullName ??
                string.Empty,
            trinket is null
                ? null
                : FarmReadAdapter
                    .ReadItemSpecialState(trinket));
    }

    private sealed record TrinketLoadoutSlot(
        int slot_index,
        bool unlocked,
        bool occupied,
        string item_id,
        string qualified_item_id,
        string runtime_type,
        object? special_state);
}
