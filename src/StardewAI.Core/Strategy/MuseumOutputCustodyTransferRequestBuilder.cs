using System;
using System.Collections.Generic;
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

namespace StardewAI.Core.Strategy;

public static partial class MuseumOutputCustodyTransferRequestBuilder
{
    public static MuseumOutputCustodyTransferPreparation Build(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        string reservationId, string destinationNodeId)
    {
        var source = ReadSource(snapshot, ledger, reservationId, destinationNodeId, out var sourceErrors);
        if (source is null) return new(null, null, sourceErrors);
        var (custody, graph, intent, position) = source;
        var errors = new List<string>();
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

    private static int? AvailableMinutes(SnapshotEnvelope snapshot) =>
        SnapshotGameClockBudgetReader.AvailableMinutes(snapshot, GameClockBudgetPolicy.DayEndTime);
}

public sealed record MuseumOutputCustodyTransferPreparation(
    ReservationPortfolioCommitRequest? Request, PolicyEventCandidatePrediction? Candidate, string[] Errors);
