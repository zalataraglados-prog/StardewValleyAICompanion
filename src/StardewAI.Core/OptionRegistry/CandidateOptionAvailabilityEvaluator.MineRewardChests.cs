using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] MineRewardChestCandidates(SnapshotEnvelope snapshot)
        {
            var chests = ReadStateFieldValue(snapshot, "mining", "reward_chests");
            if (!chests.HasValue || chests.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return chests.Value.EnumerateArray().Select(chest =>
            {
                var x = ReadInt(chest, "tile_x");
                var y = ReadInt(chest, "tile_y");
                var stand = BestAdjacentStand(snapshot, x, y, playerX, playerY);
                var item = chest.TryGetProperty("item", out var itemValue) && itemValue.ValueKind == JsonValueKind.Object
                    ? itemValue
                    : default;
                var reasons = new List<string>();
                if (!string.Equals(ReadString(chest, "status"), "ready", StringComparison.Ordinal))
                {
                    reasons.Add("mine_reward_chest_not_ready:" + ReadString(chest, "status"));
                }
                if (stand is null)
                {
                    reasons.Add("mine_reward_chest_no_reachable_adjacent_stand");
                }
                if (ReadBool(chest, "contains_skull_key") == true)
                {
                    reasons.Add("mine_reward_chest_skull_key_uses_specialized_chain");
                }

                var parameters = stand is null ? Array.Empty<SmallModelActionParameter>() : new[]
                {
                    Parameter("target_location", locationId),
                    Parameter("target_tile_x", x.ToString()),
                    Parameter("target_tile_y", y.ToString()),
                    Parameter("stand_tile_x", stand.X.ToString()),
                    Parameter("stand_tile_y", stand.Y.ToString()),
                    Parameter("target_runtime_type", ReadString(chest, "runtime_type")),
                    Parameter("reward_branch", ReadString(chest, "reward_branch")),
                    Parameter("mine_level", ReadInt(chest, "mine_level").ToString()),
                    Parameter("qualified_item_id", ReadString(item, "qualified_item_id")),
                    Parameter("quantity", ReadInt(item, "quantity").ToString()),
                    Parameter("expected_output_quality", ReadInt(item, "quality").ToString()),
                    Parameter("expected_output_items_json", ReadString(chest, "expected_output_items_json")),
                    Parameter("expected_skill_id", "luck"),
                    Parameter("expected_skill_experience_delta", ReadInt(chest, "expected_luck_experience_delta").ToString()),
                    Parameter("native_gain_experience_call_amount", ReadInt(chest, "native_gain_experience_call_amount").ToString()),
                    Parameter("expected_action_type", "MineRewardChest"),
                    Parameter("interaction_kind", "overlay_object"),
                    Parameter("is_stardrop", (ReadBool(chest, "is_stardrop") == true).ToString().ToLowerInvariant()),
                    Parameter("expected_stardrop_max_stamina_delta", ReadInt(chest, "expected_stardrop_max_stamina_delta").ToString()),
                    Parameter("native_contract", ReadString(chest, "native_contract")),
                    Parameter("max_movement_tiles", "512")
                };
                if (parameters.Length > 0)
                {
                    reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                    {
                        OptionId = "executor.claim_mine_reward_chest",
                        Parameters = parameters
                    }));
                }

                var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                return new EventCandidate
                {
                    CandidateId = "mine-reward-chest:" + locationId + ":" + x + "," + y,
                    Kind = "claim_mine_reward_chest",
                    Available = reasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ItemId = ReadString(item, "item_id"),
                    QualifiedItemId = ReadString(item, "qualified_item_id"),
                    Quantity = Math.Max(1, ReadInt(item, "quantity")),
                    ExpectedEffect = MineRewardChestExpectedEffect(chest, stand),
                    EstimatedTicks = Math.Max(90, distance * 60 + (ReadBool(chest, "is_stardrop") == true ? 600 : 120)),
                    EnergyCost = 0,
                    AvailabilityClass = "transparent_native_mineshaft_reward_chest",
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            }).OrderBy(candidate => candidate.EstimatedTicks).ThenBy(candidate => candidate.TileY).ThenBy(candidate => candidate.TileX).ToArray();
        }

        private static CandidateTile? BestAdjacentStand(SnapshotEnvelope snapshot, int x, int y, int playerX, int playerY)
        {
            return new[] { new CandidateTile(x, y - 1), new CandidateTile(x, y + 1), new CandidateTile(x - 1, y), new CandidateTile(x + 1, y) }
                .Where(tile => !CollisionGridBlocksTile(snapshot, tile.X, tile.Y))
                .OrderBy(tile => Math.Abs(playerX - tile.X) + Math.Abs(playerY - tile.Y))
                .FirstOrDefault();
        }

        private static string MineRewardChestExpectedEffect(JsonElement chest, CandidateTile? stand)
        {
            var x = ReadInt(chest, "tile_x");
            var y = ReadInt(chest, "tile_y");
            return (stand is null ? string.Empty : "stand_tile=" + stand.X + "," + stand.Y + ";") +
                "mining.reward_chests[" + x + "," + y + "].removed=true" +
                ";reward_branch=" + ReadString(chest, "reward_branch") +
                ";qualified_item_id=" + ReadString(chest.GetProperty("item"), "qualified_item_id") +
                ";native_gain_experience_call_amount=" + ReadInt(chest, "native_gain_experience_call_amount") +
                ";expected_luck_experience_delta=" + ReadInt(chest, "expected_luck_experience_delta") +
                ";expected_stardrop_max_stamina_delta=" + ReadInt(chest, "expected_stardrop_max_stamina_delta");
        }
    }
}
