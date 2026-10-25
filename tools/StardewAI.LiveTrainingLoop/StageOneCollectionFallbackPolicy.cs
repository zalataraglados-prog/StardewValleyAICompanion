namespace StardewAI.LiveTrainingLoop;

public static class StageOneCollectionFallbackPolicy
{
    public const string NoCurrentCandidateStatus =
        "blocked_no_current_collection_candidate";

    public const string EmptyCurrentCandidateMembershipReason =
        "unified_current_collection_candidate_membership_is_empty";

    public static bool ShouldYieldToDailyPlan(
        bool requireGoldenWalnutSourceScheduleReady,
        string status,
        string detail)
    {
        return requireGoldenWalnutSourceScheduleReady &&
            string.Equals(
                status,
                NoCurrentCandidateStatus,
                StringComparison.Ordinal) &&
            string.Equals(
                detail,
                EmptyCurrentCandidateMembershipReason,
                StringComparison.Ordinal);
    }
}
