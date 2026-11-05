namespace StardewAI.GoalConditionedBootstrap;

public enum FriendshipTeacherAdmissionMode
{
    CompleteOrder,
    IndependentPairs
}

internal static class FriendshipTeacherAdmissionPolicy
{
    internal static FriendshipTeacherAdmissionMode Parse(string? value) => value switch
    {
        null or "complete_order" => FriendshipTeacherAdmissionMode.CompleteOrder,
        "independent_pairs" => FriendshipTeacherAdmissionMode.IndependentPairs,
        _ => throw new ArgumentException("Friendship --admission must be complete_order or independent_pairs.")
    };

    internal static void Validate(FriendshipTeacherAdmissionMode mode)
    {
        if (mode is not (FriendshipTeacherAdmissionMode.CompleteOrder or FriendshipTeacherAdmissionMode.IndependentPairs))
            throw new ArgumentOutOfRangeException(nameof(mode));
    }

    // Scope selection only: callers must first rebuild through Build/Verify, never trust a stored flag.
    internal static bool IsEligible(CurrentFriendshipTeacherPreference replayed, FriendshipTeacherAdmissionMode mode)
    {
        Validate(mode);
        return replayed.SelectedCandidate is not null && !replayed.NativeOutcomeVerified &&
            !replayed.FormalProductTrainingAuthorized &&
            (mode == FriendshipTeacherAdmissionMode.CompleteOrder
                ? replayed.TeacherPreferenceLabelEligible
                : replayed.IndependentPairwiseLabelEligible);
    }
}
