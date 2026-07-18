using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] PanningCandidates(SnapshotEnvelope snapshot)
    {
        var value = ReadStateFieldValue(snapshot, "current_location", "panning");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }
        var pan = value.Value;
        var x = ReadInt(pan, "ore_pan_point_x");
        var y = ReadInt(pan, "ore_pan_point_y");
        var locationId = ReadString(pan, "location_id");
        var stand = ReadBool(pan, "ore_pan_point_active") == true ? FindBestStandTile(snapshot, x, y) : null;
        var outputJson = ReadString(pan, "expected_output_items_json");
        var outputs = ParseOutputSummary(outputJson);
        var blockReasons = new List<string>();
        if (ReadString(pan, "status") != "exact")
        {
            blockReasons.Add(string.IsNullOrWhiteSpace(ReadString(pan, "status")) ? "panning_projection_unavailable" : ReadString(pan, "status"));
        }
        if (stand is null)
        {
            blockReasons.Add("panning_no_adjacent_stand_tile");
        }
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(outputJson) || outputs.Quantity <= 0)
        {
            blockReasons.Add("panning_output_identity_incomplete");
        }

        var parameters = stand is null ? Array.Empty<SmallModelActionParameter>() : PanningParameters(pan, x, y, stand.X, stand.Y);
        if (parameters.Length > 0)
        {
            blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.pan_ore_spot",
                Parameters = parameters
            }));
        }
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "pan-ore-spot:" + locationId + ":" + x + ":" + y + ":" + ReadInt(pan, "times_panned_before"),
                Kind = "pan_ore_spot",
                Available = blockReasons.Count == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                QualifiedItemId = outputs.FirstQualifiedItemId,
                Quantity = outputs.Quantity,
                ExpectedEffect = PanningExpectedEffect(pan, stand),
                EstimatedTicks = Math.Max(180, distance * 60 + 180),
                AvailabilityClass = "transparent_native_panning",
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] PanningParameters(JsonElement pan, int x, int y, int standX, int standY) => new[]
    {
        Parameter("target_location", ReadString(pan, "location_id")),
        Parameter("location_id", ReadString(pan, "location_id")),
        Parameter("target_tile_x", x.ToString()),
        Parameter("target_tile_y", y.ToString()),
        Parameter("stand_tile_x", standX.ToString()),
        Parameter("stand_tile_y", standY.ToString()),
        Parameter("target_runtime_type", "StardewValley.Tools.Pan"),
        Parameter("required_tool_kind", "Pan"),
        Parameter("tool_slot_index", ReadInt(pan, "pan_tool_slot_index").ToString()),
        Parameter("pan_upgrade_level", ReadInt(pan, "pan_upgrade_level").ToString()),
        Parameter("pan_enchantments_json", ReadString(pan, "pan_enchantments_json")),
        Parameter("click_pixel_x", ReadInt(pan, "click_pixel_x").ToString()),
        Parameter("click_pixel_y", ReadInt(pan, "click_pixel_y").ToString()),
        Parameter("expected_output_items_json", ReadString(pan, "expected_output_items_json")),
        Parameter("expected_stat_increments_json", ReadString(pan, "expected_receipt_stat_increments_json")),
        Parameter("native_receipt_callbacks_status", ReadString(pan, "native_receipt_callbacks_status")),
        Parameter("expected_times_panned_before", ReadInt(pan, "times_panned_before").ToString()),
        Parameter("expected_times_panned_after", ReadInt(pan, "times_panned_after").ToString()),
        Parameter("expected_mining_experience_before", ReadInt(pan, "mining_experience_before").ToString()),
        Parameter("expected_mining_experience_delta", ReadInt(pan, "mining_experience_delta").ToString()),
        Parameter("expected_mining_experience_after", ReadInt(pan, "mining_experience_after").ToString()),
        Parameter("expected_foraging_experience_before", ReadInt(pan, "foraging_experience_before").ToString()),
        Parameter("expected_foraging_experience_delta", ReadInt(pan, "foraging_experience_delta").ToString()),
        Parameter("expected_foraging_experience_after", ReadInt(pan, "foraging_experience_after").ToString()),
        Parameter("post_use_ore_pan_point_status", ReadString(pan, "post_use_ore_pan_point_status")),
        Parameter("post_use_respawn_attempts", ReadInt(pan, "post_use_respawn_attempts").ToString()),
        Parameter("max_movement_tiles", "512")
    };

    private static string PanningExpectedEffect(JsonElement pan, CandidateTile? stand) =>
        (stand is null ? string.Empty : "panning_stand_tile=" + stand.X + "," + stand.Y + ";") +
        "ore_pan_point=" + ReadInt(pan, "ore_pan_point_x") + "," + ReadInt(pan, "ore_pan_point_y") +
        ";expected_output_items_json=" + ReadString(pan, "expected_output_items_json") +
        ";expected_receipt_stat_increments_json=" + ReadString(pan, "expected_receipt_stat_increments_json") +
        ";native_receipt_callbacks_status=" + ReadString(pan, "native_receipt_callbacks_status") +
        ";expected_times_panned_after=" + ReadInt(pan, "times_panned_after") +
        ";expected_mining_experience_delta=" + ReadInt(pan, "mining_experience_delta") +
        ";expected_foraging_experience_delta=" + ReadInt(pan, "foraging_experience_delta") +
        ";post_use_ore_pan_point_status=" + ReadString(pan, "post_use_ore_pan_point_status");

    private static (string FirstQualifiedItemId, int Quantity) ParseOutputSummary(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return (string.Empty, 0);
            }
            var rows = document.RootElement.EnumerateArray().ToArray();
            return (rows.FirstOrDefault().ValueKind == JsonValueKind.Object
                    ? ReadString(rows[0], "QualifiedItemId", ReadString(rows[0], "qualifiedItemId"))
                    : string.Empty,
                rows.Sum(row => ReadInt(row, "Quantity", ReadInt(row, "quantity"))));
        }
        catch (JsonException)
        {
            return (string.Empty, 0);
        }
    }
}
