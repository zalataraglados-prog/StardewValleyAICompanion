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
    private EventCandidate[] FairWheelSpinCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "fair_wheel_spin");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "festival_id") != "festival_fall16")
            return Array.Empty<EventCandidate>();

        var reasons = new List<string>();
        var gate = ReadString(projection.Value, "gate_status");
        if (!string.Equals(gate, "ready", StringComparison.Ordinal))
            reasons.Add(string.IsNullOrWhiteSpace(gate) ? "fair_wheel_projection_unavailable" : gate);
        var remainingDemand = ReadInt(projection.Value, "remaining_star_token_demand");
        var wager = ReadInt(projection.Value, "wager_star_tokens");
        var score = ReadInt(projection.Value, "festival_score");
        var expectedWager = remainingDemand >= 2 ? Math.Min(remainingDemand, score * 7 / 15) : 0;
        if (remainingDemand < 2)
            reasons.Add("fair_wheel_requires_stardrop_demand_of_at_least_two_tokens");
        if (wager < 1 || wager != expectedWager)
            reasons.Add("fair_wheel_exact_zero_luck_kelly_wager_unavailable");
        if (ReadString(projection.Value, "selected_color") != "green")
            reasons.Add("fair_wheel_green_strategy_required");
        if (ActiveMenuOpenForCandidate(snapshot))
            reasons.Add("fair_wheel_active_menu_open");

        var locationId = ReadString(projection.Value, "festival_location_id");
        JsonElement? selected = null;
        if (projection.Value.TryGetProperty("interaction_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            selected = tiles.EnumerateArray()
                .Where(row => ReadInt(row, "tile_index", -1) is 308 or 309)
                .Where(row => Math.Abs(ReadInt(row, "tile_x") - ReadInt(row, "stand_tile_x")) +
                    Math.Abs(ReadInt(row, "tile_y") - ReadInt(row, "stand_tile_y")) == 1)
                .Where(row => !CollisionGridBlocksTile(snapshot,
                    ReadInt(row, "stand_tile_x"), ReadInt(row, "stand_tile_y")))
                .OrderBy(row => Math.Abs(playerX - ReadInt(row, "stand_tile_x")) +
                    Math.Abs(playerY - ReadInt(row, "stand_tile_y")))
                .Cast<JsonElement?>()
                .FirstOrDefault();
        }
        if (!selected.HasValue)
            reasons.Add("fair_wheel_reachable_stand_unavailable");
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.Ordinal))
            reasons.Add("fair_wheel_player_not_in_festival_location");

        var parameters = selected.HasValue
            ? FairWheelSpinParameters(projection.Value, selected.Value)
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
                CandidateId = "fair-wheel:green-zero-luck-kelly-stardrop-demand",
                Kind = "spin_fair_wheel",
                Available = distinctReasons.Length == 0,
                LocationId = locationId,
                TileX = selected.HasValue ? ReadInt(selected.Value, "tile_x") : null,
                TileY = selected.HasValue ? ReadInt(selected.Value, "tile_y") : null,
                ExpectedEffect = "festival_score=stochastic_plus_or_minus_" + wager +
                    ";green_base_win_probability=22/30;remaining_star_token_demand_replan_after_result",
                ItemId = string.Empty,
                QualifiedItemId = string.Empty,
                SlotIndex = null,
                Quantity = wager,
                EstimatedTicks = 900 + distance * 60,
                AvailabilityClass = "transparent_native_fall_fair_green_zero_luck_kelly_stardrop_wager",
                AllowedNow = distinctReasons.Length == 0,
                AllowedToday = true,
                BlockReasons = distinctReasons,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] FairWheelSpinParameters(JsonElement projection, JsonElement row)
    {
        var distribution = projection.GetProperty("base_zero_luck_distribution");
        return new[]
        {
            Parameter("target_location", ReadString(projection, "festival_location_id")),
            Parameter("interaction_tile_x", ReadInt(row, "tile_x").ToString()),
            Parameter("interaction_tile_y", ReadInt(row, "tile_y").ToString()),
            Parameter("stand_tile_x", ReadInt(row, "stand_tile_x").ToString()),
            Parameter("stand_tile_y", ReadInt(row, "stand_tile_y").ToString()),
            Parameter("fair_wheel_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("festival_id", ReadString(projection, "festival_id")),
            Parameter("festival_score_before", ReadInt(projection, "festival_score").ToString()),
            Parameter("stardrop_price_star_tokens", ReadInt(projection, "stardrop_price_star_tokens").ToString()),
            Parameter("projected_unclaimed_grange_tokens", ReadInt(projection, "projected_unclaimed_grange_tokens").ToString()),
            Parameter("remaining_star_token_demand", ReadInt(projection, "remaining_star_token_demand").ToString()),
            Parameter("selected_color", ReadString(projection, "selected_color")),
            Parameter("wager_star_tokens", ReadInt(projection, "wager_star_tokens").ToString()),
            Parameter("luck_level", ReadInt(projection, "effective_luck_level").ToString()),
            Parameter("base_green_wins", ReadInt(distribution, "green_wins").ToString()),
            Parameter("base_orange_wins", ReadInt(distribution, "orange_wins").ToString()),
            Parameter("base_outcome_count", ReadInt(distribution, "constructor_outcomes").ToString()),
            Parameter("prestart_duration_ms", ReadInt(projection, "prestart_duration_ms").ToString()),
            Parameter("result_duration_ms", ReadInt(projection, "result_duration_ms").ToString()),
            Parameter("dialogue_key", ReadString(projection, "dialogue_key")),
            Parameter("response_key", ReadString(projection, "response_key")),
            Parameter("wager_policy", ReadString(projection, "wager_policy")),
            Parameter("native_contract", ReadString(projection, "native_contract"))
        };
    }
}
