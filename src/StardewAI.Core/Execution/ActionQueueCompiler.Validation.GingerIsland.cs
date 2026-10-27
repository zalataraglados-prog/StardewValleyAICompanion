using System;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static string[] ValidateGingerIslandBoatInteraction(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        int targetX,
        int targetY,
        string expectedActionType)
    {
        if (!string.Equals(
                ReadParameter(action, "required_executor_profile"),
                "ginger_island_access_executor",
                StringComparison.Ordinal))
        {
            return new[] { "ginger_island_executor_profile_required" };
        }
        var access = GingerIslandAccessProjection(snapshot);
        if (!access.HasValue ||
            !string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "BoatTunnel", StringComparison.Ordinal))
        {
            return new[] { "ginger_island_boat_tunnel_projection_missing" };
        }

        var status = ReadString(access.Value, "status");
        if (string.Equals(expectedActionType, "WillyBoatTicket", StringComparison.Ordinal))
        {
            var ticket = access.Value.GetProperty("ticket");
            return status == "ready_to_buy_ticket" &&
                ReadBool(ticket, "identity_exact") == true &&
                ReadInt(ticket, "exact_action_count") == 1 &&
                ReadInt(ticket, "tile_x", int.MinValue) == targetX &&
                ReadInt(ticket, "tile_y", int.MinValue) == targetY &&
                ReadInt(ticket, "price", -1) == 1000 &&
                ReadInt(ticket, "money_available", -1) >= 1000 &&
                ReadBool(access.Value.GetProperty("repairs"), "boat_fixed_received") == true
                    ? Array.Empty<string>()
                    : new[] { "ginger_island_boat_ticket_projection_drifted" };
        }

        var repairKey = expectedActionType switch
        {
            "WillyBoatDonateBatteries" => "battery",
            "WillyBoatDonateHardwood" => "hull",
            "WillyBoatDonateIridium" => "anchor",
            _ => string.Empty
        };
        var requiredStatus = repairKey switch
        {
            "battery" => "ready_to_donate_batteries",
            "hull" => "ready_to_donate_hardwood",
            "anchor" => "ready_to_donate_iridium",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(repairKey))
            return new[] { "ginger_island_boat_action_type_unsupported" };
        var repair = access.Value.GetProperty("repairs").GetProperty(repairKey);
        return status == requiredStatus &&
            ReadString(repair, "expected_action_type") == expectedActionType &&
            ReadInt(repair, "tile_x", int.MinValue) == targetX &&
            ReadInt(repair, "tile_y", int.MinValue) == targetY &&
            ReadBool(repair, "complete_or_pending") == false &&
            ReadInt(repair, "required_count", -1) > 0 &&
            ReadInt(repair, "available_count", -1) >= ReadInt(repair, "required_count", int.MaxValue)
                ? Array.Empty<string>()
                : new[] { "ginger_island_boat_repair_projection_drifted" };
    }

    private static string[] ValidateGingerIslandPerchInteraction(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        int targetX,
        int targetY,
        string expectedActionType)
    {
        if (!string.Equals(
                ReadParameter(action, "required_executor_profile"),
                "ginger_island_access_executor",
                StringComparison.Ordinal))
        {
            return new[] { "ginger_island_executor_profile_required" };
        }
        var mapping = expectedActionType switch
        {
            "IslandFirstParrot" => (Field: "first_parrot", Status: "ready_to_unlock_first_parrot", Location: "IslandHut"),
            "IslandWesternTurtle" => (Field: "western_turtle_parrot", Status: "ready_to_move_western_turtle", Location: "IslandSouth"),
            "IslandHouse" => (Field: "island_house_parrot", Status: "ready_to_restore_island_house", Location: "IslandWest"),
            "IslandResort" => (Field: "resort_parrot", Status: "ready_to_restore_resort", Location: "IslandSouth"),
            _ => (Field: string.Empty, Status: string.Empty, Location: string.Empty)
        };
        if (string.IsNullOrWhiteSpace(mapping.Field))
            return new[] { "ginger_island_parrot_action_type_unsupported" };
        var access = GingerIslandAccessProjection(snapshot);
        if (!access.HasValue ||
            ReadString(access.Value, "status") != mapping.Status ||
            ReadStateFieldString(snapshot, "player", "location_id") != mapping.Location)
        {
            return new[] { "ginger_island_parrot_projection_missing" };
        }
        var perch = access.Value.GetProperty(mapping.Field);
        var walnuts = access.Value.GetProperty("walnuts");
        return ReadBool(perch, "identity_exact") == true &&
            ReadBool(perch, "available") == true &&
            ReadBool(perch, "complete_or_pending") == false &&
            ReadInt(perch, "tile_x", int.MinValue) == targetX &&
            ReadInt(perch, "tile_y", int.MinValue) == targetY &&
            ReadInt(perch, "required_nuts", -1) == ReadInt(perch, "expected_required_nuts", -2) &&
            ReadString(perch, "required_mail") == ReadString(perch, "expected_required_mail") &&
            ReadInt(walnuts, "current", -1) >= ReadInt(perch, "required_nuts", int.MaxValue)
                ? Array.Empty<string>()
                : new[] { "ginger_island_parrot_projection_drifted" };
    }

    private static JsonElement? GingerIslandAccessProjection(SnapshotEnvelope snapshot)
    {
        var access = ReadStateFieldValue(snapshot, "locations", "ginger_island_access");
        return access.HasValue && access.Value.ValueKind == JsonValueKind.Object &&
            ReadString(access.Value, "schema_version") == "ginger_island_access.v1" &&
            ReadString(access.Value, "projection_status") == "complete_locked_base_1.6.15_native_contract"
                ? access.Value
                : null;
    }
}
