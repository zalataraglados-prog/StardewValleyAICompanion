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
    private EventCandidate[] StarterGiftCandidates(SnapshotEnvelope snapshot)
    {
        var gift = ReadStateFieldValue(snapshot, "current_location", "starter_gift_claim");
        if (!gift.HasValue || gift.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var reasons = new List<string>();
        var status = ReadString(gift.Value, "status");
        if (!string.Equals(status, "ready", StringComparison.Ordinal))
        {
            reasons.Add("starter_gift_not_ready:" + status);
        }
        var stand = SelectStarterGiftStand(gift.Value, snapshot);
        if (stand is null)
        {
            reasons.Add("starter_gift_no_available_adjacent_stand");
        }
        var parameters = StarterGiftCandidateParameters(gift.Value, stand);
        if (stand is not null)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "rewards.claim_starter_gift",
                Parameters = parameters
            }));
        }

        var x = ReadInt(gift.Value, "target_tile_x");
        var y = ReadInt(gift.Value, "target_tile_y");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "starter-gift:" + ReadString(gift.Value, "location_id") + ":" + x + "," + y,
                Kind = "claim_starter_gift",
                Available = reasons.Count == 0,
                LocationId = ReadString(gift.Value, "location_id"),
                TileX = x,
                TileY = y,
                QualifiedItemId = ReadString(gift.Value, "qualified_item_id"),
                Quantity = Math.Max(1, ReadInt(gift.Value, "content_slot_count")),
                ExpectedEffect = "current_location.starter_gift_claim.exact_starter_gift_present=false" +
                    ";quest_id=" + ReadString(gift.Value, "expected_quest_id") +
                    ";quest_present_after=true" +
                    ";starter_gift_outputs_added_to_inventory=true" +
                    ";fresh_snapshot_replan_required=true",
                EstimatedTicks = Math.Max(90, distance * 60 + 180),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_farmhouse_starter_gift",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private static CandidateTile? SelectStarterGiftStand(JsonElement gift, SnapshotEnvelope snapshot)
    {
        if (!gift.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
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

    private static SmallModelActionParameter[] StarterGiftCandidateParameters(JsonElement gift, CandidateTile? stand)
    {
        if (stand is null)
        {
            return Array.Empty<SmallModelActionParameter>();
        }
        return new[]
        {
            Parameter("target_location", ReadString(gift, "location_id")),
            Parameter("target_tile_x", ReadInt(gift, "target_tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(gift, "target_tile_y").ToString()),
            Parameter("stand_tile_x", stand.X.ToString()),
            Parameter("stand_tile_y", stand.Y.ToString()),
            Parameter("target_runtime_type", ReadString(gift, "target_runtime_type")),
            Parameter("qualified_item_id", ReadString(gift, "qualified_item_id")),
            Parameter("quantity", Math.Max(1, ReadInt(gift, "content_slot_count")).ToString()),
            Parameter("expected_output_items_json", ReadString(gift, "expected_output_items_json")),
            Parameter("quest_id", ReadString(gift, "expected_quest_id")),
            Parameter("reward_branch", "starter_gift_farm_type_" + ReadInt(gift, "farm_type")),
            Parameter("interaction_kind", ReadString(gift, "interaction_kind")),
            Parameter("expected_action_type", ReadString(gift, "expected_action_type")),
            Parameter("native_contract", ReadString(gift, "native_contract")),
            Parameter("projection_fingerprint", ReadString(gift, "projection_fingerprint")),
            Parameter("max_movement_tiles", "64")
        };
    }
}
