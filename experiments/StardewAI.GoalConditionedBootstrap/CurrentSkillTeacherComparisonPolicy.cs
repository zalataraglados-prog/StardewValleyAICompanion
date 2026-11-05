using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class CurrentSkillTeacherComparisonPolicy
{
    public static PolicyTeacherPreferencePair? Compare(
        CurrentSkillTeacherCandidate preferred,
        CurrentSkillTeacherCandidate alternative)
    {
        if (!Admitted(preferred) || !Admitted(alternative) ||
            preferred.CandidateId == alternative.CandidateId ||
            preferred.NativeActionIdentity == alternative.NativeActionIdentity ||
            preferred.OptionId != alternative.OptionId || preferred.Kind != alternative.Kind ||
            preferred.NativeEffectSignature != alternative.NativeEffectSignature ||
            preferred.EstimatedTicks > alternative.EstimatedTicks ||
            preferred.CompiledMinutes > alternative.CompiledMinutes ||
            preferred.EnergyCost > alternative.EnergyCost)
            return null;

        var other = alternative.UsefulXp.ToDictionary(row => row.SkillId, StringComparer.Ordinal);
        if (preferred.UsefulXp.Length != other.Count ||
            preferred.UsefulXp.Any(row => !other.TryGetValue(row.SkillId, out var value) ||
                row.Minimum < value.Maximum))
            return null;
        var strictXp = preferred.UsefulXp.Any(row => row.Minimum > other[row.SkillId].Maximum);
        if (!strictXp && preferred.EstimatedTicks == alternative.EstimatedTicks &&
            preferred.CompiledMinutes == alternative.CompiledMinutes &&
            preferred.EnergyCost == alternative.EnergyCost)
            return null;

        return new PolicyTeacherPreferencePair
        {
            PreferredCandidateId = preferred.CandidateId,
            AlternativeCandidateId = alternative.CandidateId,
            FirstDifferingAuthoritativeCriterion = strictXp
                ? "guaranteed_useful_xp_dominates_possible_alternative_xp"
                : preferred.EstimatedTicks < alternative.EstimatedTicks
                    ? "same_native_effect_lower_verified_estimated_ticks"
                    : preferred.CompiledMinutes < alternative.CompiledMinutes
                        ? "same_native_effect_lower_compiled_minutes"
                        : "same_native_effect_lower_native_energy_cost",
            LabelSemantics = "preferred_over_current_eligible_counterfactual"
        };
    }

    private static bool Admitted(CurrentSkillTeacherCandidate candidate) =>
        candidate.Status == "admitted_current_xp" &&
        !string.IsNullOrWhiteSpace(candidate.CandidateId) &&
        !string.IsNullOrWhiteSpace(candidate.NativeEffectSignature) &&
        !string.IsNullOrWhiteSpace(candidate.NativeActionIdentity) &&
        candidate.EstimatedTicks > 0 && candidate.CompiledMinutes > 0 && candidate.EnergyCost >= 0 &&
        candidate.UsefulXp.Length == 5 &&
        candidate.UsefulXp.Select(row => row.SkillId).Distinct(StringComparer.Ordinal).Count() == 5 &&
        candidate.UsefulXp.All(row => row.SkillId is "farming" or "fishing" or "foraging" or "mining" or "combat") &&
        candidate.UsefulXp.All(row => row.Minimum >= 0 && row.Maximum >= row.Minimum) &&
        candidate.UsefulXp.Any(row => row.Minimum > 0);
}
