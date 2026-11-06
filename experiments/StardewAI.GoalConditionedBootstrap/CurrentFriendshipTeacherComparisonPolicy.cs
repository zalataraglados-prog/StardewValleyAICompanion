using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class CurrentFriendshipTeacherComparisonPolicy
{
    public static PolicyTeacherPreferencePair? Compare(
        CurrentFriendshipTeacherCandidate preferred, CurrentFriendshipTeacherCandidate alternative)
    {
        if (!Admitted(preferred) || !Admitted(alternative) ||
            preferred.CandidateId == alternative.CandidateId ||
            preferred.NativeActionIdentity == alternative.NativeActionIdentity ||
            preferred.OptionId != alternative.OptionId || !SameComparisonFamily(preferred, alternative) ||
            preferred.TimingEvidenceId != alternative.TimingEvidenceId ||
            preferred.TimingEvidenceKind != alternative.TimingEvidenceKind ||
            preferred.ExpectedNewQualifiers < alternative.ExpectedNewQualifiers ||
            preferred.UsefulFriendshipDelta < alternative.UsefulFriendshipDelta ||
            preferred.ContactBudgetArrivalTime > alternative.ContactBudgetArrivalTime ||
            preferred.CompiledMinutes > alternative.CompiledMinutes ||
            preferred.EnergyCost > alternative.EnergyCost)
            return null;
        // Different gift items/qualities/slots or normal-limit effects are resource tradeoffs.
        if (preferred.GiftQualifiedItemId != alternative.GiftQualifiedItemId ||
            preferred.GiftQuality != alternative.GiftQuality ||
            preferred.GiftSlotIndex != alternative.GiftSlotIndex ||
            preferred.GiftNormalLimitEffect != alternative.GiftNormalLimitEffect)
            return null;
        var criterion = preferred.ExpectedNewQualifiers > alternative.ExpectedNewQualifiers
            ? "greater_current_native_qualifier_gain_without_other_local_cost_regression"
            : preferred.UsefulFriendshipDelta > alternative.UsefulFriendshipDelta
                ? "greater_saturated_native_point_gain_without_local_cost_regression"
                : preferred.ContactBudgetArrivalTime < alternative.ContactBudgetArrivalTime
                    ? "lower_calibrated_contact_budget_not_observed_duration"
                    : preferred.CompiledMinutes < alternative.CompiledMinutes
                        ? "lower_original_compiled_minutes"
                        : preferred.EnergyCost < alternative.EnergyCost ? "lower_native_energy_cost" : null;
        return criterion is null ? null : new PolicyTeacherPreferencePair
        {
            PreferredCandidateId = preferred.CandidateId,
            AlternativeCandidateId = alternative.CandidateId,
            FirstDifferingAuthoritativeCriterion = criterion,
            LabelSemantics = "preferred_over_current_eligible_counterfactual"
        };
    }

    private static bool SameComparisonFamily(
        CurrentFriendshipTeacherCandidate preferred, CurrentFriendshipTeacherCandidate alternative) =>
        preferred.Kind == alternative.Kind ||
        // An immediate talk can dominate a routed talk's complete contact budget.
        // Do not infer the reverse from a route's cheaper first dispatch segment.
        preferred.OptionId == "social.talk_npc" && preferred.Kind == "social_talk_current" &&
        !string.IsNullOrWhiteSpace(preferred.NpcName) && !string.IsNullOrWhiteSpace(alternative.NpcName) &&
        preferred.NpcName != alternative.NpcName &&
        alternative.Kind == "route_connector_tile" && alternative.RouteContactBudget is { FullContactBudgetVerified: true };

    private static bool Admitted(CurrentFriendshipTeacherCandidate row) =>
        row.Status == "admitted_current_social_comparison" && row.AvailableCurrentSource && row.ComparisonRequired &&
        !string.IsNullOrWhiteSpace(row.CandidateId) && !string.IsNullOrWhiteSpace(row.NativeActionIdentity) &&
        !string.IsNullOrWhiteSpace(row.TimingEvidenceId) &&
        row.TimingEvidenceKind is "ExactDuration" or "ConservativeUpperBound" &&
        row.FriendshipPointsBefore is >= 0 and < 1975 &&
        row.ExpectedFriendshipPointsAfter > row.FriendshipPointsBefore && row.ExpectedFriendshipPointsAfter <= 999999 &&
        row.UsefulFriendshipDelta == Math.Min(1975, row.ExpectedFriendshipPointsAfter) - row.FriendshipPointsBefore &&
        row.ExpectedNewQualifiers == (row.ExpectedFriendshipPointsAfter >= 1975 ? 1 : 0) &&
        (row.Kind != "route_connector_tile" || row.RouteContactBudget is { FullContactBudgetVerified: true } budget &&
            budget.CandidateId == row.CandidateId && budget.OpportunityId == row.OpportunityId &&
            budget.TimingEvidenceId == row.TimingEvidenceId && budget.ContactWindow!.EarliestInteractionTime == row.ContactBudgetArrivalTime) &&
        row.CompiledMinutes > 0 && row.EnergyCost >= 0 &&
        row.ContactBudgetArrivalTime >= 600 && row.ContactBudgetArrivalTime < 2600 &&
        row.ContactBudgetArrivalTime % 100 < 60;
}
