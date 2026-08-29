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
    private EventCandidate[] FairFishingGameCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "fair_fishing_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "festival_id") != "festival_fall16")
            return Array.Empty<EventCandidate>();

        var reasons = new List<string>();
        var gate = ReadString(projection.Value, "gate_status");
        if (!string.Equals(gate, "ready", StringComparison.Ordinal))
            reasons.Add(string.IsNullOrWhiteSpace(gate) ? "fair_fishing_projection_unavailable" : gate);
        if (ReadInt(projection.Value, "player_money") < ReadInt(projection.Value, "entry_fee_money"))
            reasons.Add("fair_fishing_entry_fee_unavailable");
        if (ReadInt(projection.Value, "remaining_star_token_demand") <= 0)
            reasons.Add("fair_fishing_no_remaining_automatic_token_demand");
        if (ActiveMenuOpenForCandidate(snapshot))
            reasons.Add("fair_fishing_active_menu_open");

        var locationId = ReadString(projection.Value, "festival_location_id");
        CandidateTile? stand = null;
        int? interactionX = null;
        int? interactionY = null;
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
                    interactionX = x;
                    interactionY = y;
                }
            }
        }
        if (stand is null || !interactionX.HasValue || !interactionY.HasValue)
            reasons.Add("fair_fishing_reachable_interaction_endpoint_unavailable");
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.Ordinal))
            reasons.Add("fair_fishing_player_not_in_festival_location");

        var parameters = stand is null || !interactionX.HasValue || !interactionY.HasValue
            ? Array.Empty<SmallModelActionParameter>()
            : FairFishingGameParameters(projection.Value, stand, interactionX.Value, interactionY.Value);
        var distance = stand is null ? 0 :
            Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - stand.X) +
            Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - stand.Y);
        var distinctReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "fair-fishing:stardrop-demand:" + ReadInt(projection.Value, "remaining_star_token_demand"),
                Kind = "play_fair_fishing_game",
                Available = distinctReasons.Length == 0,
                LocationId = locationId,
                TileX = interactionX,
                TileY = interactionY,
                ExpectedEffect = "money=-50;festival_score=+native_fishing_game_reward;remaining_star_token_demand=decrease",
                ItemId = string.Empty,
                QualifiedItemId = string.Empty,
                SlotIndex = null,
                Quantity = 1,
                EstimatedTicks = 6900 + distance * 60,
                AvailabilityClass = "transparent_native_fall_fair_fishing_game",
                AllowedNow = distinctReasons.Length == 0,
                AllowedToday = true,
                BlockReasons = distinctReasons,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] FairFishingGameParameters(
        JsonElement projection,
        CandidateTile stand,
        int interactionX,
        int interactionY)
    {
        return new[]
        {
            Parameter("target_location", ReadString(projection, "festival_location_id")),
            Parameter("interaction_tile_x", interactionX.ToString()),
            Parameter("interaction_tile_y", interactionY.ToString()),
            Parameter("stand_tile_x", stand.X.ToString()),
            Parameter("stand_tile_y", stand.Y.ToString()),
            Parameter("fair_fishing_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("festival_id", ReadString(projection, "festival_id")),
            Parameter("money_before", ReadInt(projection, "player_money").ToString()),
            Parameter("entry_fee_money", ReadInt(projection, "entry_fee_money").ToString()),
            Parameter("festival_score_before", ReadInt(projection, "festival_score").ToString()),
            Parameter("stardrop_price_star_tokens", ReadInt(projection, "stardrop_price_star_tokens").ToString()),
            Parameter("projected_unclaimed_grange_tokens", ReadInt(projection, "projected_unclaimed_grange_tokens").ToString()),
            Parameter("remaining_star_token_demand", ReadInt(projection, "remaining_star_token_demand").ToString()),
            Parameter("game_duration_ms", ReadInt(projection, "game_duration_ms").ToString()),
            Parameter("results_duration_ms", ReadInt(projection, "results_duration_ms").ToString()),
            Parameter("dialogue_key", ReadString(projection, "dialogue_key")),
            Parameter("play_response_key", ReadString(projection, "play_response_key")),
            Parameter("execution_strategy", ReadString(projection, "execution_strategy")),
            Parameter("native_contract", ReadString(projection, "native_contract"))
        };
    }
}
