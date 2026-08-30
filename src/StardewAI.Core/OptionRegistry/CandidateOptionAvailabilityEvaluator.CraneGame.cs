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
    private EventCandidate[] CraneGameCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "crane_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "invocation_policy") != "player_command_only")
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(row, "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return CraneGameRouteCandidates(snapshot, row, currentLocation, targetLocation);
        var fingerprint = ReadString(row, "projection_fingerprint");

        var endpoint = ReadCraneGameTiles(row)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(value => value.stand is not null)
            .OrderBy(value => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - value.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - value.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (ReadString(row, "gate_status") != "ready")
            reasons.Add(ReadString(row, "gate_status"));
        if (endpoint is null)
            reasons.Add("crane_game_has_no_reachable_interaction_stand");
        if (!CraneGameProjectionIsTyped(row))
            reasons.Add("crane_game_typed_projection_invalid");

        return new[]
        {
            new EventCandidate
            {
                CandidateId = "crane-game:one-native-session:" +
                    (fingerprint.Length >= 12 ? fingerprint[..12] : "invalid"),
                Kind = "play_crane_game",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = endpoint?.tile.X,
                TileY = endpoint?.tile.Y,
                EstimatedTicks = 4200,
                EnergyCost = 0,
                AvailabilityClass = "player_command_only_native_crane_game_session",
                ExpectedEffect = "money_delta=-500;native_crane_attempts=3;native_reward_menu_settled=true",
                BlockReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = endpoint is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : CraneGameParameters(row, endpoint.tile, endpoint.stand!)
            }
        };
    }

    private EventCandidate[] CraneGameRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(projection, "gate_status") != "route_to_movie_theater_required" ||
            !CraneGameProjectionIsTyped(projection))
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstConnectorCandidate is null)
            return Array.Empty<EventCandidate>();
        return new[]
        {
            CloneCandidate(
                route.FirstConnectorCandidate,
                candidateId: "crane-game-route:" + currentLocation,
                expectedEffect: route.FirstConnectorCandidate.ExpectedEffect + ";crane_game_player_command_continuation=true",
                parameters: route.FirstConnectorCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "minigame.play_crane_game"),
                    Parameter("continuation.crane_selection_policy", ReadString(projection, "selection_policy")),
                    Parameter("continuation.crane_fee_gold", ReadInt(projection, "fee_gold").ToString(CultureInfo.InvariantCulture))
                }).ToArray(),
                availabilityClass: "crane_game_player_command_rolling_route")
        };
    }

    private static SmallModelActionParameter[] CraneGameParameters(
        JsonElement projection,
        CraneGameTile tile,
        CandidateTile stand) =>
        new[]
        {
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("crane_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("crane_action_raw", tile.ActionRaw),
            Parameter("crane_action_token", tile.ActionToken),
            Parameter("crane_yes_response_key", "Yes"),
            Parameter("crane_fee_gold", ReadInt(projection, "fee_gold").ToString(CultureInfo.InvariantCulture)),
            Parameter("crane_money_before", ReadInt(projection, "money").ToString(CultureInfo.InvariantCulture)),
            Parameter("crane_empty_slots_before", ReadInt(projection, "inventory_empty_slots").ToString(CultureInfo.InvariantCulture)),
            Parameter("crane_attempts", ReadInt(projection, "attempts_per_session").ToString(CultureInfo.InvariantCulture)),
            Parameter("crane_timer_ticks_per_attempt", ReadInt(projection, "timer_ticks_per_attempt").ToString(CultureInfo.InvariantCulture)),
            Parameter("crane_selection_policy", ReadString(projection, "selection_policy")),
            Parameter("crane_exit_policy", "finish_three_attempts_then_collect_all_native_rewards"),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };

    private static bool CraneGameProjectionIsTyped(JsonElement projection) =>
        ReadString(projection, "projection_fingerprint").Length == 64 &&
        ReadBool(projection, "machine_occupied") == false &&
        ReadInt(projection, "fee_gold") == 500 &&
        ReadInt(projection, "money") >= 500 &&
        ReadInt(projection, "inventory_empty_slots") >= 3 &&
        ReadInt(projection, "attempts_per_session") == 3 &&
        ReadInt(projection, "timer_ticks_per_attempt") == 900 &&
        ReadString(projection, "selection_policy") == "best_reachable_live_prize_nonlarge_stationary_then_distance;refresh_each_attempt" &&
        ReadString(projection, "native_contract") ==
            "MovieTheater_CraneGame_checkAction_then_yes_500g_then_native_CraneGame_directional_input_then_native_ItemGrabMenu_rewards";

    private static CraneGameTile[] ReadCraneGameTiles(JsonElement projection)
    {
        if (!projection.TryGetProperty("interaction_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<CraneGameTile>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new CraneGameTile(
                ReadInt(row, "tile_x"),
                ReadInt(row, "tile_y"),
                ReadString(row, "action_raw"),
                ReadString(row, "action_token")))
            .ToArray();
    }

    private sealed record CraneGameTile(int X, int Y, string ActionRaw, string ActionToken);
}
