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
    private EventCandidate[] GrangeDisplayCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "grange_display");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            !projection.Value.TryGetProperty("next_operation", out var operation) ||
            operation.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();

        var reasons = new List<string>();
        var gate = ReadString(projection.Value, "gate_status");
        if (!string.Equals(gate, "ready", StringComparison.Ordinal))
            reasons.Add(string.IsNullOrWhiteSpace(gate) ? "grange_projection_unavailable" : gate);
        if (!string.Equals(ReadString(operation, "status"), "ready", StringComparison.Ordinal))
            reasons.Add(ReadString(operation, "status"));
        if (ReadBool(projection.Value, "mutex_locked_by_other") == true)
            reasons.Add("grange_mutex_locked_by_other");
        if (ActiveMenuOpenForCandidate(snapshot))
            reasons.Add("grange_active_menu_open");

        var locationId = ReadString(projection.Value, "festival_location_id");
        CandidateTile? stand = null;
        int? actionX = null;
        int? actionY = null;
        if (projection.Value.TryGetProperty("interaction_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            foreach (var row in tiles.EnumerateArray())
            {
                var x = NullableReadInt(row, "tile_x");
                var y = NullableReadInt(row, "tile_y");
                if (!x.HasValue || !y.HasValue)
                    continue;
                var candidateStand = FindBestStandTile(snapshot, x.Value, y.Value);
                if (candidateStand is null)
                    continue;
                if (stand is null ||
                    Math.Abs(playerX - candidateStand.X) + Math.Abs(playerY - candidateStand.Y) <
                    Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y))
                {
                    stand = candidateStand;
                    actionX = x;
                    actionY = y;
                }
            }
        }
        if (stand is null || !actionX.HasValue || !actionY.HasValue)
            reasons.Add("grange_reachable_interaction_endpoint_unavailable");
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.Ordinal))
            reasons.Add("grange_player_not_in_festival_location");

        var parameters = stand is null || !actionX.HasValue || !actionY.HasValue
            ? Array.Empty<SmallModelActionParameter>()
            : GrangeDisplayParameters(projection.Value, operation, stand, actionX.Value, actionY.Value);
        var distance = stand is null ? 0 : Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - stand.X) +
            Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - stand.Y);
        var distinctReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "grange:" + ReadString(operation, "objective") + ":" +
                    ReadString(operation, "operation") + ":" + ReadInt(operation, "display_slot_index"),
                Kind = "manage_grange_display",
                Available = distinctReasons.Length == 0,
                LocationId = locationId,
                TileX = actionX,
                TileY = actionY,
                ExpectedEffect = "grange_display_slot=" + ReadInt(operation, "display_slot_index") +
                    ":" + ReadString(operation, "operation") + ";score=" + ReadInt(operation, "score_after") +
                    ";occupied_slots=" + ReadInt(operation, "occupied_slots_after"),
                ItemId = ReadString(operation, "item_id"),
                QualifiedItemId = ReadString(operation, "qualified_item_id"),
                SlotIndex = ReadInt(operation, "display_slot_index"),
                Quantity = 1,
                EstimatedTicks = Math.Max(180, distance * 60 + 180),
                AvailabilityClass = "transparent_native_fair_grange_management",
                AllowedNow = distinctReasons.Length == 0,
                AllowedToday = true,
                BlockReasons = distinctReasons,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] GrangeDisplayParameters(
        JsonElement projection,
        JsonElement operation,
        CandidateTile stand,
        int actionX,
        int actionY)
    {
        return new[]
        {
            Parameter("target_location", ReadString(projection, "festival_location_id")),
            Parameter("interaction_tile_x", actionX.ToString()),
            Parameter("interaction_tile_y", actionY.ToString()),
            Parameter("stand_tile_x", stand.X.ToString()),
            Parameter("stand_tile_y", stand.Y.ToString()),
            Parameter("grange_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("festival_id", ReadString(projection, "festival_id")),
            Parameter("grange_judged", ReadBool(projection, "grange_judged") == true ? "true" : "false"),
            Parameter("objective", ReadString(operation, "objective")),
            Parameter("operation", ReadString(operation, "operation")),
            Parameter("display_slot_index", ReadInt(operation, "display_slot_index").ToString()),
            Parameter("inventory_slot_index", ReadInt(operation, "inventory_slot_index").ToString()),
            Parameter("inventory_stack_before", ReadInt(operation, "inventory_stack_before").ToString()),
            Parameter("inventory_stack_after", ReadInt(operation, "inventory_stack_after").ToString()),
            Parameter("sink_inventory_slot_index", ReadInt(operation, "sink_inventory_slot_index").ToString()),
            Parameter("qualified_item_id", ReadString(operation, "qualified_item_id")),
            Parameter("item_id", ReadString(operation, "item_id")),
            Parameter("runtime_type", ReadString(operation, "runtime_type")),
            Parameter("quality", ReadInt(operation, "quality").ToString()),
            Parameter("actual_sell_price", ReadInt(operation, "actual_sell_price").ToString()),
            Parameter("item_points", ReadInt(operation, "item_points").ToString()),
            Parameter("scoring_group", ReadString(operation, "scoring_group")),
            Parameter("score_before", ReadInt(operation, "score_before").ToString()),
            Parameter("score_after", ReadInt(operation, "score_after").ToString()),
            Parameter("occupied_slots_before", ReadInt(operation, "occupied_slots_before").ToString()),
            Parameter("occupied_slots_after", ReadInt(operation, "occupied_slots_after").ToString()),
            Parameter("best_available_score", ReadInt(projection, "best_available_score").ToString()),
            Parameter("first_place_score", ReadInt(projection, "first_place_score").ToString()),
            Parameter("native_contract", ReadString(projection, "native_contract"))
        };
    }
}
