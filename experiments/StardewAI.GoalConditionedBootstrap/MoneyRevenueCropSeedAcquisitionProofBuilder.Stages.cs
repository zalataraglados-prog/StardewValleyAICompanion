using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSeedAcquisitionProofBuilder
{
    private static int VerifyPurchaseSlice(
        LoadedTransition transition,
        PurchaseBinding binding,
        int beforeQuantity,
        int afterQuantity,
        int beforeMoney,
        int afterMoney)
    {
        var items = transition.Queue.Items ?? Array.Empty<ActionQueueItem>();
        var purchaseItems = items.Where(value =>
            value.OptionId == "executor.buy_shop_item").ToArray();
        Require(purchaseItems.Length == 1 &&
                items.All(value => PurchaseQueueOptions.Contains(
                    value.OptionId)),
            "Money Revenue crop seed purchase queue shape is invalid.");
        var parameters = purchaseItems[0].NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>();
        var expectedUnitPrice = SingleIntParameter(
            parameters,
            "expected_unit_price");
        Require(SingleParameter(parameters, "qualified_item_id") ==
                    binding.QualifiedItemId &&
                SingleIntParameter(parameters, "quantity") == 1 &&
                SingleParameter(parameters, "expected_shop_id") ==
                    binding.ShopId &&
                SingleParameter(parameters, "expected_stock_id") ==
                    binding.StockId &&
                SingleIntParameter(parameters, "expected_output_stack") ==
                    binding.OutputStack &&
                SingleIntParameter(parameters, "expected_output_quality") ==
                    binding.OutputQuality &&
                SingleIntParameter(parameters, "max_unit_price") ==
                    binding.MaxUnitPrice &&
                expectedUnitPrice is > 0 &&
                expectedUnitPrice <= binding.MaxUnitPrice &&
                afterQuantity - beforeQuantity == binding.OutputStack &&
                beforeMoney - afterMoney == expectedUnitPrice,
            "Money Revenue crop seed purchase did not produce its exact bound item and money deltas.");
        Require(FullShipmentRecoverySettlementBoundaryProjector
                    .TryProjectExpectedResourceMutationInvariant(
                        transition.Before,
                        out var beforeInvariant) &&
                FullShipmentRecoverySettlementBoundaryProjector
                    .TryProjectExpectedResourceMutationInvariant(
                        transition.After,
                        out var afterInvariant) &&
                beforeInvariant == afterInvariant,
            "Money Revenue crop seed purchase changed unrelated persistent state.");
        return expectedUnitPrice ?? throw new InvalidDataException(
            "Money Revenue crop seed purchase expected price is missing.");
    }

    private static void VerifySupportingStage(
        LoadedTransition transition,
        string[] optionIds,
        int beforeQuantity,
        int afterQuantity,
        int beforeMoney,
        int afterMoney)
    {
        Require(optionIds.Length > 0 &&
                optionIds.All(
                    MoneyRevenueResourceNeutralTerminalRouteVerifier
                        .ResourceNeutralOptions.Contains) &&
                optionIds.Any(value => value is
                    "executor.move_to_tile" or
                    "executor.traverse_connector" or
                    "executor.interact" or
                    "executor.choose_dialogue_response") &&
                beforeQuantity == afterQuantity &&
                beforeMoney == afterMoney,
            "Money Revenue crop seed supporting stage is not resource neutral.");
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(
                    transition.Before,
                    out var beforeBoundary) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProject(
                    transition.After,
                    out var afterBoundary) &&
                beforeBoundary == afterBoundary,
            "Money Revenue crop seed supporting stage changed persistent resources.");
    }

    private static PurchaseBinding ReadPurchaseBinding(
        ActionQueueEnvelope queue,
        string expectedQualifiedItemId)
    {
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        var anchors = items.Where(value =>
                HasParameter(
                    value.NormalizedCommand?.Parameters,
                    "continuation.option_id"))
            .ToArray();
        Require(anchors.Length > 0 &&
                items.Except(anchors).All(value =>
                    value.OptionId == "executor.close_menu"),
            "Money Revenue crop seed queue lost its continuation identity.");
        var bindings = anchors.Select(value =>
        {
            var parameters = value.NormalizedCommand?.Parameters ??
                Array.Empty<SmallModelActionParameter>();
            return new PurchaseBinding(
                SingleParameter(parameters, "continuation.shop_id"),
                SingleParameter(parameters, "continuation.stock_id"),
                SingleParameter(
                    parameters,
                    "continuation.qualified_item_id"),
                SingleIntParameter(
                    parameters,
                    "continuation.output_stack_per_purchase") ?? -1,
                SingleIntParameter(
                    parameters,
                    "continuation.output_quality") ?? -1,
                SingleIntParameter(
                    parameters,
                    "continuation.max_unit_price") ?? -1,
                SingleIntParameter(
                    parameters,
                    "continuation.quantity") ?? -1);
        }).Distinct().ToArray();
        Require(bindings.Length == 1 &&
                SingleParameter(
                    anchors[0].NormalizedCommand.Parameters,
                    "continuation.option_id") ==
                    "economy.buy_supplies" &&
                !string.IsNullOrWhiteSpace(bindings[0].ShopId) &&
                !string.IsNullOrWhiteSpace(bindings[0].StockId) &&
                bindings[0].QualifiedItemId == expectedQualifiedItemId &&
                bindings[0].OutputStack > 0 &&
                bindings[0].OutputQuality >= 0 &&
                bindings[0].MaxUnitPrice > 0 &&
                bindings[0].Quantity == 1,
            "Money Revenue crop seed continuation binding is incomplete or ambiguous.");
        return bindings[0];
    }

    private static bool HasParameter(
        SmallModelActionParameter[]? parameters,
        string name) =>
        (parameters ?? Array.Empty<SmallModelActionParameter>())
            .Any(value => value.Name == name);

    private static string SingleParameter(
        SmallModelActionParameter[] parameters,
        string name)
    {
        var values = parameters.Where(value => value.Name == name)
            .Select(value => value.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
    }

    private static int? SingleIntParameter(
        SmallModelActionParameter[] parameters,
        string name) => int.TryParse(
            SingleParameter(parameters, name),
            out var value)
                ? value
                : null;
}
