namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueConcreteAlternativeDenominatorBuilder
{
    public static MoneyRevenueConcreteAlternativeDenominatorCatalog Build(
        string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money Revenue concrete alternative denominator manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueConcreteAlternativeDenominatorManifest>(
            fullManifestPath,
            "Money Revenue concrete alternative denominator manifest");
        ValidateManifest(manifest);

        var alternativePath = RequiredFile(
            manifest.AlternativeDenominatorCatalogPath,
            "Money Revenue alternative denominator catalog");
        var alternatives = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueAlternativeDenominatorCatalog>(
            alternativePath,
            "Money Revenue alternative denominator catalog");
        var rebuiltAlternatives = MoneyRevenueAlternativeDenominatorBuilder
            .Build(alternatives.SourceCandidateCatalogPath);
        Require(EqualJson(alternatives, rebuiltAlternatives),
            "Money Revenue alternative denominator did not equal its source rebuild.");

        var rebindManifestPath = RequiredFile(
            manifest.TerminalRebindManifestPath,
            "Money Revenue terminal rebind manifest");
        var rebindReceiptPath = RequiredFile(
            manifest.TerminalRebindReceiptPath,
            "Money Revenue terminal rebind receipt");
        var rebindManifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalCandidateRebindManifest>(
            rebindManifestPath,
            "Money Revenue terminal rebind manifest");
        var rebindReceipt = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalCandidateRebindReceipt>(
            rebindReceiptPath,
            "Money Revenue terminal rebind receipt");
        var rebuiltRebind = MoneyRevenueTerminalCandidateRebindBuilder.Build(
            rebindManifestPath);
        Require(EqualJson(rebindReceipt, rebuiltRebind),
            "Money Revenue terminal rebind receipt did not equal its source rebuild.");
        Require(string.Equals(
                    Path.GetFullPath(
                        rebindManifest.AlternativeDenominatorCatalogPath),
                    alternativePath,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    Path.GetFullPath(
                        rebindReceipt.AlternativeDenominatorCatalogPath),
                    alternativePath,
                    StringComparison.OrdinalIgnoreCase) &&
                rebindReceipt.AlternativeDenominatorFileSha256 ==
                    Hash(alternativePath) &&
                rebindReceipt.AlternativeDenominatorCatalogSha256 ==
                    alternatives.CatalogSha256,
            "Money Revenue terminal rebind does not bind the selected alternative denominator.");

        var sourceCatalog = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCandidateDenominatorCatalog>(
            alternatives.SourceCandidateCatalogPath,
            "Money Revenue source candidate denominator catalog");
        return BuildCatalog(
            fullManifestPath,
            Hash(fullManifestPath),
            alternativePath,
            Hash(alternativePath),
            alternatives,
            sourceCatalog,
            rebindManifestPath,
            Hash(rebindManifestPath),
            rebindManifest,
            rebindReceiptPath,
            Hash(rebindReceiptPath),
            rebindReceipt);
    }

    internal static MoneyRevenueConcreteAlternativeDenominatorCatalog
        BuildCatalog(
            string manifestPath,
            string manifestSha256,
            string alternativePath,
            string alternativeFileSha256,
            MoneyRevenueAlternativeDenominatorCatalog skeletonCatalog,
            MoneyRevenueCandidateDenominatorCatalog sourceCatalog,
            string rebindManifestPath,
            string rebindManifestSha256,
            MoneyRevenueTerminalCandidateRebindManifest rebindManifest,
            string rebindReceiptPath,
            string rebindReceiptSha256,
            MoneyRevenueTerminalCandidateRebindReceipt rebindReceipt)
    {
        ValidateInputs(
            skeletonCatalog,
            sourceCatalog,
            rebindManifest,
            rebindReceipt);
        var sourceCandidates = SourceCandidates(sourceCatalog);
        var requests = rebindManifest.Rebinds.ToDictionary(
            row => row.AlternativeId,
            StringComparer.Ordinal);
        var reboundEvidence = rebindReceipt.Rebinds.ToDictionary(
            row => row.AlternativeId,
            StringComparer.Ordinal);
        var concrete = new List<MoneyRevenueConcreteAlternative>();
        var directCount = 0;
        foreach (var skeleton in skeletonCatalog.Alternatives
                     .OrderBy(row => row.AlternativeId,
                         StringComparer.Ordinal))
        {
            var source = RequireSourceCandidate(
                skeleton,
                sourceCandidates);
            if (!skeleton.RequiresFreshTerminalCandidateRebind)
            {
                concrete.Add(BuildDirect(
                    skeleton,
                    source,
                    sourceCatalog.SourceStateHash,
                    sourceCatalog.SnapshotPath,
                    sourceCatalog.SnapshotSha256));
                directCount++;
                continue;
            }
            var evidence = reboundEvidence[skeleton.AlternativeId];
            var request = requests[skeleton.AlternativeId];
            concrete.AddRange(evidence.TerminalCandidates.Select(candidate =>
                BuildRebound(
                    skeleton,
                    source,
                    evidence,
                    request,
                    candidate)));
        }

        var ordered = concrete
            .OrderBy(row => row.ConcreteAlternativeId, StringComparer.Ordinal)
            .ToArray();
        ValidateConcreteDenominator(skeletonCatalog, ordered);
        var blockers = new[]
            {
                "verified_counterfactual_revenue_recurrence_not_supplied"
            }
            .Concat(sourceCatalog.DependencyExpansionBlockers.Select(row =>
                "long_horizon_dependency_blocker:" + row))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var catalog = new MoneyRevenueConcreteAlternativeDenominatorCatalog
        {
            Status = "current_concrete_alternative_denominator_authoritative",
            SourceStateHash = skeletonCatalog.SourceStateHash,
            SourceSnapshotPath = sourceCatalog.SnapshotPath,
            SourceSnapshotSha256 = sourceCatalog.SnapshotSha256,
            ManifestPath = manifestPath,
            ManifestSha256 = manifestSha256,
            AlternativeDenominatorCatalogPath = alternativePath,
            AlternativeDenominatorFileSha256 = alternativeFileSha256,
            AlternativeDenominatorCatalogSha256 =
                skeletonCatalog.CatalogSha256,
            TerminalRebindManifestPath = rebindManifestPath,
            TerminalRebindManifestSha256 = rebindManifestSha256,
            TerminalRebindReceiptPath = rebindReceiptPath,
            TerminalRebindReceiptSha256 = rebindReceiptSha256,
            SkeletonDenominatorComplete = true,
            TerminalRebindDenominatorComplete = true,
            CurrentConcreteDenominatorAuthoritative = true,
            LongHorizonDenominatorAuthoritative =
                sourceCatalog.LongHorizonDenominatorAuthoritative,
            ProductionTeacherPreferenceLabelEligible = false,
            SkeletonCount = skeletonCatalog.AlternativeCount,
            DirectAlternativeCount = directCount,
            ReboundAlternativeCount = ordered.Length - directCount,
            ConcreteAlternativeCount = ordered.Length,
            ConcreteAlternativeIds = ordered.Select(row =>
                row.ConcreteAlternativeId).ToArray(),
            Alternatives = ordered,
            BlockingReasons = blockers,
            FormalProductTrainingAuthorized = false
        };
        catalog.CatalogSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                catalog);
        return catalog;
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(path));
}
