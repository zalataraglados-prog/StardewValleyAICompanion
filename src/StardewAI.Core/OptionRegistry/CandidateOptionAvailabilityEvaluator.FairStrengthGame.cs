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
    private EventCandidate[] FairStrengthGameCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "fair_strength_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "festival_id") != "festival_fall16")
            return Array.Empty<EventCandidate>();

        var reasons = new List<string>();
        var gate = ReadString(projection.Value, "gate_status");
        if (!string.Equals(gate, "ready", StringComparison.Ordinal))
            reasons.Add(string.IsNullOrWhiteSpace(gate) ? "fair_strength_projection_unavailable" : gate);
        if (ReadInt(projection.Value, "remaining_star_token_demand") != 1)
            reasons.Add("fair_strength_requires_exact_one_token_stardrop_top_up");
        if (ActiveMenuOpenForCandidate(snapshot))
            reasons.Add("fair_strength_active_menu_open");

        var locationId = ReadString(projection.Value, "festival_location_id");
        JsonElement? selected = null;
        if (projection.Value.TryGetProperty("interaction_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            selected = tiles.EnumerateArray()
                .Where(row => ReadInt(row, "tile_index", -1) == 540 &&
                    ReadInt(row, "stand_tile_x", -1) == 29 &&
                    Math.Abs(ReadInt(row, "tile_x") - ReadInt(row, "stand_tile_x")) +
                    Math.Abs(ReadInt(row, "tile_y") - ReadInt(row, "stand_tile_y")) == 1 &&
                    !CollisionGridBlocksTile(snapshot, ReadInt(row, "stand_tile_x"), ReadInt(row, "stand_tile_y")))
                .OrderBy(row => Math.Abs(playerX - ReadInt(row, "stand_tile_x")) +
                    Math.Abs(playerY - ReadInt(row, "stand_tile_y")))
                .Cast<JsonElement?>()
                .FirstOrDefault();
        }
        if (!selected.HasValue)
            reasons.Add("fair_strength_reachable_exact_stand_unavailable");
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.Ordinal))
            reasons.Add("fair_strength_player_not_in_festival_location");

        var parameters = selected.HasValue
            ? FairStrengthGameParameters(projection.Value, selected.Value)
            : Array.Empty<SmallModelActionParameter>();
        var distance = selected.HasValue
            ? Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - ReadInt(selected.Value, "stand_tile_x")) +
              Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - ReadInt(selected.Value, "stand_tile_y"))
            : 0;
        var distinctReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "fair-strength:exact-one-token-stardrop-top-up",
                Kind = "play_fair_strength_game",
                Available = distinctReasons.Length == 0,
                LocationId = locationId,
                TileX = selected.HasValue ? ReadInt(selected.Value, "tile_x") : null,
                TileY = selected.HasValue ? ReadInt(selected.Value, "tile_y") : null,
                ExpectedEffect = "festival_score=+1;remaining_star_token_demand=1->0;native_max_power_result=true",
                ItemId = string.Empty,
                QualifiedItemId = string.Empty,
                SlotIndex = null,
                Quantity = 1,
                EstimatedTicks = 210 + distance * 60,
                AvailabilityClass = "transparent_native_fall_fair_strength_exact_one_token_top_up",
                AllowedNow = distinctReasons.Length == 0,
                AllowedToday = true,
                BlockReasons = distinctReasons,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] FairStrengthGameParameters(JsonElement projection, JsonElement row)
    {
        return new[]
        {
            Parameter("target_location", ReadString(projection, "festival_location_id")),
            Parameter("interaction_tile_x", ReadInt(row, "tile_x").ToString()),
            Parameter("interaction_tile_y", ReadInt(row, "tile_y").ToString()),
            Parameter("stand_tile_x", ReadInt(row, "stand_tile_x").ToString()),
            Parameter("stand_tile_y", ReadInt(row, "stand_tile_y").ToString()),
            Parameter("fair_strength_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("festival_id", ReadString(projection, "festival_id")),
            Parameter("festival_score_before", ReadInt(projection, "festival_score").ToString()),
            Parameter("stardrop_price_star_tokens", ReadInt(projection, "stardrop_price_star_tokens").ToString()),
            Parameter("projected_unclaimed_grange_tokens", ReadInt(projection, "projected_unclaimed_grange_tokens").ToString()),
            Parameter("remaining_star_token_demand", ReadInt(projection, "remaining_star_token_demand").ToString()),
            Parameter("entry_fee_money", ReadInt(projection, "entry_fee_money").ToString()),
            Parameter("expected_reward_star_tokens", ReadInt(projection, "expected_reward_star_tokens").ToString()),
            Parameter("perfect_power_minimum", ReadDouble(projection, "perfect_power_minimum").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("power_maximum", ReadDouble(projection, "power_maximum").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("required_player_tile_x", ReadInt(projection, "required_player_tile_x").ToString()),
            Parameter("swing_start_frame", ReadInt(projection.GetProperty("swing_animation"), "start_frame").ToString()),
            Parameter("swing_interval_ms", ReadDouble(projection.GetProperty("swing_animation"), "interval_ms").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("swing_frame_count", ReadInt(projection.GetProperty("swing_animation"), "frame_count").ToString()),
            Parameter("perfect_result_delay_ms", ReadDouble(projection, "perfect_result_delay_ms").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("execution_strategy", ReadString(projection, "execution_strategy")),
            Parameter("native_contract", ReadString(projection, "native_contract"))
        };
    }
}
