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
    private const string GourmandSchemaVersion = "island_gourmand_progress.v1";
    private const string GourmandNativeContract =
        "IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward";

    private EventCandidate[] IslandGourmandCandidates(SnapshotEnvelope snapshot)
    {
        var progress = ReadStateFieldValue(snapshot, "world_progress", "island_gourmand");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();

        var row = progress.Value;
        if (ReadString(row, "schema_version") != GourmandSchemaVersion ||
            ReadString(row, "projection_status") != "exact_native_active_request_projected" ||
            ReadBool(row, "required_mail_satisfied") != true ||
            ReadBool(row, "host_authority") != true ||
            ReadBool(row, "complete") == true ||
            ReadInt(row, "ready_matching_crop_count") <= 0 ||
            !HasExactReadyGourmandCrop(row))
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(row, "location_id");
        var requestIndex = ReadNullableInt(row, "active_request_index");
        var cropItemId = ReadString(row, "required_crop_item_id");
        if (!requestIndex.HasValue || requestIndex is < 0 or > 2 ||
            !GourmandCropMatchesRequest(requestIndex.Value, cropItemId) ||
            string.IsNullOrWhiteSpace(targetLocation))
            return Array.Empty<EventCandidate>();

        if (ReadBool(row, "is_current_location") != true)
            return IslandGourmandRouteCandidates(
                snapshot,
                row,
                currentLocation,
                targetLocation,
                requestIndex.Value,
                cropItemId);

        var endpoint = ReadGourmandActionTiles(row)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(value => value.stand is not null)
            .OrderBy(value =>
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - value.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - value.stand!.Y))
            .FirstOrDefault();
        var stage = ReadBool(row, "talked_to_gourmand_received_or_pending") == true
            ? "complete_request"
            : "introduce_request";
        var reasons = new List<string>();
        if (ReadBool(row, "menu_clear") != true)
            reasons.Add("gourmand_menu_dialogue_or_event_not_clear");
        if (ReadBool(row, "mutex_projection_available") != true)
            reasons.Add("gourmand_mutex_projection_unavailable");
        if (ReadBool(row, "mutex_locked") == true)
            reasons.Add("gourmand_mutex_locked");
        if (ReadBool(row, "triggered_gourmand") == true)
            reasons.Add("gourmand_request_already_triggered");
        if (endpoint is null)
            reasons.Add("gourmand_no_reachable_action_stand");

        return new[]
        {
            new EventCandidate
            {
                CandidateId = "island-gourmand:" + requestIndex.Value + ":" + stage,
                Kind = "advance_gourmand_request",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = endpoint?.tile.X,
                TileY = endpoint?.tile.Y,
                EstimatedTicks = stage == "introduce_request" ? 600 : 1800,
                AvailabilityClass = "transparent_native_island_gourmand_request",
                ExpectedEffect = GourmandExpectedEffect(stage, requestIndex.Value),
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = endpoint is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : GourmandParameters(row, endpoint.tile, endpoint.stand!, stage, requestIndex.Value, cropItemId)
            }
        };
    }

    private EventCandidate[] IslandGourmandRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement progress,
        string currentLocation,
        string targetLocation,
        int requestIndex,
        string cropItemId)
    {
        var route = FindResolvedRoutePlan(
            snapshot,
            currentLocation,
            targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue)
                .Where(candidate => candidate.Kind == "route_connector_tile")
                .ToArray());
        if (route?.FirstActionCandidate is null)
            return Array.Empty<EventCandidate>();

        var stage = ReadBool(progress, "talked_to_gourmand_received_or_pending") == true
            ? "complete_request"
            : "introduce_request";
        return new[]
        {
            CloneCandidate(
                route.FirstActionCandidate,
                candidateId: "island-gourmand-route:" + requestIndex + ":" + currentLocation,
                expectedEffect: route.FirstActionCandidate.ExpectedEffect +
                    ";gourmand_request_index=" + requestIndex +
                    ";gourmand_interaction_stage=" + stage,
                parameters: route.FirstActionCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "island.complete_gourmand_request"),
                    Parameter("continuation.gourmand_request_index", requestIndex.ToString(CultureInfo.InvariantCulture)),
                    Parameter("continuation.gourmand_crop_item_id", cropItemId),
                    Parameter("continuation.gourmand_interaction_stage", stage)
                }).ToArray(),
                availabilityClass: "island_gourmand_rolling_route")
        };
    }

    private static SmallModelActionParameter[] GourmandParameters(
        JsonElement progress,
        GourmandActionTile tile,
        CandidateTile stand,
        string stage,
        int requestIndex,
        string cropItemId)
    {
        var completing = stage == "complete_request";
        return new[]
        {
            Parameter("target_location", ReadString(progress, "location_id")),
            Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("gourmand_action", tile.Action),
            Parameter("gourmand_schema_version", GourmandSchemaVersion),
            Parameter("gourmand_projection_status", ReadString(progress, "projection_status")),
            Parameter("gourmand_interaction_stage", stage),
            Parameter("request_index_before", requestIndex.ToString(CultureInfo.InvariantCulture)),
            Parameter("request_index_after", (completing ? requestIndex + 1 : requestIndex).ToString(CultureInfo.InvariantCulture)),
            Parameter("required_crop_item_id", cropItemId),
            Parameter("required_crop_qualified_item_id", ReadString(progress, "required_crop_qualified_item_id")),
            Parameter("ready_matching_crop_count", ReadInt(progress, "ready_matching_crop_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("talked_to_gourmand_before", GourmandBoolText(progress, "talked_to_gourmand_received_or_pending")),
            Parameter("talked_to_gourmand_after", "true"),
            Parameter("expected_collected_nut_key", completing ? "IslandGourmand" + (requestIndex + 1) : string.Empty),
            Parameter("expected_walnut_debris_spawn_count", completing ? "5" : "0"),
            Parameter("native_contract", GourmandNativeContract)
        };
    }

    private static string GourmandExpectedEffect(string stage, int requestIndex) =>
        stage == "complete_request"
            ? "gourmand_requests_fulfilled=" + (requestIndex + 1) +
              ";collected_nut_key=IslandGourmand" + (requestIndex + 1) +
              ";walnut_debris_spawn_count=5;fresh_snapshot_required=true"
            : "talkedToGourmand_received_or_pending=true;gourmand_requests_fulfilled=" + requestIndex +
              ";fresh_snapshot_required=true";

    private static bool HasExactReadyGourmandCrop(JsonElement progress)
    {
        if (!progress.TryGetProperty("matching_crops", out var crops) || crops.ValueKind != JsonValueKind.Array)
            return false;
        var itemId = ReadString(progress, "required_crop_item_id");
        return crops.EnumerateArray().Any(crop =>
            crop.ValueKind == JsonValueKind.Object &&
            ReadString(crop, "harvest_item_id") == itemId &&
            ReadBool(crop, "ready_for_request") == true);
    }

    private static bool GourmandCropMatchesRequest(int requestIndex, string itemId) =>
        (requestIndex == 0 && itemId == "254") ||
        (requestIndex == 1 && itemId == "262") ||
        (requestIndex == 2 && itemId == "248");

    private static GourmandActionTile[] ReadGourmandActionTiles(JsonElement progress)
    {
        if (!progress.TryGetProperty("action_tiles", out var tiles) || tiles.ValueKind != JsonValueKind.Array)
            return Array.Empty<GourmandActionTile>();
        return tiles.EnumerateArray()
            .Where(tile => tile.ValueKind == JsonValueKind.Object && ReadString(tile, "action") == "Gourmand")
            .Select(tile => new GourmandActionTile(ReadInt(tile, "tile_x"), ReadInt(tile, "tile_y"), "Gourmand"))
            .ToArray();
    }

    private static string GourmandBoolText(JsonElement row, string property) =>
        ReadBool(row, property) == true ? "true" : "false";

    private sealed record GourmandActionTile(int X, int Y, string Action);
}
