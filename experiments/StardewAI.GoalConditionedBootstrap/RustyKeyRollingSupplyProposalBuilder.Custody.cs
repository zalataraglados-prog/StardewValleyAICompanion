using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyRollingSupplyProposalBuilder
{
    private sealed record CustodyChoice(PolicyEventCandidatePrediction Candidate, RustyKeyRollingCustodySelection Selection,
        SmallModelPlanEnvelope? Plan, ActionQueueEnvelope? Queue);

    private static bool TryStoredCustody(RustyKeyRollingSupplyProposal result,
        RustyKeyAcquisitionPortfolioBuilder.RustyKeySourceScope source, SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger, List<RustyKeyRollingCandidateEvaluation> evaluations, string routeTimingCalibrationPath)
    {
        if (!MaterialInventoryGraphReader.TryRead(snapshot, out var graph)) return false;
        var players = graph!.InventoryNodes.Where(node => node.InventoryKind == "player_inventory" &&
            node.ActorUseAuthorized && node.OwnerPlayerId.ToString(CultureInfo.InvariantCulture) == ledger.PlayerId).ToArray();
        if (players.Length != 1) return false;
        var player = players[0];
        var missing = source.Museum.Groups.Where(group => !source.Progress.Requirements[group.RequirementId].Completed)
            .ToDictionary(group => group.Alternatives[0].QualifiedItemId, group => group.RequirementId, StringComparer.Ordinal);
        var choices = new List<CustodyChoice>();
        foreach (var custody in ledger.MaterialReservations.Where(row => row.NodeId != player.NodeId &&
                     missing.ContainsKey(row.QualifiedItemId) && MaterialOutputCustodyContract.IsAuthorized(row, ledger,
                         MaterialOutputCustodyContract.MuseumConsumer, result.GoalId)).OrderBy(row => row.ReservationId, StringComparer.Ordinal))
        {
            if (!MaterialOutputCustodyContract.TryGetPositionBinding(custody, ledger, out var position)) continue;
            var transfer = MuseumOutputCustodyTransferRequestBuilder.Build(snapshot, ledger, custody.ReservationId, player.NodeId);
            var kind = "custody_transfer";
            var candidate = transfer.Candidate;
            SmallModelPlanEnvelope? plan = null;
            ActionQueueEnvelope? queue = null;
            var errors = transfer.Errors;
            if (transfer.Request is null)
            {
                var approach = MuseumOutputCustodyTransferRequestBuilder.BuildApproach(snapshot, ledger, custody.ReservationId, player.NodeId);
                if (approach.Queue is not null)
                {
                    kind = "custody_navigation";
                    candidate = approach.Candidate;
                    plan = approach.Plan;
                    queue = approach.Queue;
                    errors = approach.Errors;
                }
            }
            var ready = candidate is not null && errors.Length == 0;
            var candidateId = candidate?.CandidateId ?? "custody-transfer:" + custody.ReservationId;
            evaluations.Add(new(candidateId, missing[custody.QualifiedItemId], kind, ready, errors));
            if (ready) choices.Add(new(candidate!, new(kind, missing[custody.QualifiedItemId], custody.ReservationId,
                player.NodeId, position, kind == "custody_transfer" ? transfer.Request : null), plan, queue));
        }
        var selected = choices.OrderBy(row => row.Candidate.EstimatedTicks)
            .ThenBy(row => row.Candidate.EnergyCost).ThenBy(row => row.Candidate.CandidateId, StringComparer.Ordinal)
            .ThenBy(row => row.Selection.ReservationId, StringComparer.Ordinal).FirstOrDefault();
        if (selected is null) return false;
        result.SelectedCandidateId = selected.Candidate.CandidateId;
        result.SelectedCustodyTransfer = selected.Selection;
        result.SelectedCustodyDonationSchedule = RustyKeyCustodyDonationScheduleBuilder.Build(result.GoalId, snapshot, ledger, graph!,
            selected.Selection, selected.Candidate, DonationRouteScheduler(snapshot, routeTimingCalibrationPath));
        result.CompiledPlan = selected.Plan;
        result.CompiledQueue = selected.Queue;
        result.RollingStepReady = true;
        result.Status = "ready_" + selected.Selection.Kind + "_step_pending_dispatch";
        return true;
    }
}
