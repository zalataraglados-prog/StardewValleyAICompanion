using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class StageOneCollectionFallbackPolicyTests
{
    [Fact]
    public void GoldenWalnutLongRunYieldsWhenCurrentMembershipIsEmpty()
    {
        Assert.True(StageOneCollectionFallbackPolicy.ShouldYieldToDailyPlan(
            requireGoldenWalnutSourceScheduleReady: true,
            StageOneCollectionFallbackPolicy.NoCurrentCandidateStatus,
            StageOneCollectionFallbackPolicy.EmptyCurrentCandidateMembershipReason));
    }

    [Fact]
    public void PureCollectionContinuationStillStopsWhenMembershipIsEmpty()
    {
        Assert.False(StageOneCollectionFallbackPolicy.ShouldYieldToDailyPlan(
            requireGoldenWalnutSourceScheduleReady: false,
            StageOneCollectionFallbackPolicy.NoCurrentCandidateStatus,
            StageOneCollectionFallbackPolicy.EmptyCurrentCandidateMembershipReason));
    }

    [Theory]
    [InlineData(
        "blocked_authoritatively_tied_top_candidates",
        "unified_current_collection_candidate_membership_is_empty")]
    [InlineData(
        "blocked_no_current_collection_candidate",
        "candidate_membership_is_ambiguous")]
    [InlineData(
        "blocked_no_current_collection_candidate",
        "unified_current_collection_candidate_membership_is_empty,other")]
    public void OtherAuthorityFailuresStillStop(
        string status,
        string detail)
    {
        Assert.False(StageOneCollectionFallbackPolicy.ShouldYieldToDailyPlan(
            requireGoldenWalnutSourceScheduleReady: true,
            status,
            detail));
    }

    [Fact]
    public void LiveLoopUsesPolicyBeforeSharedDailyPlan()
    {
        var source = LiveTrainingLoopSources.All;
        var policyCheck = source.IndexOf(
            "StageOneCollectionFallbackPolicy.ShouldYieldToDailyPlan(",
            StringComparison.Ordinal);
        var yieldedProgress = source.IndexOf(
            "stage_one_collection_teacher_yielded",
            policyCheck,
            StringComparison.Ordinal);
        var sharedDailyPlan = source.IndexOf(
            "BuildQueueFromDailyPlanAsync(",
            yieldedProgress,
            StringComparison.Ordinal);

        Assert.True(policyCheck >= 0);
        Assert.True(yieldedProgress > policyCheck);
        Assert.True(sharedDailyPlan > yieldedProgress);
    }
}
