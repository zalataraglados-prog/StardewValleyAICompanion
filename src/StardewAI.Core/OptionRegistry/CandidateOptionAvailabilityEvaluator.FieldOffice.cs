using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] FieldOfficeDonationCandidates(SnapshotEnvelope snapshot)
    {
        var office = ReadStateFieldValue(snapshot, "world_progress", "island_field_office");
        if (!office.HasValue || office.Value.ValueKind != JsonValueKind.Object ||
            !office.Value.TryGetProperty("donation_candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var officeRow = office.Value;
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(officeRow, "location_id");
        if (ReadBool(officeRow, "is_current_location") != true)
            return FieldOfficeRouteCandidates(snapshot, officeRow, candidates, currentLocation, targetLocation);

        var deskTiles = ReadFieldOfficeDeskTiles(officeRow);
        var endpoint = deskTiles
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(value => value.stand is not null)
            .OrderBy(value => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - value.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - value.stand!.Y))
            .FirstOrDefault();

        return candidates.EnumerateArray()
            .Where(candidate => candidate.ValueKind == JsonValueKind.Object)
            .Select(candidate =>
            {
                var reasons = new List<string>();
                var status = ReadString(candidate, "action_status");
                if (status != "ready")
                    reasons.Add(string.IsNullOrWhiteSpace(status) ? "field_office_projection_unavailable" : status);
                if (ReadString(officeRow, "projection_status") != "exact_locked_base_1.6.15")
                    reasons.Add("field_office_projection_not_locked");
                if (endpoint is null)
                    reasons.Add("field_office_no_reachable_desk_stand");
                if (ReadInt(candidate, "slot_index") < 0 || ReadInt(candidate, "target_piece_index") is < 0 or >= 11 ||
                    ReadInt(candidate, "stack_before") < 1 ||
                    ReadInt(candidate, "stack_after") != ReadInt(candidate, "stack_before") - 1 ||
                    ReadInt(candidate, "donated_piece_count_after") != ReadInt(candidate, "donated_piece_count_before") + 1)
                    reasons.Add("field_office_candidate_typed_projection_invalid");

                var parameters = endpoint is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : FieldOfficeDonationParameters(officeRow, candidate, endpoint.tile, endpoint.stand!);
                return new EventCandidate
                {
                    CandidateId = FieldOfficeCandidateId(candidate),
                    Kind = "donate_field_office_piece",
                    Available = reasons.Count == 0,
                    AllowedNow = reasons.Count == 0,
                    LocationId = targetLocation,
                    TileX = endpoint?.tile.X,
                    TileY = endpoint?.tile.Y,
                    ItemId = ReadString(candidate, "item_id"),
                    QualifiedItemId = ReadString(candidate, "qualified_item_id"),
                    SlotIndex = ReadInt(candidate, "slot_index"),
                    Quantity = 1,
                    EstimatedTicks = 600,
                    AvailabilityClass = "transparent_native_field_office_donation",
                    ExpectedEffect = FieldOfficeExpectedEffect(candidate),
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            })
            .ToArray();
    }

    private EventCandidate[] FieldOfficeRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement office,
        JsonElement candidates,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(office, "projection_status") != "exact_locked_base_1.6.15" ||
            ReadBool(office, "north_cave_opened") != true ||
            ReadString(office, "location_id") != targetLocation)
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
        if (route?.FirstConnectorCandidate is null)
            return Array.Empty<EventCandidate>();

        return candidates.EnumerateArray()
            .Where(candidate => candidate.ValueKind == JsonValueKind.Object &&
                ReadString(candidate, "action_status") == "route_to_field_office_required")
            .Select(candidate => CloneCandidate(
                route.FirstConnectorCandidate,
                candidateId: "field-office-route:" + FieldOfficeCandidateId(candidate) + ":" + currentLocation,
                expectedEffect: route.FirstConnectorCandidate.ExpectedEffect + ";field_office_target_piece=" + ReadInt(candidate, "target_piece_index"),
                parameters: route.FirstConnectorCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "island.field_office_donate"),
                    Parameter("continuation.inventory_slot_index", ReadInt(candidate, "slot_index").ToString(CultureInfo.InvariantCulture)),
                    Parameter("continuation.qualified_item_id", ReadString(candidate, "qualified_item_id")),
                    Parameter("continuation.target_piece_index", ReadInt(candidate, "target_piece_index").ToString(CultureInfo.InvariantCulture)),
                    Parameter("continuation.confirm_donation", "true")
                }).ToArray(),
                availabilityClass: "field_office_donation_rolling_route"))
            .ToArray();
    }

    private static SmallModelActionParameter[] FieldOfficeDonationParameters(
        JsonElement office,
        JsonElement candidate,
        FieldOfficeDeskTile tile,
        CandidateTile stand) => new[]
    {
        Parameter("confirm_donation", "true"),
        Parameter("target_location", ReadString(office, "location_id")),
        Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("field_office_desk_action_raw", tile.ActionRaw),
        Parameter("inventory_slot_index", ReadInt(candidate, "slot_index").ToString(CultureInfo.InvariantCulture)),
        Parameter("item_id", ReadString(candidate, "item_id")),
        Parameter("qualified_item_id", ReadString(candidate, "qualified_item_id")),
        Parameter("target_runtime_type", ReadString(candidate, "runtime_type")),
        Parameter("expected_stack_before", ReadInt(candidate, "stack_before").ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_stack_after", ReadInt(candidate, "stack_after").ToString(CultureInfo.InvariantCulture)),
        Parameter("target_piece_index", ReadInt(candidate, "target_piece_index").ToString(CultureInfo.InvariantCulture)),
        Parameter("target_piece_kind", ReadString(candidate, "target_piece_kind")),
        Parameter("target_set_kind", ReadString(candidate, "target_set_kind")),
        Parameter("expected_donated_piece_count_before", ReadInt(candidate, "donated_piece_count_before").ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_donated_piece_count_after", ReadInt(candidate, "donated_piece_count_after").ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_completes_set", FieldOfficeBoolText(candidate, "completes_set")),
        Parameter("new_reward_items_json", Raw(candidate, "new_reward_items")),
        Parameter("uncollected_rewards_before_json", Raw(candidate, "uncollected_rewards_before")),
        Parameter("uncollected_rewards_after_json", Raw(candidate, "uncollected_rewards_after")),
        Parameter("expected_collected_nut_key", ReadString(candidate, "expected_collected_nut_key")),
        Parameter("collected_nut_before", FieldOfficeBoolText(candidate, "collected_nut_before")),
        Parameter("expected_finale_ready_after", FieldOfficeBoolText(candidate, "expected_finale_ready_after")),
        Parameter("plants_restored_left_before", FieldOfficeBoolText(office, "plants_restored_left")),
        Parameter("plants_restored_right_before", FieldOfficeBoolText(office, "plants_restored_right")),
        Parameter("finale_received_or_pending_before", FieldOfficeBoolText(office, "finale_received_or_pending")),
        Parameter("golden_walnuts_found_before", ReadInt(office, "golden_walnuts_found").ToString(CultureInfo.InvariantCulture)),
        Parameter("field_office_projection_status", ReadString(office, "projection_status")),
        Parameter("native_contract", "FieldOfficeDesk_mutex_then_Safari_Donate_then_FieldOfficeMenu_inventory_and_exact_piece_holder_then_native_ok_exit"),
        Parameter("max_movement_tiles", "512")
    };

    private static FieldOfficeDeskTile[] ReadFieldOfficeDeskTiles(JsonElement office)
    {
        if (!office.TryGetProperty("desk_action_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<FieldOfficeDeskTile>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new FieldOfficeDeskTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y"), ReadString(row, "action_raw")))
            .ToArray();
    }

    private static string FieldOfficeCandidateId(JsonElement candidate) =>
        "field-office-donate:" + ReadInt(candidate, "slot_index") + ":" +
        ReadString(candidate, "qualified_item_id") + ":" + ReadInt(candidate, "target_piece_index");

    private static string FieldOfficeExpectedEffect(JsonElement candidate) =>
        "field_office_piece=" + ReadInt(candidate, "target_piece_index") + ":donated=true" +
        ";inventory_slot=" + ReadInt(candidate, "slot_index") + ":stack=" + ReadInt(candidate, "stack_after") +
        ";donated_piece_count=" + ReadInt(candidate, "donated_piece_count_after") +
        ";set_complete=" + FieldOfficeBoolText(candidate, "completes_set") +
        ";uncollected_rewards=" + Raw(candidate, "uncollected_rewards_after") +
        ";finale_ready=" + FieldOfficeBoolText(candidate, "expected_finale_ready_after");

    private static string Raw(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) ? value.GetRawText() : "[]";

    private static string FieldOfficeBoolText(JsonElement row, string property) =>
        ReadBool(row, property) == true ? "true" : "false";

    private sealed record FieldOfficeDeskTile(int X, int Y, string ActionRaw);
}
