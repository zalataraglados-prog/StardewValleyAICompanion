using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class CurrentMarriageTeacherResources
{
    private readonly MaterialInventoryNode inventory;
    private readonly MaterialSupplyProjectionResult supply;
    private readonly JsonElement items;

    internal CurrentMarriageTeacherResources(string ledgerPath, SnapshotEnvelope snapshot, JsonElement raw)
    {
        var ledgerJson = CurrentTeacherFrontierSupport.Read<JsonElement>(ledgerPath, "Marriage resource ledger");
        Require(ledgerJson.ValueKind == JsonValueKind.Object &&
            ledgerJson.EnumerateObject().GroupBy(row => row.Name, StringComparer.OrdinalIgnoreCase).All(group => group.Count() == 1),
            "Marriage comparison ledger has ambiguous root properties.");
        foreach (var name in new[] { "material_reservations", "currency_reservations", "crop_planting_commitments", "machine_relocation_intents", "machine_support_intents", "history" })
            Require(ledgerJson.TryGetProperty(name, out var rows) && rows.ValueKind == JsonValueKind.Array,
                "Marriage comparison requires explicit current commitment arrays: " + name);
        var state = AcquisitionStrategyLedgerReader.Read(ledgerPath, snapshot);
        var ledger = state.Ledger;
        Require(ledger.PreparedMaterialTransfer is null, "Marriage comparison cannot ignore a pending material transfer.");
        // SourceStateHash records the last ledger mutation. Validate persistent reservations against the fresh graph instead of rewriting history.
        // These obligations need their existing producer's budget, not an invented empty reservation.
        Require(!ledger.CropPlantingCommitments.Any(row => row.Status == StrategyCommitmentStatuses.Active) &&
            !ledger.MachineRelocationIntents.Any(row => row.Status == StrategyCommitmentStatuses.Active) &&
            !ledger.MachineSupportIntents.Any(row => row.Status == StrategyCommitmentStatuses.Active),
            "Marriage gift comparison has unbudgeted active production or relocation commitments.");
        var resources = AcquisitionResourceInputSnapshotState.Read(raw);
        AcquisitionReservationSupplyValidator.Validate(state, resources, resources.ShopQuotes);
        Require(resources.MaterialEvidenceAvailable && resources.MaterialGraph is not null &&
            resources.MaterialGraph.PlayerId == state.ActorPlayerId, "Marriage gift material graph is unavailable or foreign.");
        var graph = resources.MaterialGraph!;
        var owned = graph.InventoryNodes.Where(node => node.InventoryKind == "player_inventory" && node.OwnerPlayerId == state.ActorPlayerId &&
            node.ActorUseAuthorized && node.SupplyState == "available").ToArray();
        Require(owned.Length == 1, "Marriage gift requires one exact current owned player inventory.");
        inventory = owned[0];
        supply = new MaterialSupplyProjection().Project(graph, ledger.MaterialReservations);
        Require(supply.Status == "available", "Marriage gift reservations are inconsistent.");
        items = Value(snapshot, "player", "inventory");
        Require(items.ValueKind == JsonValueKind.Array, "Marriage gift inventory is not an array.");
    }

    internal bool TryGift(int slot, string itemId, int quality, int stack, out int price, out string reason)
    {
        price = 0;
        reason = "marriage_gift_inventory_reservation_or_price_unavailable";
        var rows = items.EnumerateArray().Where(row => Integer(row, "slot_index", out var index) && index == slot).ToArray();
        var native = inventory.Slots.Where(row => row.SlotIndex == slot).ToArray();
        var projected = supply.Slots.Where(row => row.NodeId == inventory.NodeId && row.SlotIndex == slot).ToArray();
        if (rows.Length != 1 || native.Length != 1 || projected.Length != 1 ||
            native[0].QualifiedItemId != itemId || native[0].Quality != quality || native[0].Stack != stack ||
            projected[0].QualifiedItemId != itemId || projected[0].AvailableQuantity < 1 ||
            !Text(rows[0], "qualified_item_id", itemId) || !Integer(rows[0], "quality", out var actualQuality) || actualQuality != quality ||
            !Integer(rows[0], "stack", out var actualStack) || actualStack != stack ||
            !rows[0].TryGetProperty("protected_from_auto_sell", out var protectedItem) || protectedItem.ValueKind != JsonValueKind.False ||
            !Integer(rows[0], "sell_to_store_price", out price) || price < 0) return false;
        reason = string.Empty;
        return true;
    }

    internal bool HasUnreservedProposalItem(EventCandidate proposal) => proposal.SlotIndex.HasValue &&
        inventory.Slots.Count(row => row.SlotIndex == proposal.SlotIndex && row.QualifiedItemId == proposal.QualifiedItemId && row.Stack > 0) == 1 &&
        supply.Slots.Count(row => row.NodeId == inventory.NodeId && row.SlotIndex == proposal.SlotIndex &&
            row.QualifiedItemId == proposal.QualifiedItemId && row.AvailableQuantity >= 1) == 1;

    private static bool Integer(JsonElement row, string name, out int value)
    {
        value = 0;
        return row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out value);
    }
    private static bool Text(JsonElement row, string name, string expected) => row.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String && value.GetString() == expected;
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
