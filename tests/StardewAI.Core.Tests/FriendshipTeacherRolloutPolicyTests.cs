using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FriendshipTeacherRolloutPolicyTests
{
    private readonly FriendshipTeacherRolloutPolicy policy = new();

    [Fact]
    public void ReadyLabelExecutesOnlyItsSelectedCandidate()
    {
        var decision = policy.Decide(Observation(
            disposition: FriendshipTeacherLabelDisposition.Ready,
            candidateId: "social:talk:Clint:route:AnimalShop:13,20"));

        Assert.Equal(
            FriendshipTeacherRolloutPhase.ExecuteSelectedObjective,
            decision.Phase);
        Assert.Equal(
            "social:talk:Clint:route:AnimalShop:13,20",
            decision.CandidateId);
    }

    [Fact]
    public void VerifiedObjectiveAlwaysForcesFreshReplan()
    {
        var observation = Observation();
        observation.PendingCandidateId = "social:talk:Marnie";
        observation.PendingObjectiveReceiptVerified = true;

        var decision = policy.Decide(observation);

        Assert.Equal(
            FriendshipTeacherRolloutPhase.BuildFreshTeacherLabel,
            decision.Phase);
    }

    [Fact]
    public void BlockingMenuTakesPriorityOverPendingExecution()
    {
        var observation = Observation();
        observation.ActiveMenuOpen = true;
        observation.PendingCandidateId = "social:talk:Marnie";

        var decision = policy.Decide(observation);

        Assert.Equal(
            FriendshipTeacherRolloutPhase.RecoverBlockingMenu,
            decision.Phase);
    }

    [Fact]
    public void ActiveStoryEventUsesNativeStoryExecutorBeforeMenuRecovery()
    {
        var observation = Observation();
        observation.ActiveStoryEvent = true;
        observation.ActiveMenuOpen = true;

        var decision = policy.Decide(observation);

        Assert.Equal(
            FriendshipTeacherRolloutPhase.AdvanceBlockingStoryEvent,
            decision.Phase);
    }

    [Fact]
    public void ConfiguredStoryEventEvidenceTargetEndsTheBoundedRun()
    {
        var observation = Observation();
        observation.CompletedStoryEventAdvances = 1;
        observation.StopAfterStoryEventAdvances = 1;

        var decision = policy.Decide(observation);

        Assert.Equal(
            FriendshipTeacherRolloutPhase.BoundedRunComplete,
            decision.Phase);
        Assert.Equal(
            "configured_story_event_evidence_target_reached",
            decision.Reason);
    }

    [Fact]
    public void InvalidStoryEventEvidenceBoundsFailClosed()
    {
        var observation = Observation();
        observation.StopAfterStoryEventAdvances = -1;

        var decision = policy.Decide(observation);

        Assert.Equal(FriendshipTeacherRolloutPhase.Blocked, decision.Phase);
        Assert.Contains(
            "friendship_teacher_story_event_bounds_invalid",
            decision.BlockingReasons);
    }

    [Fact]
    public void ExactDayExhaustionMayAdvanceToNativeSaveBoundary()
    {
        var label = new CurrentSocialDayTeacherLabel
        {
            BlockingReasons = new[]
            {
                "current_social_itinerary_no_verified_first_visit"
            }
        };
        var disposition = policy.Classify(label);
        var decision = policy.Decide(Observation(disposition));

        Assert.Equal(
            FriendshipTeacherLabelDisposition.DayExhausted,
            disposition);
        Assert.Equal(
            FriendshipTeacherRolloutPhase.CloseDayAtNativeSaveBoundary,
            decision.Phase);
    }

    [Fact]
    public void UnknownCoverageBlockerCannotBeConvertedIntoSleep()
    {
        var label = new CurrentSocialDayTeacherLabel
        {
            BlockingReasons = new[] { "current_social_npc_coverage_incomplete" }
        };
        var disposition = policy.Classify(label);
        var decision = policy.Decide(Observation(disposition));

        Assert.Equal(FriendshipTeacherLabelDisposition.Blocked, disposition);
        Assert.Equal(FriendshipTeacherRolloutPhase.Blocked, decision.Phase);
    }

    [Fact]
    public void NativeSaveRequiresTransitionAuditBeforeNextLabel()
    {
        var observation = Observation();
        observation.DayTransitionState =
            FriendshipTeacherDayTransitionState.NativeSaveVerified;

        var decision = policy.Decide(observation);

        Assert.Equal(
            FriendshipTeacherRolloutPhase.AuditNativeDayTransition,
            decision.Phase);
    }

    [Fact]
    public void VerifiedTransitionStartsFreshDayPlanning()
    {
        var observation = Observation();
        observation.DayTransitionState =
            FriendshipTeacherDayTransitionState.AuditVerified;

        var decision = policy.Decide(observation);

        Assert.Equal(
            FriendshipTeacherRolloutPhase.BuildFreshTeacherLabel,
            decision.Phase);
    }

    [Fact]
    public void GoalCompletionWinsBeforeDeadlineBlock()
    {
        var observation = Observation();
        observation.CurrentTotalDays = 224;
        observation.DeadlineTotalDaysExclusive = 224;
        observation.QualifyingCount = 10;

        var decision = policy.Decide(observation);

        Assert.Equal(FriendshipTeacherRolloutPhase.Complete, decision.Phase);
    }

    [Fact]
    public void UnsatisfiedGoalAtDeadlineFailsClosed()
    {
        var observation = Observation();
        observation.CurrentTotalDays = 224;
        observation.DeadlineTotalDaysExclusive = 224;

        var decision = policy.Decide(observation);

        Assert.Equal(FriendshipTeacherRolloutPhase.Blocked, decision.Phase);
        Assert.Contains(
            "friendship_teacher_deadline_reached",
            decision.BlockingReasons);
    }

    [Fact]
    public void RepeatedNoProgressDaysStopTheLoop()
    {
        var observation = Observation();
        observation.ConsecutiveNoProgressDays = 3;

        var decision = policy.Decide(observation);

        Assert.Equal(FriendshipTeacherRolloutPhase.Blocked, decision.Phase);
        Assert.Contains(
            "friendship_teacher_no_progress_day_limit_reached",
            decision.BlockingReasons);
    }

    [Fact]
    public void ConfiguredTransitionLimitEndsOnlyTheBoundedRun()
    {
        var observation = Observation();
        observation.CompletedDayTransitions = 1;
        observation.MaxDayTransitions = 1;

        var decision = policy.Decide(observation);

        Assert.Equal(
            FriendshipTeacherRolloutPhase.BoundedRunComplete,
            decision.Phase);
        Assert.Equal(
            "configured_rollout_transition_limit_reached",
            decision.Reason);
    }

    private static FriendshipTeacherRolloutObservation Observation(
        FriendshipTeacherLabelDisposition disposition =
            FriendshipTeacherLabelDisposition.NotBuilt,
        string candidateId = "") => new()
    {
        CurrentTotalDays = 100,
        DeadlineTotalDaysExclusive = 224,
        QualifyingCount = 0,
        RequiredQualifyingCount = 10,
        MaxObjectivesPerDay = 16,
        MaxConsecutiveNoProgressDays = 3,
        MaxDayTransitions = 1,
        LabelDisposition = disposition,
        LabelCandidateId = candidateId
    };
}
