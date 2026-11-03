using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed class MuseumDonationMaterialReservationGuard
{
    private readonly StrategyCommitmentLedger? ledger;
    private readonly List<string> reasons = new();
    private readonly MaterialReservation[] active = Array.Empty<MaterialReservation>();
    private readonly Dictionary<int, MaterialSupplySlot> playerSlots = new();
    private readonly string playerNodeId = string.Empty;

    internal MuseumDonationMaterialReservationGuard(SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger)
    {
        this.ledger = ledger;
        if (ledger is null)
            return;
        var saveId = ReadStateFieldString(snapshot, "identity", "save_id");
        var playerId = ReadStateFieldString(snapshot, "identity", "player_id");
        if (ledger.SchemaVersion != "strategy_commitment_ledger.v1" || string.IsNullOrWhiteSpace(ledger.LedgerId) ||
            ledger.Revision < 0 || string.IsNullOrWhiteSpace(saveId) || string.IsNullOrWhiteSpace(playerId) ||
            ledger.SaveId != saveId || ledger.PlayerId != playerId || ledger.MaterialReservations is null ||
            ledger.MaterialReservations.Any(row => row is null))
        {
            reasons.Add("museum_donation_strategy_ledger_identity_invalid");
            return;
        }
        active = ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active).ToArray();
        if (active.Length == 0)
            return;
        if (!MaterialInventoryGraphReader.TryRead(snapshot, out var graph) || graph is null ||
            graph.PlayerId.ToString(CultureInfo.InvariantCulture) != playerId)
        {
            reasons.Add("museum_donation_material_inventory_graph_unavailable_or_wrong_actor");
            return;
        }
        playerNodeId = "player:" + playerId;
        var supply = new MaterialSupplyProjection().Project(graph, active);
        if (supply.Status != "available")
        {
            reasons.AddRange(supply.BlockingReasons);
            return;
        }
        foreach (var slot in supply.Slots.Where(slot => slot.NodeId == playerNodeId))
            playerSlots.Add(slot.SlotIndex, slot);
    }

    internal MuseumDonationMaterialReservationResult Evaluate(int slotIndex, string qualifiedItemId, int expectedStack)
    {
        if (ledger is null)
            return new(true, Array.Empty<string>(), Array.Empty<SmallModelActionParameter>());
        var errors = new List<string>(reasons);
        if (slotIndex < 0 || string.IsNullOrWhiteSpace(qualifiedItemId) || expectedStack < 1)
            errors.Add("museum_donation_material_target_invalid");
        if (active.Length > 0 && errors.Count == 0 &&
            (!playerSlots.TryGetValue(slotIndex, out var slot) || slot.QualifiedItemId != qualifiedItemId ||
             slot.Stack != expectedStack || slot.AvailableQuantity < 1))
            errors.Add("museum_donation_item_reserved_or_material_projection_drifted");
        var ids = active.Where(row => row.NodeId == playerNodeId && row.SlotIndex == slotIndex &&
                row.QualifiedItemId == qualifiedItemId)
            .Select(row => row.ReservationId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var status = errors.Count > 0 ? "blocked" : active.Length == 0 ? "ready_no_active_material_reservations" : "ready";
        return new(errors.Count == 0, errors.Distinct(StringComparer.Ordinal).ToArray(), new[]
        {
            Parameter("material_reservation_guard_status", status),
            Parameter("material_reservation_ledger_id", ledger.LedgerId),
            Parameter("material_reservation_ledger_revision", ledger.Revision.ToString(CultureInfo.InvariantCulture)),
            Parameter("commitment_ledger_id", ledger.LedgerId),
            Parameter("commitment_ledger_revision", ledger.Revision.ToString(CultureInfo.InvariantCulture)),
            Parameter("material_reservation_ids_json", JsonSerializer.Serialize(ids)),
            Parameter("museum_donation_save_id", ledger.SaveId),
            Parameter("museum_donation_player_id", ledger.PlayerId)
        });
    }

    internal static bool IsBindingParameter(string name) => name is
        "material_reservation_guard_status" or "material_reservation_ledger_id" or "material_reservation_ledger_revision" or
        "commitment_ledger_id" or "commitment_ledger_revision" or "material_reservation_ids_json" or
        "museum_donation_save_id" or "museum_donation_player_id";

    private static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };
}

internal sealed record MuseumDonationMaterialReservationResult(
    bool Ready, string[] BlockingReasons, SmallModelActionParameter[] Parameters);
