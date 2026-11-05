using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record SkillTeacherDispatch(CurrentSkillTeacherPreference Preference,
    SmallModelPlanEnvelope Plan, ActionQueueEnvelope Queue);

public static class SkillTeacherDispatchBuilder
{
    public static SkillTeacherDispatch Compile(string preferencePath)
    {
        var preference = CurrentSkillTeacherPreferenceBuilder.Verify(RequiredFile(preferencePath));
        if (!preference.TeacherPreferenceLabelEligible || preference.SelectedCandidate is null ||
            preference.FormalProductTrainingAuthorized || preference.NativeOutcomeVerified)
            throw new InvalidDataException("Skills dispatch requires a fresh independent comparison, never an outcome or training claim.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(preference.SnapshotPath, "Skills dispatch snapshot");
        var ledger = string.IsNullOrWhiteSpace(preference.StrategyLedgerPath) ? null :
            AcquisitionStrategyLedgerReader.Read(preference.StrategyLedgerPath, snapshot).Ledger;
        var time = Value(snapshot, "time", "time").GetInt32();
        var energy = Value(snapshot, "player", "energy").GetDouble();
        var plan = new DailyPlanCompiler().Compile(new[] { preference.SelectedCandidate }, snapshot.StateHash,
            goalId: "goal.skills.raise_skill_levels", maxCandidates: 1,
            availableMinutes: GameClockBudgetPolicy.ClockMinutesBetween(time, GameClockBudgetPolicy.AutonomousRecoveryStartTime),
            energyBudget: (int)Math.Floor(energy));
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        var reasons = CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue);
        if (plan.Steps.Length == 0 || queue.Items.Length == 0 || queue.Status != "pending" ||
            queue.Items.Any(item => item.Status != "pending") || reasons.Length > 0)
            throw new InvalidDataException("Original Skills compiler rejected dispatch: " + string.Join(",", reasons));
        return new(preference, plan, queue);
    }
}
