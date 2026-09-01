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
    private EventCandidate[] PrairieKingCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "prairie_king");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "invocation_policy") != "autonomous_timed_equivalent" ||
            ReadString(row, "native_proxy_policy") != "post_core_training_player_command_only" ||
            ReadInt64(row, "completed_without_dying_before") > 0)
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(row, "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return PrairieKingRouteCandidates(snapshot, row, currentLocation, targetLocation);

        var endpoint = ReadPrairieKingTiles(row)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(value => value.stand is not null)
            .OrderBy(value => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - value.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - value.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (ReadString(row, "gate_status") != "ready")
            reasons.Add(ReadString(row, "gate_status"));
        if (endpoint is null)
            reasons.Add("prairie_king_has_no_reachable_interaction_stand");
        if (!PrairieKingProjectionIsTyped(row))
            reasons.Add("prairie_king_typed_projection_invalid");
        var fingerprint = ReadString(row, "projection_fingerprint");

        return new[]
        {
            new EventCandidate
            {
                CandidateId = "prairie-king:complete-without-dying:" +
                    (fingerprint.Length >= 12 ? fingerprint[..12] : "invalid"),
                Kind = "play_prairie_king",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = endpoint?.tile.X,
                TileY = endpoint?.tile.Y,
                EstimatedTicks = ReadInt(row, "equivalent_duration_ticks"),
                EnergyCost = 0,
                AvailabilityClass = "autonomous_timed_equivalent_prairie_king_completion",
                ExpectedEffect = "completedPrairieKing_delta=1;completedPrairieKingWithoutDying_delta=1;native_phase1_settlement=true",
                BlockReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = endpoint is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : PrairieKingParameters(row, endpoint.tile, endpoint.stand!)
            }
        };
    }

    private EventCandidate[] PrairieKingRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(projection, "gate_status") != "route_to_saloon_required" ||
            !PrairieKingProjectionIsTyped(projection))
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstActionCandidate is null)
            return Array.Empty<EventCandidate>();
        return new[]
        {
            CloneCandidate(
                route.FirstActionCandidate,
                candidateId: "prairie-king-route:" + currentLocation,
                expectedEffect: route.FirstActionCandidate.ExpectedEffect + ";prairie_king_continuation=true",
                parameters: route.FirstActionCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "minigame.play_prairie_king"),
                    Parameter("continuation.prairie_king_completion_goal", "complete_without_dying")
                }).ToArray(),
                availabilityClass: "prairie_king_rolling_saloon_route")
        };
    }

    private static SmallModelActionParameter[] PrairieKingParameters(
        JsonElement projection,
        PrairieKingTile tile,
        CandidateTile stand) =>
        new[]
        {
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("prairie_king_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("prairie_king_action_raw", tile.ActionRaw),
            Parameter("prairie_king_action_token", tile.ActionToken),
            Parameter("prairie_king_dialogue_key", ReadString(projection, "dialogue_key")),
            Parameter("prairie_king_dialogue_response_key", ReadString(projection, "dialogue_response_key")),
            Parameter("prairie_king_completed_before", ReadInt64(projection, "completed_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("prairie_king_completed_without_dying_before", ReadInt64(projection, "completed_without_dying_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("prairie_king_completion_goal", ReadString(projection, "completion_goal")),
            Parameter("prairie_king_equivalent_duration_ticks", ReadInt(projection, "equivalent_duration_ticks").ToString(CultureInfo.InvariantCulture)),
            Parameter("prairie_king_equivalent_acceleration", ReadInt(projection, "equivalent_acceleration").ToString(CultureInfo.InvariantCulture)),
            Parameter("prairie_king_equivalent_contract", ReadString(projection, "equivalent_contract")),
            Parameter("minigame_id", "PrairieKing"),
            Parameter("max_movement_tiles", "512")
        };

    private static bool PrairieKingProjectionIsTyped(JsonElement projection) =>
        ReadString(projection, "projection_fingerprint").Length == 64 &&
        ReadString(projection, "location_id") == "Saloon" &&
        ReadInt64(projection, "completed_without_dying_before") == 0 &&
        ReadString(projection, "completion_goal") == "complete_without_dying" &&
        ReadInt(projection, "equivalent_duration_ticks") == 108000 &&
        ReadInt(projection, "equivalent_acceleration") == 60 &&
        ReadString(projection, "native_completion_trigger") == "AbigailGame.usePowerup(-3)" &&
        ReadString(projection, "equivalent_contract") ==
            "Saloon_Arcade_Prairie_checkAction_optional_CowboyGame_NewGame_then_timed_equivalent_then_AbigailGame_usePowerup_minus3_native_phase1_settlement";

    private static PrairieKingTile[] ReadPrairieKingTiles(JsonElement projection)
    {
        if (!projection.TryGetProperty("interaction_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<PrairieKingTile>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new PrairieKingTile(
                ReadInt(row, "tile_x"),
                ReadInt(row, "tile_y"),
                ReadString(row, "action_raw"),
                ReadString(row, "action_token")))
            .ToArray();
    }

    private sealed record PrairieKingTile(int X, int Y, string ActionRaw, string ActionToken);
}
