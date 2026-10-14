using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalCandidateRebindBuilder
{
    public static MoneyRevenueTerminalCandidateRebindReceipt Build(
        string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money Revenue terminal candidate rebind manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalCandidateRebindManifest>(
            fullManifestPath,
            "Money Revenue terminal candidate rebind manifest");
        ValidateManifest(manifest);

        var alternativePath = RequiredFile(
            manifest.AlternativeDenominatorCatalogPath,
            "Money Revenue alternative denominator catalog");
        var actualAlternatives = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueAlternativeDenominatorCatalog>(
            alternativePath,
            "Money Revenue alternative denominator catalog");
        var rebuiltAlternatives = MoneyRevenueAlternativeDenominatorBuilder
            .Build(actualAlternatives.SourceCandidateCatalogPath);
        Require(EqualJson(actualAlternatives, rebuiltAlternatives),
            "Money Revenue alternative denominator did not equal its source rebuild.");
        Require(actualAlternatives.AlternativeSkeletonDenominatorComplete &&
                actualAlternatives.SourceCandidateDenominatorAuthoritative &&
                !actualAlternatives.FormalProductTrainingAuthorized,
            "Money Revenue alternative skeleton denominator is not complete and bounded.");

        var required = actualAlternatives.Alternatives
            .Where(row => row.RequiresFreshTerminalCandidateRebind)
            .OrderBy(row => row.AlternativeId, StringComparer.Ordinal)
            .ToArray();
        ValidateRebindDenominator(required, manifest.Rebinds);
        var sourceCatalog = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCandidateDenominatorCatalog>(
            actualAlternatives.SourceCandidateCatalogPath,
            "Money Revenue source candidate denominator catalog");
        var requestById = manifest.Rebinds.ToDictionary(
            row => row.AlternativeId,
            StringComparer.Ordinal);
        var evidence = required.Select(skeleton => VerifyRebind(
                skeleton,
                requestById[skeleton.AlternativeId],
                sourceCatalog))
            .OrderBy(row => row.AlternativeId, StringComparer.Ordinal)
            .ToArray();

        Require(evidence.All(row => row.TerminalCandidates.Length > 0) &&
                evidence.SelectMany(row => row.TerminalCandidates)
                    .Select(row => row.ConcreteAlternativeId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                evidence.Sum(row => row.TerminalCandidates.Length),
            "Money Revenue rebound concrete alternative identities are incomplete or duplicated.");
        return new MoneyRevenueTerminalCandidateRebindReceipt
        {
            Status = "verified_terminal_candidate_rebind_denominator",
            ManifestSha256 = Hash(fullManifestPath),
            AlternativeDenominatorCatalogPath = alternativePath,
            AlternativeDenominatorFileSha256 = Hash(alternativePath),
            AlternativeDenominatorCatalogSha256 =
                actualAlternatives.CatalogSha256,
            SourceStateHash = actualAlternatives.SourceStateHash,
            RequiredRebindCount = required.Length,
            VerifiedRebindCount = evidence.Length,
            ConcreteTerminalCandidateCount = evidence.Sum(row =>
                row.TerminalCandidates.Length),
            TerminalRebindDenominatorComplete = true,
            CurrentAlternativeDenominatorAuthoritative = false,
            ProductionTeacherPreferenceLabelEligible = false,
            Rebinds = evidence,
            BlockingReasons = new[]
            {
                "direct_and_rebound_concrete_alternative_union_not_built"
            },
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueTerminalCandidateRebindEvidence VerifyRebind(
        MoneyRevenueAlternativeSkeleton skeleton,
        MoneyRevenueTerminalCandidateRebindRequest request,
        MoneyRevenueCandidateDenominatorCatalog sourceCatalog)
    {
        var rolloutManifestPath = RequiredFile(
            request.AcquisitionRolloutProofManifestPath,
            "Acquisition rollout proof manifest");
        var rolloutReceiptPath = RequiredFile(
            request.AcquisitionRolloutProofReceiptPath,
            "Acquisition rollout proof receipt");
        var manifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            rolloutManifestPath,
            "Acquisition rollout proof manifest");
        var rebuilt = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildVerifiedReceipt(rolloutManifestPath);
        var receipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
            rolloutReceiptPath,
            "Acquisition rollout proof receipt");
        Require(EqualJson(receipt, rebuilt.Receipt) &&
                receipt.ProofChainVerified &&
                receipt.PortfolioCompletionVerified &&
                !receipt.FormalTrainingAuthorized,
            "Money Revenue terminal rebind rollout receipt is not complete and exact.");

        var verifiedSteps = Steps(manifest)
            .Select((step, index) => ReadStep(step, index))
            .ToArray();
        Require(verifiedSteps.Length > 0 &&
                verifiedSteps[0].Binding.BeforeStateHash ==
                    sourceCatalog.SourceStateHash &&
                verifiedSteps[0].Binding.BeforeSnapshotSha256 ==
                    sourceCatalog.SnapshotSha256,
            "Money Revenue rollout did not start from the source candidate snapshot.");
        var matches = verifiedSteps
            .Where(row => string.Equals(
                row.Binding.SelectedCandidateId,
                skeleton.SourceCandidateId,
                StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == 1,
            "Money Revenue skeleton source candidate did not match exactly one verified rollout transition: " +
            skeleton.AlternativeId + ".");
        return BuildEvidence(
            skeleton,
            matches[0],
            rolloutManifestPath,
            rolloutReceiptPath,
            receipt,
            sourceCatalog.DependencyExpansionPath);
    }

    private static MoneyRevenueTerminalCandidateRebindEvidence BuildEvidence(
        MoneyRevenueAlternativeSkeleton skeleton,
        VerifiedRolloutStep step,
        string rolloutManifestPath,
        string rolloutReceiptPath,
        AcquisitionRoutePortfolioRolloutProofReceipt rolloutReceipt,
        string dependencyExpansionPath)
    {
        var transition = step.FreshReceipt.TerminalTransition ??
            throw new InvalidDataException(
                "Money Revenue source transition has no terminal evidence.");
        Require(step.FreshReceipt.FreshTerminalReceiptVerified &&
                step.FreshReceipt.RouteTrainingEvidenceEligible &&
                !step.FreshReceipt.FormalTrainingAuthorized &&
                transition.Verified &&
                transition.Resolved &&
                transition.TransitionKind ==
                    "exact_player_inventory_quantity_increase" &&
                !string.IsNullOrWhiteSpace(transition.QualifiedItemId) &&
                transition.QuantityIncrease.HasValue,
            "Money Revenue source transition has no exact inventory output receipt.");
        var quantityIncrease = transition.QuantityIncrease.GetValueOrDefault();

        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            step.BeforeSnapshotPath,
            "Money Revenue rebind before snapshot");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            step.AfterSnapshotPath,
            "Money Revenue rebind terminal snapshot");
        Require(step.FreshReceipt.AfterSnapshotSha256 ==
                    Hash(step.AfterSnapshotPath) &&
                step.Binding.BeforeSnapshotSha256 ==
                    Hash(step.BeforeSnapshotPath) &&
                step.Binding.BeforeStateHash == before.StateHash,
            "Money Revenue rebind transition snapshot identity drifted.");
        var aggregate = ExactInventoryReceiptVerifier.Verify(
            before,
            after,
            transition.QualifiedItemId,
            transition.RequiredAmount,
            transition.MinimumQuality);
        Require(aggregate.Verified &&
                aggregate.BeforeQuantity == transition.BeforeQuantity &&
                aggregate.AfterQuantity == transition.AfterQuantity &&
                aggregate.QuantityIncrease == transition.QuantityIncrease,
            "Money Revenue rebind inventory totals drifted from the fresh terminal receipt.");
        var slots = IncreasedSlots(
            before,
            after,
            transition.QualifiedItemId,
            transition.MinimumQuality,
            transition.RequiredAmount,
            quantityIncrease);
        var freshCatalog = MoneyRevenueCandidateDenominatorBuilder.Build(
            step.AfterSnapshotPath,
            dependencyExpansionPath);
        Require(freshCatalog.SourceStateHash == after.StateHash &&
                freshCatalog.SnapshotSha256 == Hash(step.AfterSnapshotPath),
            "Fresh Money Revenue candidate catalog did not bind the terminal snapshot.");
        var candidates = ReboundCandidates(
            skeleton,
            transition.QualifiedItemId,
            slots,
            freshCatalog);
        var evidence = new MoneyRevenueTerminalCandidateRebindEvidence
        {
            AlternativeId = skeleton.AlternativeId,
            SkeletonPathSha256 = skeleton.PathSha256,
            SourceCatalogCandidateId = skeleton.SourceCatalogCandidateId,
            SourceCandidateId = skeleton.SourceCandidateId,
            TerminalOptionId = skeleton.TerminalOptionId,
            ProofShape = skeleton.ProofShape,
            RolloutId = rolloutReceipt.RolloutId,
            RolloutManifestSha256 = Hash(rolloutManifestPath),
            RolloutReceiptSha256 = Hash(rolloutReceiptPath),
            MatchedTransitionIndex = step.TransitionIndex,
            RouteOccurrenceId = step.Binding.RouteOccurrenceId,
            BeforeSnapshotPath = step.BeforeSnapshotPath,
            BeforeSnapshotSha256 = Hash(step.BeforeSnapshotPath),
            TerminalSnapshotPath = step.AfterSnapshotPath,
            TerminalSnapshotSha256 = Hash(step.AfterSnapshotPath),
            TerminalStateHash = after.StateHash,
            FreshTerminalReceiptSha256 = Hash(step.FreshReceiptPath),
            QualifiedItemId = transition.QualifiedItemId,
            RequiredAmount = transition.RequiredAmount,
            MinimumQuality = transition.MinimumQuality,
            QuantityIncrease = quantityIncrease,
            IncreasedInventorySlots = slots,
            FreshCandidateCatalogSha256 = freshCatalog.CatalogSha256,
            TerminalCandidates = candidates
        };
        evidence.EvidenceSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                evidence);
        return evidence;
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
