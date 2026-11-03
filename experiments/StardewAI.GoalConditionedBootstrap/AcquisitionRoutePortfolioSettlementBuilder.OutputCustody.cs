using System.Globalization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioSettlementBuilder
{
    internal static ReservationPortfolioOutputCustodyClaim[]? BuildOutputCustodyClaims(
        AcquisitionRouteExecutionBinding binding, AcquisitionRouteFreshTerminalReceiptAdmission fresh,
        SnapshotEnvelope after, StrategyCommitmentLedger ledger, string[] completedInputIds)
    {
        if (binding.RequirementSetId != "museum_collection") return null;
        var transition = fresh.TerminalTransition;
        Require(binding.DispatchBindingReady && fresh.FreshTerminalReceiptVerified &&
            fresh.RouteOccurrenceId == binding.RouteOccurrenceId && fresh.GoalId == binding.GoalId &&
            binding.RequiredAmount == 1 && binding.MinimumQuality == 0 &&
            transition is { Resolved: true, Verified: true, RequiredAmount: 1 } &&
            transition.QualifiedItemId == binding.QualifiedItemId && transition.QuantityIncrease >= 1,
            "Museum output custody requires the exact independently verified native item gain.");
        Require(MaterialInventoryGraphReader.TryRead(after, out var graph) && graph is not null &&
            graph.PlayerId.ToString(CultureInfo.InvariantCulture) == ledger.PlayerId,
            "Museum output custody requires the fresh actor-bound material graph.");
        var completed = completedInputIds.ToHashSet(StringComparer.Ordinal);
        var supply = new MaterialSupplyProjection().Project(graph!, ledger.MaterialReservations
            .Where(row => row.Status == StrategyCommitmentStatuses.Active && !completed.Contains(row.ReservationId)).ToArray());
        Require(supply.Status == "available", "Museum output custody cannot bypass another goal's material protection.");
        var slot = supply.Slots.Where(row => row.NodeId == "player:" + ledger.PlayerId &&
                row.QualifiedItemId == binding.QualifiedItemId && row.AvailableQuantity >= 1)
            .OrderBy(row => row.SlotIndex).FirstOrDefault();
        Require(slot is not null, "The verified museum output has no safe current player-inventory unit to retain.");
        return new[] { new ReservationPortfolioOutputCustodyClaim
        {
            NodeId = slot!.NodeId, SlotIndex = slot.SlotIndex, QualifiedItemId = binding.QualifiedItemId,
            Quantity = 1, ConsumerOptionId = MaterialOutputCustodyContract.MuseumConsumer
        } };
    }
}
