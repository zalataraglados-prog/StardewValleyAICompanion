using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
{
    public ReservationPortfolioSupportingTransitionSettlementResult
        SettleSupportingTransition(
            StrategyCommitmentLedger? current,
            SnapshotEnvelope snapshot,
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        var consumptions = MaterialConsumptions(request, errors);
        var relocations = NonNullEntries(
            request.MaterialRelocations,
            "supporting_transition_relocated_claim_invalid",
            errors);
        var currencyConsumptions = NonNullEntries(
            request.CurrencyConsumptions,
            "supporting_transition_currency_claim_invalid",
            errors);
        var rebindIds = request.RebindActiveReservationIds ??
            Array.Empty<string>();
        var rebindMachineIntent =
            !string.IsNullOrWhiteSpace(request.MachineSupportIntentId);
        ValidateSupportingTransitionSettlementRequest(
            current,
            snapshot,
            request,
            consumptions,
            relocations,
            currencyConsumptions,
            rebindIds,
            errors);
        if (errors.Count > 0)
            return SupportingTransitionSettlementRejected(
                current,
                request,
                consumptions,
                relocations,
                currencyConsumptions,
                rebindIds,
                errors);

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        var byReservation = consumptions.ToDictionary(
            value => value.MaterialReservationId,
            StringComparer.Ordinal);
        var relocationByReservation = relocations.ToDictionary(
            value => value.MaterialReservationId,
            StringComparer.Ordinal);
        var currencyByReservation = currencyConsumptions.ToDictionary(
            value => value.CurrencyReservationId,
            StringComparer.Ordinal);
        var rebindSet = rebindIds.ToHashSet(StringComparer.Ordinal);
        ledger.MaterialReservations = ledger.MaterialReservations.Select(row =>
        {
            if (relocationByReservation.TryGetValue(
                    row.ReservationId,
                    out var relocation))
            {
                var relocated = StrategyCommitmentLedgerSupport.CloneMaterial(row);
                relocated.Revision++;
                relocated.SourceStateHash = snapshot.StateHash;
                relocated.NodeId = relocation.DestinationNodeId;
                relocated.SlotIndex = relocation.DestinationSlotIndex;
                relocated.Status = StrategyCommitmentStatuses.Active;
                relocated.CompletionReason = string.Empty;
                relocated.CompletionEvidenceSha256 = string.Empty;
                return relocated;
            }
            if (!byReservation.TryGetValue(
                    row.ReservationId,
                    out var consumption))
            {
                if (!rebindSet.Contains(row.ReservationId))
                    return row;
                var rebound = StrategyCommitmentLedgerSupport
                    .CloneMaterial(row);
                rebound.Revision++;
                rebound.SourceStateHash = snapshot.StateHash;
                return rebound;
            }
            var settled = StrategyCommitmentLedgerSupport.CloneMaterial(row);
            settled.Revision++;
            settled.SourceStateHash = snapshot.StateHash;
            if (consumption.ConsumedQuantity == row.Quantity)
            {
                settled.Status = StrategyCommitmentStatuses.Completed;
                settled.CompletionReason = request.Reason;
                settled.CompletionEvidenceSha256 =
                    request.SupportingTransitionReceiptSha256;
            }
            else
            {
                settled.Quantity -= consumption.ConsumedQuantity;
                settled.Status = StrategyCommitmentStatuses.Active;
                settled.CompletionReason = string.Empty;
                settled.CompletionEvidenceSha256 = string.Empty;
            }
            return settled;
        }).ToArray();
        ledger.CurrencyReservations = ledger.CurrencyReservations.Select(row =>
        {
            if (!currencyByReservation.TryGetValue(
                    row.ReservationId,
                    out var consumption))
            {
                if (!rebindSet.Contains(row.ReservationId))
                    return row;
                var rebound = StrategyCommitmentLedgerSupport
                    .CloneCurrency(row);
                rebound.Revision++;
                rebound.SourceStateHash = snapshot.StateHash;
                return rebound;
            }
            var settled = StrategyCommitmentLedgerSupport.CloneCurrency(row);
            settled.Revision++;
            settled.SourceStateHash = snapshot.StateHash;
            if (consumption.ConsumedAmount == row.Amount)
            {
                settled.Status = StrategyCommitmentStatuses.Completed;
                settled.CompletionReason = request.Reason;
                settled.CompletionEvidenceSha256 =
                    request.SupportingTransitionReceiptSha256;
            }
            else
            {
                settled.Amount -= consumption.ConsumedAmount;
                settled.Status = StrategyCommitmentStatuses.Active;
                settled.CompletionReason = string.Empty;
                settled.CompletionEvidenceSha256 = string.Empty;
            }
            return settled;
        }).ToArray();
        if (rebindMachineIntent)
        {
            ledger.MachineSupportIntents = ledger.MachineSupportIntents
                .Select(row =>
                {
                    if (!string.Equals(
                            row.IntentId,
                            request.MachineSupportIntentId,
                            StringComparison.Ordinal))
                    {
                        return row;
                    }

                    var rebound = StrategyCommitmentLedgerSupport
                        .CloneMachineSupport(row);
                    rebound.Revision++;
                    rebound.SourceStateHash = snapshot.StateHash;
                    return rebound;
                })
                .ToArray();
        }
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        var settlements = consumptions.Select(consumption =>
        {
            var settled = ledger.MaterialReservations.Single(row =>
                row.ReservationId == consumption.MaterialReservationId);
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                settled.ReservationId,
                settled.Revision,
                settled.SourceDecisionId,
                settled.Status == StrategyCommitmentStatuses.Completed
                    ? "material_reservation_consumed"
                    : "material_reservation_partially_consumed",
                updatedAt,
                request.Reason);
            return new ReservationPortfolioMaterialSettlement
            {
                MaterialReservationId = settled.ReservationId,
                ConsumedQuantity = consumption.ConsumedQuantity,
                RemainingQuantity =
                    settled.Status == StrategyCommitmentStatuses.Completed
                        ? 0
                        : settled.Quantity,
                ReservationStatus = settled.Status
            };
        }).ToArray();
        foreach (var relocation in relocations)
        {
            var relocated = ledger.MaterialReservations.Single(row =>
                row.ReservationId == relocation.MaterialReservationId);
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                relocated.ReservationId,
                relocated.Revision,
                relocated.SourceDecisionId,
                "material_reservation_relocated",
                updatedAt,
                request.Reason);
        }
        var currencySettlements = currencyConsumptions.Select(consumption =>
        {
            var settled = ledger.CurrencyReservations.Single(row =>
                row.ReservationId == consumption.CurrencyReservationId);
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                settled.ReservationId,
                settled.Revision,
                settled.SourceDecisionId,
                settled.Status == StrategyCommitmentStatuses.Completed
                    ? "currency_reservation_consumed"
                    : "currency_reservation_partially_consumed",
                updatedAt,
                request.Reason);
            return new ReservationPortfolioCurrencySettlement
            {
                CurrencyReservationId = settled.ReservationId,
                ConsumedAmount = consumption.ConsumedAmount,
                RemainingAmount =
                    settled.Status == StrategyCommitmentStatuses.Completed
                        ? 0
                        : settled.Amount,
                ReservationStatus = settled.Status
            };
        }).ToArray();
        var mutatedIds = consumptions.Select(value =>
                value.MaterialReservationId)
            .Concat(relocations.Select(value => value.MaterialReservationId))
            .Concat(currencyConsumptions.Select(value =>
                value.CurrencyReservationId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var reservationId in rebindIds.Where(id =>
                     !mutatedIds.Contains(id)))
        {
            var material = ledger.MaterialReservations.SingleOrDefault(row =>
                row.ReservationId == reservationId);
            var currency = ledger.CurrencyReservations.SingleOrDefault(row =>
                row.ReservationId == reservationId);
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                reservationId,
                material?.Revision ?? currency!.Revision,
                material?.SourceDecisionId ?? currency!.SourceDecisionId,
                "reservation_rebound_after_supporting_transition",
                updatedAt,
                request.Reason);
        }
        if (rebindMachineIntent)
        {
            var intent = ledger.MachineSupportIntents.Single(row =>
                row.IntentId == request.MachineSupportIntentId);
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                intent.IntentId,
                intent.Revision,
                intent.SourceDecisionId,
                "machine_support_intent_rebound_after_supporting_transition",
                updatedAt,
                request.Reason);
        }
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            request.PortfolioId,
            ledger.Revision,
            request.RouteSourceDecisionId,
            "reservation_portfolio_supporting_transition_complete",
            updatedAt,
            request.SupportingTransitionReceiptSha256);
        return new ReservationPortfolioSupportingTransitionSettlementResult
        {
            Accepted = true,
            PortfolioId = request.PortfolioId,
            RouteSourceDecisionId = request.RouteSourceDecisionId,
            SupportingTransitionReceiptSha256 =
                request.SupportingTransitionReceiptSha256,
            CommittedLedgerRevision = ledger.Revision,
            CompletedMaterialReservationId = settlements.Length == 1 &&
                settlements[0].ReservationStatus ==
                    StrategyCommitmentStatuses.Completed
                    ? settlements[0].MaterialReservationId
                    : string.Empty,
            CompletedMaterialReservationIds = settlements.Where(value =>
                    value.ReservationStatus ==
                        StrategyCommitmentStatuses.Completed)
                .Select(value => value.MaterialReservationId)
                .ToArray(),
            ActiveMaterialReservationIds = settlements.Where(value =>
                    value.ReservationStatus ==
                        StrategyCommitmentStatuses.Active)
                .Select(value => value.MaterialReservationId)
                .Concat(relocations.Select(value =>
                    value.MaterialReservationId))
                .ToArray(),
            RelocatedMaterialReservationIds = relocations.Select(value =>
                    value.MaterialReservationId)
                .ToArray(),
            ConsumedQuantity = settlements.Sum(value =>
                value.ConsumedQuantity),
            MaterialSettlements = settlements,
            MaterialRelocations = relocations,
            CompletedCurrencyReservationIds = currencySettlements
                .Where(value => value.ReservationStatus ==
                    StrategyCommitmentStatuses.Completed)
                .Select(value => value.CurrencyReservationId)
                .ToArray(),
            ActiveCurrencyReservationIds = currencySettlements
                .Where(value => value.ReservationStatus ==
                    StrategyCommitmentStatuses.Active)
                .Select(value => value.CurrencyReservationId)
                .ToArray(),
            CurrencySettlements = currencySettlements,
            ReboundActiveReservationIds = rebindIds,
            ReboundMachineSupportIntentId = rebindMachineIntent
                ? request.MachineSupportIntentId
                : string.Empty,
            Ledger = ledger
        };
    }

    private static void ValidateSupportingTransitionSettlementRequest(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        ReservationPortfolioSupportingTransitionSettlementRequest request,
        ReservationPortfolioMaterialConsumption[] consumptions,
        ReservationPortfolioMaterialRelocation[] relocations,
        ReservationPortfolioCurrencyConsumption[] currencyConsumptions,
        string[] rebindIds,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.PortfolioId))
            errors.Add("reservation_portfolio_id_required");
        if (string.IsNullOrWhiteSpace(request.GoalId))
            errors.Add("goal_id_required");
        if (string.IsNullOrWhiteSpace(request.RouteSourceDecisionId))
            errors.Add("route_source_decision_id_required");
        if (!IsLowerSha256(request.SupportingTransitionReceiptSha256))
            errors.Add("supporting_transition_receipt_sha256_invalid");
        var mutationModeCount =
            (consumptions.Length > 0 ? 1 : 0) +
            (relocations.Length > 0 ? 1 : 0) +
            (currencyConsumptions.Length > 0 ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.MachineSupportIntentId)
                ? 1
                : 0);
        if (consumptions.Length > 0 && relocations.Length > 0)
        {
            errors.Add("supporting_transition_material_mutation_mode_invalid");
        }
        else if (mutationModeCount > 1 ||
                 (mutationModeCount == 0 && rebindIds.Length == 0))
        {
            errors.Add("supporting_transition_reservation_mutation_mode_invalid");
        }
        if (consumptions.Length > 0 && (
            consumptions.Any(value =>
                string.IsNullOrWhiteSpace(value.MaterialReservationId) ||
                string.IsNullOrWhiteSpace(value.NodeId) ||
                value.SlotIndex < 0 ||
                string.IsNullOrWhiteSpace(value.QualifiedItemId) ||
                value.ConsumedQuantity <= 0) ||
            consumptions.Select(value => value.MaterialReservationId)
                .Distinct(StringComparer.Ordinal).Count() !=
                consumptions.Length ||
            consumptions.Select(value => value.NodeId + ":" + value.SlotIndex)
                .Distinct(StringComparer.Ordinal).Count() !=
                consumptions.Length ||
            consumptions.Sum(value => (long)value.ConsumedQuantity) >
                int.MaxValue))
        {
            errors.Add("supporting_transition_consumed_claim_invalid");
        }
        if (relocations.Length > 0 && (
            relocations.Any(value =>
                string.IsNullOrWhiteSpace(value.MaterialReservationId) ||
                string.IsNullOrWhiteSpace(value.SourceNodeId) ||
                value.SourceSlotIndex < 0 ||
                string.IsNullOrWhiteSpace(value.DestinationNodeId) ||
                value.DestinationSlotIndex < 0 ||
                value.SourceNodeId == value.DestinationNodeId ||
                string.IsNullOrWhiteSpace(value.QualifiedItemId) ||
                value.Quantity <= 0) ||
            relocations.Select(value => value.MaterialReservationId)
                .Distinct(StringComparer.Ordinal).Count() !=
                relocations.Length ||
            relocations.Sum(value => (long)value.Quantity) > int.MaxValue))
        {
            errors.Add("supporting_transition_relocated_claim_invalid");
        }
        if (currencyConsumptions.Any(value =>
                string.IsNullOrWhiteSpace(value.CurrencyReservationId) ||
                !NativeShopCurrencies.TryGetKey(value.CurrencyId, out _) ||
                value.ConsumedAmount <= 0) ||
            currencyConsumptions.Select(value => value.CurrencyReservationId)
                .Distinct(StringComparer.Ordinal).Count() !=
                    currencyConsumptions.Length)
        {
            errors.Add("supporting_transition_currency_claim_invalid");
        }
        if (rebindIds.Any(string.IsNullOrWhiteSpace) ||
            rebindIds.Distinct(StringComparer.Ordinal).Count() !=
                rebindIds.Length)
        {
            errors.Add("supporting_transition_rebind_set_invalid");
        }
        var machineIntentMarkerPresent = !string.IsNullOrWhiteSpace(
            request.MachineSupportIntentId);
        if (machineIntentMarkerPresent !=
                !string.IsNullOrWhiteSpace(
                    request.MachineSupportIntentStage) ||
            machineIntentMarkerPresent !=
                (!string.IsNullOrWhiteSpace(
                     request.MachineSupportSourcesJson) &&
                 request.MachineSupportSourcesJson != "[]"))
        {
            errors.Add("supporting_transition_machine_intent_marker_invalid");
        }
        if (machineIntentMarkerPresent && rebindIds.Length > 0)
        {
            errors.Add(
                "supporting_transition_machine_intent_mixed_rebind_invalid");
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
            errors.Add("supporting_transition_reason_required");
        if (current is null)
        {
            errors.Add("reservation_portfolio_ledger_required");
            return;
        }
        var history = current.History ??
            Array.Empty<StrategyCommitmentHistoryEntry>();
        if (history.Count(row =>
                row.CommitmentId == request.PortfolioId &&
                row.SourceDecisionId == request.PortfolioId &&
                row.Operation == "reservation_portfolio_commit") != 1)
        {
            errors.Add("supporting_transition_portfolio_commit_not_found");
        }
        if (history.Any(row =>
                row.CommitmentId == request.PortfolioId &&
                row.Operation ==
                    "reservation_portfolio_supporting_transition_complete"))
        {
            errors.Add("supporting_transition_portfolio_already_settled");
        }
        foreach (var consumption in consumptions)
        {
            var claims = current.MaterialReservations.Where(row =>
                    row.ReservationId ==
                        consumption.MaterialReservationId)
                .ToArray();
            if (claims.Length != 1 ||
                claims[0].Status != StrategyCommitmentStatuses.Active ||
                claims[0].GoalId != request.GoalId ||
                claims[0].SourceDecisionId !=
                    request.RouteSourceDecisionId ||
                claims[0].NodeId != consumption.NodeId ||
                claims[0].SlotIndex != consumption.SlotIndex ||
                claims[0].QualifiedItemId !=
                    consumption.QualifiedItemId ||
                claims[0].Quantity < consumption.ConsumedQuantity)
            {
                errors.Add("supporting_transition_consumed_claim_mismatch");
            }
        }
        foreach (var relocation in relocations)
        {
            var claims = current.MaterialReservations.Where(row =>
                    row.ReservationId == relocation.MaterialReservationId)
                .ToArray();
            if (claims.Length != 1 ||
                claims[0].Status != StrategyCommitmentStatuses.Active ||
                claims[0].GoalId != request.GoalId ||
                claims[0].SourceDecisionId !=
                    request.RouteSourceDecisionId ||
                claims[0].NodeId != relocation.SourceNodeId ||
                claims[0].SlotIndex != relocation.SourceSlotIndex ||
                claims[0].QualifiedItemId !=
                    relocation.QualifiedItemId ||
                claims[0].Quantity != relocation.Quantity)
            {
                errors.Add("supporting_transition_relocated_claim_mismatch");
            }
        }
        ValidateRelocationDestinations(
            current,
            snapshot,
            relocations,
            errors);
        foreach (var consumption in currencyConsumptions)
        {
            var claims = current.CurrencyReservations.Where(row =>
                    row.ReservationId == consumption.CurrencyReservationId)
                .ToArray();
            if (claims.Length != 1 ||
                claims[0].Status != StrategyCommitmentStatuses.Active ||
                claims[0].GoalId != request.GoalId ||
                claims[0].SourceDecisionId !=
                    request.RouteSourceDecisionId ||
                claims[0].CurrencyId != consumption.CurrencyId ||
                claims[0].Amount < consumption.ConsumedAmount)
            {
                errors.Add("supporting_transition_currency_claim_mismatch");
            }
        }
        foreach (var reservationId in rebindIds)
        {
            var material = current.MaterialReservations.Where(row =>
                    row.ReservationId == reservationId)
                .ToArray();
            var currency = current.CurrencyReservations.Where(row =>
                    row.ReservationId == reservationId)
                .ToArray();
            var materialConsumption = consumptions.SingleOrDefault(value =>
                value.MaterialReservationId == reservationId);
            var currencyConsumption = currencyConsumptions
                .SingleOrDefault(value =>
                    value.CurrencyReservationId == reservationId);
            if (material.Length + currency.Length != 1 ||
                material.Any(row =>
                    row.Status != StrategyCommitmentStatuses.Active ||
                    row.GoalId != request.GoalId ||
                    row.SourceDecisionId !=
                        request.RouteSourceDecisionId ||
                    materialConsumption is not null &&
                    materialConsumption.ConsumedQuantity >= row.Quantity) ||
                currency.Any(row =>
                    row.Status != StrategyCommitmentStatuses.Active ||
                    row.GoalId != request.GoalId ||
                    row.SourceDecisionId !=
                        request.RouteSourceDecisionId ||
                    currencyConsumption is not null &&
                    currencyConsumption.ConsumedAmount >= row.Amount))
            {
                errors.Add("supporting_transition_rebind_claim_mismatch");
            }
        }
        if (machineIntentMarkerPresent)
        {
            var intents = current.MachineSupportIntents.Where(row =>
                    row.IntentId == request.MachineSupportIntentId)
                .ToArray();
            if (intents.Length != 1 ||
                intents[0].Status != StrategyCommitmentStatuses.Active ||
                intents[0].GoalId != request.GoalId ||
                intents[0].SourceDecisionId !=
                    request.RouteSourceDecisionId ||
                intents[0].Stage != request.MachineSupportIntentStage ||
                intents[0].SupportSourcesJson !=
                    request.MachineSupportSourcesJson)
            {
                errors.Add(
                    "supporting_transition_machine_intent_marker_mismatch");
            }
        }
    }

    private static void ValidateRelocationDestinations(
        StrategyCommitmentLedger current,
        SnapshotEnvelope snapshot,
        ReservationPortfolioMaterialRelocation[] relocations,
        ICollection<string> errors)
    {
        if (relocations.Length == 0)
            return;

        var value = ReadStateFieldValue(
            snapshot,
            "farm",
            "material_inventory_graph");
        MaterialInventoryGraph? graph = null;
        try
        {
            if (value is { ValueKind: JsonValueKind.Object })
            {
                graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(
                    value.Value.GetRawText());
            }
        }
        catch (JsonException)
        {
            // The shared error below is stable for missing and malformed graphs.
        }
        var graphMatchesActor = ReadableStatus(ReadStateFieldStatus(
                snapshot,
                "farm",
                "material_inventory_graph")) &&
            long.TryParse(
                StrategyCommitmentLedgerSupport.PlayerId(snapshot),
                out var actorPlayerId) &&
            graph is
            {
                SchemaVersion: "material_inventory_graph.v1",
                Status: "available"
            } &&
            graph.PlayerId == actorPlayerId;
        var inventoryNodes = graph?.InventoryNodes ??
            Array.Empty<MaterialInventoryNode>();
        var relocatingIds = relocations.Select(row => row.MaterialReservationId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var group in relocations.GroupBy(row => (
                     row.DestinationNodeId,
                     row.DestinationSlotIndex)))
        {
            var qualifiedItemIds = group
                .Select(row => row.QualifiedItemId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var qualifiedItemId = qualifiedItemIds.Length == 1
                ? qualifiedItemIds[0]
                : string.Empty;
            var nodes = inventoryNodes.Where(node =>
                    node.NodeId == group.Key.DestinationNodeId &&
                    node.InventoryKind == "player_inventory" &&
                    node.SupplyState == "available" &&
                    node.ActorUseAuthorized &&
                    node.OwnerPlayerId == graph!.PlayerId)
                .ToArray();
            var slots = nodes.Length == 1
                ? (nodes[0].Slots ?? Array.Empty<MaterialInventorySlot>())
                    .Where(slot =>
                        slot.SlotIndex == group.Key.DestinationSlotIndex &&
                        slot.QualifiedItemId == qualifiedItemId)
                    .ToArray()
                : Array.Empty<MaterialInventorySlot>();
            var otherDestinationClaims = current.MaterialReservations
                .Where(row =>
                    row.Status == StrategyCommitmentStatuses.Active &&
                    !relocatingIds.Contains(row.ReservationId) &&
                    row.NodeId == group.Key.DestinationNodeId &&
                    row.SlotIndex == group.Key.DestinationSlotIndex)
                .ToArray();
            var otherReserved = otherDestinationClaims.Where(row =>
                    row.QualifiedItemId == qualifiedItemId)
                .Sum(row => (long)row.Quantity);
            var incoming = group.Sum(row => (long)row.Quantity);
            if (!graphMatchesActor ||
                qualifiedItemIds.Length != 1 ||
                nodes.Length != 1 ||
                slots.Length != 1 ||
                otherDestinationClaims.Any(row =>
                    row.QualifiedItemId != qualifiedItemId) ||
                otherReserved + incoming > slots[0].Stack)
            {
                errors.Add(
                    "supporting_transition_relocation_destination_unavailable");
            }
        }
    }

    private static ReservationPortfolioMaterialConsumption[]
        MaterialConsumptions(
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            ICollection<string> errors)
    {
        var values = NonNullEntries(
            request.MaterialConsumptions,
            "supporting_transition_consumed_claim_invalid",
            errors);
        var legacyPresent =
            !string.IsNullOrWhiteSpace(request.MaterialReservationId) ||
            !string.IsNullOrWhiteSpace(request.NodeId) ||
            request.SlotIndex != 0 ||
            !string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            request.ConsumedQuantity != 0;
        if (values.Length > 0)
        {
            if (legacyPresent)
                errors.Add("supporting_transition_consumption_forms_mixed");
            return values;
        }
        return legacyPresent
            ? new[]
            {
                new ReservationPortfolioMaterialConsumption
                {
                    MaterialReservationId = request.MaterialReservationId,
                    NodeId = request.NodeId,
                    SlotIndex = request.SlotIndex,
                    QualifiedItemId = request.QualifiedItemId,
                    ConsumedQuantity = request.ConsumedQuantity
                }
            }
            : Array.Empty<ReservationPortfolioMaterialConsumption>();
    }

    private static T[] NonNullEntries<T>(
        T[]? values,
        string error,
        ICollection<string> errors)
        where T : class
    {
        var supplied = values ?? Array.Empty<T>();
        if (supplied.Any(value => value is null))
            errors.Add(error);
        return supplied.OfType<T>().ToArray();
    }

    private static ReservationPortfolioSupportingTransitionSettlementResult
        SupportingTransitionSettlementRejected(
            StrategyCommitmentLedger? ledger,
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            ReservationPortfolioMaterialConsumption[] consumptions,
            ReservationPortfolioMaterialRelocation[] relocations,
            ReservationPortfolioCurrencyConsumption[] currencyConsumptions,
            string[] rebindIds,
            IEnumerable<string> errors) => new()
            {
                Accepted = false,
                PortfolioId = request.PortfolioId,
                RouteSourceDecisionId = request.RouteSourceDecisionId,
                SupportingTransitionReceiptSha256 =
                    request.SupportingTransitionReceiptSha256,
                CompletedMaterialReservationId =
                    consumptions.Length == 1
                        ? consumptions[0].MaterialReservationId
                        : string.Empty,
                ConsumedQuantity = SafeConsumedQuantity(consumptions),
                RelocatedMaterialReservationIds = relocations.Select(value =>
                        value.MaterialReservationId)
                    .ToArray(),
                MaterialRelocations = relocations,
                CurrencySettlements = currencyConsumptions.Select(value =>
                        new ReservationPortfolioCurrencySettlement
                        {
                            CurrencyReservationId =
                                value.CurrencyReservationId,
                            ConsumedAmount = value.ConsumedAmount
                        })
                    .ToArray(),
                ReboundActiveReservationIds = rebindIds,
                ReboundMachineSupportIntentId =
                    request.MachineSupportIntentId,
                Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
                Ledger = ledger
            };

    private static int SafeConsumedQuantity(
        IEnumerable<ReservationPortfolioMaterialConsumption> consumptions)
    {
        var total = consumptions.Sum(value =>
            (long)value.ConsumedQuantity);
        return total is >= 0 and <= int.MaxValue ? (int)total : 0;
    }
}
