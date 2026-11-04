using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Strategy;

public static partial class MuseumOutputCustodyTransferRequestBuilder
{
    public static MuseumOutputCustodyApproachPreparation BuildApproach(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        string reservationId, string destinationNodeId)
    {
        var source = ReadSource(snapshot, ledger, reservationId, destinationNodeId, out var sourceErrors);
        if (source is null) return new(null, null, null, sourceErrors);
        if (!AvailableMinutes(snapshot).HasValue)
            return new(null, null, null, new[] { "museum_custody_transfer_clock_unavailable" });
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = "inventory.transfer_item", Parameters = MaterialTransferIntentBinder.ToParameters(source.Intent)
                    .Append(MuseumOutputCustodyTransferBinding.Parameter(MaterialTransferIntentBinder.PhaseParameter,
                        MaterialTransferIntentBinder.ApproachPhase)).ToArray()
            }
        }, includeExecutorCalibrationOptions: true, commitmentLedger: ledger);
        var candidates = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, source.Custody.GoalId)
            .Where(row => (row.Available && row.AllowedToday != false && row.TimelineStatus != "blocked" && row.BlockReasons.Length == 0 ||
                    ScheduledRouteWaitPolicy.IsDeferredNativeGate(row)) &&
                row.Parameters.Any(value => value.Name == "material_transfer.approach_only" && value.Value == "true")).ToArray();
        if (candidates.Length != 1)
            return new(null, null, null, availability.Options.SelectMany(option => option.BlockingReasons.Concat(option.MissingStateFactors).Concat(
                option.EventCandidates.SelectMany(row => row.BlockReasons))).Append("museum_custody_approach_not_uniquely_schedulable")
                .Distinct(StringComparer.Ordinal).ToArray());
        var candidate = candidates[0];
        var plan = BuildMechanicalPlan(candidate, snapshot, source.Custody.GoalId);
        plan.SourceModel = "deterministic_output_custody_control";
        var identity = Hash(new { ledger.LedgerId, ledger.Revision, snapshot.StateHash, reservationId, destinationNodeId,
            source.Position, candidate.CandidateId, Kind = "custody_approach" });
        plan.PlanId = "museum_custody_approach_plan." + identity;
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        ActionQueueIdentity.Stabilize(queue, identity, "museum_custody_approach");
        var errors = new List<string>(plan.CandidateAudit.Where(row => row.Decision != "accepted").SelectMany(row => row.Reasons));
        errors.AddRange(queue.CompilerDiagnostics);
        errors.AddRange(queue.Items.SelectMany(row => row.BlockingReasons.Concat(row.MissingStateFactors)));
        if (queue.Status != "pending" || queue.Items.Length == 0 || queue.Items.Any(row => row.Status != "pending" || row.OptionId == "executor.transfer_material"))
            errors.Add("museum_custody_approach_not_dispatchable");
        return errors.Count == 0 ? new(candidate, plan, queue, Array.Empty<string>()) :
            new(null, null, null, errors.Distinct(StringComparer.Ordinal).ToArray());
    }
}

public sealed record MuseumOutputCustodyApproachPreparation(PolicyEventCandidatePrediction? Candidate,
    SmallModelPlanEnvelope? Plan, ActionQueueEnvelope? Queue, string[] Errors);
