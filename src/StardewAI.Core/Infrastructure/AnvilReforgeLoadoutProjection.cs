using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal static class AnvilReforgeLoadoutProjection
{
    internal static AnvilReforgeLoadout Read(
        SnapshotEnvelope snapshot,
        string outcomeKind)
    {
        var capability = Capability(outcomeKind);
        var field = ReadStateFieldValue(
            snapshot,
            "player",
            "trinket_loadout");
        if (string.IsNullOrWhiteSpace(capability) ||
            !field.HasValue ||
            field.Value.ValueKind !=
                JsonValueKind.Object ||
            !string.Equals(
                ReadString(field.Value, "schema_version"),
                "trinket_loadout_context.v1",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(field.Value, "status"),
                "available_exact_live_slots",
                StringComparison.Ordinal) ||
            !field.Value.TryGetProperty(
                "slots",
                out var slots) ||
            slots.ValueKind != JsonValueKind.Array)
        {
            return AnvilReforgeLoadout.Blocked;
        }

        var unlocked = ReadInt(
            field.Value,
            "unlocked_slot_count");
        var occupied = ReadInt(
            field.Value,
            "occupied_slot_count");
        var empty = ReadInt(
            field.Value,
            "empty_unlocked_slot_count");
        if (unlocked < 0 ||
            occupied < 0 ||
            occupied > unlocked ||
            empty < 0 ||
            empty != unlocked - occupied)
        {
            return AnvilReforgeLoadout.Blocked;
        }

        var slotRows = slots
            .EnumerateArray()
            .ToArray();
        if (slotRows.Any(slot =>
                slot.ValueKind !=
                    JsonValueKind.Object ||
                !HasBoolean(slot, "unlocked") ||
                !HasBoolean(slot, "occupied") ||
                ReadInt(slot, "slot_index") < 0) ||
            slotRows
                .Select(slot =>
                    ReadInt(slot, "slot_index"))
                .Distinct()
                .Count() != slotRows.Length ||
            slotRows.Count(slot =>
                ReadBool(slot, "unlocked")) !=
                unlocked ||
            slotRows.Count(slot =>
                ReadBool(slot, "occupied")) !=
                occupied ||
            slotRows.Count(slot =>
                ReadBool(slot, "unlocked") &&
                !ReadBool(slot, "occupied")) !=
                empty ||
            slotRows.Any(slot =>
                ReadBool(slot, "occupied") &&
                !ReadBool(slot, "unlocked")))
        {
            return AnvilReforgeLoadout.Blocked;
        }

        var equippedKinds = slotRows
            .Where(slot =>
                ReadBool(slot, "occupied"))
            .Select(ReadEquippedOutcomeKind)
            .ToArray();
        if (equippedKinds.Any(
                string.IsNullOrWhiteSpace) ||
            equippedKinds.Length != occupied)
        {
            return AnvilReforgeLoadout.Blocked;
        }

        var sameType = equippedKinds.Count(kind =>
            string.Equals(
                kind,
                outcomeKind,
                StringComparison.Ordinal));
        var relation = empty > 0
            ? "empty_unlocked_slot_available"
            : sameType > 0
                ? "same_type_already_equipped"
                : unlocked > 0
                    ? "different_type_equipped_swap_required"
                    : "no_unlocked_trinket_slot";
        return new AnvilReforgeLoadout(
            true,
            "exact_live_trinket_loadout",
            capability,
            outcomeKind == "frog_egg"
                ? "removes_monster_without_kill_credit"
                : "preserves_native_kill_credit",
            outcomeKind == "frog_egg"
                ? "removes_monster_without_drops"
                : "preserves_native_drop_resolution",
            unlocked,
            occupied,
            empty,
            sameType,
            Math.Max(0, occupied - sameType),
            relation);
    }

    internal static string Capability(
        string outcomeKind)
    {
        return outcomeKind switch
        {
            "iridium_spur" =>
                "critical_hit_mobility",
            "parrot_egg" =>
                "kill_triggered_currency",
            "frog_egg" =>
                "enemy_removal_no_kill_or_loot_credit",
            "fairy_box" =>
                "reactive_healing",
            "ice_rod" =>
                "ranged_freeze_control",
            "magic_quiver" =>
                "ranged_direct_damage",
            _ => string.Empty
        };
    }

    private static string ReadEquippedOutcomeKind(
        JsonElement slot)
    {
        return slot.TryGetProperty(
                    "special_state",
                    out var special) &&
                special.ValueKind ==
                    JsonValueKind.Object &&
                string.Equals(
                    ReadString(
                        special,
                        "schema_version"),
                    "trinket_item_state.v1",
                    StringComparison.Ordinal)
            ? ReadString(
                special,
                "vanilla_outcome_kind")
            : string.Empty;
    }

    private static string ReadString(
        JsonElement value,
        string property)
    {
        return value.TryGetProperty(
                    property,
                    out var field) &&
                field.ValueKind ==
                    JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(
        JsonElement value,
        string property)
    {
        return value.TryGetProperty(
                    property,
                    out var field) &&
                field.TryGetInt32(out var parsed)
            ? parsed
            : -1;
    }

    private static bool ReadBool(
        JsonElement value,
        string property)
    {
        return value.TryGetProperty(
                    property,
                    out var field) &&
            field.ValueKind == JsonValueKind.True;
    }

    private static bool HasBoolean(
        JsonElement value,
        string property)
    {
        return value.TryGetProperty(
                property,
                out var field) &&
            field.ValueKind is
                JsonValueKind.True or
                JsonValueKind.False;
    }
}

internal readonly record struct AnvilReforgeLoadout(
    bool Supported,
    string Status,
    string CapabilityClass,
    string KillCreditPolicy,
    string LootPolicy,
    int UnlockedSlotCount,
    int OccupiedSlotCount,
    int EmptyUnlockedSlotCount,
    int SameTypeEquippedCount,
    int OtherTypeEquippedCount,
    string Relation)
{
    internal static AnvilReforgeLoadout Blocked =>
        new(
            false,
            "blocked_loadout_context_unavailable",
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            string.Empty);
}
