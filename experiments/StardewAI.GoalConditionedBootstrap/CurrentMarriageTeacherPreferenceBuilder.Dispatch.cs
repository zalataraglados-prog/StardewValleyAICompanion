using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentMarriageTeacherPreferenceBuilder
{
    public static (SmallModelPlanEnvelope Plan, ActionQueueEnvelope Queue) Compile(string preferencePath, bool independentPairs = false)
    {
        var report = Verify(preferencePath);
        if (report.SelectedCandidate is null || !(independentPairs ? report.IndependentPairwiseLabelEligible : report.CompletePreferenceLabelEligible))
            throw new InvalidDataException("Marriage dispatch requires replayed evidence for the explicit pair or complete-order scope.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(report.Intake.SnapshotPath, "Marriage dispatch snapshot");
        var (_, minutes, energy) = Budget(snapshot);
        var result = CompileCandidate(report.SelectedCandidate, snapshot, minutes, energy);
        if (result.Reasons.Length > 0) throw new InvalidDataException("Original marriage preparation compilation rejected: " + string.Join(",", result.Reasons));
        return (result.Plan, result.Queue);
    }

    private static (SmallModelPlanEnvelope Plan, ActionQueueEnvelope Queue, string[] Reasons) CompileCandidate(
        PolicyEventCandidatePrediction candidate, SnapshotEnvelope snapshot, int minutes, int energy)
    {
        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, snapshot.StateHash,
            goalId: GoalId, maxCandidates: 1, availableMinutes: minutes, energyBudget: energy);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var reasons = CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue);
        if (plan.Steps.Length == 0 || queue.Items.Length == 0 || queue.Status != "pending" || queue.Items.Any(item => item.Status != "pending"))
            reasons = reasons.Append("marriage_preparation_original_queue_not_dispatchable").Distinct(StringComparer.Ordinal).ToArray();
        return (plan, queue, reasons);
    }
}
