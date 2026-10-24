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
                var matched = MasterAnglerRodEquipmentAcquisitionProtocol
                    .TryMatchCandidate(candidate, out var bindingKind);
                return (Candidate: candidate, Matched: matched, BindingKind: bindingKind);
            })
            .Where(value => value.Matched)
            .ToArray();
        if (matches.Length == 0)
        {
            return new SupportingPrerequisiteResult(
                "deferred_no_current_exact_rod_purchase_candidate",
                Array.Empty<CurrentMasterAnglerSupportingPrerequisiteBinding>(),
                new[]
                {
                    "no_current_candidate_matches_the_audited_fish_shop_bamboo_pole_purchase",
                    "future_or_currently_unavailable_routes_are_not_negative_labels"
                });
        }
        if (matches.Length != 1)
        {
            return new SupportingPrerequisiteResult(
                "blocked_ambiguous_current_rod_purchase_candidates",
                Array.Empty<CurrentMasterAnglerSupportingPrerequisiteBinding>(),
                new[]
                {
                    "multiple_current_candidates_match_the_same_audited_rod_prerequisite"
                });
        }

        var match = matches[0];
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
                    match.BindingKind,
                    MasterAnglerRodEquipmentAcquisitionProtocol.QualifiedItemId,
                    MasterAnglerRodEquipmentAcquisitionProtocol.ShopId,
                    MasterAnglerRodEquipmentAcquisitionProtocol.RequiredQuantity,
                    MasterAnglerRodEquipmentAcquisitionProtocol.MaximumUnitPrice,
                    "authoritative_empty_rod_inventory+exact_audited_purchase_identity")
            },
            Array.Empty<string>());
    }

    private sealed record SupportingPrerequisiteResult(
        string Status,
        CurrentMasterAnglerSupportingPrerequisiteBinding[] Bindings,
        string[] BlockingReasons);
}
