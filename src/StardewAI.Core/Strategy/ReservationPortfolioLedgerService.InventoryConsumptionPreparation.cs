using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
{
    private static void ValidateInventoryConsumptionPreparation(StrategyCommitmentLedger? current,
        ReservationPortfolioCommitRequest request, ICollection<string> errors)
    {
        if (current?.PreparedInventoryConsumption is not null)
            errors.Add("inventory_consumption_preparation_already_pending");
        var intent = request.InventoryConsumptionPreparation;
        if (intent is null) return;
        if (intent.SchemaVersion != PreparedInventoryConsumptionContract.RequestSchema ||
            intent.ConsumerOptionId != PreparedInventoryConsumptionContract.Consumer ||
            string.IsNullOrWhiteSpace(intent.RouteSourceDecisionId) || string.IsNullOrWhiteSpace(intent.RecipeName) ||
            string.IsNullOrWhiteSpace(intent.CookingSourceId) || request.SourceDecisionId != request.PortfolioId)
            errors.Add("inventory_consumption_preparation_invalid");
        if (request.MaterialClaims is not { Length: 0 } || request.CurrencyClaims is not { Length: 0 } ||
            request.ReleaseReservationIds is not { Length: 0 } || request.MachineSupportIntent is not null ||
            request.MaterialConsumption is not null || request.MaterialTransferPreparation is not null || current?.PreparedMaterialTransfer is not null)
            errors.Add("inventory_consumption_preparation_mixed_mutation");
    }

    private static void PrepareInventoryConsumption(StrategyCommitmentLedger staged, SnapshotEnvelope snapshot,
        ReservationPortfolioCommitRequest request, ICollection<string> errors)
    {
        var intent = request.InventoryConsumptionPreparation;
        if (intent is null || errors.Count > 0) return;
        var recipe = CropSaleStockCookingPreflight.ReadRecipe(snapshot, intent.RecipeName, intent.CookingSourceId);
        if (recipe is not { } row || ReadString(row, "craft_candidate_status") != "ready_for_native_cooking_page" ||
            ReadBool(row, "output_inventory_acceptance_after_material_consumption") != true ||
            string.IsNullOrWhiteSpace(ReadString(row, "output_qualified_item_id")) || ReadInt(row, "output_count_per_craft") <= 0)
        { errors.Add("inventory_consumption_native_recipe_unavailable"); return; }
        var reasons = CropSaleStockCookingPreflight.Resolve(snapshot, staged, recipe, out var graph, out var consumed);
        foreach (var reason in reasons) errors.Add(reason);
        if (errors.Count > 0) return;
        var projection = new InventoryConsumptionProjector().Project(graph!, staged, consumed);
        foreach (var reason in projection.BlockingReasons) errors.Add(reason);
        if (errors.Count > 0 || projection.Status != "projected") return;
        var touched = projection.NativeSlotTransitions.Select(slot => slot.NodeId).ToHashSet(StringComparer.Ordinal);
        var nodes = graph!.InventoryNodes.Where(node => touched.Contains(node.NodeId)).ToArray();
        var access = graph.AccessPoints.Where(point => touched.Contains(point.NodeId)).ToArray();
        var retained = new PreparedInventoryConsumption
        {
            SchemaVersion = PreparedInventoryConsumptionContract.Schema, LedgerId = staged.LedgerId,
            SaveId = staged.SaveId, PlayerId = staged.PlayerId, PortfolioId = request.PortfolioId, GoalId = request.GoalId,
            RouteSourceDecisionId = intent.RouteSourceDecisionId, SourceStateHash = snapshot.StateHash, SourceGameTick = snapshot.GameTick,
            ExpectedLedgerRevision = staged.Revision, MaterialReservationsSha256 = PreparedMaterialTransferContract.ComputeMaterialReservationsSha256(staged),
            Intent = intent, SourceRecipe = row, Consumptions = consumed, Projection = projection,
            BeforeInventoryGraph = new MaterialInventoryGraph
            {
                PlayerId = graph.PlayerId, InventoryNodes = nodes, AccessPoints = access, PhysicalInventoryCount = nodes.Length,
                AccessPointCount = access.Length, DeduplicatedAccessPointCount = access.Length,
                DefaultSharedResourcePolicy = graph.DefaultSharedResourcePolicy
            }
        };
        // Only affected inventories and the original recipe row survive the source snapshot cache.
        retained = JsonSerializer.Deserialize<PreparedInventoryConsumption>(JsonSerializer.Serialize(retained))!;
        retained.BindingSha256 = PreparedInventoryConsumptionContract.ComputeBindingSha256(retained);
        staged.PreparedInventoryConsumption = retained;
    }
}
