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
    private EventCandidate[] GarbageCanRummageCandidates(SnapshotEnvelope snapshot)
    {
        var cans = ReadStateFieldValue(snapshot, "current_location", "garbage_cans");
        if (!cans.HasValue || cans.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return cans.Value.EnumerateArray()
            .Where(can => can.ValueKind == JsonValueKind.Object)
            .Select(can =>
            {
                var x = ReadInt(can, "tile_x");
                var y = ReadInt(can, "tile_y");
                var interaction = FindBestTerrainInteraction(snapshot, x, y, 1);
                var produced = ReadBool(can, "predicted_item_produced") == true;
                var output = can.TryGetProperty("expected_output", out var outputElement) &&
                    outputElement.ValueKind == JsonValueKind.Object
                        ? outputElement
                        : default;
                var outputId = output.ValueKind == JsonValueKind.Object
                    ? ReadString(output, "qualified_item_id")
                    : string.Empty;
                var outputQuantity = output.ValueKind == JsonValueKind.Object
                    ? ReadInt(output, "quantity")
                    : 0;
                var status = ReadString(can, "rummage_status");
                var reasons = new List<string>();
                if (!string.Equals(status, "ready", StringComparison.Ordinal))
                    reasons.Add(string.IsNullOrWhiteSpace(status) ? "garbage_can_projection_unavailable" : status);
                if (ReadBool(can, "garbage_can_id_known") != true ||
                    !string.Equals(ReadString(can, "data_contract_status"), "exact_locked_base_1.6.15", StringComparison.Ordinal) ||
                    !string.Equals(ReadString(can, "prediction_status"), "exact_native_non_mutating_prediction", StringComparison.Ordinal))
                    reasons.Add("garbage_can_prediction_contract_incomplete");
                if (produced != (output.ValueKind == JsonValueKind.Object) ||
                    (produced && (string.IsNullOrWhiteSpace(outputId) || outputQuantity <= 0)))
                    reasons.Add("garbage_can_output_projection_incomplete");
                if (interaction is null) reasons.Add("garbage_can_no_reachable_adjacent_interaction");

                var parameters = interaction is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : GarbageCanParameters(can, locationId, interaction);
                if (parameters.Length > 0)
                    reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                    {
                        OptionId = "executor.rummage_garbage",
                        Parameters = parameters
                    }));

                var distance = interaction is null
                    ? 0
                    : Math.Abs(playerX - interaction.Stand.X) + Math.Abs(playerY - interaction.Stand.Y);
                return new EventCandidate
                {
                    CandidateId = "rummage-garbage:" + locationId + ":" + x + "," + y + ":" + ReadString(can, "garbage_can_id"),
                    Kind = "rummage_garbage",
                    Available = reasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ItemId = UnqualifiedObjectId(outputId),
                    QualifiedItemId = outputId,
                    Quantity = outputQuantity,
                    ExpectedEffect = GarbageCanExpectedEffect(can, interaction),
                    EstimatedTicks = Math.Max(45, distance * 60 + 45),
                    EnergyCost = 0,
                    AvailabilityClass = produced
                        ? "transparent_deterministic_native_garbage_output"
                        : "transparent_deterministic_native_garbage_empty",
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            })
            .OrderBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private static SmallModelActionParameter[] GarbageCanParameters(
        JsonElement can,
        string locationId,
        TerrainInteraction interaction)
    {
        var output = can.TryGetProperty("expected_output", out var value) ? value : default;
        var outputJson = output.ValueKind is JsonValueKind.Object or JsonValueKind.Null
            ? output.GetRawText()
            : "null";
        var contextTags = output.ValueKind == JsonValueKind.Object
            ? ReadStringArray(output, "context_tags")
            : Array.Empty<string>();
        var reactionJson = can.TryGetProperty("reacting_npc", out var reaction) &&
            reaction.ValueKind is JsonValueKind.Object or JsonValueKind.Null
                ? reaction.GetRawText()
                : "null";
        return new[]
        {
            Parameter("target_location", locationId),
            Parameter("target_tile_x", ReadInt(can, "tile_x").ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", ReadInt(can, "tile_y").ToString(CultureInfo.InvariantCulture)),
            Parameter("interaction_tile_x", interaction.Action.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("interaction_tile_y", interaction.Action.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", interaction.Stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", interaction.Stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("garbage_can_action", ReadString(can, "action")),
            Parameter("garbage_can_id", ReadString(can, "garbage_can_id")),
            Parameter("expected_checked_today_before", (ReadBool(can, "checked_today") == true).ToString().ToLowerInvariant()),
            Parameter("expected_checked_today_after", (ReadBool(can, "expected_checked_today_after") == true).ToString().ToLowerInvariant()),
            Parameter("expected_trash_cans_checked_before", ReadInt(can, "trash_cans_checked_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_trash_cans_checked_delta", ReadInt(can, "expected_trash_cans_checked_delta").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_daily_luck", ReadDouble(can, "daily_luck").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("expected_alleyway_buffet_read", (ReadBool(can, "alleyway_buffet_read") == true).ToString().ToLowerInvariant()),
            Parameter("predicted_item_produced", (ReadBool(can, "predicted_item_produced") == true).ToString().ToLowerInvariant()),
            Parameter("selected_entry_id", ReadString(can, "selected_entry_id")),
            Parameter("selected_ignore_base_chance", (ReadBool(can, "selected_ignore_base_chance") == true).ToString().ToLowerInvariant()),
            Parameter("selected_mega_success", (ReadBool(can, "selected_mega_success") == true).ToString().ToLowerInvariant()),
            Parameter("selected_double_mega_success", (ReadBool(can, "selected_double_mega_success") == true).ToString().ToLowerInvariant()),
            Parameter("output_delivery", ReadString(can, "output_delivery")),
            Parameter("expected_output_json", outputJson),
            Parameter("garbage_output_context_tags_json", JsonSerializer.Serialize(contextTags)),
            Parameter("reacting_npc_json", reactionJson),
            Parameter("safe_slot_index", ReadInt(can, "safe_slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("safe_slot_kind", "empty"),
            Parameter("restore_slot_index", ReadInt(can, "restore_slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("garbage_can_data_payload_sha256", ReadString(can, "data_payload_sha256")),
            Parameter("garbage_can_data_contract_status", ReadString(can, "data_contract_status")),
            Parameter("garbage_can_prediction_status", ReadString(can, "prediction_status")),
            Parameter("garbage_can_native_contract", ReadString(can, "native_contract")),
            Parameter("garbage_can_projection_fingerprint", ReadString(can, "projection_fingerprint")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string GarbageCanExpectedEffect(JsonElement can, TerrainInteraction? interaction)
    {
        var outputJson = can.TryGetProperty("expected_output", out var output) &&
            output.ValueKind is JsonValueKind.Object or JsonValueKind.Null
                ? output.GetRawText()
                : "null";
        return (interaction is null
                ? string.Empty
                : "garbage_can_stand_tile=" + interaction.Stand.X + "," + interaction.Stand.Y +
                  ";garbage_can_interaction_tile=" + interaction.Action.X + "," + interaction.Action.Y + ";") +
            "garbage_can_id=" + ReadString(can, "garbage_can_id") +
            ";expected_checked_today_after=true" +
            ";expected_trash_cans_checked_delta=" + ReadInt(can, "expected_trash_cans_checked_delta") +
            ";predicted_item_produced=" + (ReadBool(can, "predicted_item_produced") == true).ToString().ToLowerInvariant() +
            ";output_delivery=" + ReadString(can, "output_delivery") +
            ";expected_output_json=" + outputJson +
            ";selected_entry_id=" + ReadString(can, "selected_entry_id") +
            ";garbage_can_projection_fingerprint=" + ReadString(can, "projection_fingerprint") +
            ";max_movement_tiles=512";
    }
}
