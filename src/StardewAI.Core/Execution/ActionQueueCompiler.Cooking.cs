using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileCookRecipeStep(SmallModelAction action)
    {
        var recipeName = ReadParameter(action, "recipe_name");
        var sourceId = ReadParameter(action, "cooking_source_id");
        return string.IsNullOrWhiteSpace(recipeName) || string.IsNullOrWhiteSpace(sourceId)
            ? Array.Empty<CompiledActionStep>()
            : new[]
            {
                Step(
                    "cook_recipe",
                    sourceId + ":" + recipeName,
                    "native_cooking_material_seasoning_output_recipe_count_quest_and_achievement_receipt_verified",
                    240)
            };
    }

    private static string[] ValidateCookRecipePlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.cook_recipe")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var recipeName = ReadParameter(action, "recipe_name");
        var sourceId = ReadParameter(action, "cooking_source_id");
        var reason = ReadParameter(action, "cooking_reason");
        var craftCount = CookingIntParameter(action, "craft_count");
        var locationId = ReadParameter(action, "location_id");
        var interactionX = CookingIntParameter(action, "interaction_tile_x");
        var interactionY = CookingIntParameter(action, "interaction_tile_y");
        var standX = CookingIntParameter(action, "stand_tile_x");
        var standY = CookingIntParameter(action, "stand_tile_y");
        if (string.IsNullOrWhiteSpace(recipeName) || string.IsNullOrWhiteSpace(sourceId) ||
            string.IsNullOrWhiteSpace(reason) || craftCount != 1 || string.IsNullOrWhiteSpace(locationId) ||
            !interactionX.HasValue || !interactionY.HasValue || !standX.HasValue || !standY.HasValue)
        {
            return new[] { "cook_recipe_typed_projection_required" };
        }
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("cook_recipe_location_drifted");
        }
        if (Math.Abs(standX.Value - interactionX.Value) + Math.Abs(standY.Value - interactionY.Value) != 1)
        {
            reasons.Add("cook_recipe_stand_tile_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("cook_recipe_menu_must_be_clear");
        }

        var row = CookingRow(snapshot, recipeName, sourceId);
        if (!row.HasValue)
        {
            reasons.Add("cook_recipe_not_verified_by_transparent_state");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        var expectedIngredients = row.Value.TryGetProperty("ingredient_rows", out var ingredients)
            ? ingredients.GetRawText()
            : "[]";
        var expectedSeasoning = row.Value.TryGetProperty("seasoning_rows", out var seasoning)
            ? seasoning.GetRawText()
            : "[]";
        var expectedContainers = row.Value.TryGetProperty("material_container_ids", out var containers)
            ? containers.GetRawText()
            : "[]";
        if (!string.Equals(ReadString(row.Value, "craft_candidate_status"), "ready_for_native_cooking_page", StringComparison.Ordinal) ||
            ReadBool(row.Value, "output_inventory_acceptance_after_material_consumption") != true)
        {
            reasons.Add("cook_recipe_not_ready");
        }
        if (!string.Equals(ReadParameter(action, "cooking_source_kind"), ReadString(row.Value, "cooking_source_kind"), StringComparison.Ordinal) ||
            !string.Equals(locationId, ReadString(row.Value, "location_id"), StringComparison.Ordinal) ||
            interactionX != ReadInt(row.Value, "interaction_tile_x") ||
            interactionY != ReadInt(row.Value, "interaction_tile_y") ||
            !string.Equals(ReadParameter(action, "output_item_id"), ReadString(row.Value, "output_item_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "output_qualified_item_id"), ReadString(row.Value, "output_qualified_item_id"), StringComparison.Ordinal) ||
            CookingIntParameter(action, "output_count") != Math.Max(1, ReadInt(row.Value, "output_count_per_craft", 1)) ||
            CookingIntParameter(action, "expected_output_quality") != ReadInt(row.Value, "output_quality") ||
            !string.Equals(ReadParameter(action, "expected_output_order_data"), ReadString(row.Value, "output_order_data"), StringComparison.Ordinal) ||
            CookingIntParameter(action, "recipes_cooked_before") != ReadInt(row.Value, "recipes_cooked_before") ||
            !string.Equals(ReadParameter(action, "ingredient_rows_json"), expectedIngredients, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "seasoning_rows_json"), expectedSeasoning, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "material_container_ids_json"), expectedContainers, StringComparison.Ordinal))
        {
            reasons.Add("cook_recipe_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? CookingRow(
        SnapshotEnvelope snapshot,
        string recipeName,
        string sourceId)
    {
        var context = ReadStateFieldValue(snapshot, "player", "cooking");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(context.Value, "projection_status"),
                "complete_learned_cooking_recipe_and_native_source_projection", StringComparison.Ordinal) ||
            !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "recipe_name"), recipeName, StringComparison.Ordinal) &&
                string.Equals(ReadString(row, "cooking_source_id"), sourceId, StringComparison.Ordinal))
            {
                return row.Clone();
            }
        }
        return null;
    }

    private static int? CookingIntParameter(SmallModelAction action, string name)
    {
        var value = ReadParameter(action, name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
