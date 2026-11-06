using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MuseumTerminalTeacherComparisonBuilder
{
    private static MuseumTerminalTeacherAlternative EvaluateDonation(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        PolicyEventCandidatePrediction candidate, string missingQualifiedItemId, string goalId)
    {
        var reasons = (candidate.BlockReasons ?? Array.Empty<string>()).ToList();
        if (!candidate.Available || candidate.AllowedNow == false) reasons.Add("original_candidate_not_currently_available");
        if (candidate.Kind != "donate_museum_item" || candidate.QualifiedItemId != missingQualifiedItemId)
            reasons.Add("outside_exact_missing_final_donation_scope");
        if (candidate.EstimatedTicks is not >= 1 or > HorizonTicks) reasons.Add("outside_declared_comparison_horizon");
        if (!Parameter(candidate, "expected_donated_count_before", "94") || !Parameter(candidate, "expected_donated_count_after", "95") ||
            !Parameter(candidate, "museum_total_donatable_items", "95") || !Parameter(candidate, "expected_collection_complete_after", "true") ||
            !Parameter(candidate, "expected_complete_collection_achievement_after", "true") || !Parameter(candidate, "reward_projection_status", "ready"))
            reasons.Add("native_full_collection_projection_unavailable");
        var plan = new DailyPlanCompiler().Compile(new[]
        {
            CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(candidate, PolicyId)
        }, snapshot.StateHash, goalId, maxCandidates: 1,
            availableMinutes: SnapshotGameClockBudgetReader.AvailableMinutes(snapshot, GameClockBudgetPolicy.AutonomousRecoveryStartTime));
        plan.PlanId = "museum_terminal_teacher_plan." + CompilationIdentity(snapshot, ledger, candidate.CandidateId, goalId);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        reasons.AddRange(CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue));
        if (plan.Steps.Length != 1 || queue.Status != "pending" || queue.Items.Length != 1 ||
            queue.Items.Any(item => item.Status != "pending" || item.OptionId != "executor.donate_museum_item"))
            reasons.Add("original_donation_compiler_rejected");
        Stabilize(queue, snapshot, ledger, candidate.CandidateId);
        var admitted = reasons.Count == 0;
        return new(candidate.CandidateId, candidate.Kind, candidate.QualifiedItemId, admitted,
            reasons.Distinct(StringComparer.Ordinal).ToArray(), admitted ? 1 : 0, candidate.EstimatedTicks, plan, queue);
    }

    private static MuseumTerminalTeacherAlternative EvaluateDefer(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, string goalId)
    {
        // A compiler-checked counterfactual, not a new autonomous candidate or executor.
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = DeferId, SourceModel = PolicyId, StateHash = snapshot.StateHash, GoalId = goalId,
            Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer),
            Steps = new[] { new SmallModelPlanStep { StepId = DeferId, Kind = "wait_ticks", WaitTicks = HorizonTicks,
                TargetLocation = "ArchaeologyHouse", EstimatedMinutes = GameClockBudgetPolicy.TicksToGameMinutes(HorizonTicks) } }
        };
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        var reasons = CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue).ToList();
        if (queue.Status != "pending" || queue.Items.Length != 1 ||
            queue.Items.Any(item => item.Status != "pending" || item.OptionId != "executor.wait_ticks"))
            reasons.Add("original_bounded_wait_compiler_rejected");
        Stabilize(queue, snapshot, ledger, DeferId);
        return new(DeferId, "bounded_no_donation_counterfactual", string.Empty, reasons.Count == 0,
            reasons.Distinct(StringComparer.Ordinal).ToArray(), 0, HorizonTicks, plan, queue);
    }

    private static bool Parameter(PolicyEventCandidatePrediction candidate, string name, string expected) =>
        CurrentTeacherFrontierSupport.TryReadUniqueParameter(candidate, name, out var value) && value == expected;

    private static void Stabilize(ActionQueueEnvelope queue, SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, string candidateId) =>
        AcquisitionRouteDispatchCompilationBuilder.StabilizeQueueIdentity(queue,
            CompilationIdentity(snapshot, ledger, candidateId, queue.GoalId), "museum_terminal_teacher");

    private static string CompilationIdentity(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, string candidateId, string goalId) =>
        AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(new
            { snapshot.StateHash, ledger.LedgerId, ledger.Revision, candidateId, goalId, PolicyId });
}
