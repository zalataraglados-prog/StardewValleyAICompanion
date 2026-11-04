using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.Strategy;

public static partial class MuseumOutputCustodyTransferRequestBuilder
{
    public static MuseumOutputCustodyTransferCompilation Compile(SnapshotEnvelope snapshot, StrategyCommitmentLedger before,
        string reservationId, string destinationNodeId, ReservationPortfolioCommitResult committed)
    {
        var request = Build(snapshot, before, reservationId, destinationNodeId);
        if (request.Request is null || request.Candidate is null) return new(null, null, request.Errors);
        if (!committed.Accepted || committed.Ledger is null)
            return new(null, null, new[] { "museum_custody_transfer_commit_not_accepted" });
        var expected = new ReservationPortfolioLedgerService().Commit(before, snapshot, request.Request, committed.Ledger.UpdatedAt);
        if (!expected.Accepted || JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(committed))
            return new(null, null, new[] { "museum_custody_transfer_commit_replay_drifted" });
        var ledger = committed.Ledger;
        var custody = ledger.MaterialReservations.Single(row => row.ReservationId == reservationId);
        var plan = BuildMechanicalPlan(request.Candidate, snapshot, custody.GoalId);
        plan.SourceModel = "deterministic_output_custody_control";
        plan.PlanId = "museum_custody_transfer_plan." + Hash(new { request.Request, ledger.PreparedMaterialTransfer!.BindingSha256 });
        foreach (var step in plan.Steps.Where(step => step.Kind == "transfer_material"))
            step.Parameters = step.Parameters.Concat(MuseumOutputCustodyTransferBinding.Parameters(ledger, custody)).ToArray();
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        ActionQueueIdentity.Stabilize(queue, plan.PlanId, "museum_custody_transfer");
        var errors = new List<string>();
        errors.AddRange(plan.CandidateAudit.Where(row => row.Decision != "accepted").SelectMany(row => row.Reasons));
        errors.AddRange(queue.CompilerDiagnostics);
        errors.AddRange(queue.Items.SelectMany(item => item.BlockingReasons.Concat(item.MissingStateFactors)));
        if (plan.Steps.Length == 0 || queue.Status != "pending" || queue.Items.Length == 0 || queue.Items.Any(item => item.Status != "pending"))
            errors.Add("museum_custody_transfer_compilation_not_dispatchable");
        var transfers = queue.Items.Where(item => item.OptionId == "executor.transfer_material").ToArray();
        if (transfers.Length != 1 || !MuseumOutputCustodyTransferBinding.IsBound(transfers[0], ledger))
            errors.Add("museum_custody_transfer_compiled_binding_invalid");
        else errors.AddRange(new ActionQueueDispatchReadinessService().Evaluate(queue, transfers[0], ledger, snapshot.StateHash).BlockingReasons);
        return errors.Count == 0 ? new(plan, queue, Array.Empty<string>()) : new(null, null, errors.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static SmallModelPlanEnvelope BuildMechanicalPlan(PolicyEventCandidatePrediction candidate, SnapshotEnvelope snapshot, string goal) =>
        new DailyPlanCompiler().Compile(new[] { candidate }, snapshot.StateHash, goalId: goal, maxCandidates: 1,
            availableMinutes: AvailableMinutes(snapshot));
}

public sealed record MuseumOutputCustodyTransferCompilation(
    SmallModelPlanEnvelope? Plan, ActionQueueEnvelope? Queue, string[] Errors);
