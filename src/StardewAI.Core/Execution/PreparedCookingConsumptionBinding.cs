using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Execution;

public static class PreparedCookingConsumptionBinding
{
    public static bool IsControlParameter(string name) => name.StartsWith("prepared_inventory_consumption_", StringComparison.OrdinalIgnoreCase);
    public static bool IsPresent(SmallModelActionParameter[] parameters) => parameters.Any(row => row is not null &&
        IsControlParameter(row.Name));

    public static SmallModelActionParameter[] ToParameters(StrategyCommitmentLedger ledger)
    {
        var prepared = ledger.PreparedInventoryConsumption ?? throw new InvalidOperationException("Cooking consumption is not prepared.");
        if (!PreparedInventoryConsumptionContract.IsBound(ledger, prepared.BindingSha256, prepared.PortfolioId, prepared.GoalId, prepared.RouteSourceDecisionId))
            throw new InvalidOperationException("Cooking consumption preparation is not bound.");
        return new[] { new SmallModelActionParameter { Name = PreparedInventoryConsumptionContract.BindingParameter, Value = prepared.BindingSha256 } };
    }

    public static bool IsBound(ActionQueueItem item, StrategyCommitmentLedger ledger) =>
        item.OptionId == PreparedInventoryConsumptionContract.Consumer && item.NormalizedCommand.OptionId == item.OptionId &&
        ParametersAreBound(item.NormalizedCommand.Parameters, item.NormalizedCommand.GoalId, ledger);

    private static bool ParametersAreBound(SmallModelActionParameter[] parameters, string? goalId, StrategyCommitmentLedger ledger)
    {
        var prepared = ledger.PreparedInventoryConsumption;
        if (prepared is null || goalId != prepared.GoalId || parameters is null || parameters.Any(row => row is null) ||
            parameters.Where(row => row.Name is not "precondition" and not "expected_effect" and not "safety_constraint" and not "failure_policy")
                .GroupBy(row => row.Name, StringComparer.Ordinal).Any(group => group.Count() != 1) ||
            parameters.Any(row => (IsControlParameter(row.Name) && row.Name != PreparedInventoryConsumptionContract.BindingParameter) ||
                row.Name == PreparedMaterialTransferContract.BindingParameter || CropSaleStockMaterialTransferBinding.IsForeignControlParameter(row.Name) ||
                CropSaleStockMaterialTransferBinding.IsControlParameter(row.Name))) return false;
        string? Read(string name) => parameters.SingleOrDefault(row => row.Name == name)?.Value;
        return Read("recipe_name") == prepared.Intent.RecipeName && Read("cooking_source_id") == prepared.Intent.CookingSourceId &&
            PreparedInventoryConsumptionContract.IsBound(ledger, Read(PreparedInventoryConsumptionContract.BindingParameter),
                prepared.PortfolioId, goalId, prepared.RouteSourceDecisionId);
    }

    internal static bool SourceMatches(SmallModelAction action, SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger, string? goalId)
    {
        if (ledger is null || action.OptionId != PreparedInventoryConsumptionContract.Consumer ||
            !ParametersAreBound(action.Parameters, goalId, ledger) || snapshot.SaveId.Status != FieldStatus.Available ||
            snapshot.PlayerId.Status != FieldStatus.Available || snapshot.SaveId.Value != ledger.SaveId || snapshot.PlayerId.Value != ledger.PlayerId ||
            !MaterialInventoryGraphReader.TryRead(snapshot, out var graph)) return false;
        var prepared = ledger.PreparedInventoryConsumption!;
        var recipe = CropSaleStockCookingPreflight.ReadRecipe(snapshot, prepared.Intent.RecipeName, prepared.Intent.CookingSourceId);
        return recipe.HasValue && JsonSerializer.Serialize(recipe.Value) == JsonSerializer.Serialize(prepared.SourceRecipe) &&
            snapshot.GameTick >= prepared.SourceGameTick && InventoryConsumptionProjector.VerifyBefore(prepared, graph!).Length == 0 &&
            new MaterialSupplyProjection().Project(graph!, ledger.MaterialReservations).BlockingReasons.Length == 0;
    }
}
