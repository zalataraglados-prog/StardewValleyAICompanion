using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentCollectionTeacherFrontierBuilder
{
    private static readonly string[] ExactCandidateQualityParameters =
    {
        "expected_output_quality",
        "projected_harvest_quality",
        "item_quality",
        "geode_expected_output_quality",
        "prize_ticket_reward_quality"
    };

    private static CurrentCollectionCandidateBinding[] BuildMuseumBindings(
        string requirementSetId,
        GoalRequirementGroup group,
        AcquisitionRequirementGroupLowering lowering,
        CurrentCollectionRequirementProgress progress,
        CurrentCollectionProgress setProgress,
        PolicyEventCandidatePrediction[] candidates)
    {
        var alternative = lowering.Alternatives.Single();
        var result = new List<CurrentCollectionCandidateBinding>();
        foreach (var match in CurrentTeacherFrontierSupport.ExactItemCandidates(
                     alternative.QualifiedItemId,
                     candidates))
        {
            var candidate = match.Candidate;
            var directCompletion = IsExactMuseumDonation(
                candidate,
                setProgress.AggregateCompletedCount,
                setProgress.Requirements.Count,
                group.Alternatives.Single().Amount,
                out var directEvidence);
            var routes = CurrentTeacherFrontierSupport.AdmittedEndpointRoutes(
                alternative,
                candidate.OptionId);
            if (!directCompletion &&
                (routes.Length == 0 || candidate.Quantity <= 0))
            {
                continue;
            }

            result.Add(new CurrentCollectionCandidateBinding(
                requirementSetId,
                group.RequirementId,
                0,
                alternative.ItemId,
                alternative.QualifiedItemId,
                candidate.CandidateId,
                candidate.OptionId,
                candidate.Kind,
                candidate.Rank,
                directCompletion
                    ? "native_museum_donation_completion"
                    : "authoritative_acquisition_endpoint",
                alternative.Amount,
                alternative.MinimumQuality,
                candidate.Quantity,
                null,
                progress.RemainingSlotCount,
                directCompletion
                    ? "native_donation_consumes_exact_projected_inventory"
                    : "reserve_exact_item_until_native_museum_donation",
                match.IdentityEvidence + (directCompletion
                    ? "+" + directEvidence
                    : "+candidate.quantity"),
                routes));
        }
        return OrderedBindings(result);
    }

    private static CurrentCollectionCandidateBinding[]
        BuildCommunityCenterBindings(
            string requirementSetId,
            GoalRequirementGroup group,
            AcquisitionRequirementGroupLowering lowering,
            CurrentCollectionRequirementProgress progress,
            CurrentCollectionProgress setProgress,
            PolicyEventCandidatePrediction[] candidates)
    {
        var result = new List<CurrentCollectionCandidateBinding>();
        for (var index = 0; index < lowering.Alternatives.Length; index++)
        {
            if (progress.CompletedAlternatives[index])
                continue;
            var alternative = lowering.Alternatives[index];

            foreach (var candidate in candidates)
            {
                if (!IsExactCommunityCenterDonation(
                        candidate,
                        progress,
                        group,
                        alternative,
                        index,
                        out var directQuality,
                        out var directEvidence))
                {
                    continue;
                }
                result.Add(new CurrentCollectionCandidateBinding(
                    requirementSetId,
                    group.RequirementId,
                    index,
                    alternative.ItemId,
                    alternative.QualifiedItemId,
                    candidate.CandidateId,
                    candidate.OptionId,
                    candidate.Kind,
                    candidate.Rank,
                    alternative.MatchKind == "money_payment"
                        ? "native_community_center_payment_completion"
                        : "native_community_center_donation_completion",
                    alternative.Amount,
                    alternative.MinimumQuality,
                    candidate.Quantity,
                    directQuality,
                    progress.RemainingSlotCount,
                    alternative.MatchKind == "money_payment"
                        ? "native_payment_consumes_exact_projected_money"
                        : "native_donation_consumes_exact_projected_inventory",
                    directEvidence,
                    Array.Empty<CurrentRequirementRouteEvidence>()));
            }

            if (alternative.MatchKind != "item_id")
                continue;
            foreach (var match in CurrentTeacherFrontierSupport.ExactItemCandidates(
                         alternative.QualifiedItemId,
                         candidates))
            {
                var candidate = match.Candidate;
                if (string.Equals(
                        candidate.Kind,
                        "donate_community_center_item",
                        StringComparison.Ordinal) ||
                    candidate.Quantity <= 0)
                {
                    continue;
                }
                var routes = CurrentTeacherFrontierSupport.AdmittedEndpointRoutes(
                    alternative,
                    candidate.OptionId);
                if (routes.Length == 0)
                    continue;
                int? candidateQuality = null;
                var qualityEvidence = string.Empty;
                if (alternative.MinimumQuality > 0)
                {
                    if (!TryReadExactCandidateQuality(
                            candidate,
                            out var quality,
                            out qualityEvidence) ||
                        quality < alternative.MinimumQuality)
                    {
                        continue;
                    }
                    candidateQuality = quality;
                }
                result.Add(new CurrentCollectionCandidateBinding(
                    requirementSetId,
                    group.RequirementId,
                    index,
                    alternative.ItemId,
                    alternative.QualifiedItemId,
                    candidate.CandidateId,
                    candidate.OptionId,
                    candidate.Kind,
                    candidate.Rank,
                    "authoritative_acquisition_endpoint",
                    alternative.Amount,
                    alternative.MinimumQuality,
                    candidate.Quantity,
                    candidateQuality,
                    progress.RemainingSlotCount,
                    "reserve_required_quantity_at_minimum_quality_until_native_bundle_donation",
                    match.IdentityEvidence + "+candidate.quantity" +
                        (qualityEvidence.Length == 0
                            ? string.Empty
                            : "+" + qualityEvidence),
                    routes));
            }
        }
        return OrderedBindings(result);
    }

    private static bool IsExactMuseumDonation(
        PolicyEventCandidatePrediction candidate,
        int donatedBefore,
        int totalDonatableItems,
        int requiredQuantity,
        out string evidence)
    {
        evidence = string.Empty;
        if (!string.Equals(
                candidate.OptionId,
                "museum.donate_items",
                StringComparison.Ordinal) ||
            !string.Equals(
                candidate.Kind,
                "donate_museum_item",
                StringComparison.Ordinal) ||
            candidate.Quantity != requiredQuantity ||
            !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "expected_donated_count_before",
                out var candidateBefore) ||
            candidateBefore != donatedBefore ||
            !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "expected_donated_count_after",
                out var candidateAfter) ||
            candidateAfter != donatedBefore + 1 ||
            !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "expected_stack_before",
                out var stackBefore) ||
            !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "expected_stack_after",
                out var stackAfter) ||
            stackAfter != stackBefore - requiredQuantity ||
            !ReadIntParameterEquals(
                candidate,
                "museum_total_donatable_items",
                totalDonatableItems) ||
            !ReadBoolParameterEquals(
                candidate,
                "expected_collection_complete_after",
                donatedBefore + 1 == totalDonatableItems))
        {
            return false;
        }
        evidence =
            "native_museum_donation_projection+exact_inventory_consumption";
        return true;
    }

    private static bool IsExactCommunityCenterDonation(
        PolicyEventCandidatePrediction candidate,
        CurrentCollectionRequirementProgress progress,
        GoalRequirementGroup group,
        AcquisitionRequirementAlternativeLowering alternative,
        int alternativeIndex,
        out int? candidateQuality,
        out string evidence)
    {
        candidateQuality = null;
        evidence = string.Empty;
        var exactItemIdentity = alternative.MatchKind == "item_id" &&
            string.Equals(
                candidate.QualifiedItemId,
                alternative.QualifiedItemId,
                StringComparison.Ordinal);
        var exactMoneyIdentity = alternative.MatchKind == "money_payment" &&
            string.IsNullOrWhiteSpace(candidate.QualifiedItemId) &&
            string.Equals(candidate.ItemId, "-1", StringComparison.Ordinal);
        var completesBundle = progress.RemainingSlotCount == 1;
        var expectedCompletedAfter = completesBundle
            ? group.Alternatives.Length
            : progress.CompletedAlternativeCount + 1;
        if ((!exactItemIdentity && !exactMoneyIdentity) ||
            !string.Equals(
                candidate.OptionId,
                "community_center.donate_bundle_items",
                StringComparison.Ordinal) ||
            !string.Equals(
                candidate.Kind,
                "donate_community_center_item",
                StringComparison.Ordinal) ||
            candidate.Quantity != alternative.Amount ||
            !ReadParameterEquals(candidate, "bundle_data_key", progress.RuntimeKey) ||
            !ReadIntParameterEquals(
                candidate,
                "bundle_ingredient_index",
                alternativeIndex) ||
            !ReadIntParameterEquals(
                candidate,
                "required_stack",
                alternative.Amount) ||
            !ReadIntParameterEquals(
                candidate,
                "expected_bundle_completed_count_before",
                progress.CompletedAlternativeCount) ||
            !ReadIntParameterEquals(
                candidate,
                "expected_bundle_completed_count_after",
                expectedCompletedAfter) ||
            !ReadBoolParameterEquals(
                candidate,
                "expected_bundle_complete_after",
                completesBundle))
        {
            return false;
        }

        if (alternative.MatchKind == "money_payment")
        {
            if (!CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                    candidate,
                    "expected_money_before",
                    out var moneyBefore) ||
                !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                    candidate,
                    "expected_money_after",
                    out var moneyAfter) ||
                moneyBefore < alternative.Amount ||
                moneyAfter != moneyBefore - alternative.Amount)
            {
                return false;
            }
            evidence =
                "native_bundle_payment_projection+exact_money_consumption";
            return true;
        }
        if (!CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "expected_item_quality",
                out var quality) ||
            quality < alternative.MinimumQuality ||
            !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "expected_stack_before",
                out var stackBefore) ||
            !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "expected_stack_after",
                out var stackAfter) ||
            stackAfter != stackBefore - alternative.Amount ||
            !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "inventory_item_total_before",
                out var inventoryBefore) ||
            !CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "inventory_item_total_after",
                out var inventoryAfter) ||
            inventoryAfter != inventoryBefore - alternative.Amount)
        {
            return false;
        }
        candidateQuality = quality;
        evidence =
            "candidate.qualified_item_id+native_bundle_donation_projection+exact_quantity_quality_inventory_consumption";
        return true;
    }

    private static bool TryReadExactCandidateQuality(
        PolicyEventCandidatePrediction candidate,
        out int quality,
        out string evidence)
    {
        quality = 0;
        evidence = string.Empty;
        var values = new List<(string Name, int Value)>();
        foreach (var name in ExactCandidateQualityParameters)
        {
            if (CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                    candidate,
                    name,
                    out var value))
            {
                values.Add((name, value));
            }
        }
        if (values.Count == 0 ||
            values.Any(value => value.Value < 0) ||
            values.Select(value => value.Value).Distinct().Count() != 1)
        {
            return false;
        }
        quality = values[0].Value;
        evidence = string.Join(
            "+",
            values.Select(value => "candidate.parameters." + value.Name));
        return true;
    }

    private static bool ReadParameterEquals(
        PolicyEventCandidatePrediction candidate,
        string name,
        string expected) =>
        CurrentTeacherFrontierSupport.TryReadUniqueParameter(
            candidate,
            name,
            out var value) &&
        string.Equals(value, expected, StringComparison.Ordinal);

    private static bool ReadIntParameterEquals(
        PolicyEventCandidatePrediction candidate,
        string name,
        int expected) =>
        CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
            candidate,
            name,
            out var value) && value == expected;

    private static bool ReadBoolParameterEquals(
        PolicyEventCandidatePrediction candidate,
        string name,
        bool expected) =>
        CurrentTeacherFrontierSupport.TryReadUniqueParameter(
            candidate,
            name,
            out var value) &&
        bool.TryParse(value, out var parsed) && parsed == expected;

    private static CurrentCollectionCandidateBinding[] OrderedBindings(
        IEnumerable<CurrentCollectionCandidateBinding> bindings) => bindings
        .OrderBy(value => value.Rank)
        .ThenBy(value => value.AlternativeIndex)
        .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
        .ToArray();
}
