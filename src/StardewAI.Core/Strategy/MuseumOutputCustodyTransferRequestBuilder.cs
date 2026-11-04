using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public static partial class MuseumOutputCustodyTransferRequestBuilder
{
    public static MuseumOutputCustodyTransferPreparation Build(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        string reservationId, string destinationNodeId)
    {
        var errors = new List<string>();
        if (ledger.PreparedMaterialTransfer is not null)
            return new(null, null, new[] { "museum_custody_transfer_preparation_already_pending" });
        var rows = ledger.MaterialReservations.Where(row => row.ReservationId == reservationId).ToArray();
        if (rows.Length != 1 || !MaterialOutputCustodyContract.TryGetPositionBinding(rows[0], ledger, out _))
            return new(null, null, new[] { "museum_custody_transfer_source_not_authorized" });
        var custody = rows[0];
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.SaveId.Value != ledger.SaveId ||
            snapshot.PlayerId.Status != FieldStatus.Available || snapshot.PlayerId.Value != ledger.PlayerId ||
            string.IsNullOrWhiteSpace(snapshot.StateHash) ||
            !MaterialInventoryGraphReader.TryRead(snapshot, out var graph) ||
            graph!.PlayerId.ToString(CultureInfo.InvariantCulture) != ledger.PlayerId)
            return new(null, null, new[] { "museum_custody_transfer_snapshot_identity_invalid" });
        var nodes = graph.InventoryNodes.Where(node => node.NodeId == custody.NodeId).ToArray();
        var slots = nodes.Length == 1 ? nodes[0].Slots.Where(slot => slot.SlotIndex == custody.SlotIndex &&
            slot.QualifiedItemId == custody.QualifiedItemId).ToArray() : Array.Empty<MaterialInventorySlot>();
        if (slots.Length != 1)
            return new(null, null, new[] { "museum_custody_transfer_source_slot_not_unique" });
        var intent = new MaterialTransferIntent
        {
            SourceNodeId = custody.NodeId, SourceSlotIndex = custody.SlotIndex, DestinationNodeId = destinationNodeId,
            QualifiedItemId = custody.QualifiedItemId, Quantity = custody.Quantity,
            Quality = slots[0].Quality, ExpectedSourceStack = slots[0].Stack
        };
        var physical = new MaterialTransferProjector().Project(graph, intent);
        errors.AddRange(physical.BlockingReasons);
        if (physical.DestinationSlotChanges.Length != 1)
            errors.Add("museum_custody_transfer_whole_unit_destination_not_unique");
        if (errors.Count > 0) return new(null, null, errors.Distinct(StringComparer.Ordinal).ToArray());
        var move = new ReservationPortfolioMaterialRelocation
        {
            MaterialReservationId = custody.ReservationId, SourceNodeId = custody.NodeId, SourceSlotIndex = custody.SlotIndex,
            DestinationNodeId = destinationNodeId, DestinationSlotIndex = physical.DestinationSlotChanges[0].SlotIndex,
            QualifiedItemId = custody.QualifiedItemId, Quantity = custody.Quantity
        };
        var projection = new MaterialTransferProjector().ProjectReservations(graph, intent, ledger, new[] { move });
        errors.AddRange(projection.BlockingReasons);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = MaterialTransferIntentBinder.ToParameters(intent) }
        }, includeExecutorCalibrationOptions: true, commitmentLedger: ledger);
        var candidates = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, custody.GoalId)
            .Where(row => row.OptionId == "inventory.transfer_item" && row.Kind == "transfer_inventory_item" && row.Available).ToArray();
        if (candidates.Length != 1)
        {
            errors.AddRange(availability.Options.SelectMany(option => option.BlockingReasons.Concat(
                option.EventCandidates.SelectMany(candidate => candidate.BlockReasons))));
            errors.Add("museum_custody_transfer_candidate_not_uniquely_available");
        }
        if (errors.Count > 0) return new(null, null, errors.Distinct(StringComparer.Ordinal).ToArray());
        if (!AvailableMinutes(snapshot).HasValue)
            return new(null, null, new[] { "museum_custody_transfer_clock_unavailable" });
        var plan = BuildMechanicalPlan(candidates[0], snapshot, custody.GoalId);
        if (plan.Steps.Length == 0 || plan.CandidateAudit.Any(row => row.Decision != "accepted"))
            return new(null, null, plan.CandidateAudit.SelectMany(row => row.Reasons)
                .Append("museum_custody_transfer_does_not_fit_day_budget").Distinct(StringComparer.Ordinal).ToArray());
        MaterialOutputCustodyContract.TryGetPositionBinding(custody, ledger, out var position);
        var identity = Hash(new { Kind = MuseumOutputCustodyTransferBinding.Kind, ledger.LedgerId, ledger.Revision,
            snapshot.StateHash, custody.ReservationId, Position = position, Intent = intent });
        var portfolio = "museum_custody_transfer." + identity;
        return new(new ReservationPortfolioCommitRequest
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = portfolio, SourceDecisionId = portfolio, GoalId = custody.GoalId,
            MaterialTransferPreparation = new MaterialTransferPreparationRequest
            {
                RouteSourceDecisionId = custody.SourceDecisionId, Intent = intent, MaterialRelocations = new[] { move }
            }
        }, candidates[0], Array.Empty<string>());
    }

    internal static string Hash(object value)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))).Select(item => item.ToString("x2")));
    }

    private static int? AvailableMinutes(SnapshotEnvelope snapshot)
    {
        var clocks = new[] { "time", "time_of_day" }
            .Where(field => ReadableStatus(ReadStateFieldStatus(snapshot, "time", field)))
            .Select(field => ReadStateFieldIntOptional(snapshot, "time", field)).ToArray();
        if (clocks.Any(value => !value.HasValue)) return null;
        var values = clocks.Select(value => value!.Value).Distinct().ToArray();
        return values.Length == 1 && values[0] is >= 600 and <= GameClockBudgetPolicy.DayEndTime && values[0] % 100 < 60
            ? GameClockBudgetPolicy.ClockMinutesBetween(values[0], GameClockBudgetPolicy.DayEndTime) : null;
    }
}

public sealed record MuseumOutputCustodyTransferPreparation(
    ReservationPortfolioCommitRequest? Request, PolicyEventCandidatePrediction? Candidate, string[] Errors);
