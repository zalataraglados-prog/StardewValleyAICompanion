using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentMasterAnglerTeacherFrontierBuilder
{
    private static SupportingPrerequisiteResult BuildRodPrerequisite(
        SnapshotEnvelope snapshot,
        IEnumerable<PolicyEventCandidatePrediction> currentCandidates,
        bool goalAlreadySatisfied)
    {
        if (goalAlreadySatisfied)
        {
            return new SupportingPrerequisiteResult(
                "not_required_goal_satisfied",
                Array.Empty<CurrentMasterAnglerSupportingPrerequisiteBinding>(),
                Array.Empty<string>());
        }

        var inventoryStatus =
            MasterAnglerRodEquipmentAcquisitionProtocol.ObserveRodInventory(
                snapshot);
        if (inventoryStatus == "satisfied")
        {
            return new SupportingPrerequisiteResult(
                "satisfied",
                Array.Empty<CurrentMasterAnglerSupportingPrerequisiteBinding>(),
                Array.Empty<string>());
        }
        if (inventoryStatus != "missing")
        {
            return new SupportingPrerequisiteResult(
                "blocked_authoritative_rod_inventory_unavailable",
                Array.Empty<CurrentMasterAnglerSupportingPrerequisiteBinding>(),
                new[] { "state.fishing.rod_inventory_is_not_authoritatively_readable" });
        }

        var matches = currentCandidates
            .Select(candidate =>
            {
                var match = MasterAnglerRodEquipmentAcquisitionProtocol
                    .MatchCandidate(candidate);
                return (Candidate: candidate, Match: match);
            })
            .Where(value => value.Match is not null)
            .Where(value =>
                !value.Match!.IsPurchaseFallback ||
                MasterAnglerRodEquipmentAcquisitionProtocol
                    .PurchaseFallbackAllowed(snapshot))
            .ToArray();
        if (matches.Length == 0)
        {
            return new SupportingPrerequisiteResult(
                "deferred_no_current_exact_rod_acquisition_candidate",
                Array.Empty<CurrentMasterAnglerSupportingPrerequisiteBinding>(),
                new[]
                {
                    "no_current_candidate_matches_the_native_free_rod_chain_or_audited_purchase_fallback",
                    "future_or_currently_unavailable_routes_are_not_negative_labels"
                });
        }

        var bestPriority = matches.Min(value => value.Match!.Priority);
        var preferredMatches = matches
            .Where(value => value.Match!.Priority == bestPriority)
            .ToArray();
        if (preferredMatches.Length != 1)
        {
            return new SupportingPrerequisiteResult(
                "blocked_ambiguous_current_rod_acquisition_candidates",
                Array.Empty<CurrentMasterAnglerSupportingPrerequisiteBinding>(),
                new[]
                {
                    "multiple_current_candidates_match_the_same_highest_priority_rod_prerequisite_stage"
                });
        }

        var match = preferredMatches[0];
        var stage = match.Match!;
        return new SupportingPrerequisiteResult(
            "current_positive_candidate",
            new[]
            {
                new CurrentMasterAnglerSupportingPrerequisiteBinding(
                    MasterAnglerRodEquipmentAcquisitionProtocol.RequirementId,
                    match.Candidate.CandidateId,
                    match.Candidate.OptionId,
                    match.Candidate.Kind,
                    match.Candidate.Rank,
                    stage.BindingKind,
                    MasterAnglerRodEquipmentAcquisitionProtocol.QualifiedItemId,
                    stage.ShopId,
                    MasterAnglerRodEquipmentAcquisitionProtocol.RequiredQuantity,
                    stage.MaximumUnitPrice,
                    stage.IdentityEvidence)
            },
            Array.Empty<string>());
    }

    private sealed record SupportingPrerequisiteResult(
        string Status,
        CurrentMasterAnglerSupportingPrerequisiteBinding[] Bindings,
        string[] BlockingReasons);
}
