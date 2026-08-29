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
    private EventCandidate[] FairSlingshotGameCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "fair_slingshot_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "festival_id") != "festival_fall16")
            return Array.Empty<EventCandidate>();

        var reasons = new List<string>();
        var gate = ReadString(projection.Value, "gate_status");
        if (!string.Equals(gate, "ready", StringComparison.Ordinal))
            reasons.Add(string.IsNullOrWhiteSpace(gate) ? "fair_slingshot_projection_unavailable" : gate);
        if (ReadInt(projection.Value, "player_money") < ReadInt(projection.Value, "entry_fee_money"))
            reasons.Add("fair_slingshot_entry_fee_unavailable");
        if (ReadInt(projection.Value, "remaining_star_token_demand") <= 0)
            reasons.Add("fair_slingshot_no_remaining_automatic_token_demand");
        if (ActiveMenuOpenForCandidate(snapshot))
            reasons.Add("fair_slingshot_active_menu_open");

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
            reasons.Add("fair_slingshot_reachable_interaction_endpoint_unavailable");
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.Ordinal))
            reasons.Add("fair_slingshot_player_not_in_festival_location");

        var parameters = stand is null || !interactionX.HasValue || !interactionY.HasValue
            ? Array.Empty<SmallModelActionParameter>()
            : FairSlingshotGameParameters(projection.Value, stand, interactionX.Value, interactionY.Value);
        var distance = stand is null ? 0 :
            Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - stand.X) +
            Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - stand.Y);
        var distinctReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "fair-slingshot:stardrop-demand:" + ReadInt(projection.Value, "remaining_star_token_demand"),
                Kind = "play_fair_slingshot_game",
                Available = distinctReasons.Length == 0,
                LocationId = locationId,
                TileX = interactionX,
                TileY = interactionY,
                ExpectedEffect = "money=-50;festival_score=+native_slingshot_game_reward;remaining_star_token_demand=decrease",
                ItemId = string.Empty,
                QualifiedItemId = string.Empty,
                SlotIndex = null,
                Quantity = 1,
                EstimatedTicks = 4200 + distance * 60,
                AvailabilityClass = "transparent_native_fall_fair_slingshot_game",
                AllowedNow = distinctReasons.Length == 0,
                AllowedToday = true,
                BlockReasons = distinctReasons,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] FairSlingshotGameParameters(
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
            Parameter("fair_slingshot_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("festival_id", ReadString(projection, "festival_id")),
            Parameter("money_before", ReadInt(projection, "player_money").ToString()),
            Parameter("entry_fee_money", ReadInt(projection, "entry_fee_money").ToString()),
            Parameter("festival_score_before", ReadInt(projection, "festival_score").ToString()),
            Parameter("stardrop_price_star_tokens", ReadInt(projection, "stardrop_price_star_tokens").ToString()),
            Parameter("projected_unclaimed_grange_tokens", ReadInt(projection, "projected_unclaimed_grange_tokens").ToString()),
            Parameter("remaining_star_token_demand", ReadInt(projection, "remaining_star_token_demand").ToString()),
            Parameter("prestart_duration_ms", ReadInt(projection, "prestart_duration_ms").ToString()),
            Parameter("game_duration_ms", ReadInt(projection, "game_duration_ms").ToString()),
            Parameter("post_game_delay_ms", ReadInt(projection, "post_game_delay_ms").ToString()),
            Parameter("results_duration_ms", ReadInt(projection, "results_duration_ms").ToString()),
            Parameter("target_count", projection.TryGetProperty("target_sequence", out var targets) && targets.ValueKind == JsonValueKind.Array
                ? targets.GetArrayLength().ToString()
                : "0"),
            Parameter("dialogue_key", ReadString(projection, "dialogue_key")),
            Parameter("play_response_key", ReadString(projection, "play_response_key")),
            Parameter("execution_strategy", ReadString(projection, "execution_strategy")),
            Parameter("native_contract", ReadString(projection, "native_contract"))
        };
    }
}
