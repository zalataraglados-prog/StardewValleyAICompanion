using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] CrabPotLifecycleCandidates(SnapshotEnvelope snapshot)
    {
        return CrabPotReadyCollectCandidates(snapshot)
            .Concat(CrabPotBaitCandidates(snapshot))
            .Concat(CrabPotRemoteServiceCandidates(snapshot))
            .Concat(CrabPotPlacementCandidates(snapshot))
            .Concat(CrabPotRemotePlacementRouteCandidates(snapshot))
            .ToArray();
    }

    private EventCandidate[] CrabPotReadyCollectCandidates(SnapshotEnvelope snapshot)
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

    private EventCandidate[] CrabPotBaitCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return objects.Value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(item, "crab_pot_bait_load_status"),
                    "ready",
                    StringComparison.Ordinal))
            .Select(item =>
            {
                var x = ReadInt(item, "tile_x");
                var y = ReadInt(item, "tile_y");
                var stand = FindBestStandTile(snapshot, x, y);
                var baitRows = item.TryGetProperty(
                        "crab_pot_bait_load_inventory_rows",
                        out var rows) && rows.ValueKind == JsonValueKind.Array
                    ? rows.EnumerateArray()
                        .Where(row => row.ValueKind == JsonValueKind.Object &&
                            ReadBool(row, "native_probe_accepts") == true)
                        .OrderBy(row => ReadInt(row, "inventory_slot_index"))
                        .ToArray()
                    : Array.Empty<JsonElement>();
                var possibleFish = ReadCrabPotStringArray(
                    item,
                    "crab_pot_possible_qualified_item_ids");
                var blockReasons = new List<string>();
                if (stand is null)
                    blockReasons.Add("crab_pot_no_adjacent_stand_tile");
                if (baitRows.Length == 0)
                    blockReasons.Add("crab_pot_no_native_accepted_bait");
                if (possibleFish.Length == 0 ||
                    string.IsNullOrWhiteSpace(
                        ReadString(item, "crab_pot_production_signature")))
                {
                    blockReasons.Add("crab_pot_production_domain_incomplete");
                }

                var bait = baitRows.FirstOrDefault();
                var parameters = stand is null || bait.ValueKind != JsonValueKind.Object
                    ? Array.Empty<SmallModelActionParameter>()
                    : new[]
                    {
                        Parameter("target_location", locationId),
                        Parameter("target_tile_x", x.ToString()),
                        Parameter("target_tile_y", y.ToString()),
                        Parameter("stand_tile_x", stand.X.ToString()),
                        Parameter("stand_tile_y", stand.Y.ToString()),
                        Parameter(
                            "inventory_slot_index",
                            ReadInt(bait, "inventory_slot_index").ToString()),
                        Parameter(
                            "expected_stack_before",
                            ReadInt(bait, "stack").ToString()),
                        Parameter(
                            "qualified_item_id",
                            ReadString(bait, "qualified_item_id")),
                        Parameter(
                            "bait_runtime_type",
                            ReadString(bait, "runtime_type")),
                        Parameter(
                            "bait_quality",
                            ReadInt(bait, "quality").ToString()),
                        Parameter(
                            "expected_container_bait_qualified_item_id",
                            ReadString(
                                bait,
                                "expected_container_bait_qualified_item_id")),
                        Parameter(
                            "expected_container_bait_unit_state_sha256",
                            ReadString(
                                bait,
                                "expected_container_bait_unit_state_sha256")),
                        Parameter(
                            "target_runtime_type",
                            ReadString(item, "type")),
                        Parameter(
                            "expected_container_owner_player_id_before",
                            ReadInt64(
                                item,
                                "crab_pot_owner_player_id_before_bait").ToString()),
                        Parameter(
                            "expected_container_owner_player_id_after",
                            ReadInt64(
                                item,
                                "crab_pot_expected_owner_player_id_after_bait").ToString()),
                        Parameter(
                            "native_contract",
                            ReadString(item, "crab_pot_bait_load_native_contract")),
                        Parameter(
                            "crab_pot_bait_reason",
                            "master_angler_trap_production_service"),
                        Parameter(
                            "crab_pot_production_signature",
                            ReadString(item, "crab_pot_production_signature")),
                        Parameter(
                            "possible_qualified_item_ids_json",
                            JsonSerializer.Serialize(possibleFish)),
                        Parameter("outcome_distribution_complete", "true")
                    };
                if (parameters.Length > 0)
                {
                    blockReasons.AddRange(CompilerProbeBlockingReasons(
                        snapshot,
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "executor.load_crab_pot_bait",
                            Parameters = parameters
                        }));
                }

                var distance = stand is null
                    ? 0
                    : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                return new EventCandidate
                {
                    CandidateId = "bait-crab-pot:" + locationId + ":" + x + "," + y,
                    Kind = "load_crab_pot_bait",
                    Available = blockReasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ExpectedEffect =
                        "crab_pot_bait_loaded=true;crab_pot_production_signature=" +
                        ReadString(item, "crab_pot_production_signature") +
                        ";possible_qualified_item_ids_json=" +
                        JsonSerializer.Serialize(possibleFish),
                    EstimatedTicks = Math.Max(45, distance * 60 + 45),
                    EnergyCost = 0,
                    AvailabilityClass =
                        "transparent_crab_pot_native_bait_service",
                    BlockReasons = blockReasons
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    Parameters = parameters
                };
            })
            .ToArray();
    }
}
