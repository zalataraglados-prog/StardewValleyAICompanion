using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public static class PreparedInventoryConsumptionContract
{
    public const string Schema = "prepared_inventory_consumption.v1";
    public const string RequestSchema = "inventory_consumption_preparation.v1";
    public const string Consumer = "executor.cook_recipe";
    public const string BindingParameter = "prepared_inventory_consumption_sha256";

    public static string ComputeBindingSha256(PreparedInventoryConsumption value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { value.SchemaVersion, value.LedgerId, value.SaveId, value.PlayerId, value.PortfolioId, value.GoalId, value.RouteSourceDecisionId, value.SourceStateHash, value.SourceGameTick,
            value.ExpectedLedgerRevision, value.MaterialReservationsSha256, value.Intent, value.SourceRecipe,
            value.Consumptions, value.BeforeInventoryGraph, value.Projection })));
        return string.Concat(bytes.Select(value => value.ToString("x2")));
    }

    public static bool IsBound(StrategyCommitmentLedger ledger, string? hash, string? portfolioId, string? goalId, string? sourceDecision)
    {
        var value = ledger.PreparedInventoryConsumption;
        return MaterialOutputCustodyContract.IsLowerSha256(hash ?? string.Empty) && value is not null && value.SchemaVersion == Schema &&
            ledger.MaterialReservations is not null && ledger.MaterialReservations.All(row => row is not null) &&
            value.Intent is { SchemaVersion: RequestSchema, ConsumerOptionId: Consumer } &&
            !string.IsNullOrWhiteSpace(value.Intent.RecipeName) && !string.IsNullOrWhiteSpace(value.Intent.CookingSourceId) &&
            value.SourceRecipe.ValueKind == JsonValueKind.Object &&
            ReadString(value.SourceRecipe, "recipe_name") == value.Intent.RecipeName &&
            ReadString(value.SourceRecipe, "cooking_source_id") == value.Intent.CookingSourceId &&
            value.BeforeInventoryGraph is not null && value.Consumptions is { Length: > 0 } && value.Projection is { Status: "projected" } &&
            value.Consumptions.All(row => row is not null) &&
            value.Projection.NativeSlotTransitions is { Length: > 0 } && value.Projection.PassiveReservationTransitions is not null &&
            value.Projection.NativeSlotTransitions.All(row => row is not null) && value.Projection.PassiveReservationTransitions.All(row => row is not null) &&
            value.LedgerId == ledger.LedgerId && value.SaveId == ledger.SaveId && value.PlayerId == ledger.PlayerId &&
            value.BeforeInventoryGraph.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) == ledger.PlayerId &&
            ledger.PreparedMaterialTransfer is null && !string.IsNullOrWhiteSpace(portfolioId) && !string.IsNullOrWhiteSpace(goalId) &&
            !string.IsNullOrWhiteSpace(sourceDecision) && value.PortfolioId == portfolioId && value.GoalId == goalId &&
            value.RouteSourceDecisionId == sourceDecision && value.Intent.RouteSourceDecisionId == sourceDecision &&
            value.ExpectedLedgerRevision == ledger.Revision && value.SourceStateHash == ledger.SourceStateHash &&
            value.MaterialReservationsSha256 == PreparedMaterialTransferContract.ComputeMaterialReservationsSha256(ledger) &&
            value.BindingSha256 == hash && hash == ComputeBindingSha256(value) && (ledger.History ?? Array.Empty<StrategyCommitmentHistoryEntry>()).Count(row =>
                row.CommitmentId == portfolioId && row.SourceDecisionId == sourceDecision && row.Operation == "inventory_consumption_prepared" &&
                row.LedgerRevision == ledger.Revision && row.CommitmentRevision == ledger.Revision && row.Reason == hash) == 1 &&
            ProjectionMatches(ledger, value);
    }

    private static bool ProjectionMatches(StrategyCommitmentLedger ledger, PreparedInventoryConsumption value)
    {
        if (value.BeforeInventoryGraph.InventoryNodes is null || value.BeforeInventoryGraph.InventoryNodes.Any(row => row is null)) return false;
        if (CropSaleStockCookingPreflight.ResolvePlan(value.BeforeInventoryGraph, value.SourceRecipe, out var consumed).Length != 0 ||
            JsonSerializer.Serialize(consumed) != JsonSerializer.Serialize(value.Consumptions)) return false;
        var nodes = value.BeforeInventoryGraph.InventoryNodes.Select(row => row.NodeId).ToHashSet(StringComparer.Ordinal);
        var view = new StrategyCommitmentLedger
        {
            PlayerId = ledger.PlayerId, Revision = ledger.Revision,
            MaterialReservations = ledger.MaterialReservations.Where(row => nodes.Contains(row.NodeId)).ToArray()
        };
        var projection = new InventoryConsumptionProjector().Project(value.BeforeInventoryGraph, view, value.Consumptions);
        return projection.Status == "projected" && JsonSerializer.Serialize(projection) == JsonSerializer.Serialize(value.Projection);
    }
}
