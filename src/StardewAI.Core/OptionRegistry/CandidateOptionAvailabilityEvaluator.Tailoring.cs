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
    private EventCandidate[] TailoringCandidates(SnapshotEnvelope snapshot, SmallModelActionParameter[] intent)
    {
        var context = ReadStateFieldValue(snapshot, "player", "tailoring");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            ReadString(context.Value, "projection_status") != "complete_live_native_tailoring_recipe_input_endpoint_and_output_domain_projection" ||
            !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var requestedCandidate = TailoringIntent(intent, "tailoring_candidate_id");
        var requestedPurpose = TailoringIntent(intent, "tailoring_purpose");
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var result = new List<EventCandidate>();
        foreach (var row in rows.EnumerateArray()
                     .Where(value => value.ValueKind == JsonValueKind.Object)
                     .Where(value => string.IsNullOrWhiteSpace(requestedCandidate) ||
                         ReadString(value, "tailoring_candidate_id") == requestedCandidate)
                     .Where(value => string.IsNullOrWhiteSpace(requestedPurpose) ||
                         ReadString(value, "tailoring_purpose") == requestedPurpose))
        {
            var locationId = ReadString(row, "location_id");
            if (!string.Equals(currentLocation, locationId, StringComparison.OrdinalIgnoreCase))
            {
                if (ReadBool(row, "source_ready") != true)
                    continue;
                var plan = FindResolvedRoutePlan(snapshot, currentLocation, locationId,
                    RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
                if (plan?.FirstActionCandidate is not null)
                {
                    result.Add(CloneCandidate(
                        plan.FirstActionCandidate,
                        candidateId: "tailoring-route:" + ReadString(row, "tailoring_candidate_id") + ":" + currentLocation,
                        expectedEffect: plan.FirstActionCandidate.ExpectedEffect + ";tailoring_target=" + ReadString(row, "tailoring_operation"),
                        parameters: plan.FirstActionCandidate.Parameters.Concat(new[]
                        {
                            Parameter("continuation.option_id", "tailoring.sew_item"),
                            Parameter("continuation.tailoring_candidate_id", ReadString(row, "tailoring_candidate_id")),
                            Parameter("continuation.tailoring_purpose", ReadString(row, "tailoring_purpose"))
                        }).ToArray(),
                        availabilityClass: "tailoring_rolling_route"));
                }
                continue;
            }

            var x = ReadInt(row, "interaction_tile_x");
            var y = ReadInt(row, "interaction_tile_y");
            var stand = FindBestStandTile(snapshot, x, y);
            if (stand is null)
                continue;
            var parameters = TailoringExecutionParameters(row, stand.X, stand.Y);
            var reasons = new List<string>();
            if (ReadString(row, "tailoring_candidate_status") != "ready_for_native_tailoring_menu")
                reasons.Add(ReadString(row, "tailoring_candidate_status"));
            if (ActiveMenuOpenForCandidate(snapshot))
                reasons.Add("tailoring_menu_must_be_clear");
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.tailor_item",
                Parameters = parameters
            }));
            result.Add(new EventCandidate
            {
                CandidateId = "tailoring:" + ReadString(row, "tailoring_candidate_id"),
                Kind = "tailor_item",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                DisplayName = ReadString(row, "left_display_name") + " + " + ReadString(row, "right_display_name"),
                ItemId = ReadString(row, "left_qualified_item_id"),
                QualifiedItemId = ReadString(row, "left_qualified_item_id"),
                Quantity = 1,
                ExpectedEffect = "native_tailoring_completed=true;tailoring_operation=" + ReadString(row, "tailoring_operation") +
                    ";tailoring_purpose=" + ReadString(row, "tailoring_purpose"),
                EstimatedTicks = Math.Max(180, (Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - stand.X) +
                    Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - stand.Y)) * 60 + 180),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_tailoring_menu",
                BlockReasons = reasons.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            });
        }
        return result.OrderBy(value => value.CandidateId, StringComparer.Ordinal).ToArray();
    }

    private static SmallModelActionParameter[] TailoringExecutionParameters(JsonElement row, int standX, int standY)
    {
        string S(string name) => ReadString(row, name);
        string I(string name) => ReadInt(row, name).ToString(CultureInfo.InvariantCulture);
        return new[]
        {
            Parameter("tailoring_candidate_id", S("tailoring_candidate_id")),
            Parameter("tailoring_operation", S("tailoring_operation")),
            Parameter("tailoring_purpose", S("tailoring_purpose")),
            Parameter("tailoring_recipe_id", S("recipe_id")),
            Parameter("tailoring_source_id", S("source_id")),
            Parameter("tailoring_source_kind", S("source_kind")),
            Parameter("location_id", S("location_id")),
            Parameter("interaction_tile_x", I("interaction_tile_x")),
            Parameter("interaction_tile_y", I("interaction_tile_y")),
            Parameter("stand_tile_x", standX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", standY.ToString(CultureInfo.InvariantCulture)),
            Parameter("left_source_id", S("left_source_id")),
            Parameter("left_state_json", S("left_state_json")),
            Parameter("right_source_id", S("right_source_id")),
            Parameter("right_state_json", S("right_state_json")),
            Parameter("tailoring_spend_left_count", I("spend_left_count")),
            Parameter("tailoring_spend_right_count", I("spend_right_count")),
            Parameter("tailoring_output_contract_kind", S("output_contract_kind")),
            Parameter("expected_output_state_json", S("expected_output_state_json")),
            Parameter("random_outcome_contract_json", S("random_outcome_contract_json")),
            Parameter("tailoring_tailored_counts_before_json", S("tailored_counts_before_json")),
            Parameter("tailoring_marks_tailored_item", ReadBool(row, "marks_tailored_item") == true ? "true" : "false"),
            Parameter("tailoring_native_contract", S("native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string TailoringIntent(SmallModelActionParameter[] values, string name)
    {
        var value = IntentParameter(values, name);
        return string.IsNullOrWhiteSpace(value) ? IntentParameter(values, "continuation." + name) : value;
    }
}
