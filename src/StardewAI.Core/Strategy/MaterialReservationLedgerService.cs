using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public sealed class MaterialReservationLedgerService
{
    private readonly MaterialSupplyProjection projection = new();

    public StrategyCommitmentMutationResult Upsert(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        MaterialReservationUpsertRequest request,
        string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        ValidateRequest(request, errors);

        var graph = TryReadGraph(snapshot, errors);
        MaterialInventoryNode? node = null;
        MaterialInventorySlot? slot = null;
        if (graph is not null)
        {
            if (!long.TryParse(
                    StrategyCommitmentLedgerSupport.PlayerId(snapshot),
                    out var actorPlayerId) ||
                actorPlayerId != graph.PlayerId)
            {
                errors.Add("material_inventory_graph_player_mismatch");
            }
            var matchingNodes = graph.InventoryNodes
                .Where(row => string.Equals(row.NodeId, request.NodeId, StringComparison.Ordinal))
                .ToArray();
            if (matchingNodes.Length != 1)
            {
                errors.Add(matchingNodes.Length == 0
                    ? "material_reservation_node_not_found"
                    : "material_reservation_node_not_unique");
            }
            else
            {
                node = matchingNodes[0];
                if (!string.Equals(node.SupplyState, "available", StringComparison.Ordinal))
                {
                    errors.Add("material_reservation_node_not_available");
                }
                if (!node.ActorUseAuthorized)
                {
                    errors.Add("material_reservation_node_not_actor_authorized");
                }
                var matchingSlots = node.Slots
                    .Where(row => row.SlotIndex == request.SlotIndex)
                    .ToArray();
                if (matchingSlots.Length != 1)
                {
                    errors.Add(matchingSlots.Length == 0
                        ? "material_reservation_slot_not_found"
                        : "material_reservation_slot_not_unique");
                }
                else
                {
                    slot = matchingSlots[0];
                    if (!string.Equals(
                            slot.QualifiedItemId,
                            request.QualifiedItemId,
                            StringComparison.Ordinal))
                    {
                        errors.Add("material_reservation_item_mismatch");
                    }
                }
            }
        }

        if (graph is not null && node is not null && slot is not null && errors.Count == 0)
        {
            var otherActive = (current?.MaterialReservations ?? Array.Empty<MaterialReservation>())
                .Where(row =>
                    string.Equals(row.Status, StrategyCommitmentStatuses.Active, StringComparison.Ordinal) &&
                    !string.Equals(row.ReservationId, request.ReservationId, StringComparison.Ordinal))
                .ToArray();
            var supply = projection.Project(graph, otherActive);
            if (!string.Equals(supply.Status, "available", StringComparison.Ordinal))
            {
                errors.AddRange(supply.BlockingReasons);
            }
            var projectedSlot = supply.Slots.SingleOrDefault(row =>
                string.Equals(row.NodeId, request.NodeId, StringComparison.Ordinal) &&
                row.SlotIndex == request.SlotIndex &&
                string.Equals(row.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal));
            if (projectedSlot is null || projectedSlot.AvailableQuantity < request.Quantity)
            {
                errors.Add("material_reservation_insufficient_unreserved_quantity");
            }
        }

        if (errors.Count > 0)
        {
            return Rejected(current, errors);
        }

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(current, snapshot, updatedAt);
        var existing = ledger.MaterialReservations.FirstOrDefault(row =>
            string.Equals(row.ReservationId, request.ReservationId, StringComparison.Ordinal));
        var reservation = new MaterialReservation
        {
            ReservationId = request.ReservationId,
            Revision = (existing?.Revision ?? 0) + 1,
            Status = StrategyCommitmentStatuses.Active,
            SourceDecisionId = request.SourceDecisionId,
            SourceStateHash = snapshot.StateHash,
            GoalId = request.GoalId,
            OwnerPlayerId = graph!.PlayerId,
            NodeId = request.NodeId,
            SlotIndex = request.SlotIndex,
            QualifiedItemId = request.QualifiedItemId,
            Quantity = request.Quantity,
            Purpose = request.Purpose
        };
        ledger.MaterialReservations = ledger.MaterialReservations
            .Where(row => !string.Equals(
                row.ReservationId,
                reservation.ReservationId,
                StringComparison.Ordinal))
            .Append(reservation)
            .OrderBy(row => row.ReservationId, StringComparer.Ordinal)
            .ToArray();
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            reservation.ReservationId,
            reservation.Revision,
            reservation.SourceDecisionId,
            "material_reservation_upsert",
            updatedAt,
            string.Empty);
        return Accepted(ledger);
    }

    public StrategyCommitmentMutationResult Cancel(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        string reservationId,
        StrategyCommitmentCancelRequest request,
        string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        var existing = current?.MaterialReservations.FirstOrDefault(row =>
            string.Equals(row.ReservationId, reservationId, StringComparison.Ordinal));
        if (existing is null)
        {
            errors.Add("material_reservation_not_found");
        }
        else if (!string.Equals(
                     existing.Status,
                     StrategyCommitmentStatuses.Active,
                     StringComparison.Ordinal))
        {
            errors.Add("only_active_material_reservation_can_be_cancelled");
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("cancel_reason_required");
        }
        if (errors.Count > 0)
        {
            return Rejected(current, errors);
        }

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(current, snapshot, updatedAt);
        ledger.MaterialReservations = ledger.MaterialReservations.Select(row =>
        {
            if (!string.Equals(row.ReservationId, reservationId, StringComparison.Ordinal))
            {
                return row;
            }
            var cancelled = StrategyCommitmentLedgerSupport.CloneMaterial(row);
            cancelled.Status = StrategyCommitmentStatuses.Cancelled;
            cancelled.Revision++;
            cancelled.CancelReason = request.Reason;
            return cancelled;
        }).ToArray();
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        var cancelledReservation = ledger.MaterialReservations.Single(row =>
            string.Equals(row.ReservationId, reservationId, StringComparison.Ordinal));
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            cancelledReservation.ReservationId,
            cancelledReservation.Revision,
            cancelledReservation.SourceDecisionId,
            "material_reservation_cancel",
            updatedAt,
            request.Reason);
        return Accepted(ledger);
    }

    private static void ValidateRequest(
        MaterialReservationUpsertRequest request,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.ReservationId))
        {
            errors.Add("material_reservation_id_required");
        }
        if (string.IsNullOrWhiteSpace(request.SourceDecisionId))
        {
            errors.Add("source_decision_id_required");
        }
        if (string.IsNullOrWhiteSpace(request.GoalId))
        {
            errors.Add("goal_id_required");
        }
        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            errors.Add("material_reservation_node_id_required");
        }
        if (request.SlotIndex < 0)
        {
            errors.Add("material_reservation_slot_index_invalid");
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            errors.Add("material_reservation_qualified_item_id_required");
        }
        if (request.Quantity <= 0)
        {
            errors.Add("material_reservation_quantity_not_positive");
        }
        if (string.IsNullOrWhiteSpace(request.Purpose))
        {
            errors.Add("material_reservation_purpose_required");
        }
    }

    private static MaterialInventoryGraph? TryReadGraph(
        SnapshotEnvelope snapshot,
        ICollection<string> errors)
    {
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "farm", "material_inventory_graph")))
        {
            errors.Add("material_inventory_graph_unavailable");
            return null;
        }
        var value = ReadStateFieldValue(snapshot, "farm", "material_inventory_graph");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add("material_inventory_graph_unavailable");
            return null;
        }
        try
        {
            var graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(value.Value.GetRawText());
            if (graph is null ||
                !string.Equals(
                    graph.SchemaVersion,
                    "material_inventory_graph.v1",
                    StringComparison.Ordinal) ||
                !string.Equals(graph.Status, "available", StringComparison.Ordinal))
            {
                errors.Add("material_inventory_graph_unavailable");
                return null;
            }
            return graph;
        }
        catch (JsonException)
        {
            errors.Add("material_inventory_graph_invalid");
            return null;
        }
    }

    private static StrategyCommitmentMutationResult Accepted(
        StrategyCommitmentLedger ledger) => new()
        {
            Accepted = true,
            Ledger = ledger
        };

    private static StrategyCommitmentMutationResult Rejected(
        StrategyCommitmentLedger? ledger,
        IEnumerable<string> errors) => new()
        {
            Accepted = false,
            Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
            Ledger = ledger
        };
}
