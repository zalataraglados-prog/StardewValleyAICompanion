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
    private static bool TryOwnedSupplyDeferral(RustyKeyRollingSupplyProposal result, SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger, string[] pending, HashSet<string> temporalRequirements, bool resumeOnly = false)
    {
        if (pending.Length < result.RemainingDonationCount || SnapshotGameClockBudgetReader.CurrentTime(snapshot) is not { } time)
            return false;
        if (time < GameClockBudgetPolicy.AutonomousRecoveryStartTime && NativeControlSettleRecovery.TryCreate(snapshot) is null)
            return false;
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { "recovery.stabilize_day" }, commitmentLedger: ledger);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, result.GoalId);
        // Exact native continuation is not permission to close arbitrary menus or force an early save boundary.
        var continuation = ranked.Where(candidate => candidate.Available && candidate.BlockReasons.Length == 0 &&
            (candidate.CandidateId == NativeControlSettleRecovery.CandidateId && candidate.Kind == NativeControlSettleRecovery.CandidateKind ||
             time >= GameClockBudgetPolicy.AutonomousRecoveryStartTime && candidate.CandidateId == "recovery:resume_sleep_prompt" &&
             candidate.Kind == "recovery_resume_sleep_prompt")).ToArray();
        var nextDay = true;
        PolicyEventCandidatePrediction? selected = null;
        if (continuation.Length == 1)
        {
            selected = continuation[0];
            nextDay = selected.Kind != NativeControlSettleRecovery.CandidateKind;
        }
        else if (!resumeOnly && time >= GameClockBudgetPolicy.AutonomousRecoveryStartTime &&
                 temporalRequirements.Count >= result.RemainingDonationCount)
        {
            var recoveryId = time >= 2400 ? "recovery:sleep_immediately" : "recovery:return_home";
            var recovery = ranked.Where(candidate => candidate.Available && candidate.BlockReasons.Length == 0 &&
                candidate.CandidateId == recoveryId).ToArray();
            if (recovery.Length == 1) selected = recovery[0];
        }
        if (selected is null) return false;
        // Donation on D+1 still requires the real reward on D+2 before the Year 3 boundary.
        var resumeDay = result.SourceTotalDay + (nextDay ? 1 : 0);
        if (resumeDay + 1 >= RustyKeyAcquisitionPortfolioBuilder.StageOneDeadlineExclusive) return false;
        const string policy = "deterministic_control.owned_supply_ordinary_recovery.v1";
        var neutral = CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(selected, policy);
        var plan = new DailyPlanCompiler().Compile(new[] { neutral }, snapshot.StateHash, goalId: result.GoalId,
            maxCandidates: 1, availableMinutes: SnapshotGameClockBudgetReader.AvailableMinutes(snapshot, GameClockBudgetPolicy.DayEndTime));
        var identity = AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(new
        {
            result.GoalId, result.SnapshotSha256, result.RequirementInventorySha256, result.StrategyLedgerSha256,
            Policy = policy, selected.CandidateId
        });
        plan.PlanId = "rusty_key_deferral_plan." + identity;
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        AcquisitionRouteDispatchCompilationBuilder.StabilizeQueueIdentity(queue, identity, "rusty_key_deferral");
        if (CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue).Length > 0 ||
            plan.Steps.Length == 0 || queue.Status != "pending" || queue.Items.Length == 0 || queue.Items.Any(item => item.Status != "pending"))
            return false;
        result.SelectedCandidateId = selected.CandidateId;
        result.PendingDonationRequirementIds = pending;
        result.SelectedOwnedSupplyDeferral = new(nextDay ? "owned_supply_evening_recovery" : "owned_supply_native_control_settle",
            result.SourceTotalDay, resumeDay, pending, selected.CandidateId, selected.Kind);
        result.CompiledPlan = plan;
        result.CompiledQueue = queue;
        result.SelectionPolicyId = policy;
        result.RollingStepReady = true;
        result.Status = "ready_owned_supply_deferral_pending_dispatch";
        return true;
    }
}
