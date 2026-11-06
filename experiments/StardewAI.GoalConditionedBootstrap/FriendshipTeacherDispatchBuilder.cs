using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class FriendshipTeacherDispatchBuilder
{
    public static (SmallModelPlanEnvelope Plan, ActionQueueEnvelope Queue) Compile(string preferencePath)
        => Compile(preferencePath, FriendshipTeacherAdmissionMode.CompleteOrder);

    public static (SmallModelPlanEnvelope Plan, ActionQueueEnvelope Queue) Compile(
        string preferencePath, FriendshipTeacherAdmissionMode admissionMode)
    {
        var source = CompileSource(preferencePath, admissionMode);
        return (source.Plan, source.Queue);
    }

    internal static (CurrentFriendshipTeacherPreference Preference, SmallModelPlanEnvelope Plan, ActionQueueEnvelope Queue) CompileSource(
        string preferencePath, FriendshipTeacherAdmissionMode admissionMode)
    {
        FriendshipTeacherAdmissionPolicy.Validate(admissionMode);
        var preference = CurrentFriendshipTeacherPreferenceBuilder.Verify(RequiredFile(preferencePath));
        if (!FriendshipTeacherAdmissionPolicy.IsEligible(preference, admissionMode))
            throw new InvalidDataException("Friendship dispatch requires independently replayed evidence for the explicit admission scope.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(preference.SnapshotPath, "Friendship dispatch snapshot");
        var time = Value(snapshot, "time", "time").GetInt32();
        var energy = Value(snapshot, "player", "energy").GetDouble();
        var plan = new DailyPlanCompiler().Compile(new[] { preference.SelectedCandidate! }, snapshot.StateHash,
            goalId: "goal.social.raise_friendships", maxCandidates: 1,
            availableMinutes: GameClockBudgetPolicy.ClockMinutesBetween(time, GameClockBudgetPolicy.AutonomousRecoveryStartTime),
            energyBudget: (int)Math.Floor(energy));
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var reasons = CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue);
        if (plan.Steps.Length == 0 || queue.Items.Length == 0 || queue.Status != "pending" ||
            queue.Items.Any(item => item.Status != "pending") || reasons.Length > 0)
            throw new InvalidDataException("Original social compiler rejected dispatch: " + string.Join(",", reasons));
        return (preference, plan, queue);
    }
}
