using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal static class IncubatorSnapshotProjection
{
    public static bool IsBirthMessage(SnapshotEnvelope snapshot)
    {
        var activeMenu = ReadStateFieldValue(
            snapshot,
            "menus",
            "active_menu");
        if (!activeMenu.HasValue ||
            activeMenu.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(activeMenu.Value, "type"),
                "DialogueBox",
                StringComparison.Ordinal) ||
            ReadBool(activeMenu.Value, "event_up") != true ||
            ReadBool(
                activeMenu.Value,
                "dialogue_is_question") == true ||
            ReadInt(
                activeMenu.Value,
                "dialogue_response_count") != 0)
        {
            return false;
        }

        var playerLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var machines = ReadStateFieldValue(
            snapshot,
            "farm",
            "machines");
        return machines.HasValue &&
            machines.Value.ValueKind == JsonValueKind.Array &&
            machines.Value.EnumerateArray().Any(machine =>
                string.Equals(
                    ReadString(machine, "location_id"),
                    playerLocation,
                    StringComparison.OrdinalIgnoreCase) &&
                IsIncubator(machine) &&
                machine.TryGetProperty(
                    "machine_special_state",
                    out var special) &&
                special.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(special, "status"),
                    "ready_requires_native_naming_event",
                    StringComparison.Ordinal) &&
                ReadBool(
                    special,
                    "native_ready_selected") == true &&
                ReadBool(
                    special,
                    "animal_house_has_capacity") == true);
    }

    private static bool IsIncubator(JsonElement machine)
    {
        if (ReadBool(machine, "machine_is_incubator") == true)
        {
            return true;
        }

        return machine.TryGetProperty(
                "machine_data",
                out var machineData) &&
            machineData.ValueKind == JsonValueKind.Object &&
            ReadBool(machineData, "is_incubator") == true;
    }
}
