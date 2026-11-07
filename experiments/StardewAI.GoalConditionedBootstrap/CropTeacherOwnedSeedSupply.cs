using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherOwnedSeedSlot(string ReservationId, string NodeId, string LocationId,
    string InventoryKind, int SlotIndex, string QualifiedItemId, int Quality, int Quantity, int NativeStack);

public sealed record CropTeacherOwnedSeedCommitment(string CommitmentId, string SeedQualifiedItemId,
    int PlantingTotalDay, int TileCount, int PlayerQuantity, int ExternalQuantity, int PurchaseBudget,
    IReadOnlyList<CropTeacherOwnedSeedSlot> Slots);

public sealed class CropTeacherOwnedSeedSupply
{
    private readonly MaterialInventoryGraph graph;
    private readonly MaterialInventoryNode player;

    private CropTeacherOwnedSeedSupply(MaterialInventoryGraph graph, MaterialInventoryNode player,
        IReadOnlyList<CropTeacherOwnedSeedCommitment> commitments)
    {
        this.graph = graph;
        this.player = player;
        Commitments = commitments;
    }

    public IReadOnlyList<CropTeacherOwnedSeedCommitment> Commitments { get; }

    internal MaterialInventoryNode CopyPlayerInventory() => JsonSerializer.Deserialize<MaterialInventoryNode>(
        JsonSerializer.Serialize(player, JsonDefaults.Options), JsonDefaults.Options)!;

