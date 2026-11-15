using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

// Only the explicit sale-stock protocol opts in; original native legality remains authoritative.
public static class CropSaleStockConsumptionPreflight
{
    public static string[] Evaluate(string optionId, SmallModelActionParameter[] parameters,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger)
    {
        var consumesOne = optionId is "social.gift_npc" or "social.advance_partnership" or "executor.consume_food" ||
            optionId == "executor.social_interact" && parameters.Any(row => row.Name == "social_action_kind" &&
                row.Value is "gift" or "bouquet" or "propose_marriage" or "propose_roommate");
        if (!consumesOne || ledger is null || !ledger.MaterialReservations.Any(row =>
            row.Status == StrategyCommitmentStatuses.Active && row.NodeId == "player:" + ledger.PlayerId &&
            CropSaleStockReservationContract.TryRead(row, out _))) return Array.Empty<string>();
        var slots = parameters.Where(row => row.Name == "slot_index").ToArray();
        var items = parameters.Where(row => row.Name == "qualified_item_id").ToArray();
        if (slots.Length != 1 || items.Length != 1 || string.IsNullOrWhiteSpace(items[0].Value) ||
            !int.TryParse(slots[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0)
            return new[] { "crop_sale_stock_consumption_intent_unavailable" };
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.PlayerId.Status != FieldStatus.Available ||
            snapshot.SaveId.Value != ledger.SaveId || snapshot.PlayerId.Value != ledger.PlayerId ||
            ledger.SourceStateHash != snapshot.StateHash || !MaterialInventoryGraphReader.TryRead(snapshot, out var graph) ||
            graph!.PlayerId.ToString(CultureInfo.InvariantCulture) != ledger.PlayerId)
            return new[] { "crop_sale_stock_material_supply_unavailable" };
        var supply = new MaterialSupplyProjection().Project(graph, ledger.MaterialReservations);
        var node = graph.InventoryNodes.Where(row => row.NodeId == "player:" + ledger.PlayerId).ToArray();
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        if (supply.Status != "available" || node.Length != 1 || node[0].InventoryKind != "player_inventory" ||
            node[0].OwnerPlayerId != graph.PlayerId || inventory?.ValueKind != JsonValueKind.Array)
            return new[] { "crop_sale_stock_material_supply_unavailable" };
        var physical = node[0].Slots.Where(row => row.SlotIndex == index && row.QualifiedItemId == items[0].Value).ToArray();
        var actual = inventory.Value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty("slot_index", out var slot) && slot.TryGetInt32(out var number) && number == index).ToArray();
        if (physical.Length != 1 || actual.Length != 1 ||
            !actual[0].TryGetProperty("qualified_item_id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != items[0].Value ||
            !actual[0].TryGetProperty("stack", out var stack) || !stack.TryGetInt32(out var count) || count != physical[0].Stack ||
            !actual[0].TryGetProperty("quality", out var quality) || !quality.TryGetInt32(out var grade) || grade != physical[0].Quality)
            return new[] { "crop_sale_stock_material_supply_unavailable" };
        var available = supply.Slots.Single(row => row.NodeId == node[0].NodeId && row.SlotIndex == index);
        return available.AvailableQuantity >= 1 ? Array.Empty<string>() : new[] { "inventory_item_reserved_for_crop_sale" };
    }
}
