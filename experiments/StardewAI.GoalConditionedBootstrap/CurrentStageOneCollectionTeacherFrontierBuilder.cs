namespace StardewAI.GoalConditionedBootstrap;

public static class CurrentStageOneCollectionTeacherFrontierBuilder
{
    private static readonly string[] RequiredSetIds =
    {
        "full_shipment",
        "master_angler",
        "museum_collection",
        "community_center_standard"
    };

    public static CurrentStageOneCollectionTeacherFrontier Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string rankingPath,
        string snapshotPath,
        string masterAnglerTargetDateIntentsPath)
    {
        var fullShipment = CurrentFullShipmentTeacherFrontierBuilder.Build(
            requirementInventoryPath,
            acquisitionLoweringPath,
            rankingPath,
            snapshotPath);
        var collection = CurrentCollectionTeacherFrontierBuilder.Build(
            requirementInventoryPath,
            acquisitionLoweringPath,
            rankingPath,
            snapshotPath);
        var masterAngler = CurrentMasterAnglerTeacherFrontierBuilder.Build(
            requirementInventoryPath,
            acquisitionLoweringPath,
            rankingPath,
            snapshotPath,
            masterAnglerTargetDateIntentsPath);
        ValidateCommonIdentity(fullShipment, collection, masterAngler);

        var selectionGroups = BuildSelectionGroups(
            fullShipment,
            collection,
            masterAngler);
        var candidateChoices = BuildCandidateChoices(
            fullShipment,
            collection,
            masterAngler);
        var blockedSetCount = collection.RequirementSets.Count(value =>
            value.Status == "blocked");
        var membershipEligible = candidateChoices.Length > 0;
        var status = membershipEligible
            ? blockedSetCount > 0
                ? "candidate_contract_ready_with_blocked_requirement_sets"
                : "candidate_contract_ready"
            : blockedSetCount > 0
                ? "no_current_candidate_and_blocked_requirement_sets"
                : "no_current_matching_candidate";

        return new CurrentStageOneCollectionTeacherFrontier
        {
            Status = status,
            GoalId = fullShipment.GoalId,
            SourceStateHash = fullShipment.SourceStateHash,
            RequirementSetCount = RequiredSetIds.Length,
            CurrentCandidateMembershipEligible = membershipEligible,
            TeacherPreferenceLabelEligible = false,
            UsesLearnerRankOrScore = false,
            EmitsNegativeLabelsForUnavailableRoutes = false,
            SelectionContract = new CurrentCollectionCandidateSelectionContract
            {
                RequiredRequirementSetIds = RequiredSetIds,
                SelectionGroups = selectionGroups,
                CandidateChoices = candidateChoices
            },
            FullShipment = fullShipment,
            MuseumAndCommunityCenter = collection,
            MasterAngler = masterAngler
        };
    }

    private static void ValidateCommonIdentity(
        CurrentFullShipmentTeacherFrontier fullShipment,
        CurrentCollectionTeacherFrontier collection,
        CurrentMasterAnglerTeacherFrontier masterAngler)
    {
        if (fullShipment.GoalId != collection.GoalId ||
            fullShipment.GoalId != masterAngler.GoalId ||
            fullShipment.SourceStateHash != collection.SourceStateHash ||
            fullShipment.SourceStateHash != masterAngler.SourceStateHash ||
            !SameHash(fullShipment.RequirementInventorySha256,
                collection.RequirementInventorySha256) ||
            !SameHash(fullShipment.RequirementInventorySha256,
                masterAngler.RequirementInventorySha256) ||
            !SameHash(fullShipment.AcquisitionLoweringSha256,
                collection.AcquisitionLoweringSha256) ||
            !SameHash(fullShipment.AcquisitionLoweringSha256,
                masterAngler.AcquisitionLoweringSha256) ||
            !SameHash(fullShipment.RankingSha256, collection.RankingSha256) ||
            !SameHash(fullShipment.RankingSha256, masterAngler.RankingSha256) ||
            !SameHash(fullShipment.SnapshotSha256, collection.SnapshotSha256) ||
            !SameHash(fullShipment.SnapshotSha256, masterAngler.SnapshotSha256) ||
            fullShipment.RequirementSetId != "full_shipment" ||
            masterAngler.RequirementSetId != "master_angler" ||
            !RequiredSetIds.Skip(2).ToHashSet(StringComparer.Ordinal).SetEquals(
                collection.RequirementSets.Select(value => value.RequirementSetId)))
        {
            throw new InvalidDataException(
                "The four collection frontiers do not share one goal, state, and authority input set.");
        }
    }

    private static CurrentCollectionSelectionGroup[] BuildSelectionGroups(
        CurrentFullShipmentTeacherFrontier fullShipment,
        CurrentCollectionTeacherFrontier collection,
        CurrentMasterAnglerTeacherFrontier masterAngler)
    {
        var result = new List<CurrentCollectionSelectionGroup>();
        result.AddRange(fullShipment.Requirements
            .Where(value => !value.Completed)
            .Select(value => SelectionGroup(
                "full_shipment",
                value.RequirementId,
                "all_required",
                value.CurrentStatus,
                1,
                value.MatchedCandidateIds)));
        result.AddRange(masterAngler.Requirements
            .Where(value => !value.Completed)
            .Select(value => SelectionGroup(
                "master_angler",
                value.RequirementId,
                "all_required",
                value.CurrentStatus,
                1,
                value.MatchedCandidateIds)));
        foreach (var set in collection.RequirementSets)
        {
            result.AddRange(set.Requirements
                .Where(value => value.Completed != true)
                .Select(value => SelectionGroup(
                    set.RequirementSetId,
                    value.RequirementId,
                    value.SelectionRule,
                    value.CurrentStatus,
                    value.RemainingSlotCount,
                    value.Alternatives.SelectMany(alternative =>
                        alternative.MatchedCandidateIds).ToArray())));
        }
        return result
            .OrderBy(value => value.RequirementSetId, StringComparer.Ordinal)
            .ThenBy(value => value.RequirementId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CurrentCollectionSelectionGroup SelectionGroup(
        string requirementSetId,
        string requirementId,
        string selectionRule,
        string currentStatus,
        int? remainingSlots,
        IEnumerable<string> candidateIds)
    {
        var candidates = candidateIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new CurrentCollectionSelectionGroup(
            requirementSetId + "|" + requirementId,
            requirementSetId,
            requirementId,
            selectionRule,
            currentStatus,
            remainingSlots,
            candidates.Length > 0 && remainingSlots.GetValueOrDefault() > 0
                ? 1
                : 0,
            candidates);
    }

    private static CurrentCollectionCandidateChoice[] BuildCandidateChoices(
        CurrentFullShipmentTeacherFrontier fullShipment,
        CurrentCollectionTeacherFrontier collection,
        CurrentMasterAnglerTeacherFrontier masterAngler)
    {
        var bindings = new List<UnifiedBinding>();
        bindings.AddRange(fullShipment.CandidateBindings.Select(value =>
            new UnifiedBinding(
                value.CandidateId,
                value.OptionId,
                value.Kind,
                new CurrentCollectionRequirementCredit(
                    "full_shipment",
                    value.RequirementId,
                    0,
                    value.QualifiedItemId,
                    value.BindingKind,
                    1,
                    0,
                    value.BindingKind == "native_full_shipment_completion"
                        ? "native_completion_consumes_selected_stack"
                        : "reserve_exact_item_until_native_full_shipment"))));
        bindings.AddRange(collection.CandidateBindings.Select(value =>
            new UnifiedBinding(
                value.CandidateId,
                value.OptionId,
                value.Kind,
                new CurrentCollectionRequirementCredit(
                    value.RequirementSetId,
                    value.RequirementId,
                    value.AlternativeIndex,
                    value.QualifiedItemId,
                    value.BindingKind,
                    value.RequiredQuantity,
                    value.MinimumQuality,
                    value.ReservationStatus))));
        bindings.AddRange(masterAngler.CandidateBindings.Select(value =>
            new UnifiedBinding(
                value.CandidateId,
                value.OptionId,
                value.Kind,
                new CurrentCollectionRequirementCredit(
                    "master_angler",
                    value.RequirementId,
                    0,
                    value.QualifiedItemId,
                    value.BindingKind,
                    1,
                    0,
                    "continue_hash_locked_intent_until_fresh_native_catch_receipt"))));

        return bindings
            .GroupBy(value => value.CandidateId, StringComparer.Ordinal)
            .Select(group =>
            {
                var identities = group
                    .Select(value => (value.OptionId, value.Kind))
                    .Distinct()
                    .ToArray();
                if (identities.Length != 1)
                {
                    throw new InvalidDataException(
                        "A shared collection candidate ID has inconsistent option or kind identity: " +
                        group.Key);
                }
                return new CurrentCollectionCandidateChoice(
                    group.Key,
                    identities[0].OptionId,
                    identities[0].Kind,
                    "execute_once_and_credit_all_exact_bindings",
                    group.Select(value => value.Credit)
                        .Distinct()
                        .OrderBy(value => value.RequirementSetId,
                            StringComparer.Ordinal)
                        .ThenBy(value => value.RequirementId,
                            StringComparer.Ordinal)
                        .ThenBy(value => value.AlternativeIndex)
                        .ToArray());
            })
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool SameHash(string left, string right) => string.Equals(
        left,
        right,
        StringComparison.OrdinalIgnoreCase);

    private sealed record UnifiedBinding(
        string CandidateId,
        string OptionId,
        string Kind,
        CurrentCollectionRequirementCredit Credit);
}
