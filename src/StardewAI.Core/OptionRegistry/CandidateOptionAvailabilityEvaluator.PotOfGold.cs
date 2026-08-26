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
    private EventCandidate[] PotOfGoldCandidates(SnapshotEnvelope snapshot)
    {
        var reward = ReadStateFieldValue(snapshot, "current_location", "pot_of_gold_reward");
        if (!reward.HasValue || reward.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var reasons = new List<string>();
        if (!string.Equals(ReadString(reward.Value, "status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("pot_of_gold_not_ready:" + ReadString(reward.Value, "status"));
        }
        var stand = SelectPotOfGoldStand(reward.Value, snapshot);
        if (stand is null)
        {
            reasons.Add("pot_of_gold_no_available_adjacent_stand");
        }
        var parameters = PotOfGoldCandidateParameters(reward.Value, stand);
        if (stand is not null)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "rewards.claim_pot_of_gold",
                Parameters = parameters
            }));
        }

        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
        var x = ReadInt(reward.Value, "target_tile_x");
        var y = ReadInt(reward.Value, "target_tile_y");
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "pot-of-gold:Forest:" + x + "," + y,
                Kind = "claim_pot_of_gold",
                Available = reasons.Count == 0,
                LocationId = "Forest",
                TileX = x,
                TileY = y,
                ItemId = "PotOfGold",
                QualifiedItemId = ReadString(reward.Value, "qualified_item_id"),
                Quantity = 1,
                ExpectedEffect = "current_location.pot_of_gold_reward.exact_object_present=false" +
                    ";reward_branch=" + ReadString(reward.Value, "reward_branch") +
                    ";expected_coin_quantity=" + ReadInt(reward.Value, "expected_coin_quantity") +
                    ";expected_hat_quantity=" + ReadInt(reward.Value, "expected_hat_quantity") +
                    ";fresh_snapshot_pickup_handoff=true",
                EstimatedTicks = Math.Max(90, distance * 60 + 90),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_spring_17_pot_of_gold",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private static CandidateTile? SelectPotOfGoldStand(JsonElement reward, SnapshotEnvelope snapshot)
    {
        if (!reward.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return stands.EnumerateArray()
            .Where(row => ReadBool(row, "available") == true)
            .Select(row => new CandidateTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y")))
            .OrderBy(row => Math.Abs(row.X - playerX) + Math.Abs(row.Y - playerY))
            .ThenBy(row => row.Y)
            .ThenBy(row => row.X)
            .FirstOrDefault();
    }

    private static SmallModelActionParameter[] PotOfGoldCandidateParameters(JsonElement reward, CandidateTile? stand)
    {
        if (stand is null)
        {
            return Array.Empty<SmallModelActionParameter>();
        }
        return new[]
        {
            Parameter("target_location", "Forest"),
            Parameter("target_tile_x", ReadInt(reward, "target_tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(reward, "target_tile_y").ToString()),
            Parameter("stand_tile_x", stand.X.ToString()),
            Parameter("stand_tile_y", stand.Y.ToString()),
            Parameter("target_runtime_type", ReadString(reward, "target_runtime_type")),
            Parameter("qualified_item_id", ReadString(reward, "qualified_item_id")),
            Parameter("quantity", ReadInt(reward, "expected_coin_quantity").ToString()),
            Parameter("expected_output_items_json", ReadString(reward, "expected_output_items_json")),
            Parameter("reward_branch", ReadString(reward, "reward_branch")),
            Parameter("interaction_kind", ReadString(reward, "interaction_kind")),
            Parameter("expected_action_type", ReadString(reward, "expected_action_type")),
            Parameter("native_contract", ReadString(reward, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }
}
