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
    private EventCandidate[] CookingCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var recipeName = CookingIntentParameter(intent, "recipe_name");
        var reason = CookingIntentParameter(intent, "cooking_reason");
        var sourceId = CookingIntentParameter(intent, "cooking_source_id");
        var craftCountText = CookingIntentParameter(intent, "craft_count");
        var craftCount = int.TryParse(craftCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 1;
        if (string.IsNullOrWhiteSpace(recipeName) || string.IsNullOrWhiteSpace(reason) || craftCount != 1)
        {
            return Array.Empty<EventCandidate>();
        }

        var context = ReadStateFieldValue(snapshot, "player", "cooking");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(context.Value, "projection_status"),
                "complete_learned_cooking_recipe_and_native_source_projection",
                StringComparison.Ordinal) ||
            !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var matches = rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "recipe_name"), recipeName, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(sourceId) ||
                 string.Equals(ReadString(row, "cooking_source_id"), sourceId, StringComparison.Ordinal)))
            .OrderByDescending(row => string.Equals(
                ReadString(row, "location_id"),
                ReadStateFieldString(snapshot, "player", "location_id"),
                StringComparison.OrdinalIgnoreCase))
            .ThenBy(row => ReadString(row, "cooking_source_id"), StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 0)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var result = new List<EventCandidate>();
        foreach (var row in matches)
        {
            var locationId = ReadString(row, "location_id");
            var selectedSourceId = ReadString(row, "cooking_source_id");
            if (!string.Equals(currentLocation, locationId, StringComparison.OrdinalIgnoreCase))
            {
                var plan = FindResolvedRoutePlan(
                    snapshot,
                    currentLocation,
                    locationId,
                    RouteConnectorCandidates(snapshot, int.MaxValue)
                        .Where(value => value.Kind == "route_connector_tile")
                        .ToArray());
                if (plan?.FirstActionCandidate is not null)
                {
                    result.Add(CloneCandidate(
                        plan.FirstActionCandidate,
                        candidateId: "cook-route:" + recipeName + ":" + selectedSourceId + ":" + currentLocation,
                        expectedEffect: plan.FirstActionCandidate.ExpectedEffect + ";cooking_target=" + recipeName,
                        parameters: plan.FirstActionCandidate.Parameters.Concat(new[]
                        {
                            Parameter("continuation.option_id", "crafting.cook_recipe"),
                            Parameter("continuation.recipe_name", recipeName),
                            Parameter("continuation.craft_count", "1"),
                            Parameter("continuation.cooking_reason", reason),
                            Parameter("continuation.cooking_source_id", selectedSourceId)
                        }).ToArray(),
                        availabilityClass: "cooking_rolling_route"));
                }
                continue;
            }

            var interactionX = ReadInt(row, "interaction_tile_x");
            var interactionY = ReadInt(row, "interaction_tile_y");
            var stand = FindBestStandTile(snapshot, interactionX, interactionY);
            if (stand is null)
            {
                continue;
            }
            var parameters = CookingExecutionParameters(row, recipeName, reason, stand.X, stand.Y);
            var reasons = new List<string>();
            if (!string.Equals(ReadString(row, "craft_candidate_status"), "ready_for_native_cooking_page", StringComparison.Ordinal))
            {
                reasons.Add("cooking_recipe_or_source_not_ready");
            }
            if (ActiveMenuOpenForCandidate(snapshot))
            {
                reasons.Add("cooking_menu_must_be_clear");
            }
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.cook_recipe",
                Parameters = parameters
            }));
            result.Add(new EventCandidate
            {
                CandidateId = "cook:" + recipeName + ":" + selectedSourceId,
                Kind = "cook_recipe",
                Available = reasons.Count == 0,
                LocationId = locationId,
                TileX = interactionX,
                TileY = interactionY,
                DisplayName = ReadString(row, "output_display_name"),
                ItemId = ReadString(row, "output_item_id"),
                QualifiedItemId = ReadString(row, "output_qualified_item_id"),
                Quantity = Math.Max(1, ReadInt(row, "output_count_per_craft", 1)),
                ExpectedEffect = "native_cooking_completed=true;recipe_name=" + recipeName +
                    ";craft_count=1;output_qualified_item_id=" + ReadString(row, "output_qualified_item_id") +
                    ";recipes_cooked_before=" + ReadInt(row, "recipes_cooked_before") +
                    ";recipes_cooked_after=" + (ReadInt(row, "recipes_cooked_before") + 1) +
                    ";cooking_reason=" + reason,
                EstimatedTicks = Math.Max(180,
                    (Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - stand.X) +
                     Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - stand.Y)) * 60 + 180),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_cooking_page",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            });
        }
        return result.OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray();
    }

    private static SmallModelActionParameter[] CookingExecutionParameters(
        JsonElement row,
        string recipeName,
        string reason,
        int standX,
        int standY) => new[]
    {
        Parameter("recipe_name", recipeName),
        Parameter("craft_count", "1"),
        Parameter("cooking_reason", reason),
        Parameter("cooking_source_id", ReadString(row, "cooking_source_id")),
        Parameter("cooking_source_kind", ReadString(row, "cooking_source_kind")),
        Parameter("location_id", ReadString(row, "location_id")),
        Parameter("interaction_tile_x", ReadInt(row, "interaction_tile_x").ToString(CultureInfo.InvariantCulture)),
        Parameter("interaction_tile_y", ReadInt(row, "interaction_tile_y").ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", standX.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", standY.ToString(CultureInfo.InvariantCulture)),
        Parameter("output_item_id", ReadString(row, "output_item_id")),
        Parameter("output_qualified_item_id", ReadString(row, "output_qualified_item_id")),
        Parameter("output_count", Math.Max(1, ReadInt(row, "output_count_per_craft", 1)).ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_output_quality", ReadInt(row, "output_quality").ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_output_order_data", ReadString(row, "output_order_data")),
        Parameter("recipes_cooked_before", ReadInt(row, "recipes_cooked_before").ToString(CultureInfo.InvariantCulture)),
        Parameter("ingredient_rows_json", ReadString(row, "ingredient_rows_json")),
        Parameter("seasoning_rows_json", ReadString(row, "seasoning_rows_json")),
        Parameter("material_container_ids_json", ReadString(row, "material_container_topology_json")),
        Parameter("max_movement_tiles", "512")
    };

    private static string CookingIntentParameter(SmallModelActionParameter[] parameters, string name)
    {
        var value = IntentParameter(parameters, name);
        return string.IsNullOrWhiteSpace(value)
            ? IntentParameter(parameters, "continuation." + name)
            : value;
    }
}
