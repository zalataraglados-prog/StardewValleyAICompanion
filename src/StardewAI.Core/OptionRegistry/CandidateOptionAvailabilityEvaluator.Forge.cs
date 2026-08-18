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
    private EventCandidate[] ForgeCandidates(SnapshotEnvelope snapshot, SmallModelActionParameter[] intent)
    {
        var operation = ForgeIntent(intent, "forge_operation");
        var reason = ForgeIntent(intent, "forge_reason");
        var leftSourceId = ForgeIntent(intent, "left_source_id");
        var rightSourceId = ForgeIntent(intent, "right_source_id");
        var sourceId = ForgeIntent(intent, "forge_source_id");
        if (string.IsNullOrWhiteSpace(operation) || string.IsNullOrWhiteSpace(reason) ||
            string.IsNullOrWhiteSpace(leftSourceId) ||
            (!operation.StartsWith("unforge_", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(rightSourceId)))
        {
            return Array.Empty<EventCandidate>();
        }

        var context = ReadStateFieldValue(snapshot, "player", "forge");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            ReadString(context.Value, "projection_status") != "complete_loaded_native_forge_source_and_live_input_projection" ||
            !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var matches = rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
                ReadString(row, "forge_operation") == operation && ReadString(row, "left_source_id") == leftSourceId &&
                ReadString(row, "right_source_id") == rightSourceId &&
                (string.IsNullOrWhiteSpace(sourceId) || ReadString(row, "forge_source_id") == sourceId))
            .OrderByDescending(row => string.Equals(ReadString(row, "location_id"),
                ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            .ThenBy(row => ReadString(row, "forge_source_id"), StringComparer.Ordinal).ToArray();
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var result = new List<EventCandidate>();
        foreach (var row in matches)
        {
            var locationId = ReadString(row, "location_id");
            if (!string.Equals(currentLocation, locationId, StringComparison.OrdinalIgnoreCase))
            {
                var plan = FindResolvedRoutePlan(snapshot, currentLocation, locationId,
                    RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
                if (plan?.FirstConnectorCandidate is not null)
                {
                    result.Add(CloneCandidate(plan.FirstConnectorCandidate,
                        candidateId: "forge-route:" + ReadString(row, "forge_candidate_id") + ":" + currentLocation,
                        expectedEffect: plan.FirstConnectorCandidate.ExpectedEffect + ";forge_target=" + operation,
                        parameters: plan.FirstConnectorCandidate.Parameters.Concat(new[]
                        {
                            Parameter("continuation.option_id", "crafting.forge_item"),
                            Parameter("continuation.forge_operation", operation),
                            Parameter("continuation.forge_reason", reason),
                            Parameter("continuation.left_source_id", leftSourceId),
                            Parameter("continuation.right_source_id", rightSourceId),
                            Parameter("continuation.forge_source_id", ReadString(row, "forge_source_id"))
                        }).ToArray(), availabilityClass: "forge_rolling_route"));
                }
                continue;
            }

            var x = ReadInt(row, "interaction_tile_x");
            var y = ReadInt(row, "interaction_tile_y");
            var stand = FindBestStandTile(snapshot, x, y);
            if (stand is null) continue;
            var parameters = ForgeExecutionParameters(row, reason, stand.X, stand.Y);
            var reasons = new List<string>();
            if (ReadString(row, "forge_candidate_status") != "ready_for_native_forge_menu") reasons.Add("forge_inputs_shards_or_output_capacity_not_ready");
            if (ActiveMenuOpenForCandidate(snapshot)) reasons.Add("forge_menu_must_be_clear");
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.forge_item", Parameters = parameters
            }));
            result.Add(new EventCandidate
            {
                CandidateId = "forge:" + ReadString(row, "forge_candidate_id"),
                Kind = "forge_item",
                Available = reasons.Count == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                DisplayName = operation + ": " + ReadString(row, "left_display_name"),
                ItemId = ReadString(row, "left_qualified_item_id"),
                QualifiedItemId = ReadString(row, "left_qualified_item_id"),
                Quantity = 1,
                ExpectedEffect = "native_forge_completed=true;forge_operation=" + operation + ";forge_reason=" + reason,
                EstimatedTicks = Math.Max(180, (Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - stand.X) +
                    Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - stand.Y)) * 60 + 180),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_forge_menu",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            });
        }
        return result.OrderBy(value => value.CandidateId, StringComparer.Ordinal).ToArray();
    }

    private static SmallModelActionParameter[] ForgeExecutionParameters(JsonElement row, string reason, int standX, int standY)
    {
        string S(string name) => ReadString(row, name);
        string I(string name) => ReadInt(row, name).ToString(CultureInfo.InvariantCulture);
        return new[]
        {
            Parameter("forge_candidate_id", S("forge_candidate_id")), Parameter("forge_operation", S("forge_operation")),
            Parameter("forge_reason", reason), Parameter("forge_source_id", S("forge_source_id")),
            Parameter("forge_source_kind", S("forge_source_kind")), Parameter("location_id", S("location_id")),
            Parameter("interaction_tile_x", I("interaction_tile_x")), Parameter("interaction_tile_y", I("interaction_tile_y")),
            Parameter("stand_tile_x", standX.ToString(CultureInfo.InvariantCulture)), Parameter("stand_tile_y", standY.ToString(CultureInfo.InvariantCulture)),
            Parameter("left_source_id", S("left_source_id")), Parameter("left_state_json", S("left_state_json")),
            Parameter("right_source_id", S("right_source_id")), Parameter("right_state_json", S("right_state_json")),
            Parameter("forge_shard_cost", I("shard_cost")), Parameter("forge_shard_refund", I("shard_refund")),
            Parameter("forge_shard_count_before", I("shard_count_before")),
            Parameter("times_enchanted_before", I("times_enchanted_before")), Parameter("times_enchanted_after", I("times_enchanted_after")),
            Parameter("forge_output_contract_kind", S("output_contract_kind")),
            Parameter("expected_output_state_json", S("expected_output_state_json")),
            Parameter("random_outcome_contract_json", S("random_outcome_contract_json")), Parameter("max_movement_tiles", "512")
        };
    }

    private static string ForgeIntent(SmallModelActionParameter[] values, string name)
    {
        var value = IntentParameter(values, name);
        return string.IsNullOrWhiteSpace(value) ? IntentParameter(values, "continuation." + name) : value;
    }
}
