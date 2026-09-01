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
    private EventCandidate[] BobberSelectionCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var styleText = BobberIntent(intent, "bobber_style_id");
        var reason = BobberIntent(intent, "bobber_reason");
        if (!int.TryParse(styleText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var styleId) ||
            styleId is < -2 or > 38 || styleId == -1 || string.IsNullOrWhiteSpace(reason) ||
            BobberIntent(intent, "confirm_bobber_style") != "true")
            return Array.Empty<EventCandidate>();

        var projection = ReadStateFieldValue(snapshot, "player", "bobber_selection");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "player_command_only")
            return Array.Empty<EventCandidate>();
        var style = FindBobberStyle(projection.Value, styleId);
        if (!style.HasValue || ReadBool(style.Value, "unlocked") != true)
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(projection.Value, "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return BobberRouteCandidates(snapshot, projection.Value, styleId, reason, currentLocation, targetLocation);

        var endpoint = BobberActionTiles(projection.Value)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(row => row.stand is not null)
            .OrderBy(row => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - row.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - row.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (ReadString(projection.Value, "service_status") != "ready")
            reasons.Add("bobber_selection_service_not_ready:" + ReadString(projection.Value, "service_status"));
        if (endpoint is null)
            reasons.Add("bobber_selection_has_no_reachable_stand");
        var parameters = endpoint is null
            ? Array.Empty<SmallModelActionParameter>()
            : BobberCandidateParameters(projection.Value, styleId, reason, endpoint.tile, endpoint.stand!);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "player.choose_bobber",
            Parameters = parameters,
            InvocationSource = OptionInvocationSource.PlayerCommand,
            ExplicitConfirmationGranted = true
        }));
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "bobber-selection:" + styleId + ":" +
                    (ReadString(projection.Value, "projection_fingerprint") is { Length: >= 12 } fingerprint
                        ? fingerprint[..12] : "invalid"),
                Kind = "choose_bobber_style",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = endpoint?.tile.X,
                TileY = endpoint?.tile.Y,
                EstimatedTicks = 420,
                EnergyCost = 0,
                AvailabilityClass = "explicit_player_command_native_bobber_cosmetic",
                ExpectedEffect = "player.bobber_style_id=" + styleId +
                    ";using_randomized_bobber=" + (styleId == -2).ToString().ToLowerInvariant(),
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private EventCandidate[] BobberRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        int styleId,
        string reason,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(projection, "service_status") != "route_to_fish_shop_required")
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue)
                .Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstActionCandidate is null)
            return Array.Empty<EventCandidate>();
        var continuation = new[]
        {
            Parameter("continuation.option_id", "player.choose_bobber"),
            Parameter("continuation.bobber_style_id", styleId.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.bobber_reason", reason),
            Parameter("continuation.confirm_bobber_style", "true")
        };
        return new[]
        {
            CloneCandidate(route.FirstActionCandidate,
                candidateId: "bobber-selection-route:" + styleId + ":" + currentLocation,
                expectedEffect: route.FirstActionCandidate.ExpectedEffect + ";bobber_style_continuation=" + styleId,
                parameters: route.FirstActionCandidate.Parameters.Concat(continuation).ToArray(),
                availabilityClass: "bobber_selection_player_command_rolling_route")
        };
    }

    private static SmallModelActionParameter[] BobberCandidateParameters(
        JsonElement projection,
        int styleId,
        string reason,
        BobberActionTile tile,
        CandidateTile stand) => new[]
    {
        Parameter("bobber_style_id", styleId.ToString(CultureInfo.InvariantCulture)),
        Parameter("bobber_reason", reason),
        Parameter("confirm_bobber_style", "true"),
        Parameter("bobber_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
        Parameter("bobber_style_before", ReadInt(projection, "current_style_id").ToString(CultureInfo.InvariantCulture)),
        Parameter("bobber_random_before", (ReadBool(projection, "using_randomized_bobber") == true).ToString().ToLowerInvariant()),
        Parameter("bobber_random_after", (styleId == -2).ToString().ToLowerInvariant()),
        Parameter("bobber_fish_caught_species_count", ReadInt(projection, "fish_caught_species_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("bobber_native_unlock_quotient", ReadInt(projection, "native_unlock_quotient").ToString(CultureInfo.InvariantCulture)),
        Parameter("target_location", ReadString(projection, "location_id")),
        Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("bobber_action_raw", tile.ActionRaw),
        Parameter("expected_menu_type_after", "ChooseFromIconsMenu"),
        Parameter("expected_menu_kind", "bobbers"),
        Parameter("native_contract", ReadString(projection, "native_contract")),
        Parameter("max_movement_tiles", "512")
    };

    private static JsonElement? FindBobberStyle(JsonElement projection, int styleId)
    {
        if (!projection.TryGetProperty("styles", out var styles) || styles.ValueKind != JsonValueKind.Array)
            return null;
        var row = styles.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object && ReadInt(value, "style_id") == styleId);
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static BobberActionTile[] BobberActionTiles(JsonElement projection) =>
        projection.TryGetProperty("action_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => new BobberActionTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y"),
                    ReadString(row, "action_raw"))).ToArray()
            : Array.Empty<BobberActionTile>();

    private static string BobberIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value ?? string.Empty;

    private sealed record BobberActionTile(int X, int Y, string ActionRaw);
}
