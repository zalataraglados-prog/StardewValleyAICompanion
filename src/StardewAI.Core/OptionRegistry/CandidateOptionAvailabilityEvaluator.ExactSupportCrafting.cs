using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private IEnumerable<ExactSupportCraftingCandidateBinding>
        ExactSupportCraftingCandidateBindings(
            SnapshotEnvelope snapshot,
            JsonElement recipeRow,
            string blockReasonPrefix)
    {
        yield return BindExactSupportCraftingCandidate(
            snapshot,
            recipeRow,
            null,
            blockReasonPrefix);
        if (!recipeRow.TryGetProperty(
                "workbench_crafting_sources",
                out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }
        foreach (var source in sources.EnumerateArray())
        {
            if (source.ValueKind != JsonValueKind.Object)
                continue;
            yield return BindExactSupportCraftingCandidate(
                snapshot,
                recipeRow,
                source,
                blockReasonPrefix);
        }
    }

    private ExactSupportCraftingCandidateBinding
        BindExactSupportCraftingCandidate(
            SnapshotEnvelope snapshot,
            JsonElement recipeRow,
            JsonElement? workbenchSource,
            string blockReasonPrefix)
    {
        var usesWorkbench = workbenchSource.HasValue;
        var source = workbenchSource ?? recipeRow;
        var locationId = usesWorkbench
            ? ReadString(source, "location_id")
            : ReadStateFieldString(snapshot, "player", "location_id");
        var targetX = usesWorkbench ? NullableReadInt(source, "tile_x") : null;
        var targetY = usesWorkbench ? NullableReadInt(source, "tile_y") : null;
        var sameLocation = string.Equals(
            locationId,
            ReadStateFieldString(snapshot, "player", "location_id"),
            StringComparison.Ordinal);
        var stand = usesWorkbench && sameLocation &&
            targetX.HasValue && targetY.HasValue
                ? FindBestStandTile(snapshot, targetX.Value, targetY.Value)
                : null;
        var ingredients = source.TryGetProperty(
            "ingredient_rows",
            out var ingredientRows)
            ? ingredientRows
            : default;
        var reservation = new MachineCraftingMaterialReservationGuard()
            .Evaluate(snapshot, ingredients, usesWorkbench, null);
        var expectedReady = usesWorkbench
            ? "ready_for_native_workbench_crafting_menu"
            : "ready_for_native_personal_crafting_menu";
        var reasons = new List<string>();
        if (!string.Equals(
                ReadString(source, "craft_candidate_status"),
                expectedReady,
                StringComparison.Ordinal))
        {
            reasons.Add(blockReasonPrefix + "_recipe_not_ready");
        }
        if (ReadBool(
                source,
                "output_inventory_acceptance_after_material_consumption") !=
            true)
        {
            reasons.Add(
                blockReasonPrefix +
                "_output_cannot_fit_after_material_consumption");
        }
        if (ActiveMenuOpenForCandidate(snapshot))
            reasons.Add(blockReasonPrefix + "_crafting_menu_must_be_clear");
        if (usesWorkbench && (!sameLocation || stand is null))
        {
            reasons.Add(sameLocation
                ? blockReasonPrefix + "_workbench_access_unavailable"
                : blockReasonPrefix +
                  "_workbench_requires_current_location_rebind");
        }
        if (!reservation.Ready)
            reasons.AddRange(reservation.BlockingReasons);

        var ingredientRowsJson = ingredients.ValueKind == JsonValueKind.Array
            ? ingredients.GetRawText()
            : "[]";
        var nodeIdsJson = usesWorkbench && source.TryGetProperty(
                "native_container_node_ids",
                out var nodeIds)
            ? nodeIds.GetRawText()
            : "[]";
        var outputCount = Math.Max(
            1,
            ReadInt(recipeRow, "output_count_per_craft", 1));
        var parameters = new[]
        {
            Parameter("recipe_name", ReadString(recipeRow, "recipe_name")),
            Parameter(
                "output_qualified_item_id",
                ReadString(recipeRow, "output_qualified_item_id")),
            Parameter(
                "output_item_id",
                ReadString(recipeRow, "output_item_id")),
            Parameter(
                "output_count",
                outputCount.ToString(CultureInfo.InvariantCulture)),
            Parameter(
                "times_crafted_before",
                Math.Max(0, ReadInt(recipeRow, "times_crafted"))
                    .ToString(CultureInfo.InvariantCulture)),
            Parameter("ingredient_rows_json", ingredientRowsJson),
            Parameter("crafting_source", usesWorkbench
                ? "native_workbench_crafting_menu"
                : "native_personal_crafting_menu"),
            Parameter("workbench_access_point_id", usesWorkbench
                ? ReadString(source, "workbench_access_point_id")
                : string.Empty),
            Parameter("workbench_container_node_ids_json", nodeIdsJson),
            Parameter("location_id", locationId),
            Parameter("target_tile_x", targetX?.ToString(
                CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", targetY?.ToString(
                CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand?.X.ToString(
                CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand?.Y.ToString(
                CultureInfo.InvariantCulture)),
            Parameter("commitment_ledger_id", reservation.LedgerId),
            Parameter(
                "commitment_ledger_revision",
                reservation.LedgerRevision.ToString(
                    CultureInfo.InvariantCulture)),
            Parameter("material_reservation_guard_status", reservation.Status),
            Parameter("material_reservation_ledger_id", reservation.LedgerId),
            Parameter(
                "material_reservation_ledger_revision",
                reservation.LedgerRevision.ToString(
                    CultureInfo.InvariantCulture)),
            Parameter(
                "material_reservation_ids_json",
                JsonSerializer.Serialize(reservation.ReservationIds))
        };
        return new ExactSupportCraftingCandidateBinding(
            recipeRow,
            source,
            usesWorkbench,
            locationId,
            outputCount,
            parameters,
            reasons.ToArray());
    }

    private sealed record ExactSupportCraftingCandidateBinding(
        JsonElement RecipeRow,
        JsonElement Source,
        bool UsesWorkbench,
        string LocationId,
        int OutputCount,
        SmallModelActionParameter[] Parameters,
        string[] BlockingReasons);
}