    public static CropTeacherOwnedSeedSupply Read(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        IReadOnlyCollection<string> commitmentIds)
    {
        var state = AcquisitionStrategyLedgerReader.Read(ledger, snapshot);
        Require(snapshot.SchemaVersion == "snapshot.v1" && snapshot.GameVersion == "1.6.15" &&
                ledger.SourceStateHash == snapshot.StateHash && commitmentIds.Count > 0 &&
                commitmentIds.Distinct(StringComparer.Ordinal).Count() == commitmentIds.Count,
            "Managed crop seed supply requires current source and unique commitment identities.");
        if (!snapshot.State.TryGetValue("farm", out var farm) || farm.ValueKind != JsonValueKind.Object ||
            !farm.TryGetProperty("material_inventory_graph", out var material) ||
            !snapshot.State.TryGetValue("player", out var actor) || actor.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Managed crop seed supply lacks native inventory evidence.");
        // Consume only material/currency domains, not a serialized copy of the full world.
        var resources = AcquisitionResourceInputSnapshotState.Read(JsonSerializer.SerializeToElement(new
        {
            state = new { farm = new { material_inventory_graph = material }, player = actor.EnumerateObject()
                .Where(row => row.Name is "money" or "shop_currency_balances")
                .ToDictionary(row => row.Name, row => row.Value, StringComparer.Ordinal) }
        }, JsonDefaults.Options));
        AcquisitionReservationSupplyValidator.Validate(state, resources, resources.ShopQuotes);
        var graph = resources.MaterialGraph ?? throw new InvalidDataException("Managed crop material graph is unavailable.");
        Require(resources.MaterialEvidenceAvailable && graph.PlayerId == state.ActorPlayerId,
            "Managed crop seed supply has no valid actor material graph.");
        var players = graph.InventoryNodes.Where(row => row.InventoryKind == "player_inventory" &&
            row.OwnerPlayerId == state.ActorPlayerId && row.ActorUseAuthorized && row.SupplyState == "available").ToArray();
        Require(players.Length == 1, "Managed crop seed supply requires one authorized player inventory.");
        VerifyPlayerSlots(snapshot, players[0]);
        var result = new List<CropTeacherOwnedSeedCommitment>();
        foreach (var id in commitmentIds)
        {
            var crops = ledger.CropPlantingCommitments.Where(row => row.CommitmentId == id &&
                row.Status == StrategyCommitmentStatuses.Active).ToArray();
            Require(crops.Length == 1, "Managed crop seed supply commitment is not uniquely active: " + id);
            var crop = crops[0];
            Require(crop.TileCount > 0 && !string.IsNullOrWhiteSpace(crop.SourceDecisionId) &&
                    !string.IsNullOrWhiteSpace(crop.SeedQualifiedItemId),
                "Managed crop seed supply commitment metadata is incomplete.");
            var seeds = ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                row.Purpose == "crop_seed_supply:" + id).ToArray();
            Require(seeds.All(row => row.SourceDecisionId == crop.SourceDecisionId && row.GoalId == "earn_money" &&
                    row.QualifiedItemId == crop.SeedQualifiedItemId),
                "Managed crop seed supply claim owner or seed identity drifted.");
            var slots = seeds.Select(row =>
            {
                var node = graph.InventoryNodes.Single(value => value.NodeId == row.NodeId);
                Require((node.InventoryKind == "chest" || node.InventoryKind == "player_inventory" &&
                        node.NodeId == players[0].NodeId) && node.ActorUseAuthorized &&
                        node.SupplyState == "available" && !string.IsNullOrWhiteSpace(node.LocationId),
                    "Managed crop seed supply requires native player/chest custody.");
                var slot = node.Slots.Single(value => value.SlotIndex == row.SlotIndex);
                return new CropTeacherOwnedSeedSlot(row.ReservationId, row.NodeId, node.LocationId,
                    node.InventoryKind, row.SlotIndex, row.QualifiedItemId, slot.Quality, row.Quantity, slot.Stack);
            }).OrderBy(row => row.NodeId, StringComparer.Ordinal).ThenBy(row => row.SlotIndex)
                .ThenBy(row => row.ReservationId, StringComparer.Ordinal).ToArray();
            var cash = ledger.CurrencyReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                row.ReservationId == id + ":seed-purchase:money").ToArray();
            Require(cash.Length <= 1 && cash.All(row => row.SourceDecisionId == crop.SourceDecisionId &&
                    row.GoalId == "earn_money" && row.CurrencyId == 0 && row.Purpose == "crop_seed_purchase:" + id),
                "Managed crop seed purchase budget owner drifted.");
            var playerQuantity = Sum(slots.Where(row => row.NodeId == players[0].NodeId).Select(row => row.Quantity));
            var externalQuantity = Sum(slots.Where(row => row.NodeId != players[0].NodeId).Select(row => row.Quantity));
            Require(checked(playerQuantity + externalQuantity) <= crop.TileCount,
                "Managed crop seed supply exceeds its original tile denominator.");
            result.Add(new(id, crop.SeedQualifiedItemId, crop.PlantingTotalDay, crop.TileCount, playerQuantity, externalQuantity,
                cash.SingleOrDefault()?.Amount ?? 0, Array.AsReadOnly(slots)));
        }
        return new(graph, players[0], result.AsReadOnly());
    }

    public int PlayerQuantity(IEnumerable<string> commitmentIds) => Sum(Select(commitmentIds).Select(row => row.PlayerQuantity));
    public int PurchaseBudget(IEnumerable<string> commitmentIds) => Sum(Select(commitmentIds).Select(row => row.PurchaseBudget));

    public CropTeacherOwnedSeedSlot? ExternalSlot(IEnumerable<string> commitmentIds) => Select(commitmentIds)
        .SelectMany(row => row.Slots).Where(row => row.NodeId != player.NodeId)
        .OrderBy(row => row.NodeId, StringComparer.Ordinal).ThenBy(row => row.SlotIndex)
        .ThenBy(row => row.ReservationId, StringComparer.Ordinal).FirstOrDefault();

    public MaterialTransferIntent TransferIntent(CropTeacherOwnedSeedSlot slot)
    {
        Require(Commitments.SelectMany(row => row.Slots).Contains(slot) && slot.InventoryKind == "chest",
            "Managed crop seed transfer does not belong to the selected owned supply.");
        var intent = new MaterialTransferIntent
        {
            SourceNodeId = slot.NodeId, DestinationNodeId = player.NodeId, SourceSlotIndex = slot.SlotIndex,
            QualifiedItemId = slot.QualifiedItemId, Quality = slot.Quality,
            Quantity = slot.Quantity, ExpectedSourceStack = slot.NativeStack
        };
        var projection = new MaterialTransferProjector().Project(graph, intent);
        Require(projection.Status == "projected",
            "Managed crop seed transfer native projection is blocked: " + string.Join(",", projection.BlockingReasons));
        return intent;
    }

    private IEnumerable<CropTeacherOwnedSeedCommitment> Select(IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        Require(selected.Count > 0 && selected.All(id => Commitments.Any(row => row.CommitmentId == id)),
            "Managed crop seed supply selection contains an unknown commitment.");
        return Commitments.Where(row => selected.Contains(row.CommitmentId));
    }

    private static int Sum(IEnumerable<int> values) => checked((int)values.Sum(value => (long)value));

    private static void VerifyPlayerSlots(SnapshotEnvelope snapshot, MaterialInventoryNode player)
    {
        if (!snapshot.State["player"].TryGetProperty("inventory", out var value))
            throw new InvalidDataException("Managed crop native player inventory is unavailable.");
        FieldEnvelope<NativePlayerSlot[]>? envelope;
        try { envelope = value.Deserialize<FieldEnvelope<NativePlayerSlot[]>>(JsonDefaults.Options); }
        catch (JsonException error) { throw new InvalidDataException("Managed crop native player inventory is invalid.", error); }
        Require(envelope is not null && FieldEnvelopeValidator.IsReadableStatus(envelope.Status) && envelope.Value is not null,
            "Managed crop native player inventory is unavailable.");
        Require(envelope!.Value!.All(row => row is not null), "Managed crop native player inventory contains a null slot.");
        var rows = envelope!.Value!.Where(row => !row.IsEmpty).ToArray();
        Require(rows.Length == player.Slots.Length && player.Slots.All(slot => rows.Count(row =>
                row.SlotIndex == slot.SlotIndex && row.QualifiedItemId == slot.QualifiedItemId &&
                row.Stack == slot.Stack && row.Quality == slot.Quality) == 1),
            "Managed crop native player inventory disagrees with its material graph.");
        var location = snapshot.State["player"].GetProperty("location_id").Deserialize<FieldEnvelope<string>>(JsonDefaults.Options);
        Require(location is not null && FieldEnvelopeValidator.IsReadableStatus(location.Status) && location.Value == player.LocationId,
            "Managed crop native player location disagrees with its material graph.");
    }

    private sealed record NativePlayerSlot(
        [property: JsonPropertyName("slot_index")] int? SlotIndex,
        [property: JsonPropertyName("qualified_item_id")] string? QualifiedItemId,
        [property: JsonPropertyName("stack")] int? Stack,
        [property: JsonPropertyName("quality")] int? Quality,
        [property: JsonPropertyName("is_empty")] bool IsEmpty);
}
