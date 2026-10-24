using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class MasterAnglerRodEquipmentAcquisitionProtocol
{
    public const string RequirementId =
        "dependency.complete_master_angler.rod_equipment_acquisition";
    public const string OptionId = "economy.buy_supplies";
    public const string QualifiedItemId = "(T)BambooPole";
    public const string ShopId = "FishShop";
    public const int RequiredQuantity = 1;
    public const int MaximumUnitPrice = 500;

    public static string ObserveRodInventory(SnapshotEnvelope snapshot)
    {
        if (!snapshot.State.TryGetValue("fishing", out var fishing) ||
            fishing.ValueKind != JsonValueKind.Object ||
            !fishing.TryGetProperty("rod_inventory", out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not ("available" or "derived") ||
            !envelope.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return "unavailable";
        }

        var rows = value.EnumerateArray().ToArray();
        if (rows.Any(row =>
                row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("qualified_item_id", out var itemId) ||
                itemId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(itemId.GetString())))
        {
            return "unavailable";
        }

        return rows.Length == 0 ? "missing" : "satisfied";
    }

    public static bool TryMatchCandidate(
        PolicyEventCandidatePrediction candidate,
        out string bindingKind)
    {
        bindingKind = candidate.Kind switch
        {
            "route_connector_tile" =>
                "authoritative_supporting_prerequisite_rolling_route_step",
            "interact_endpoint" =>
                "authoritative_supporting_prerequisite_shop_endpoint_open",
            "buy_shop_item" =>
                "authoritative_supporting_prerequisite_terminal_purchase",
            _ => string.Empty
        };
        return bindingKind.Length > 0 &&
            candidate.OptionId == OptionId &&
            candidate.QualifiedItemId == QualifiedItemId &&
            candidate.ItemId == "BambooPole" &&
            candidate.ShopId == ShopId &&
            candidate.Quantity == RequiredQuantity &&
            candidate.UnitPrice is > 0 and <= MaximumUnitPrice &&
            AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase(
                candidate.QualifiedItemId) &&
            HasParameter(candidate, "continuation.option_id", OptionId) &&
            HasParameter(candidate, "continuation.shop_id", ShopId) &&
            HasParameter(
                candidate,
                "continuation.qualified_item_id",
                QualifiedItemId) &&
            HasParameter(candidate, "continuation.stock_id", QualifiedItemId) &&
            HasIntParameter(
                candidate,
                "continuation.output_stack_per_purchase",
                RequiredQuantity) &&
            HasIntParameter(
                candidate,
                "continuation.quantity",
                RequiredQuantity) &&
            TryReadIntParameter(
                candidate,
                "continuation.max_unit_price",
                out var maximumUnitPrice) &&
            maximumUnitPrice is > 0 and <= MaximumUnitPrice &&
            candidate.UnitPrice <= maximumUnitPrice;
    }

    private static bool HasParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        string expected) =>
        CurrentTeacherFrontierSupport.TryReadUniqueParameter(
            candidate,
            name,
            out var value) &&
        value == expected;

    private static bool HasIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        int expected) =>
        TryReadIntParameter(candidate, name, out var value) && value == expected;

    private static bool TryReadIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        out int value) =>
        CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
            candidate,
            name,
            out value);
}
