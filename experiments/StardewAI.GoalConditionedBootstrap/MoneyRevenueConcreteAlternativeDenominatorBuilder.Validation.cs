namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueConcreteAlternativeDenominatorBuilder
{
    private static void ValidateManifest(
        MoneyRevenueConcreteAlternativeDenominatorManifest manifest)
    {
        Require(manifest.SchemaVersion ==
                    "money_revenue_concrete_alternative_denominator_manifest.v1" &&
                !manifest.FormalProductTrainingAuthorized &&
                !string.IsNullOrWhiteSpace(
                    manifest.AlternativeDenominatorCatalogPath) &&
                !string.IsNullOrWhiteSpace(
                    manifest.TerminalRebindManifestPath) &&
                !string.IsNullOrWhiteSpace(
                    manifest.TerminalRebindReceiptPath),
            "Money Revenue concrete alternative denominator manifest is invalid.");
    }

    private static void ValidateInputs(
        MoneyRevenueAlternativeDenominatorCatalog skeletonCatalog,
        MoneyRevenueCandidateDenominatorCatalog sourceCatalog,
        MoneyRevenueTerminalCandidateRebindManifest rebindManifest,
        MoneyRevenueTerminalCandidateRebindReceipt rebindReceipt)
    {
        Require(skeletonCatalog.AlternativeSkeletonDenominatorComplete &&
                skeletonCatalog.SourceCandidateDenominatorAuthoritative &&
                !skeletonCatalog.FormalProductTrainingAuthorized &&
                sourceCatalog.CurrentSnapshotDenominatorAuthoritative &&
                sourceCatalog.CatalogSha256 ==
                    skeletonCatalog.SourceCandidateCatalogSha256 &&
                sourceCatalog.SourceStateHash == skeletonCatalog.SourceStateHash,
            "Money Revenue source or skeleton denominator is not exact and authoritative.");
        Require(rebindReceipt.Status ==
                    "verified_terminal_candidate_rebind_denominator" &&
                rebindReceipt.TerminalRebindDenominatorComplete &&
                !rebindReceipt.CurrentAlternativeDenominatorAuthoritative &&
                !rebindReceipt.ProductionTeacherPreferenceLabelEligible &&
                !rebindReceipt.FormalProductTrainingAuthorized &&
                rebindReceipt.SourceStateHash == skeletonCatalog.SourceStateHash &&
                rebindReceipt.AlternativeDenominatorCatalogSha256 ==
                    skeletonCatalog.CatalogSha256,
            "Money Revenue terminal rebind denominator is not complete and bounded.");
        var expectedIds = skeletonCatalog.Alternatives
            .Where(row => row.RequiresFreshTerminalCandidateRebind)
            .Select(row => row.AlternativeId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var requestIds = rebindManifest.Rebinds
            .Select(row => row.AlternativeId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var evidenceIds = rebindReceipt.Rebinds
            .Select(row => row.AlternativeId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(expectedIds.SequenceEqual(requestIds, StringComparer.Ordinal) &&
                expectedIds.SequenceEqual(evidenceIds, StringComparer.Ordinal) &&
                expectedIds.Length == rebindReceipt.RequiredRebindCount &&
                expectedIds.Length == rebindReceipt.VerifiedRebindCount,
            "Money Revenue terminal rebind set does not equal the required skeleton subset.");
    }

    private static IReadOnlyDictionary<string, SourceCandidateRef>
        SourceCandidates(MoneyRevenueCandidateDenominatorCatalog source)
    {
        var rows = source.Nodes
            .Where(node => node.PolicyChoice)
            .SelectMany(node => node.Candidates
                .Where(candidate => candidate.ComparisonEligible)
                .Select(candidate => new SourceCandidateRef(node, candidate)))
            .ToArray();
        Require(rows.Length == source.ComparisonCandidateCount &&
                rows.Select(row => row.Candidate.CatalogCandidateId)
                    .Distinct(StringComparer.Ordinal).Count() == rows.Length &&
                rows.Select(row => row.Candidate.CatalogCandidateId)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(
                        source.ComparisonCandidateIds,
                        StringComparer.Ordinal),
            "Money Revenue source candidate denominator drifted.");
        return rows.ToDictionary(
            row => row.Candidate.CatalogCandidateId,
            StringComparer.Ordinal);
    }

    private static SourceCandidateRef RequireSourceCandidate(
        MoneyRevenueAlternativeSkeleton skeleton,
        IReadOnlyDictionary<string, SourceCandidateRef> sourceCandidates)
    {
        if (!sourceCandidates.TryGetValue(
                skeleton.SourceCatalogCandidateId,
                out var source))
        {
            throw new InvalidDataException(
                "Money Revenue skeleton source candidate is missing: " +
                skeleton.AlternativeId + ".");
        }
        Require(source.Candidate.CandidateSha256 ==
                    skeleton.SourceCandidateSha256 &&
                source.Candidate.CandidateId == skeleton.SourceCandidateId &&
                source.Candidate.CandidateKind == skeleton.SourceCandidateKind &&
                source.Node.NodeId == skeleton.SourceNodeId &&
                source.Node.OptionId == skeleton.SourceOptionId,
            "Money Revenue skeleton source candidate drifted: " +
            skeleton.AlternativeId + ".");
        return source;
    }

    private static void ValidateConcreteDenominator(
        MoneyRevenueAlternativeDenominatorCatalog skeletonCatalog,
        IReadOnlyList<MoneyRevenueConcreteAlternative> concrete)
    {
        Require(concrete.Count >= skeletonCatalog.AlternativeCount &&
                concrete.Select(row => row.ConcreteAlternativeId)
                    .Distinct(StringComparer.Ordinal).Count() == concrete.Count &&
                concrete.All(row => IsSha256(
                    row.ConcreteAlternativeSha256)),
            "Money Revenue concrete alternative identities are incomplete or duplicated.");
        foreach (var skeleton in skeletonCatalog.Alternatives)
        {
            var count = concrete.Count(row =>
                row.SkeletonAlternativeId == skeleton.AlternativeId);
            Require(skeleton.RequiresFreshTerminalCandidateRebind
                    ? count >= 1
                    : count == 1,
                "Money Revenue skeleton did not expand to the required concrete cardinality: " +
                skeleton.AlternativeId + ".");
        }
    }

    private sealed record SourceCandidateRef(
        MoneyRevenueCandidateNodeCoverage Node,
        MoneyRevenueCandidateCatalogEntry Candidate);
}
