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
    private EventCandidate[] GeodeProcessingCandidates(SnapshotEnvelope snapshot, SmallModelActionParameter[] intent)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "geode_processing");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            !projection.Value.TryGetProperty("inventory_inputs", out var inputs) || inputs.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var requestedQid = GeodeIntent(intent, "geode_qualified_item_id");
        var purpose = GeodeIntent(intent, "geode_purpose");
        if (string.IsNullOrWhiteSpace(purpose)) purpose = "open_for_projected_value";
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(projection.Value, "location_id");
        var rows = inputs.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            ReadBool(row, "locked_base_1_6_15") == true && ReadString(row, "status") == "available" &&
            (string.IsNullOrWhiteSpace(requestedQid) || ReadString(row, "qualified_item_id") == requestedQid))
            .OrderBy(row => ReadInt(row, "slot_index")).ToArray();
        if (rows.Length == 0) return Array.Empty<EventCandidate>();

        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
        {
            if (ReadString(projection.Value, "base_service_status") != "route_to_blacksmith_required")
                return Array.Empty<EventCandidate>();
            var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
                RouteConnectorCandidates(snapshot, int.MaxValue).Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
            if (route?.FirstConnectorCandidate is null) return Array.Empty<EventCandidate>();
            return rows.Select(row => CloneCandidate(route.FirstConnectorCandidate,
                candidateId: "geode-route:" + ReadString(row, "qualified_item_id") + ":" + currentLocation,
                expectedEffect: route.FirstConnectorCandidate.ExpectedEffect + ";geode_processing_continuation=" + ReadString(row, "qualified_item_id"),
                parameters: route.FirstConnectorCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "processing.crack_geode"),
                    Parameter("continuation.geode_qualified_item_id", ReadString(row, "qualified_item_id")),
                    Parameter("continuation.geode_purpose", purpose)
                }).ToArray(), availabilityClass: "geode_processing_rolling_route")).ToArray();
        }

        var target = GeodeCounterTiles(projection.Value)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(row => row.stand is not null)
            .OrderBy(row => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - row.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - row.stand!.Y)).FirstOrDefault();
        var candidates = new List<EventCandidate>();
        foreach (var row in rows)
        {
            var reasons = new List<string>();
            if (ReadString(projection.Value, "base_service_status") != "ready")
                reasons.Add("geode_processing_service_not_ready:" + ReadString(projection.Value, "base_service_status"));
            if (ReadInt(projection.Value, "money_before") < ReadInt(projection.Value, "price_gold", 25))
                reasons.Add("geode_processing_money_below_25");
            if (ReadBool(row, "output_capacity_allowed") != true)
                reasons.Add("geode_processing_output_capacity_unavailable");
            if (target is null) reasons.Add("geode_processing_counter_has_no_reachable_stand");
            var parameters = target is null ? Array.Empty<SmallModelActionParameter>()
                : GeodeCandidateParameters(projection.Value, row, purpose, target.tile, target.stand!);
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "processing.crack_geode", Parameters = parameters
            }));
            var primary = row.TryGetProperty("expected_output", out var output) && output.ValueKind == JsonValueKind.Object
                ? output : default;
            candidates.Add(new EventCandidate
            {
                CandidateId = "crack-geode:" + ReadString(row, "qualified_item_id") + ":slot=" + ReadInt(row, "slot_index") + ":" +
                    ShortFingerprint(ReadString(projection.Value, "projection_fingerprint")),
                Kind = "crack_geode", Available = reasons.Count == 0, AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0, LocationId = targetLocation, TileX = target?.tile.X, TileY = target?.tile.Y,
                DisplayName = ReadString(row, "display_name"), ItemId = ReadString(row, "item_id"),
                QualifiedItemId = ReadString(row, "qualified_item_id"), Quantity = 1,
                EstimatedTicks = 420, EnergyCost = 0, AvailabilityClass = "transparent_native_blacksmith_geode_processing",
                ExpectedEffect = "geode_consumed=1;money_delta=-25;geodes_cracked_delta=1;prediction_kind=" +
                    ReadString(row, "kind") + ";expected_output=" + (primary.ValueKind == JsonValueKind.Object
                        ? ReadString(primary, "qualified_item_id") : "accepted_output_family"),
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(), Parameters = parameters
            });
        }
        return candidates.OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray();
    }

    private static SmallModelActionParameter[] GeodeCandidateParameters(JsonElement projection, JsonElement row,
        string purpose, GeodeCounterTile target, CandidateTile stand)
    {
        var primary = row.TryGetProperty("expected_output", out var output) && output.ValueKind == JsonValueKind.Object
            ? output : default;
        var context = projection.TryGetProperty("predictor_context", out var predictor) && predictor.ValueKind == JsonValueKind.Object
            ? predictor : default;
        return new[]
        {
            Parameter("geode_purpose", purpose), Parameter("geode_qualified_item_id", ReadString(row, "qualified_item_id")),
            Parameter("geode_slot_index", ReadInt(row, "slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_input_quality", ReadInt(row, "quality").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_stack_before", ReadInt(row, "stack_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_free_slots_before", ReadInt(projection, "free_inventory_slots").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_money_before", ReadInt(projection, "money_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_price_gold", ReadInt(projection, "price_gold", 25).ToString(CultureInfo.InvariantCulture)),
            Parameter("geodes_cracked_before", ReadInt(projection, "geodes_cracked_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("mystery_boxes_opened_before", ReadInt(projection, "mystery_boxes_opened_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("golden_coconut_cracked_before", (ReadBool(projection, "golden_coconut_cracked_before") == true).ToString().ToLowerInvariant()),
            Parameter("golden_walnuts_before", ReadInt(projection, "golden_walnuts_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("golden_walnuts_found_before", ReadInt(projection, "golden_walnuts_found_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_archaeology_found_count", ReadInt(projection, "archaeology_found_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_save_id_half", context.ValueKind == JsonValueKind.Object ? GeodeReadLong(context, "save_id_half").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_player_id_half", context.ValueKind == JsonValueKind.Object ? GeodeReadLong(context, "player_id_half").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_season", context.ValueKind == JsonValueKind.Object ? ReadString(context, "season") : string.Empty),
            Parameter("geode_deepest_mine_level", context.ValueKind == JsonValueKind.Object ? ReadInt(context, "deepest_mine_level").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_skill_1_level", context.ValueKind == JsonValueKind.Object ? ReadInt(context, "skill_1_unmodified_level").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_farming_mastery_unlocked", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "farming_mastery_unlocked") == true).ToString().ToLowerInvariant()),
            Parameter("geode_qi_beans_rule_active", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "qi_beans_rule_active") == true).ToString().ToLowerInvariant()),
            Parameter("geode_got_mystery_book_mail_before", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "got_mystery_book_mail") == true).ToString().ToLowerInvariant()),
            Parameter("geode_artifact_found_mail_before", (context.ValueKind == JsonValueKind.Object && ReadBool(context, "artifact_found_mail") == true).ToString().ToLowerInvariant()),
            Parameter("geode_prediction_kind", ReadString(row, "kind")),
            Parameter("geode_expected_output_qid", primary.ValueKind == JsonValueKind.Object ? ReadString(primary, "qualified_item_id") : string.Empty),
            Parameter("geode_expected_output_stack", primary.ValueKind == JsonValueKind.Object ? ReadInt(primary, "stack").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_expected_output_quality", primary.ValueKind == JsonValueKind.Object ? ReadInt(primary, "quality").ToString(CultureInfo.InvariantCulture) : "0"),
            Parameter("geode_accepted_outputs_json", row.TryGetProperty("accepted_outputs", out var accepted) ? accepted.GetRawText() : "[]"),
            Parameter("geode_expected_mail_additions_json", row.TryGetProperty("expected_mail_additions", out var mail) ? mail.GetRawText() : "[]"),
            Parameter("geode_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", target.X.ToString(CultureInfo.InvariantCulture)), Parameter("target_tile_y", target.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)), Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("geode_action_raw", target.ActionRaw), Parameter("geode_action_token", target.Token),
            Parameter("native_contract", ReadString(projection, "native_contract")), Parameter("max_movement_tiles", "512")
        };
    }

    private static GeodeCounterTile[] GeodeCounterTiles(JsonElement projection) =>
        projection.TryGetProperty("counter_action_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object && ReadString(row, "action_token") == "Blacksmith")
                .Select(row => new GeodeCounterTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y"),
                    ReadString(row, "action_raw"), ReadString(row, "action_token"))).ToArray()
            : Array.Empty<GeodeCounterTile>();

    private static string GeodeIntent(SmallModelActionParameter[] intent, string name)
    {
        var value = IntentParameter(intent, name);
        return string.IsNullOrWhiteSpace(value) ? IntentParameter(intent, "continuation." + name) : value;
    }

    private static string ShortFingerprint(string value) => value.Length >= 12 ? value[..12] : "invalid";
    private static long GeodeReadLong(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetInt64(out var result) ? result : 0L;
    private sealed record GeodeCounterTile(int X, int Y, string ActionRaw, string Token);
}
