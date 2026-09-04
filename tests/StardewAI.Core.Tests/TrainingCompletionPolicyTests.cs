using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class TrainingCompletionPolicyTests
{
    [Fact]
    public void ExplicitTargetTransitionsToSaveBoundaryBeforeCompletion()
    {
        var primary = TrainingCompletionPolicy.Decide(
            0, 3, 0, 2, true, false, true, false);
        var boundary = TrainingCompletionPolicy.Decide(
            1, 3, 0, 2, true, true, true, false);
        var complete = TrainingCompletionPolicy.Decide(
            1, 3, 1, 2, true, true, true, true);

        Assert.Equal(LiveTrainingPhase.Primary, primary.Phase);
        Assert.Equal(LiveTrainingPhase.NativeSaveBoundary, boundary.Phase);
        Assert.Equal(LiveTrainingPhase.Complete, complete.Phase);
    }

    [Fact]
    public void FixedLengthRunStartsBoundaryOnlyAfterPrimaryAttemptBudget()
    {
        Assert.Equal(
            LiveTrainingPhase.Primary,
            TrainingCompletionPolicy.Decide(
                2, 3, 0, 2, false, true, true, false).Phase);
        Assert.Equal(
            LiveTrainingPhase.NativeSaveBoundary,
            TrainingCompletionPolicy.Decide(
                3, 3, 0, 2, false, true, true, false).Phase);
    }

    [Fact]
    public void FailsClosedWhenEitherAttemptBudgetIsExhausted()
    {
        var primary = TrainingCompletionPolicy.Decide(
            3, 3, 0, 2, true, false, true, false);
        var boundary = TrainingCompletionPolicy.Decide(
            1, 3, 2, 2, true, true, true, false);

        Assert.Equal(LiveTrainingPhase.Incomplete, primary.Phase);
        Assert.Equal("primary_training_target_not_met", primary.StopReason);
        Assert.Equal(LiveTrainingPhase.Incomplete, boundary.Phase);
        Assert.Equal("native_save_boundary_not_verified", boundary.StopReason);
    }
}
