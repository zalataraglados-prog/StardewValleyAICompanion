using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private static EventCandidate[] StorageCraftingCandidates(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger)
    {
        var context = ReadStateFieldValue(
            snapshot,
            "player",
            "storage_crafting");
        if (!context.HasValue ||
            context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var demand =
            StorageExpansionDemandProjection.Evaluate(snapshot);
        return rows.EnumerateArray()
            .Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadBool(row, "ordinary_material_storage") == true)
            .SelectMany(row =>
                BuildStorageCraftingCandidates(
                    snapshot,
                    row,
                    demand,
                    commitmentLedger))
            .OrderBy(candidate =>
                StorageRecipePreference(candidate.QualifiedItemId))
            .ThenBy(candidate => candidate.CandidateId,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<EventCandidate>
        BuildStorageCraftingCandidates(
            SnapshotEnvelope snapshot,
            JsonElement row,
            StorageExpansionDemandResult demand,
            StrategyCommitmentLedger? commitmentLedger)
    {
        yield return BuildStorageCraftingCandidate(
            snapshot,
            row,
            null,
            demand,
            commitmentLedger);
        if (!row.TryGetProperty(
                "workbench_crafting_sources",
                out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var source in sources.EnumerateArray())
        {
            if (source.ValueKind == JsonValueKind.Object)
            {
                yield return BuildStorageCraftingCandidate(
                    snapshot,
                    row,
                    source,
                    demand,
                    commitmentLedger);
            }
        }
    }

    private static EventCandidate BuildStorageCraftingCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        JsonElement? workbenchSource,
        StorageExpansionDemandResult demand,
        StrategyCommitmentLedger? commitmentLedger)
    {
        var usesWorkbench = workbenchSource.HasValue;
        var source = workbenchSource ?? row;
        var recipeName = ReadString(row, "recipe_name");
        var qualifiedId = ReadString(
            row,
            "output_qualified_item_id");
        var itemId = ReadString(row, "output_item_id");
        var outputCount = Math.Max(
            1,
            ReadInt(row, "output_count_per_craft", 1));
        var timesCrafted = Math.Max(
            0,
            ReadInt(row, "times_crafted"));
        var ingredientRows = source.TryGetProperty(
            "ingredient_rows",
            out var ingredients)
                ? ingredients
                : default;
        var ingredientRowsJson =
            ingredientRows.ValueKind == JsonValueKind.Array
                ? ingredientRows.GetRawText()
                : "[]";
        var craftingSource = usesWorkbench
            ? "native_workbench_crafting_menu"
            : "native_personal_crafting_menu";
        var expectedReady = usesWorkbench
            ? "ready_for_native_workbench_crafting_menu"
            : "ready_for_native_personal_crafting_menu";
        var candidateStatus = ReadString(
            source,
            "craft_candidate_status");
        var accessPointId = usesWorkbench
            ? ReadString(source, "workbench_access_point_id")
            : string.Empty;
        var locationId = usesWorkbench
            ? ReadString(source, "location_id")
            : ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
        var targetX = usesWorkbench
            ? NullableReadInt(source, "tile_x")
            : null;
        var targetY = usesWorkbench
            ? NullableReadInt(source, "tile_y")
            : null;
        var sameLocation = string.Equals(
            locationId,
            ReadStateFieldString(
                snapshot,
                "player",
                "location_id"),
            StringComparison.Ordinal);
        var stand = usesWorkbench && sameLocation &&
            targetX.HasValue && targetY.HasValue
                ? FindBestStandTile(
                    snapshot,
                    targetX.Value,
                    targetY.Value)
                : null;
        var nodeIdsJson = usesWorkbench &&
            source.TryGetProperty(
                "native_container_node_ids",
                out var nodeIds)
                    ? nodeIds.GetRawText()
                    : "[]";
        var reservation =
            new MachineCraftingMaterialReservationGuard()
                .Evaluate(
                    snapshot,
                    ingredientRows,
                    usesWorkbench,
                    commitmentLedger);
        var reasons = new List<string>();
        if (!string.Equals(
                demand.Status,
                "available",
                StringComparison.Ordinal))
        {
            reasons.AddRange(demand.BlockingReasons);
        }
        else if (!demand.AcquisitionRequired)
        {
            reasons.Add(
                demand.PlacementRequired
                    ? "storage_item_already_in_inventory_requires_placement"
                    : "ordinary_storage_capacity_already_available");
        }
        if (!string.Equals(
                candidateStatus,
                expectedReady,
                StringComparison.Ordinal))
        {
            reasons.Add("storage_recipe_not_ready");
        }
        if (ReadBool(
                source,
                "output_inventory_acceptance_after_material_consumption") !=
            true)
        {
            reasons.Add(
                "storage_recipe_output_cannot_fit_after_material_consumption");
        }
        if (ActiveMenuOpenForCandidate(snapshot))
        {
            reasons.Add("storage_crafting_menu_must_be_clear");
        }
        if (string.IsNullOrWhiteSpace(recipeName) ||
            string.IsNullOrWhiteSpace(qualifiedId))
        {
            reasons.Add("storage_recipe_identity_unavailable");
        }
        if (usesWorkbench &&
            (string.IsNullOrWhiteSpace(accessPointId) ||
             stand is null))
        {
            reasons.Add(
                sameLocation
                    ? "storage_workbench_access_unavailable"
                    : "storage_workbench_requires_current_location_rebind");
        }
        if (!reservation.Ready)
        {
            reasons.AddRange(reservation.BlockingReasons);
        }

        return new EventCandidate
        {
            CandidateId = "storage-craft:" + recipeName + ":" +
                qualifiedId + (usesWorkbench
                    ? ":workbench:" + accessPointId
                    : string.Empty),
            Kind = "craft_storage_item",
            Available = reasons.Count == 0,
            LocationId = locationId,
            ItemId = itemId,
            QualifiedItemId = qualifiedId,
            Quantity = outputCount,
            EstimatedTicks = 30,
            EnergyCost = 0,
            AvailabilityClass = usesWorkbench
                ? "transparent_storage_recipe_native_workbench_crafting"
                : "transparent_storage_recipe_native_personal_crafting",
            ExpectedEffect =
                "player.inventory.materials_consumed_by_native_recipe=true" +
                ";recipe_name=" + recipeName +
                ";output_qualified_item_id=" + qualifiedId +
                ";output_count=" + outputCount +
                ";storage_demand_class=" + demand.DemandClass +
                ";crafting_source=" + craftingSource,
            BlockReasons = reasons
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Parameters = new[]
            {
                Parameter("recipe_name", recipeName),
                Parameter("output_qualified_item_id", qualifiedId),
                Parameter("output_item_id", itemId),
                Parameter("output_count", outputCount.ToString()),
                Parameter("times_crafted_before", timesCrafted.ToString()),
                Parameter("ingredient_rows_json", ingredientRowsJson),
                Parameter("crafting_source", craftingSource),
                Parameter("workbench_access_point_id", accessPointId),
                Parameter("workbench_container_node_ids_json", nodeIdsJson),
                Parameter("location_id", locationId),
                Parameter("target_tile_x", targetX?.ToString()),
                Parameter("target_tile_y", targetY?.ToString()),
                Parameter("stand_tile_x", stand?.X.ToString()),
                Parameter("stand_tile_y", stand?.Y.ToString()),
                Parameter("native_storage_branch",
                    ReadString(row, "native_storage_branch")),
                Parameter("special_chest_type",
                    ReadString(row, "special_chest_type")),
                Parameter("actual_capacity",
                    ReadInt(row, "actual_capacity").ToString()),
                Parameter("storage_role", "ordinary_material"),
                Parameter("storage_demand_class", demand.DemandClass),
                Parameter("inventory_ordinary_storage_count",
                    demand.InventoryOrdinaryStorageCount.ToString()),
                Parameter("usable_ordinary_storage_count",
                    demand.ImmediatelyUsableOrdinaryAccessPointCount.ToString()),
                Parameter("usable_ordinary_free_stack_slots",
                    demand.ImmediatelyUsableOrdinaryFreeStackSlotCount.ToString()),
                Parameter("commitment_ledger_id",
                    reservation.LedgerId),
                Parameter("commitment_ledger_revision",
                    reservation.LedgerRevision.ToString()),
                Parameter("material_reservation_guard_status",
                    reservation.Status),
                Parameter("material_reservation_ledger_id",
                    reservation.LedgerId),
                Parameter("material_reservation_ledger_revision",
                    reservation.LedgerRevision.ToString()),
                Parameter("material_reservation_ids_json",
                    JsonSerializer.Serialize(reservation.ReservationIds))
            }
        };
    }

    private static int StorageRecipePreference(string? qualifiedId) =>
        qualifiedId switch
        {
            "(BC)130" => 0,
            "(BC)232" => 1,
            "(BC)BigChest" => 2,
            "(BC)BigStoneChest" => 3,
            _ => 10
        };
}
