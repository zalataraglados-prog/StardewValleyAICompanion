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
    private EventCandidate[] DartsGameCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "darts_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "invocation_policy") != "autonomous_progression")
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(row, "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return DartsGameRouteCandidates(snapshot, row, currentLocation, targetLocation);

        var fingerprint = ReadString(row, "projection_fingerprint");
        var endpoint = ReadDartsGameTiles(row)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(value => value.stand is not null)
            .OrderBy(value => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - value.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - value.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (ReadString(row, "gate_status") != "ready")
            reasons.Add(ReadString(row, "gate_status"));
        if (endpoint is null)
            reasons.Add("darts_game_has_no_reachable_interaction_stand");
        if (!DartsGameProjectionIsTyped(row))
            reasons.Add("darts_game_typed_projection_invalid");

        return new[]
        {
            new EventCandidate
            {
                CandidateId = "darts-game:next-walnut:" + ReadInt(row, "limited_nut_dropped_before") + ":" +
                    (fingerprint.Length >= 12 ? fingerprint[..12] : "invalid"),
                Kind = "play_darts",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = endpoint?.tile.X,
                TileY = endpoint?.tile.Y,
                EstimatedTicks = 2400,
                EnergyCost = 0,
                AvailabilityClass = "autonomous_native_darts_limited_walnut_progression",
                ExpectedEffect = "darts_limited_nut_drop_delta=1;native_score=0;throws<=6;session_closed=true",
                BlockReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = endpoint is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : DartsGameParameters(row, endpoint.tile, endpoint.stand!)
            }
        };
    }

    private EventCandidate[] DartsGameRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(projection, "gate_status") != "route_to_pirate_cove_required" ||
            !DartsGameProjectionIsTyped(projection))
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstActionCandidate is null)
            return Array.Empty<EventCandidate>();
        return new[]
        {
            CloneCandidate(
                route.FirstActionCandidate,
                candidateId: "darts-game-route:" + currentLocation,
                expectedEffect: route.FirstActionCandidate.ExpectedEffect + ";darts_game_continuation=true",
                parameters: route.FirstActionCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "minigame.play_darts"),
                    Parameter("continuation.darts_limited_nut_dropped_before", ReadInt(projection, "limited_nut_dropped_before").ToString(CultureInfo.InvariantCulture)),
                    Parameter("continuation.darts_starting_dart_count", ReadInt(projection, "starting_dart_count").ToString(CultureInfo.InvariantCulture))
                }).ToArray(),
                availabilityClass: "darts_game_rolling_island_route")
        };
    }

    private static SmallModelActionParameter[] DartsGameParameters(
        JsonElement projection,
        DartsGameTile tile,
        CandidateTile stand) =>
        new[]
        {
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("darts_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("darts_action_raw", tile.ActionRaw),
            Parameter("darts_action_token", tile.ActionToken),
            Parameter("darts_yes_response_key", "Yes"),
            Parameter("darts_limited_nut_key", ReadString(projection, "limited_nut_key")),
            Parameter("darts_limited_nut_limit", ReadInt(projection, "limited_nut_limit").ToString(CultureInfo.InvariantCulture)),
            Parameter("darts_limited_nut_dropped_before", ReadInt(projection, "limited_nut_dropped_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("darts_limited_nut_dropped_after", ReadInt(projection, "limited_nut_dropped_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("darts_starting_dart_count", ReadInt(projection, "starting_dart_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("darts_starting_points", ReadInt(projection, "starting_points").ToString(CultureInfo.InvariantCulture)),
            Parameter("darts_perfect_victory_max_throws", ReadInt(projection, "perfect_victory_max_throws").ToString(CultureInfo.InvariantCulture)),
            Parameter("darts_perfect_score_plan", ReadString(projection, "perfect_score_plan")),
            Parameter("darts_charge_release_threshold", ReadDouble(projection, "charge_release_threshold").ToString("0.00", CultureInfo.InvariantCulture)),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };

    private static bool DartsGameProjectionIsTyped(JsonElement projection) =>
        ReadString(projection, "projection_fingerprint").Length == 64 &&
        ReadBool(projection, "pirate_night") == true &&
        ReadString(projection, "limited_nut_key") == "Darts" &&
        ReadInt(projection, "limited_nut_limit") == 3 &&
        ReadInt(projection, "limited_nut_dropped_before") is >= 0 and < 3 &&
        ReadInt(projection, "limited_nut_dropped_after") == ReadInt(projection, "limited_nut_dropped_before") + 1 &&
        ReadInt(projection, "starting_dart_count") == (ReadInt(projection, "limited_nut_dropped_before") switch
        {
            1 => 15,
            2 => 10,
            _ => 20
        }) &&
        ReadInt(projection, "starting_points") == 301 &&
        ReadInt(projection, "perfect_victory_max_throws") == 6 &&
        ReadString(projection, "perfect_score_plan") == "T20,T20,T20,T20,T17,D5" &&
        Math.Abs(ReadDouble(projection, "charge_release_threshold") - 0.02d) < 0.0001d &&
        ReadString(projection, "native_contract") ==
            "IslandSouthEastCave_DartsGame_checkAction_then_yes_then_native_Darts_mouse_aim_charge_release_then_native_limited_nut_drop";

    private static DartsGameTile[] ReadDartsGameTiles(JsonElement projection)
    {
        if (!projection.TryGetProperty("interaction_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<DartsGameTile>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new DartsGameTile(
                ReadInt(row, "tile_x"),
                ReadInt(row, "tile_y"),
                ReadString(row, "action_raw"),
                ReadString(row, "action_token")))
            .ToArray();
    }

    private sealed record DartsGameTile(int X, int Y, string ActionRaw, string ActionToken);
}
