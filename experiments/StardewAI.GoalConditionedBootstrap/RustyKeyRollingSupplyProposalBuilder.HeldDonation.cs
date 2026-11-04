using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyRollingSupplyProposalBuilder
{
    private const string DonationRouteSelectionPolicyId = "deterministic_control.safe_museum_route_completion_elapsed_stable_id.v1";

    private static bool TryHeldDonation(
        RustyKeyRollingSupplyProposal result, RustyKeyAcquisitionPortfolioBuilder.RustyKeySourceScope source,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        List<RustyKeyRollingCandidateEvaluation> evaluations, string routeTimingCalibrationPath)
    {
        var missing = source.Museum.Groups.Where(group => !source.Progress.Requirements[group.RequirementId].Completed)
            .ToDictionary(group => group.Alternatives[0].QualifiedItemId, group => group.RequirementId, StringComparer.Ordinal);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { "museum.donate_items" }, commitmentLedger: ledger);
        var ranking = new AvailabilityAwarePolicyPredictionEnvelope
        {
            Availability = availability,
            RankedEventCandidates = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, result.GoalId)
        };
        var current = CurrentTeacherFrontierSupport.ReadCurrentCandidates(ranking)
            .Concat(ranking.RankedEventCandidates.Where(ScheduledRouteWaitPolicy.IsDeferredNativeGate))
            .Where(candidate => missing.ContainsKey(candidate.QualifiedItemId))
            .OrderBy(candidate => candidate.Kind == "donate_museum_item" ? 0 : 1)
            .ThenBy(candidate => candidate.EstimatedTicks is >= 0 ? candidate.EstimatedTicks : int.MaxValue)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray();
        var currentIds = current.Select(candidate => candidate.CandidateId).ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in availability.Options.SelectMany(option => option.EventCandidates)
                     .Where(candidate => missing.ContainsKey(candidate.QualifiedItemId) && !currentIds.Contains(candidate.CandidateId)))
            evaluations.Add(new(candidate.CandidateId, missing[candidate.QualifiedItemId], "held_donation", false,
                candidate.BlockReasons.Length > 0 ? candidate.BlockReasons : new[] { "held_donation_not_currently_schedulable" }));

        var planCompiler = new DailyPlanCompiler();
        var actionCompiler = new ActionQueueCompiler();
        var availableMinutes = SnapshotGameClockBudgetReader.AvailableMinutes(snapshot, GameClockBudgetPolicy.AutonomousRecoveryStartTime);
        var routeScheduler = DonationRouteScheduler(snapshot, routeTimingCalibrationPath);
        var selectedCompletion = int.MaxValue;
        var baseSelectionPolicyId = result.SelectionPolicyId;
        foreach (var candidate in current)
        {
            var reasons = new List<string>();
            if (!availableMinutes.HasValue)
            {
                evaluations.Add(new(candidate.CandidateId, missing[candidate.QualifiedItemId], "held_donation", false,
                    new[] { "rusty_key_held_donation_clock_unavailable" }));
                continue;
            }
            if (!CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(candidate, "expected_donated_count_before", out var before) ||
                before != result.DonatedCount)
                reasons.Add("rusty_key_held_donation_progress_drifted");
            var schedule = candidate.Kind == "route_connector_tile" ? routeScheduler.Value.Build(candidate) : null;
            if (schedule is { CurrentDayRouteReady: false })
            {
                evaluations.Add(new(candidate.CandidateId, missing[candidate.QualifiedItemId], "held_donation", false, schedule.BlockingReasons));
                continue;
            }
            var selectionPolicyId = schedule is null ? baseSelectionPolicyId : DonationRouteSelectionPolicyId;
            var neutral = CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(candidate, selectionPolicyId);
            var plan = planCompiler.Compile(new[] { neutral }, snapshot.StateHash, goalId: result.GoalId, maxCandidates: 1,
                availableMinutes: availableMinutes);
            var identity = AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(new
            {
                result.GoalId, SelectionPolicyId = selectionPolicyId, result.SnapshotSha256, result.RequirementInventorySha256,
                result.StrategyLedgerSha256, candidate.CandidateId
            });
            plan.PlanId = "rusty_key_donation_plan." + identity;
            var queue = actionCompiler.Compile(plan, snapshot, ledger);
            AcquisitionRouteDispatchCompilationBuilder.StabilizeQueueIdentity(queue, identity, "rusty_key_donation");
            reasons.AddRange(CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue));
            if (plan.Steps.Length == 0 || queue.Status != "pending" || queue.Items.Length == 0 ||
                queue.Items.Any(item => item.Status != "pending"))
                reasons.Add("rusty_key_held_donation_not_dispatchable");
            var ready = reasons.Count == 0;
            evaluations.Add(new(candidate.CandidateId, missing[candidate.QualifiedItemId], "held_donation", ready,
                reasons.Distinct(StringComparer.Ordinal).ToArray()));
            var completion = schedule?.DonationCompletionByTime ??
                (candidate.Kind == "donate_museum_item" ? int.MinValue : int.MaxValue - 1);
            if (!ready || result.RollingStepReady && completion >= selectedCompletion) continue;
            selectedCompletion = completion;
            result.SelectedCandidateId = candidate.CandidateId;
            result.CompiledPlan = plan;
            result.CompiledQueue = queue;
            result.SelectedDonationRouteSchedule = schedule;
            result.SelectionPolicyId = selectionPolicyId;
            result.RollingStepReady = true;
            result.Status = "ready_safe_held_donation_step_pending_dispatch";
        }
        return result.RollingStepReady;
    }
}
