using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReceiptBuilder
{
    internal static AcquisitionPurchaseTransitionEvidence
        VerifyPurchaseTransition(
            ActionQueueEnvelope queue,
            SnapshotEnvelope before,
            SnapshotEnvelope after)
    {
        var item = queue.Items.Length == 1
            ? queue.Items[0]
            : queue.Items.Single(value =>
                value.OptionId == "executor.buy_shop_item");
        var parameters = item.NormalizedCommand?.Parameters;
        var stage = UniqueParameter(
            parameters,
            "acquisition_support_purchase_stage");
        var bindingJson = UniqueParameter(
            parameters,
            "acquisition_support_purchase_prerequisite_json");
        AcquisitionPurchasePrerequisiteBinding? binding = null;
        try
        {
            binding = JsonSerializer.Deserialize<
                AcquisitionPurchasePrerequisiteBinding>(
                bindingJson,
                JsonDefaults.Options);
        }
        catch (JsonException)
        {
            // Stable diagnostics below cover malformed and missing bindings.
        }

        var reasons = new List<string>();
        if (stage is not (
                "route_connector" or
                "shop_interaction" or
                "purchase") ||
            binding is null ||
            binding.CurrencyId != 0 ||
            binding.UnitPrice <= 0 ||
            binding.OutputStackPerPurchase <= 0 ||
            string.IsNullOrWhiteSpace(binding.ShopId) ||
            string.IsNullOrWhiteSpace(binding.StockId) ||
            string.IsNullOrWhiteSpace(binding.QualifiedItemId))
        {
            reasons.Add("machine_input_purchase_receipt_binding_invalid");
        }

        int? beforeCurrency = null;
        int? afterCurrency = null;
        int? beforeQuantity = null;
        int? afterQuantity = null;
        if (binding is not null)
        {
            beforeCurrency = CurrencyBalance(before, binding.CurrencyId);
            afterCurrency = CurrencyBalance(after, binding.CurrencyId);
            beforeQuantity = PlayerInventoryQuantity(
                before,
                binding.QualifiedItemId);
            afterQuantity = PlayerInventoryQuantity(
                after,
                binding.QualifiedItemId);
            if (!beforeCurrency.HasValue || !afterCurrency.HasValue)
            {
                reasons.Add(
                    "machine_input_purchase_currency_delta_unavailable");
            }
            if (!beforeQuantity.HasValue || !afterQuantity.HasValue)
            {
                reasons.Add(
                    "machine_input_purchase_inventory_delta_unavailable");
            }
        }

        var observedCurrency = beforeCurrency.HasValue &&
            afterCurrency.HasValue
                ? beforeCurrency.Value - afterCurrency.Value
                : (int?)null;
        var observedItems = beforeQuantity.HasValue && afterQuantity.HasValue
            ? afterQuantity.Value - beforeQuantity.Value
            : (int?)null;
        var expectedCurrency = stage == "purchase"
            ? binding?.UnitPrice
            : 0;
        var expectedItems = stage == "purchase"
            ? binding?.OutputStackPerPurchase
            : 0;
        if (observedCurrency.HasValue &&
            observedCurrency != expectedCurrency)
        {
            reasons.Add("machine_input_purchase_currency_delta_mismatch");
        }
        if (observedItems.HasValue && observedItems != expectedItems)
        {
            reasons.Add("machine_input_purchase_inventory_delta_mismatch");
        }

        var blocking = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new AcquisitionPurchaseTransitionEvidence
        {
            Stage = stage,
            ShopId = binding?.ShopId ?? string.Empty,
            StockId = binding?.StockId ?? string.Empty,
            QualifiedItemId = binding?.QualifiedItemId ?? string.Empty,
            CurrencyId = binding?.CurrencyId,
            ExpectedCurrencyDecrease = expectedCurrency,
            CurrencyBefore = beforeCurrency,
            CurrencyAfter = afterCurrency,
            ObservedCurrencyDecrease = observedCurrency,
            ExpectedItemIncrease = expectedItems,
            ItemQuantityBefore = beforeQuantity,
            ItemQuantityAfter = afterQuantity,
            ObservedItemIncrease = observedItems,
            Resolved = binding is not null &&
                beforeCurrency.HasValue &&
                afterCurrency.HasValue &&
                beforeQuantity.HasValue &&
                afterQuantity.HasValue,
            Verified = blocking.Length == 0,
            BlockingReasons = blocking
        };
    }

    private static int? CurrencyBalance(
        SnapshotEnvelope snapshot,
        int currencyId)
    {
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(snapshot.State, JsonDefaults.Options));
        var lookup = new AcquisitionShopQuoteSnapshotState(
                document.RootElement)
            .CurrencyBalance(currencyId);
        return lookup.EvidenceAvailable ? lookup.Balance : null;
    }

    private static int? PlayerInventoryQuantity(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            new { state = snapshot.State },
            JsonDefaults.Options));
        var state = AcquisitionResourceInputSnapshotState.Read(
            document.RootElement);
        if (!state.MaterialEvidenceAvailable || state.MaterialGraph is null ||
            !long.TryParse(snapshot.PlayerId.Value, out var playerId))
        {
            return null;
        }
        var nodes = state.MaterialGraph.InventoryNodes.Where(node =>
                node.InventoryKind == "player_inventory" &&
                node.OwnerPlayerId == playerId &&
                node.ActorUseAuthorized &&
                node.SupplyState == "available")
            .ToArray();
        if (nodes.Length != 1)
            return null;
        var quantity = nodes[0].Slots.Where(slot =>
                slot.QualifiedItemId == qualifiedItemId)
            .Sum(slot => (long)slot.Stack);
        return quantity <= int.MaxValue ? (int)quantity : null;
    }
}
