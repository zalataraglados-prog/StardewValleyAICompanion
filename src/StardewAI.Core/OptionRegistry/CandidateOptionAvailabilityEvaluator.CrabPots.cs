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
    private EventCandidate[] CrabPotCollectCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return objects.Value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object &&
                !string.Equals(ReadString(item, "crab_pot_collect_status"), "not_applicable", StringComparison.Ordinal))
            .Select(item =>
            {
                var x = ReadInt(item, "tile_x");
                var y = ReadInt(item, "tile_y");
                var stand = FindBestStandTile(snapshot, x, y);
                var status = ReadString(item, "crab_pot_collect_status");
                var outputQualifiedItemId = ReadString(item, "crab_pot_output_qualified_item_id");
                var outputRuntimeType = ReadString(item, "crab_pot_output_runtime_type");
                var outputHash = ReadString(item, "crab_pot_output_unit_state_sha256");
                var outputStack = ReadInt(item, "crab_pot_output_stack_on_collect");
                var outputItemsJson = ReadString(item, "crab_pot_expected_output_items_json");
                var blockReasons = new List<string>();
                if (!string.Equals(status, "ready", StringComparison.Ordinal))
                {
                    blockReasons.Add(string.IsNullOrWhiteSpace(status) ? "crab_pot_projection_unavailable" : status);
                }
                if (string.IsNullOrWhiteSpace(outputQualifiedItemId) || string.IsNullOrWhiteSpace(outputRuntimeType) ||
                    outputHash.Length != 64 || outputStack <= 0)
                {
                    blockReasons.Add("crab_pot_output_identity_incomplete");
                }
                if (stand is null)
                {
                    blockReasons.Add("crab_pot_no_adjacent_stand_tile");
                }

                var typedParameters = stand is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : CrabPotParameters(item, x, y, stand.X, stand.Y, outputQualifiedItemId, outputItemsJson);
                if (stand is not null)
                {
                    blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                    {
                        OptionId = "executor.collect_crab_pot",
                        Parameters = typedParameters
                    }));
                }

                var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                return new EventCandidate
                {
                    CandidateId = "collect-crab-pot:" + locationId + ":" + x + "," + y + ":" + outputQualifiedItemId,
                    Kind = "collect_crab_pot",
                    Available = blockReasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ItemId = ReadString(item, "item_id"),
                    QualifiedItemId = outputQualifiedItemId,
                    Quantity = outputStack,
                    ExpectedEffect = CrabPotExpectedEffect(item, stand, outputQualifiedItemId, outputItemsJson),
                    EstimatedTicks = Math.Max(30, distance * 60 + 30),
                    EnergyCost = 0,
                    AvailabilityClass = "transparent_crab_pot_native_collect",
                    BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = typedParameters
                };
            })
            .ToArray();
    }

    private static SmallModelActionParameter[] CrabPotParameters(
        JsonElement item,
        int x,
        int y,
        int standX,
        int standY,
        string outputQualifiedItemId,
        string outputItemsJson)
    {
        return new[]
        {
            Parameter("target_tile_x", x.ToString()),
            Parameter("target_tile_y", y.ToString()),
            Parameter("stand_tile_x", standX.ToString()),
            Parameter("stand_tile_y", standY.ToString()),
            Parameter("target_runtime_type", ReadString(item, "type")),
            Parameter("qualified_item_id", outputQualifiedItemId),
            Parameter("quantity", ReadInt(item, "crab_pot_output_stack_on_collect").ToString()),
            Parameter("expected_output_items_json", outputItemsJson),
            Parameter("expected_output_state_context", ReadString(item, "crab_pot_output_state_context")),
            Parameter("book_double_roll_succeeded", BoolInt(item, "crab_pot_book_double_roll_succeeded")),
            Parameter("book_crabbing_owned", BoolInt(item, "crab_pot_book_crabbing_owned")),
            Parameter("book_double_applied", BoolInt(item, "crab_pot_book_double_applied")),
            Parameter("expected_skill_id", "fishing"),
            Parameter("expected_skill_experience_delta", ReadInt(item, "crab_pot_fishing_experience_on_success_min").ToString()),
            Parameter("expected_container_bait_qualified_item_id", ReadString(item, "crab_pot_bait_qualified_item_id")),
            Parameter("expected_fish_collection_eligible", BoolInt(item, "crab_pot_fish_collection_eligible")),
            Parameter("expected_fish_caught_count_before", ReadInt(item, "crab_pot_fish_caught_count_before").ToString()),
            Parameter("expected_fish_caught_count_after", ReadInt(item, "crab_pot_fish_caught_count_after").ToString()),
            Parameter("expected_fish_caught_max_size_before", ReadInt(item, "crab_pot_fish_caught_max_size_before").ToString()),
            Parameter("expected_catch_size_min", ReadInt(item, "crab_pot_catch_size_min").ToString()),
            Parameter("expected_catch_size_max", ReadInt(item, "crab_pot_catch_size_max").ToString()),
            Parameter("catch_size_projection_status", ReadString(item, "crab_pot_catch_size_projection_status")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string CrabPotExpectedEffect(JsonElement item, CandidateTile? stand, string outputQualifiedItemId, string outputItemsJson)
    {
        return (stand is not null ? "crab_pot_stand_tile=" + stand.X + "," + stand.Y + ";" : string.Empty) +
            "crab_pot_ready_for_harvest=false" +
            ";crab_pot_bait_qualified_item_id=" + ReadString(item, "crab_pot_bait_qualified_item_id") +
            ";qualified_item_id=" + outputQualifiedItemId +
            ";quantity=" + ReadInt(item, "crab_pot_output_stack_on_collect") +
            ";expected_output_items_json=" + outputItemsJson +
            ";expected_output_state_context=" + ReadString(item, "crab_pot_output_state_context") +
            ";book_double_roll_succeeded=" + BoolInt(item, "crab_pot_book_double_roll_succeeded") +
            ";book_crabbing_owned=" + BoolInt(item, "crab_pot_book_crabbing_owned") +
            ";book_double_applied=" + BoolInt(item, "crab_pot_book_double_applied") +
            ";expected_skill_id=fishing" +
            ";expected_skill_experience_delta=" + ReadInt(item, "crab_pot_fishing_experience_on_success_min") +
            ";expected_fish_collection_eligible=" + BoolInt(item, "crab_pot_fish_collection_eligible") +
            ";expected_fish_caught_count_before=" + ReadInt(item, "crab_pot_fish_caught_count_before") +
            ";expected_fish_caught_count_after=" + ReadInt(item, "crab_pot_fish_caught_count_after") +
            ";expected_fish_caught_max_size_before=" + ReadInt(item, "crab_pot_fish_caught_max_size_before") +
            ";expected_catch_size_min=" + ReadInt(item, "crab_pot_catch_size_min") +
            ";expected_catch_size_max=" + ReadInt(item, "crab_pot_catch_size_max") +
            ";catch_size_projection_status=" + ReadString(item, "crab_pot_catch_size_projection_status") +
            ";max_movement_tiles=512";
    }

    private static string BoolInt(JsonElement item, string property)
    {
        return ReadBool(item, property) == true ? "1" : "0";
    }
}
